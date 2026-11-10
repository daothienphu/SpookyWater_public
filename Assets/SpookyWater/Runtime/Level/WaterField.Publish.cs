using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    public partial class WaterField
    {
        private static readonly int PlanarReflectionTexId = Shader.PropertyToID("_PlanarReflectionTexture");
        private static readonly int ViewedFromBelowId = Shader.PropertyToID("_WaterViewedFromBelow");
        private static readonly int HorizonSeamId = Shader.PropertyToID("_WaterHorizonSeam");

        private bool _local;
        private readonly List<Renderer> _localRenderers = new();
        private readonly Dictionary<int, Vector4> _localVectors = new();
        private readonly Dictionary<int, float> _localFloats = new();
        private readonly Dictionary<int, Texture> _localTextures = new();
        private readonly Dictionary<int, Vector4[]> _localArrays = new();
        private MaterialPropertyBlock _localBlock;

        public bool IsLocal => _local;

        public float SurfaceHeight => _surfaceY;

        public void InitializeLocal(Vector2 worldOrigin, Vector2 worldSize, Material simulationMaterial,
            RippleGridSettings settings, AmbientRippleSettings ambientSettings, FrontRippleSettings frontSettings,
            bool verboseLogging = false)
        {
            _local = true;
            _localRenderers.Clear();
            _localVectors.Clear();
            _localFloats.Clear();
            _localTextures.Clear();
            _localArrays.Clear();

            PublishVector(MicroRippleParamsId, Vector4.zero);
            PublishVectorArray(MicroRipplesId, _microRipples);
            PublishFloat(BowWaveCountId, 0f);
            PublishVectorArray(BowWavesId, _bowWaveData);
            PublishVectorArray(BowWaveShapesId, _bowWaveShapeData);
            PublishFloat(WaterContactCountId, 0f);
            PublishVectorArray(WaterContactsId, _contacts);
            PublishVector(WaterWindId, Vector4.zero);
            PublishVector(WaterWindShapeId, Vector4.zero);
            PublishVector(WaterWindPhaseId, Vector4.zero);
            PublishVector(HorizonSeamId, Vector4.zero);
            PublishVector(DebugClickMarkerDataId, new Vector4(0f, 0f, -1000f, 1f));
            PublishTexture(PlanarReflectionTexId, Texture2D.blackTexture);
            PublishFloat(ViewedFromBelowId, 0f);

            Initialize(worldOrigin, worldSize, simulationMaterial, settings, ambientSettings, frontSettings, verboseLogging);
        }

        public void AddLocalRenderer(Renderer target)
        {
            if (!_local || !target) return;
            _localRenderers.Add(target);
            ApplyLocal(target);
        }

        public void ApplyLocalProperties()
        {
            if (!_local) return;
            for (var i = _localRenderers.Count - 1; i >= 0; i--)
            {
                if (_localRenderers[i]) ApplyLocal(_localRenderers[i]);
                else _localRenderers.RemoveAt(i);
            }
        }

        private void ApplyLocal(Renderer target)
        {
            _localBlock ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(_localBlock);
            foreach (var (id, value) in _localVectors) _localBlock.SetVector(id, value);
            foreach (var (id, value) in _localFloats) _localBlock.SetFloat(id, value);
            foreach (var (id, value) in _localTextures) _localBlock.SetTexture(id, value);
            foreach (var (id, value) in _localArrays) _localBlock.SetVectorArray(id, value);
            target.SetPropertyBlock(_localBlock);
        }

        private void LateUpdate()
        {
            if (!_local) return;

            var cam = ViewCamera;
            if (cam) PublishFloat(ViewedFromBelowId, cam.transform.position.y < _surfaceY ? 1f : 0f);
            ApplyLocalProperties();
        }

        private void PublishVector(int id, Vector4 value)
        {
            if (_local) _localVectors[id] = value;
            else Shader.SetGlobalVector(id, value);
        }

        private void PublishFloat(int id, float value)
        {
            if (_local) _localFloats[id] = value;
            else Shader.SetGlobalFloat(id, value);
        }

        private void PublishTexture(int id, Texture value)
        {
            if (!value) return;
            if (_local) _localTextures[id] = value;
            else Shader.SetGlobalTexture(id, value);
        }

        private void PublishVectorArray(int id, Vector4[] values)
        {
            if (_local) _localArrays[id] = values;
            else Shader.SetGlobalVectorArray(id, values);
        }
    }
}
