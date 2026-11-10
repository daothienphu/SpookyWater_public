using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace SpookyWater
{
    public partial class WaterTilemapBuilder
    {
        private static readonly string[] HorizonInheritedFloats =
        {
            "_ReflectionBlurSize", "_ReflectionDistortion", "_NormalStrength", "_ReflectionMinStrength", "_ReflectionMaxStrength", "_ReflectionFresnelPower",
            "_UnderwaterWindowAngle", "_UnderwaterWindowSoftness", "_UnderwaterBlurSize",
            "_UnderwaterRippleRefraction", "_UnderwaterRippleHighlightGain", "_UnderwaterRippleHighlightOpacity",
            "_DepthFogStartZ", "_DepthFogEndZ", "_DepthFogPower", "_DepthFogReflection",
            "_ContactLineAmount", "_ContactLineReach", "_ContactReachPixels", "_ContactLineSpread", "_ContactLineScale",
            "_ContactWaveletLength", "_ContactWaveletThickness", "_ContactWaveletSoftness", "_ContactWaveletBreakup", "_ContactLapAmount", "_ContactLineSpeed", "_ContactDriftSpeed", "_ContactPerspective",
            "_ContactTwinkleSpeed", "_ContactSmearLength", "_ContactLineSkew", "_ContactRowShift", "_ContactFarBoost", "_ContactFarDistance", "_ContactBreak", "_ContactGlintChance",
            "_GlowIntensity", "_GlowSelfLit", "_GlowFollowColor", "_GlowUnderside",
            "_EdgeFalloffWidth", "_DisplacementFalloffPower", "_ColorFadePower", "_HazeDistance", "_EdgeFadeStart",
            "_UnderwaterFogDistance", "_UnderwaterFadeDistance", "_UnderwaterFadeAmount"
        };

        private static readonly string[] HorizonInheritedVectors = { "_ReflectionOffset" };

        private MeshFilter _horizonFilter;
        private float _horizonFieldWidth;
        private float _horizonNearZ;

        private static readonly string[] HorizonInheritedColors =
        {
            "_ReflectionTint", "_ReflectionSkyColor", "_UnderwaterTint", "_UnderwaterSkyColor", "_UnderwaterColor",
            "_DepthFogColor", "_ContactGlintColor", "_GlowColor", "_RippleHighlightColor",
            "_HorizonColor", "_UnderwaterHazeColor"
        };

        private static readonly int NearColorId = Shader.PropertyToID("_NearColor");
        private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
        private const string HorizonObjectName = "WaterHorizonExtension";
        private const string HorizonShaderName = "SpookyWater/Water Horizon";

        private Material _horizonMaterial;

        private bool HorizonEnabled => _generateHorizon && HorizonMaterial;

        // Built from the shader at runtime and never saved: every value comes from the Surface material.
        private Material HorizonMaterial
        {
            get
            {
                if (_horizonMaterial) return _horizonMaterial;
                if (!_horizonShader) _horizonShader = Shader.Find(HorizonShaderName);
                if (!_horizonShader) return null;
                _horizonMaterial = new Material(_horizonShader) { name = "WaterHorizon (Generated)", hideFlags = HideFlags.DontSave };
                return _horizonMaterial;
            }
        }

        private void DestroyHorizonMaterial()
        {
            if (!_horizonMaterial) return;
            if (Application.isPlaying) Destroy(_horizonMaterial);
            else DestroyImmediate(_horizonMaterial);
            _horizonMaterial = null;
        }

        private void PublishSurfaceGlobals()
        {
            var m = _surfaceMeshMaterial;
            Shader.SetGlobalVector(WaterSurfaceDisplacementId, new Vector4(
                _displacementScale, 0f,
                m.GetFloat(DistanceFalloffPowerId), m.GetFloat(EdgeFalloffWidthId)));
            Shader.SetGlobalColor(WaterSurfaceFogColorId, m.GetColor(DepthFogColorId));
            Shader.SetGlobalVector(WaterSurfaceFogParamsId, new Vector4(
                m.GetFloat(DepthFogStartZId), m.GetFloat(DepthFogEndZId), m.GetFloat(DepthFogPowerId), 0f));
        }

        private void CopyHorizonInheritedProperties(MaterialPropertyBlock mpb)
        {
            if (!_surfaceMeshMaterial) return;
            foreach (var name in HorizonInheritedFloats)
            {
                if (_surfaceMeshMaterial.HasFloat(name)) mpb.SetFloat(name, _surfaceMeshMaterial.GetFloat(name));
            }
            foreach (var name in HorizonInheritedVectors)
            {
                if (_surfaceMeshMaterial.HasVector(name)) mpb.SetVector(name, _surfaceMeshMaterial.GetVector(name));
            }
            foreach (var name in HorizonInheritedColors)
            {
                if (_surfaceMeshMaterial.HasColor(name)) mpb.SetColor(name, _surfaceMeshMaterial.GetColor(name));
            }
            if (_surfaceMeshMaterial.HasColor(DeepColorId)) mpb.SetColor(NearColorId, _surfaceMeshMaterial.GetColor(DeepColorId));
        }

        /// <summary>
        /// Re-applies the Water Surface material's values to the generated horizon and the shared water globals
        /// without regenerating. Called by the material inspector whenever a Water Surface material is edited.
        /// </summary>
        public void RefreshFromMaterials()
        {
            var root = _generatedRoot ? _generatedRoot : transform.Find(GeneratedRootName);
            if (!_surfaceMeshMaterial || !_surfaceTilemap || !root) return;
            PublishSurfaceGlobals();

            var horizon = _horizonFilter ? _horizonFilter.transform : root.Find(HorizonObjectName);
            if (!horizon || !horizon.TryGetComponent<MeshRenderer>(out var mr) || !HorizonMaterial) return;
            mr.sharedMaterial = HorizonMaterial;
            ApplyHorizonProperties(mr, horizon.position);
        }

        private void ApplyHorizonProperties(MeshRenderer mr, Vector3 origin)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetFloat(RippleScaleId, _displacementScale);
            CopyHorizonInheritedProperties(mpb);
            mr.SetPropertyBlock(mpb);

            Shader.SetGlobalFloat(WaterHorizonEdgeFalloffId, _surfaceMeshMaterial.GetFloat(EdgeFalloffWidthId));
            if (_surfaceMeshMaterial.HasColor(UnderwaterColorId) && _surfaceMeshMaterial.HasFloat(UnderwaterFogDistanceId))
            {
                Shader.SetGlobalColor(WaterUnderwaterColorId, UnderwaterMurkColor());
                Shader.SetGlobalVector(WaterUnderwaterFogId, new Vector4(
                    origin.z, Mathf.Max(_surfaceMeshMaterial.GetFloat(UnderwaterFogDistanceId), 1e-3f), 0f, 0f));
            }
        }

        private void BuildHorizonExtension(Vector2 fieldOrigin, float fieldWidth, float nearZ, float worldY)
        {
            var go = new GameObject(HorizonObjectName);
            go.transform.SetParent(_generatedRoot, false);
            go.transform.position = new Vector3(fieldOrigin.x, worldY, nearZ);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = HorizonMaterial;
            var sg = go.AddComponent<SortingGroup>();
            sg.sortingLayerID = SpookyWaterSettings.Instance.SurfaceSortingLayerId;

            Water.RefreshCamera();
            _horizonFilter = mf;
            _horizonFieldWidth = fieldWidth;
            _horizonNearZ = nearZ;
            var dx = BuildHorizonMesh();

            var origin = go.transform.position;
            Shader.SetGlobalVector(WaterHorizonSeamId, new Vector4(origin.z, Mathf.Max(_horizonSeamBlend, 1e-3f), dx, origin.x));
            Shader.SetGlobalVector(WaterHorizonHeightScaleId, new Vector4(
                _displacementScale, 0f, 0f, 0f));
            ApplyHorizonProperties(mr, origin);
        }

        private void OnWaterCameraChanged(Camera camera)
        {
            if (_horizonFilter) BuildHorizonMesh();
        }

        private float BuildHorizonMesh()
        {
            var mf = _horizonFilter;
            var fieldWidth = _horizonFieldWidth;
            var nearZ = _horizonNearZ;
            var fieldSegments = Mathf.Max(_horizonXSegments, 1);
            var zSegments = Mathf.Max(_horizonZSegments, 1);
            var dx = fieldWidth / fieldSegments;
            var dz = _horizonDistance / zSegments;

            var tanHalfFov = 1f;
            var cameraZ = nearZ - fieldWidth * 0.5f;
            var cam = Water.Camera;
            if (cam && !cam.orthographic)
            {
                tanHalfFov = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
                cameraZ = cam.transform.position.z;
            }
            float Spread(float worldZ) => Mathf.Max(worldZ - cameraZ, 0f) * tanHalfFov * Mathf.Max(_horizonSpread, 0f);

            var extraColumns = Mathf.Min(Mathf.CeilToInt(Spread(nearZ) / dx), 256);
            var xSegments = fieldSegments + extraColumns * 2;
            var xCols = xSegments + 1;
            var zRows = zSegments + 1;

            var vertices = new Vector3[xCols * zRows];
            var uv = new Vector2[vertices.Length];

            for (var z = 0; z < zRows; z++)
            {
                var side = z == 0 ? extraColumns * dx : Mathf.Max(Spread(nearZ + z * dz), extraColumns * dx);
                var left = -side;
                var width = fieldWidth + side * 2f;
                for (var x = 0; x < xCols; x++)
                {
                    var i = x + z * xCols;
                    vertices[i] = new Vector3(left + width * x / xSegments, 0f, z * dz);
                    uv[i] = new Vector2((float)x / xSegments, (float)z / zSegments);
                }
            }

            var triangles = new int[xSegments * zSegments * 6];
            var ti = 0;
            for (var z = 0; z < zSegments; z++)
            {
                for (var x = 0; x < xSegments; x++)
                {
                    var i = x + z * xCols;

                    triangles[ti++] = i;
                    triangles[ti++] = i + xCols;
                    triangles[ti++] = i + 1;

                    triangles[ti++] = i + 1;
                    triangles[ti++] = i + xCols;
                    triangles[ti++] = i + xCols + 1;
                }
            }

            var mesh = mf.sharedMesh;
            if (!mesh) mesh = new Mesh { name = mf.gameObject.name };
            else mesh.Clear();
            mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return dx;
        }

        private Color UnderwaterMurkColor()
        {
            var murk = _surfaceMeshMaterial.GetColor(UnderwaterColorId);
            if (!_surfaceMeshMaterial.HasColor(UnderwaterHazeColorId)) return murk;
            var haze = _surfaceMeshMaterial.GetColor(UnderwaterHazeColorId);
            var blended = Color.Lerp(murk.linear, haze.linear, haze.a).gamma;
            blended.a = 1f;
            return blended;
        }
    }
}
