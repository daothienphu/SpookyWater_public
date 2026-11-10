using UnityEditor;

namespace SpookyWater.Editor
{
    [CustomEditor(typeof(WaterSplashEffects))]
    [CanEditMultipleObjects]
    public class WaterSplashEffectsEditor : GroupedInspector
    {
        protected override void DrawBeforeProperties()
        {
            DrawNoBuilderWarning();
            DrawMissingAssets(serializedObject,
                "Some particle materials are not assigned, so some splash pieces will not draw.",
                SpookyWaterDefaults.AssignSplashMaterials,
                SpookyWaterDefaults.SplashMaterialsMissing(serializedObject));
        }
    }
}
