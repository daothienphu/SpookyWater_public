#if UNITY_EDITOR
using UnityEngine;

namespace SpookyWater
{
    public static class WaterEditorDefaults
    {
        public const string SurfaceDebrisMaterial = "8e5e6f5da9b741cdbe3b2e35c1b51530";
        public const string LightShaftMaterial = "7417e6c757c645aabad8c660f58a04c9";
        public const string LightShaftUnderwaterMaterial = "4928cf3f8cff8fe498d1abe0c2e52a00";
        public const string LightShaftBandsTexture = "de43ed7c2b8d44d8a6e163d1dd1346bc";
        public const string LightShaftBandsFineTexture = "57e4a0e96a874f03a096a2fb1e6e43fc";
        public const string BubbleMaterial = "8018af1230543c64dbcc4a262e166fb1";
        public const string FoamRingMaterial = "878afd9ef97e7d04683915cbaaa98842";
        public const string SplashCrownMaterial = "0c05c1388bda44c4697d978a60838935";
        public const string SplashDropletMaterial = "c4edc76395f379143a7ff8df4ab2a561";
        public const string SplashMistMaterial = "bd154c20d87493d4da7b9e8de8945dda";
        public const string LilyPadASprite = "a1db7989f0bb81f4db0ee451b339181a";

        public static T Load<T>(string guid) where T : Object
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
#endif
