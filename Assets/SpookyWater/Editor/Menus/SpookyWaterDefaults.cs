using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    public static class SpookyWaterDefaults
    {
        public const string WaterSurfaceMaterial = "9d6920e6abb01014b8334006b90ff58c";
        public const string WaterFrontMaterial = "ef07008fd91c59c4aa4bfed529eb2287";
        public const string WaterHorizonShader = "a17e3c9b5d2f4b1e8a6f9c3d7b2e4f10";
        public const string RippleSimulationMaterial = "95d5f44b9d81f4447a803ed975fcc43d";
        public const string SurfaceDebrisMaterial = WaterEditorDefaults.SurfaceDebrisMaterial;
        public const string LightShaftMaterial = WaterEditorDefaults.LightShaftMaterial;
        public const string LightShaftUnderwaterMaterial = WaterEditorDefaults.LightShaftUnderwaterMaterial;
        public const string LightShaftBandsTexture = WaterEditorDefaults.LightShaftBandsTexture;
        public const string LightShaftBandsFineTexture = WaterEditorDefaults.LightShaftBandsFineTexture;
        public const string FogMaterial = "d9c98e48c2c3b0840842d42ac15cf77f";
        public const string SpriteLitDepthWriteMaterial = "817f859300dca0449a1da39771a1da40";
        public const string SpriteLitAutoNormalsMaterial = "2a8c06d12d414e188cdc977a58e7d7ba";

        public const string BubbleMaterial = WaterEditorDefaults.BubbleMaterial;
        public const string FoamRingMaterial = WaterEditorDefaults.FoamRingMaterial;
        public const string SplashCrownMaterial = WaterEditorDefaults.SplashCrownMaterial;
        public const string SplashDropletMaterial = WaterEditorDefaults.SplashDropletMaterial;
        public const string SplashMistMaterial = WaterEditorDefaults.SplashMistMaterial;

        public const string WaterBodyTile = "5e388552fe631c0418cd698d35017875";
        public const string WaterSurfaceTile = "d90e25f75c16ed04cae03a4bdae6bc9c";
        public const string WaterBodySprite = "2b8b532a9acc20d43a5faf43fc334624";
        public const string WaterSurfaceSprite = "e10ca10ae43492f45b4a2d6d571634d6";

        public const string LilyPadASprite = WaterEditorDefaults.LilyPadASprite;
        public const string LilyPadBSprite = "60645d4c040ef134da68ee80ec45cde9";
        public const string LeafSprite = "2e10e91977bbce542b9ecd0350ab8be7";
        public const string TwigSprite = "1e29b916424a3b04299088688cea0ce7";

        public static T Load<T>(string guid) where T : Object
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static bool AssignIfEmpty<T>(SerializedObject serializedObject, string propertyName, string guid) where T : Object
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return false;
            if (property.objectReferenceValue) return false;
            var asset = Load<T>(guid);
            if (!asset) return false;
            property.objectReferenceValue = asset;
            return true;
        }

        public static bool IsEmpty(SerializedObject serializedObject, string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.ObjectReference && !property.objectReferenceValue;
        }

        public static int AssignBuilderMaterials(SerializedObject builder)
        {
            var count = 0;
            if (AssignIfEmpty<Material>(builder, "_surfaceMeshMaterial", WaterSurfaceMaterial)) count++;
            if (AssignIfEmpty<Material>(builder, "_lineMeshMaterial", WaterFrontMaterial)) count++;
            if (AssignIfEmpty<Material>(builder, "_bodyMeshMaterial", WaterFrontMaterial)) count++;
            if (AssignIfEmpty<Shader>(builder, "_horizonShader", WaterHorizonShader)) count++;
            if (AssignIfEmpty<Material>(builder, "_rippleSimulationMaterial", RippleSimulationMaterial)) count++;
            return count;
        }

        public static bool BuilderMaterialsMissing(SerializedObject builder)
        {
            return IsEmpty(builder, "_surfaceMeshMaterial") || IsEmpty(builder, "_lineMeshMaterial") ||
                   IsEmpty(builder, "_bodyMeshMaterial") ||
                   IsEmpty(builder, "_rippleSimulationMaterial");
        }

        public static int AssignSplashMaterials(SerializedObject splash)
        {
            var count = 0;
            if (AssignIfEmpty<Material>(splash, "_dropletMaterial", SplashDropletMaterial)) count++;
            if (AssignIfEmpty<Material>(splash, "_mistMaterial", SplashMistMaterial)) count++;
            if (AssignIfEmpty<Material>(splash, "_crownMaterial", SplashCrownMaterial)) count++;
            if (AssignIfEmpty<Material>(splash, "_bubbleMaterial", BubbleMaterial)) count++;
            if (AssignIfEmpty<Material>(splash, "_ringMaterial", FoamRingMaterial)) count++;
            return count;
        }

        public static bool SplashMaterialsMissing(SerializedObject splash)
        {
            return IsEmpty(splash, "_dropletMaterial") || IsEmpty(splash, "_mistMaterial") || IsEmpty(splash, "_crownMaterial") ||
                   IsEmpty(splash, "_bubbleMaterial") || IsEmpty(splash, "_ringMaterial");
        }

        public static int AssignDebrisDefaults(SerializedObject debris)
        {
            var count = 0;
            if (AssignIfEmpty<Sprite>(debris, "_sprite", LilyPadASprite)) count++;
            if (AssignIfEmpty<Material>(debris, "_material", SurfaceDebrisMaterial)) count++;
            return count;
        }

        public static bool DebrisDefaultsMissing(SerializedObject debris)
        {
            return IsEmpty(debris, "_sprite") || IsEmpty(debris, "_material");
        }

        public static int AssignLightShaftDefaults(SerializedObject shaft)
        {
            var count = 0;
            if (AssignIfEmpty<Material>(shaft, "_material", LightShaftUnderwaterMaterial)) count++;
            if (AssignIfEmpty<Texture2D>(shaft, "_layerA.texture", LightShaftBandsTexture)) count++;
            if (AssignIfEmpty<Texture2D>(shaft, "_layerB.texture", LightShaftBandsFineTexture)) count++;
            return count;
        }

        public static bool LightShaftDefaultsMissing(SerializedObject shaft)
        {
            return IsEmpty(shaft, "_material");
        }
    }
}
