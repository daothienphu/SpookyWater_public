using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    internal static class SpookyWaterSettingsProvider
    {
        private static UnityEditor.Editor _editor;

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/SpookyWater", SettingsScope.Project)
            {
                label = "SpookyWater",
                keywords = new[] { "water", "spookywater", "sorting", "layer", "reflection" },
                guiHandler = Draw,
                deactivateHandler = () =>
                {
                    if (_editor != null) Object.DestroyImmediate(_editor);
                    _editor = null;
                }
            };
        }

        private static void Draw(string searchContext)
        {
            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Open Project Setup", GUILayout.Height(26f))) ProjectSetup.Open();
            EditorGUILayout.Space(6f);

            var asset = Resources.Load<SpookyWaterSettings>(SpookyWaterSettings.ResourceName);
            if (asset == null)
            {
                EditorGUILayout.HelpBox("There is no settings asset yet, so the package uses built-in defaults. Create one to rename the layers or change the sorting orders.", MessageType.Info);
                if (GUILayout.Button("Create Settings Asset"))
                {
                    new SettingsAssetRequirement().Fix();
                }
                return;
            }

            if (_editor == null || _editor.target != asset) UnityEditor.Editor.CreateCachedEditor(asset, null, ref _editor);
            _editor.OnInspectorGUI();
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox("After changing a layer name, open Project Setup and press Fix All so the layer exists and the 2D Renderer points at it.", MessageType.None);
        }
    }
}
