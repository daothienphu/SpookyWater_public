using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpookyWater.Editor
{
    internal static class SetupUtility
    {
        public const string Prefix = "[SpookyWater Setup] ";
        public const int FirstUserLayer = 8;
        public const int LayerCount = 32;
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";

        public static string PackageRoot
        {
            get
            {
                var guids = AssetDatabase.FindAssets("SpookyWaterSettings t:MonoScript");
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (Path.GetFileName(path) != "SpookyWaterSettings.cs") continue;
                    var index = path.LastIndexOf("/Runtime/", System.StringComparison.Ordinal);
                    if (index > 0) return path.Substring(0, index);
                }
                return "Assets/SpookyWater";
            }
        }

        public static SpookyWaterSettings Settings
        {
            get
            {
                return SpookyWaterSettings.Instance;
            }
        }

        public static bool SettingsAssetExists => Resources.Load<SpookyWaterSettings>(SpookyWaterSettings.ResourceName) != null;

        public static SerializedObject LoadTagManager()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            return assets != null && assets.Length > 0 ? new SerializedObject(assets[0]) : null;
        }

        public static void SaveTagManager(SerializedObject tagManager)
        {
            tagManager.ApplyModifiedProperties();
            EditorUtility.SetDirty(tagManager.targetObject);
            AssetDatabase.SaveAssets();
        }

        public static int FindLayerSlot(SerializedObject tagManager, string layerName)
        {
            var layers = tagManager.FindProperty("layers");
            if (layers == null || string.IsNullOrEmpty(layerName)) return -1;
            for (var i = FirstUserLayer; i < layers.arraySize && i < LayerCount; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return i;
            }
            return -1;
        }

        public static int FindFreeLayerSlot(SerializedObject tagManager)
        {
            var layers = tagManager.FindProperty("layers");
            if (layers == null) return -1;
            for (var i = FirstUserLayer; i < layers.arraySize && i < LayerCount; i++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) return i;
            }
            return -1;
        }

        public static int FindSortingLayerIndex(SerializedObject tagManager, string layerName)
        {
            var layers = tagManager.FindProperty("m_SortingLayers");
            if (layers == null || string.IsNullOrEmpty(layerName)) return -1;
            for (var i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == layerName) return i;
            }
            return -1;
        }

        public static bool TryGetSortingLayerId(SerializedObject tagManager, string layerName, out int id)
        {
            id = 0;
            var index = FindSortingLayerIndex(tagManager, layerName);
            if (index < 0) return false;
            var layers = tagManager.FindProperty("m_SortingLayers");
            var unique = layers.GetArrayElementAtIndex(index).FindPropertyRelative("uniqueID").longValue;
            id = unchecked((int)(uint)unique);
            return true;
        }

        public static void InsertSortingLayer(SerializedObject tagManager, string layerName, int position)
        {
            var layers = tagManager.FindProperty("m_SortingLayers");
            position = Mathf.Clamp(position, 0, layers.arraySize);
            layers.InsertArrayElementAtIndex(position);
            var element = layers.GetArrayElementAtIndex(position);
            element.FindPropertyRelative("name").stringValue = layerName;
            element.FindPropertyRelative("uniqueID").longValue = NewSortingLayerId(layers);
            element.FindPropertyRelative("locked").intValue = 0;
        }

        private static long NewSortingLayerId(SerializedProperty layers)
        {
            var used = new HashSet<long>();
            for (var i = 0; i < layers.arraySize; i++)
            {
                used.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("uniqueID").longValue);
            }
            long id;
            do
            {
                id = Random.Range(1, int.MaxValue);
            } while (used.Contains(id));
            return id;
        }

        public static int ActorAnchorIndex(SerializedObject tagManager)
        {
            return Mathf.Max(FindSortingLayerIndex(tagManager, "Default"), FindSortingLayerIndex(tagManager, "Player"));
        }

        public static List<UniversalRenderPipelineAsset> CollectPipelineAssets()
        {
            var result = new List<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset current) result.Add(current);
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset fallback && !result.Contains(fallback)) result.Add(fallback);
            var levels = QualitySettings.names.Length;
            for (var i = 0; i < levels; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset asset && !result.Contains(asset)) result.Add(asset);
            }
            return result;
        }

        public static List<Renderer2DData> Find2DRenderers()
        {
            var result = new List<Renderer2DData>();
            var assets = CollectPipelineAssets();
            for (var a = 0; a < assets.Count; a++)
            {
                var list = new SerializedObject(assets[a]).FindProperty("m_RendererDataList");
                if (list == null) continue;
                for (var i = 0; i < list.arraySize; i++)
                {
                    var renderer = list.GetArrayElementAtIndex(i).objectReferenceValue as Renderer2DData;
                    if (renderer != null && !result.Contains(renderer)) result.Add(renderer);
                }
            }
            return result;
        }
    }
}
