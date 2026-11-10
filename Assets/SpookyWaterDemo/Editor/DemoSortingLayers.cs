using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SpookyWater.Demo.Editor
{
    [InitializeOnLoad]
    internal static class DemoSortingLayers
    {
        private const string DemoSceneName = "Demo_TiledWater";
        private const string PlayerPrefabGuid = "fe20d6746d7846c48aca5bc799adcb71";

        private static readonly (string Name, uint Id)[] Layers =
        {
            ("SkyBox", 1309675219u),
            ("Background_3", 3213278933u),
            ("Background_2", 3203204525u),
            ("Default", 0u),
            ("Player", 2819560227u),
            ("SpookyWater_Surface", 3377283035u),
            ("SpookyWater_Front", 261461823u),
            ("Foreground", 32768599u),
        };

        static DemoSortingLayers()
        {
            EditorApplication.delayCall += EnsureLayers;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        [MenuItem("Tools/SpookyWater/Demo/Fix Demo Sorting Layers")]
        private static void FixFromMenu()
        {
            EnsureLayers();
            var scene = SceneManager.GetActiveScene();
            if (scene.name == DemoSceneName) RemapScene(scene, true);
            RemapPlayerPrefab();
        }

        private static void EnsureLayers()
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManager == null || tagManager.Length == 0) return;

            var so = new SerializedObject(tagManager[0]);
            var list = so.FindProperty("m_SortingLayers");
            if (list == null) return;

            var added = new List<string>();
            var renamedElsewhere = false;
            var previousIndex = -1;
            foreach (var (name, id) in Layers)
            {
                var byId = Find(list, p => p.FindPropertyRelative("uniqueID").uintValue == id);
                if (byId >= 0)
                {
                    previousIndex = byId;
                    continue;
                }

                var byName = Find(list, p => p.FindPropertyRelative("name").stringValue == name);
                if (byName >= 0)
                {
                    renamedElsewhere = true;
                    previousIndex = byName;
                    continue;
                }

                var insertAt = previousIndex + 1;
                list.InsertArrayElementAtIndex(insertAt);
                var element = list.GetArrayElementAtIndex(insertAt);
                element.FindPropertyRelative("name").stringValue = name;
                element.FindPropertyRelative("uniqueID").uintValue = id;
                element.FindPropertyRelative("locked").intValue = 0;
                previousIndex = insertAt;
                added.Add(name);
            }

            if (added.Count > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log("SpookyWater demo: added sorting layers " + string.Join(", ", added) + ".");
            }

            if (renamedElsewhere) RemapPlayerPrefab();
        }

        private static int Find(SerializedProperty list, System.Func<SerializedProperty, bool> match)
        {
            for (var i = 0; i < list.arraySize; i++)
            {
                if (match(list.GetArrayElementAtIndex(i))) return i;
            }
            return -1;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.name == DemoSceneName) RemapScene(scene, false);
        }

        private static bool TryRemap(int id, out int remapped)
        {
            remapped = id;
            if (SortingLayer.IsValid(id)) return false;
            foreach (var (name, layerId) in Layers)
            {
                if (unchecked((int)layerId) != id) continue;
                var target = SortingLayer.NameToID(name);
                if (!SortingLayer.IsValid(target)) return false;
                remapped = target;
                return true;
            }
            return false;
        }

        private static int RemapObjects(IEnumerable<GameObject> roots)
        {
            var changed = 0;
            foreach (var root in roots)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!TryRemap(renderer.sortingLayerID, out var id)) continue;
                    Undo.RecordObject(renderer, "Remap Sorting Layer");
                    renderer.sortingLayerID = id;
                    changed++;
                }
                foreach (var group in root.GetComponentsInChildren<SortingGroup>(true))
                {
                    if (!TryRemap(group.sortingLayerID, out var id)) continue;
                    Undo.RecordObject(group, "Remap Sorting Layer");
                    group.sortingLayerID = id;
                    changed++;
                }
            }
            return changed;
        }

        private static void RemapScene(Scene scene, bool log)
        {
            var changed = RemapObjects(scene.GetRootGameObjects());
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"SpookyWater demo: remapped {changed} renderer(s) in {scene.name} onto this project's sorting layers. Save the scene to keep it.");
            }
            else if (log)
            {
                Debug.Log($"SpookyWater demo: {scene.name} already uses valid sorting layers.");
            }
        }

        private static void RemapPlayerPrefab()
        {
            var path = AssetDatabase.GUIDToAssetPath(PlayerPrefabGuid);
            if (string.IsNullOrEmpty(path)) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (RemapObjects(new[] { root }) > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
