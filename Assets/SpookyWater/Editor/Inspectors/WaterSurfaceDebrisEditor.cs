using UnityEditor;

namespace SpookyWater.Editor
{
    [CustomEditor(typeof(WaterSurfaceDebris))]
    [CanEditMultipleObjects]
    public class WaterSurfaceDebrisEditor : GroupedInspector
    {
        protected override void DrawBeforeProperties()
        {
            DrawNoBuilderWarning();
            DrawMissingAssets(serializedObject,
                "The sprite or the material is not assigned, so nothing will draw.",
                SpookyWaterDefaults.AssignDebrisDefaults,
                SpookyWaterDefaults.DebrisDefaultsMissing(serializedObject));
        }
    }
}
