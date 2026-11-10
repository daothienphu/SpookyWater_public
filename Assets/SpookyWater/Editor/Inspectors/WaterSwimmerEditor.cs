using UnityEditor;

namespace SpookyWater.Editor
{
    [CustomEditor(typeof(WaterSwimmer))]
    [CanEditMultipleObjects]
    public class WaterSwimmerEditor : GroupedInspector
    {
        protected override void DrawBeforeProperties()
        {
            DrawNoBuilderWarning();
            EditorGUILayout.HelpBox("Reports when this body is in the water and draws its wake, spray and bubbles. Moving it is up to your own controller. " +
                                    "Splashes when it crosses the surface happen automatically, with or without this component.", MessageType.None);
        }
    }
}
