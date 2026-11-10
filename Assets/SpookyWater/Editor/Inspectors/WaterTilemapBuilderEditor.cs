using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SpookyWater.Editor
{
    [CustomEditor(typeof(WaterTilemapBuilder))]
    [CanEditMultipleObjects]
    public class WaterTilemapBuilderEditor : GroupedInspector
    {
        protected override void DrawBeforeProperties()
        {
            DrawStatus();
            DrawButtons();
            EditorGUILayout.Space(4f);
        }

        private void DrawStatus()
        {
            var builder = (WaterTilemapBuilder)target;

            if (!builder.FrontTilemap)
            {
                EditorGUILayout.HelpBox("No Front Tilemap. Assign one below, or put this component on the same GameObject as a Tilemap. " +
                                        "GameObject > SpookyWater > Water sets all of this up for you.", MessageType.Error);
            }
            else if (builder.FrontTilemap.GetUsedTilesCount() == 0)
            {
                EditorGUILayout.HelpBox("The Front Tilemap is empty. Paint the Water Body tile on it, and the Water Surface tile on the Surface Tilemap. " +
                                        "Assets > Create > SpookyWater > Tile Palette (or the Create Tile Palette button below) makes a palette with both tiles.", MessageType.Info);
            }

            DrawMissingAssets(serializedObject,
                "Some water materials are not assigned, so the water cannot be generated.",
                SpookyWaterDefaults.AssignBuilderMaterials,
                SpookyWaterDefaults.BuilderMaterialsMissing(serializedObject));

            DrawSetupWarning();

            if (BuilderCount > 1)
            {
                EditorGUILayout.HelpBox("There is more than one water builder in this scene. Only one level is supported per scene. " +
                                        "To make several ponds, paint them all on the same two tilemaps.", MessageType.Warning);
            }

            var camera = Water.Camera;
            if (camera && camera.orthographic)
            {
                EditorGUILayout.HelpBox($"The water camera '{camera.name}' is orthographic. The water surface lies flat on the XZ plane, so it looks like a thin line from a flat side view. " +
                                        "Use a perspective camera that looks slightly down at the water.", MessageType.Info);
            }
        }

        private void DrawButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Generate Water", "Builds the water meshes from the painted tilemaps so you can preview them without pressing Play. Pressing Play regenerates them anyway."), GUILayout.Height(24f)))
                {
                    foreach (var t in targets) Generate((WaterTilemapBuilder)t);
                }

                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    if (GUILayout.Button(new GUIContent("Clear", "Removes the generated water and shows the painted tiles again so you can edit them."), GUILayout.Height(24f)))
                    {
                        foreach (var t in targets) Clear((WaterTilemapBuilder)t);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var builder = (WaterTilemapBuilder)target;
                using (new EditorGUI.DisabledScope(!builder.FrontTilemap || targets.Length > 1))
                {
                    if (GUILayout.Button("Select Front Tilemap")) Selection.activeGameObject = builder.FrontTilemap.gameObject;
                }

                var surface = serializedObject.FindProperty("_surfaceTilemap").objectReferenceValue as Tilemap;
                using (new EditorGUI.DisabledScope(!surface || targets.Length > 1))
                {
                    if (GUILayout.Button("Select Surface Tilemap")) Selection.activeGameObject = surface.gameObject;
                }

                if (GUILayout.Button("Create Tile Palette")) SpookyWaterCreateMenu.CreateTilePalette();
            }
        }

        private static void Generate(WaterTilemapBuilder builder)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Generate Water");
            var group = Undo.GetCurrentGroup();
            builder.Generate();
            Undo.CollapseUndoOperations(group);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }

        private static void Clear(WaterTilemapBuilder builder)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clear Water");
            var group = Undo.GetCurrentGroup();
            builder.Clear();
            Undo.CollapseUndoOperations(group);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }
    }
}
