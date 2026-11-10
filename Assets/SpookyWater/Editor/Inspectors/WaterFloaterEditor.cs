using System;
using UnityEditor;

namespace SpookyWater.Editor
{
    [CustomEditor(typeof(WaterFloater))]
    [CanEditMultipleObjects]
    public class WaterFloaterEditor : GroupedInspector
    {
        private SerializedProperty _mode;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty("_mode");
        }

        protected override void DrawBeforeProperties()
        {
            DrawNoBuilderWarning();

            if (_mode == null || _mode.hasMultipleDifferentValues) return;

            if (_mode.enumValueIndex == (int)WaterFloaterMode.Platform)
            {
                EditorGUILayout.HelpBox("Platform: a stiff raft. It dips a little when landed on, leans toward whoever stands near an end, never sinks below the surface and stays where it was placed.", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Object: a crate, log or barrel. It sinks under anything standing on it and bobs back up once it is left alone.", MessageType.None);
            }
        }

        protected override bool IsGroupVisible(string group)
        {
            if (_mode == null || _mode.hasMultipleDifferentValues) return true;
            var isPlatform = _mode.enumValueIndex == (int)WaterFloaterMode.Platform;
            if (string.Equals(group, "Platform", StringComparison.OrdinalIgnoreCase)) return isPlatform;
            if (string.Equals(group, "Object", StringComparison.OrdinalIgnoreCase)) return !isPlatform;
            return true;
        }
    }
}
