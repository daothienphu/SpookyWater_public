using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SpookyWater
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class WaterReflectionRenderer : MonoBehaviour
    {
        private LayerMask _reflectionMask;
        private float _underwaterClipMargin = 0.25f;
        private RenderTexture _reflectionTexture;
        private float _reflectionResolutionScale = 0.5f;
        private readonly Vector3 _reflectionSurfaceNormal = Vector3.up;

        private Camera _reflectionCam;
        private Camera _mainCamera;
        private int _proxyLayer = -1;
        private bool _ready;

        private static readonly int ViewedFromBelowId = Shader.PropertyToID("_WaterViewedFromBelow");

        private static List<Light2D> _urpLights;
        private static bool _urpLightsLookedUp;
        private readonly List<(GameObject go, int layer)> _movedLights = new();
        private bool _warnedLightWithCollider;

        public Camera SourceCamera => _mainCamera;

        private void OnEnable()
        {
            if (_ready && _mainCamera) SetupRenderTexture();
        }

        private void OnDisable()
        {
            if (_reflectionTexture) _reflectionTexture.Release();
        }

        private void LateUpdate()
        {
            var camera = Water.Camera;
            if (camera != _mainCamera)
            {
                _mainCamera = camera;
                _ready = false;
            }

            if (!_ready) TrySetup();
            else if (_mainCamera) SetupRenderTexture();
            RenderToTexture();
        }

        public void Initialize(Camera reflectionCamera, RenderTexture reflectionTexture, LayerMask reflectionMask, float underwaterClipMargin, float resolutionScale)
        {
            _reflectionCam = reflectionCamera;
            _reflectionTexture = reflectionTexture;
            _reflectionMask = reflectionMask;
            _underwaterClipMargin = Mathf.Max(underwaterClipMargin, 0f);
            _reflectionResolutionScale = Mathf.Clamp(resolutionScale, 0.1f, 1f);
            _ready = false;
            TrySetup();
        }

        private void TrySetup()
        {
            if (!_reflectionCam) return;
            if (!_mainCamera) _mainCamera = Water.Camera;
            if (!_mainCamera) return;

            SetupCamera();
            SetupRenderTexture();
            _ready = true;
        }

        private void RenderToTexture()
        {
            if (!_reflectionTexture) return;
            if (!_mainCamera || !_reflectionCam) return;

            const float minDistanceFromSurface = 0.05f;
            var heightAboveSurface = Vector3.Dot(_reflectionSurfaceNormal.normalized, _mainCamera.transform.position - transform.position);
            if (Mathf.Abs(heightAboveSurface) <= minDistanceFromSurface) return;

            _reflectionCam.fieldOfView = _mainCamera.fieldOfView;
            _reflectionCam.orthographic = _mainCamera.orthographic;
            _reflectionCam.orthographicSize = _mainCamera.orthographicSize;
            _reflectionCam.aspect = _mainCamera.aspect;
            _reflectionCam.nearClipPlane = _mainCamera.nearClipPlane;
            _reflectionCam.farClipPlane = _mainCamera.farClipPlane;

            if (heightAboveSurface < 0f)
            {
                RenderUnderwaterView(-heightAboveSurface, minDistanceFromSurface);
                return;
            }

            var camPos = _mainCamera.transform.position;
            var dot = Vector3.Dot(_reflectionSurfaceNormal, camPos - transform.position);
            var reflectedPos = camPos - 2f * dot * _reflectionSurfaceNormal;
            var reflectedForward = Vector3.Reflect(_mainCamera.transform.forward, _reflectionSurfaceNormal);
            var reflectedUp = Vector3.Reflect(_mainCamera.transform.up, _reflectionSurfaceNormal);

            if (reflectedForward.sqrMagnitude < 1e-6f) return;

            _reflectionCam.transform.position = reflectedPos;
            _reflectionCam.transform.rotation = Quaternion.LookRotation(reflectedForward, reflectedUp);

            var normal = _reflectionSurfaceNormal.normalized;
            var reflectionMatrix = CalculateReflectionMatrix(new Vector4(
                normal.x, normal.y, normal.z, -Vector3.Dot(normal, transform.position)));
            _reflectionCam.worldToCameraMatrix = _mainCamera.worldToCameraMatrix * reflectionMatrix;

            var viewSpace = _reflectionCam.worldToCameraMatrix;
            var pointViewSpace = viewSpace.MultiplyPoint(transform.position);
            var normalViewSpace = viewSpace.MultiplyVector(_reflectionSurfaceNormal).normalized;
            var planeViewSpace = new Vector4(
                normalViewSpace.x,
                normalViewSpace.y,
                normalViewSpace.z,
                -Vector3.Dot(pointViewSpace, normalViewSpace));
            var projectionMatrix = _mainCamera.CalculateObliqueMatrix(planeViewSpace);

            for (var i = 0; i < 16; i++)
            {
                var v = projectionMatrix[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) return;
            }

            _reflectionCam.projectionMatrix = projectionMatrix;
            PrepareView(false);

            GL.invertCulling = true;
            try
            {
                RenderWithLights();
            }
            finally
            {
                GL.invertCulling = false;
            }
        }

        private void RenderWithLights()
        {
            IncludeLights();
            try
            {
                _reflectionCam.Render();
            }
            finally
            {
                RestoreLights();
            }
        }

        private void IncludeLights()
        {
            if (_proxyLayer < 0) return;
            var mask = _reflectionCam.cullingMask;
            foreach (var light in EnabledLights())
            {
                if (!light || !light.isActiveAndEnabled) continue;
                var go = light.gameObject;
                if ((mask & (1 << go.layer)) != 0) continue;
                if (go.TryGetComponent<Collider2D>(out _) || go.TryGetComponent<Collider>(out _))
                {
                    if (!_warnedLightWithCollider)
                    {
                        _warnedLightWithCollider = true;
                        Debug.LogWarning($"[WaterReflectionRenderer] Light2D '{light.name}' shares its GameObject with a collider, " +
                                         "so it won't light the water reflection. Move the light onto a child object.", light);
                    }
                    continue;
                }
                _movedLights.Add((go, go.layer));
                go.layer = _proxyLayer;
            }
        }

        private void RestoreLights()
        {
            foreach (var (go, layer) in _movedLights)
            {
                if (go) go.layer = layer;
            }
            _movedLights.Clear();
        }

        private static IReadOnlyList<Light2D> EnabledLights()
        {
            if (!_urpLightsLookedUp)
            {
                _urpLightsLookedUp = true;
                var manager = typeof(Light2D).Assembly.GetType("UnityEngine.Rendering.Universal.Light2DManager");
                var property = manager?.GetProperty("lights", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                _urpLights = property?.GetValue(null) as List<Light2D>;
            }
            return _urpLights ?? (IReadOnlyList<Light2D>)FindObjectsByType<Light2D>(FindObjectsSortMode.None);
        }

        private void RenderUnderwaterView(float cameraDepth, float minDistanceFromSurface)
        {
            _reflectionCam.transform.SetPositionAndRotation(_mainCamera.transform.position, _mainCamera.transform.rotation);
            _reflectionCam.worldToCameraMatrix = _mainCamera.worldToCameraMatrix;

            var margin = Mathf.Clamp(_underwaterClipMargin, 0f, Mathf.Max(cameraDepth - minDistanceFromSurface, 0f));
            var viewSpace = _reflectionCam.worldToCameraMatrix;
            var pointViewSpace = viewSpace.MultiplyPoint(transform.position - _reflectionSurfaceNormal.normalized * margin);
            var normalViewSpace = viewSpace.MultiplyVector(_reflectionSurfaceNormal).normalized;
            var planeViewSpace = new Vector4(
                normalViewSpace.x,
                normalViewSpace.y,
                normalViewSpace.z,
                -Vector3.Dot(pointViewSpace, normalViewSpace));
            var projectionMatrix = _mainCamera.CalculateObliqueMatrix(planeViewSpace);

            for (var i = 0; i < 16; i++)
            {
                var v = projectionMatrix[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) return;
            }

            _reflectionCam.projectionMatrix = projectionMatrix;
            PrepareView(true);
            RenderWithLights();
        }

        private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * plane.x * plane.x;
            m.m01 = -2f * plane.x * plane.y;
            m.m02 = -2f * plane.x * plane.z;
            m.m03 = -2f * plane.w * plane.x;

            m.m10 = -2f * plane.y * plane.x;
            m.m11 = 1f - 2f * plane.y * plane.y;
            m.m12 = -2f * plane.y * plane.z;
            m.m13 = -2f * plane.w * plane.y;

            m.m20 = -2f * plane.z * plane.x;
            m.m21 = -2f * plane.z * plane.y;
            m.m22 = 1f - 2f * plane.z * plane.z;
            m.m23 = -2f * plane.w * plane.z;
            return m;
        }

        private int ProxyMask => _proxyLayer >= 0 ? 1 << _proxyLayer : 0;

        private void PrepareView(bool viewedFromBelow)
        {
            _reflectionCam.cullingMask = _reflectionMask | ProxyMask;
            if (_proxyLayer >= 0) WaterReflectable.PrepareAll(_proxyLayer, viewedFromBelow, transform.position.y);
            Shader.SetGlobalFloat(ViewedFromBelowId, viewedFromBelow ? 1f : 0f);
        }

        private void SetupCamera()
        {
            _reflectionCam.enabled = false;

            _reflectionCam.CopyFrom(_mainCamera);

            _proxyLayer = SpookyWaterSettings.Instance.ReflectionLayerIndex;
            if (_proxyLayer < 0)
            {
                Debug.LogWarning($"[WaterReflectionRenderer] Proxy layer '{SpookyWaterSettings.Instance.reflectionLayer}' doesn't exist - " +
                                 "WaterReflectable sprites won't show in the water. Add it in Tags and Layers.", this);
            }
            else if (Application.isPlaying)
            {
                _mainCamera.cullingMask &= ~(1 << _proxyLayer);
            }
            _reflectionCam.cullingMask = _reflectionMask | ProxyMask;
            _reflectionCam.clearFlags = CameraClearFlags.SolidColor;
            _reflectionCam.backgroundColor = Color.clear;
        }

        private void SetupRenderTexture()
        {
            var w = Mathf.Max(Mathf.RoundToInt(_mainCamera.pixelWidth  * _reflectionResolutionScale), 1);
            var h = Mathf.Max(Mathf.RoundToInt(_mainCamera.pixelHeight * _reflectionResolutionScale), 1);

            if (!_reflectionTexture) return;
            if (_reflectionTexture.width == w && _reflectionTexture.height == h && _reflectionTexture.IsCreated()
                && _reflectionCam.targetTexture == _reflectionTexture) return;

            _reflectionTexture.Release();
            _reflectionTexture.width = w;
            _reflectionTexture.height = h;

            _reflectionCam.targetTexture = _reflectionTexture;
        }
    }
}
