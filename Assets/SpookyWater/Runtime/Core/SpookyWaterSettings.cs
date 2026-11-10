using UnityEngine;

namespace SpookyWater
{
    [CreateAssetMenu(fileName = ResourceName, menuName = "SpookyWater/Settings")]
    public class SpookyWaterSettings : ScriptableObject
    {
        public const string ResourceName = "SpookyWaterSettings";
        public const string DefaultReflectionLayer = "SpookyWater_Reflections";
        public const string DefaultSurfaceSortingLayer = "SpookyWater_Surface";
        public const string DefaultFrontSortingLayer = "SpookyWater_Front";

        private static SpookyWaterSettings _instance;
        private static float _nextLoadTime;

        [Tooltip("Physics layer that holds the hidden proxy sprites WaterReflectable creates. Only the reflection camera renders it. Never put your own objects on it.")]
        public string reflectionLayer = DefaultReflectionLayer;

        [Tooltip("Sorting layer for the water surface. Everything on layers before it is refracted by the front glass, so the URP 2D Renderer's Camera Sorting Layer Texture must stop here.")]
        public string surfaceSortingLayer = DefaultSurfaceSortingLayer;

        [Tooltip("Sorting layer for the front glass of the water. It must come after the surface layer.")]
        public string frontSortingLayer = DefaultFrontSortingLayer;

        [Tooltip("Sorting order of floating debris on the surface layer. It must be above the surface tiles, which use 0.")]
        public int debrisSortingOrder = 10;

        [Tooltip("Sorting order of underwater light shafts on the front layer. It must be above the front tiles and below the spray.")]
        public int underwaterShaftSortingOrder = 500;

        [Tooltip("Sorting order of bubbles and surface rings on the surface layer. It is high so they draw after the surface tiles.")]
        public int bubbleSortingOrder = 1000;

        [Tooltip("Sorting order of spray thrown above the water on the front layer. It is high so it draws after the front glass.")]
        public int spraySortingOrder = 1000;

        [Tooltip("Sorting layers the front glass bends (WaterFront Distortion). Untick a layer, e.g. Player, to keep everything on it sharp behind the water: still tinted and fogged, just not wobbled. Only layers before the surface layer are listed, since only they are seen through the glass. Layers added later start ticked.")]
        public SortingLayerFilter distortedSortingLayers = SortingLayerFilter.Everything;

        [Tooltip("When no camera is given by Water.SetCamera, an enabled Water Camera Source or Water.CameraResolver, use the camera tagged MainCamera. Turn off if your project tags another camera (e.g. a UI camera) MainCamera, so the water waits for your camera system instead of following the wrong one.")]
        public bool mainCameraFallback = true;

        public static SpookyWaterSettings Instance
        {
            get
            {
                if (_instance && _instance.hideFlags == HideFlags.None) return _instance;
                if (!_instance || Time.realtimeSinceStartup >= _nextLoadTime)
                {
                    _nextLoadTime = Time.realtimeSinceStartup + 1f;
                    var loaded = Resources.Load<SpookyWaterSettings>(ResourceName);
                    if (loaded) _instance = loaded;
                }
                if (_instance) return _instance;
                _instance = CreateInstance<SpookyWaterSettings>();
                _instance.hideFlags = HideFlags.HideAndDontSave;
                return _instance;
            }
        }

        public int ReflectionLayerIndex => string.IsNullOrEmpty(reflectionLayer) ? -1 : LayerMask.NameToLayer(reflectionLayer);

        public int SurfaceSortingLayerId => ResolveSortingLayer(surfaceSortingLayer);

        public int FrontSortingLayerId => ResolveSortingLayer(frontSortingLayer);

        private static int ResolveSortingLayer(string layerName)
        {
            return HasSortingLayer(layerName) ? SortingLayer.NameToID(layerName) : SortingLayer.NameToID("Default");
        }

        private static bool HasSortingLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return false;
            var layers = SortingLayer.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == layerName) return true;
            }
            return false;
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(reflectionLayer)) reflectionLayer = DefaultReflectionLayer;
            if (string.IsNullOrEmpty(surfaceSortingLayer)) surfaceSortingLayer = DefaultSurfaceSortingLayer;
            if (string.IsNullOrEmpty(frontSortingLayer)) frontSortingLayer = DefaultFrontSortingLayer;
        }
    }
}
