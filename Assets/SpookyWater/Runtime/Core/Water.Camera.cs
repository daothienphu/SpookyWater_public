using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    public static partial class Water
    {
        private static readonly List<WaterCameraSource> CameraSources = new();
        private static Camera _explicitCamera;
        private static Func<Camera> _cameraResolver;
        private static Camera _lastCamera;
        private static bool _resolvingCamera;

        public static event Action<Camera> CameraChanged;

        public static Camera Camera
        {
            get
            {
                var camera = ResolveCamera();
                if (camera != _lastCamera) NotifyCameraChanged(camera);
                return camera;
            }
        }

        public static Func<Camera> CameraResolver
        {
            get => _cameraResolver;
            set
            {
                _cameraResolver = value;
                RefreshCamera();
            }
        }

        public static void SetCamera(Camera camera)
        {
            _explicitCamera = camera;
            RefreshCamera();
        }

        public static void RefreshCamera()
        {
            var camera = ResolveCamera();
            if (camera != _lastCamera) NotifyCameraChanged(camera);
        }

        internal static void RegisterCameraSource(WaterCameraSource source)
        {
            CameraSources.Remove(source);
            CameraSources.Add(source);
            RefreshCamera();
        }

        internal static void UnregisterCameraSource(WaterCameraSource source)
        {
            if (CameraSources.Remove(source)) RefreshCamera();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCameraStatics()
        {
            _explicitCamera = null;
            _lastCamera = null;
            CameraSources.RemoveAll(s => !s);
        }

        private static Camera ResolveCamera()
        {
            if (_explicitCamera) return _explicitCamera;

            for (var i = CameraSources.Count - 1; i >= 0; i--)
            {
                var source = CameraSources[i];
                if (!source)
                {
                    CameraSources.RemoveAt(i);
                    continue;
                }
                var camera = source.TargetCamera;
                if (camera && camera.isActiveAndEnabled) return camera;
            }

            if (_cameraResolver != null && !_resolvingCamera)
            {
                _resolvingCamera = true;
                try
                {
                    var resolved = _cameraResolver();
                    if (resolved) return resolved;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    _resolvingCamera = false;
                }
            }

            return SpookyWaterSettings.Instance.mainCameraFallback ? UnityEngine.Camera.main : null;
        }

        private static void NotifyCameraChanged(Camera camera)
        {
            _lastCamera = camera;
            CameraChanged?.Invoke(camera);
        }
    }
}
