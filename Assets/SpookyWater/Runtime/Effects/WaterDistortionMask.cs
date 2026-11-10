using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace SpookyWater
{
    public static class WaterDistortionMask
    {
        public const string ShaderResourceName = "SpookyWaterDistortionMask";
        private const int MaxRanges = 8;

        private static readonly int MaskId = Shader.PropertyToID("_WaterDistortionMask");
        private static readonly int MaskOnId = Shader.PropertyToID("_WaterDistortionMaskOn");
        private static readonly List<Vector2Int> Ranges = new List<Vector2Int>();

        private static MaskPass _pass;
        private static Shader _shader;
        private static bool _warnedMissingShader;
        private static float _nextShaderLoadTime;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Hook()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Release;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
        }

        private static void Release()
        {
            _pass?.Dispose();
            _pass = null;
        }

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;

            CollectRanges();
            if (Ranges.Count == 0 || !LoadShader())
            {
                Shader.SetGlobalFloat(MaskOnId, 0f);
                return;
            }

            var cameraData = camera.GetUniversalAdditionalCameraData();
            var renderer = cameraData != null ? cameraData.scriptableRenderer : null;
            if (renderer == null) return;

            _pass ??= new MaskPass();
            _pass.Shader = _shader;
            renderer.EnqueuePass(_pass);
        }

        private static bool LoadShader()
        {
            if (_shader) return true;
            if (Time.realtimeSinceStartup < _nextShaderLoadTime) return false;
            _nextShaderLoadTime = Time.realtimeSinceStartup + 1f;
            var shader = Resources.Load<Shader>(ShaderResourceName);
            if (shader && shader.isSupported)
            {
                _shader = shader;
                return true;
            }
            if (!_warnedMissingShader)
            {
                _warnedMissingShader = true;
                Debug.LogWarning($"SpookyWater: the distortion mask shader (Resources/{ShaderResourceName}) is missing or unsupported, so sorting layers unticked in Distorted Sorting Layers are distorted anyway.");
            }
            return false;
        }

        private static void CollectRanges()
        {
            Ranges.Clear();

            var settings = SpookyWaterSettings.Instance;
            var filter = settings.distortedSortingLayers;
            if (filter.IsEverything) return;

            var surfaceValue = SortingLayer.GetLayerValueFromID(settings.SurfaceSortingLayerId);
            var layers = SortingLayer.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                var value = layers[i].value;
                if (value >= surfaceValue || filter.Includes(layers[i].name)) continue;
                var last = Ranges.Count - 1;
                if (last >= 0 && Ranges[last].y == value - 1)
                {
                    Ranges[last] = new Vector2Int(Ranges[last].x, value);
                }
                else if (Ranges.Count < MaxRanges)
                {
                    Ranges.Add(new Vector2Int(value, value));
                }
            }
        }

        private sealed class MaskPass : ScriptableRenderPass
        {
            private static readonly ShaderTagId[] PassTags =
            {
                new ShaderTagId("Universal2D"),
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            private readonly Dictionary<Camera, RTHandle> _targets = new Dictionary<Camera, RTHandle>();
            private readonly List<Camera> _stale = new List<Camera>();

            public Shader Shader;

            private class PassData
            {
                public readonly RendererListHandle[] Lists = new RendererListHandle[MaxRanges];
                public int Count;
            }

            public MaskPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
                profilingSampler = new ProfilingSampler("SpookyWater Distortion Mask");
            }

            public void Dispose()
            {
                foreach (var target in _targets.Values) target?.Release();
                _targets.Clear();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var camera = cameraData.camera;

                var target = GetTarget(camera, cameraData.cameraTargetDescriptor);
                var targetHandle = renderGraph.ImportTexture(target);

                using var builder = renderGraph.AddRasterRenderPass<PassData>("SpookyWater Distortion Mask", out var passData, profilingSampler);

                var sorting = new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent };
                var drawing = new DrawingSettings(PassTags[0], sorting)
                {
                    overrideShader = Shader,
                    overrideShaderPassIndex = 0,
                    perObjectData = PerObjectData.None
                };
                for (var i = 1; i < PassTags.Length; i++) drawing.SetShaderPassName(i, PassTags[i]);

                passData.Count = 0;
                for (var i = 0; i < Ranges.Count; i++)
                {
                    var filtering = new FilteringSettings(RenderQueueRange.all, camera.cullingMask)
                    {
                        sortingLayerRange = new SortingLayerRange((short)Ranges[i].x, (short)Ranges[i].y)
                    };
                    var list = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
                    passData.Lists[passData.Count++] = list;
                    builder.UseRendererList(list);
                }

                builder.SetRenderAttachment(targetHandle, 0, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(targetHandle, MaskId);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(false, true, Color.clear);
                    for (var i = 0; i < data.Count; i++) context.cmd.DrawRendererList(data.Lists[i]);
                    context.cmd.SetGlobalFloat(MaskOnId, 1f);
                });
            }

            private RTHandle GetTarget(Camera camera, RenderTextureDescriptor cameraDescriptor)
            {
                if (_targets.Count > 8) DropDestroyedCameras();

                var descriptor = cameraDescriptor;
                descriptor.width = Mathf.Max(1, descriptor.width / 2);
                descriptor.height = Mathf.Max(1, descriptor.height / 2);
                descriptor.msaaSamples = 1;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;
                descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R8_UNorm
                    : GraphicsFormat.R8G8B8A8_UNorm;

                _targets.TryGetValue(camera, out var target);
                RenderingUtils.ReAllocateHandleIfNeeded(ref target, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_WaterDistortionMask");
                _targets[camera] = target;
                return target;
            }

            private void DropDestroyedCameras()
            {
                _stale.Clear();
                foreach (var pair in _targets)
                {
                    if (!pair.Key) _stale.Add(pair.Key);
                }
                foreach (var camera in _stale)
                {
                    _targets[camera]?.Release();
                    _targets.Remove(camera);
                }
            }
        }
    }
}
