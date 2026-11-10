using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    [CustomPropertyDrawer(typeof(SortingLayerFilter))]
    public class SortingLayerFilterDrawer : PropertyDrawer
    {
        private static readonly List<string> Candidates = new List<string>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var excluded = property.FindPropertyRelative("_excluded");
            CollectCandidates(fieldInfo != null && fieldInfo.GetCustomAttribute<AllSortingLayersAttribute>() != null);

            EditorGUI.BeginProperty(position, label, property);
            if (Candidates.Count == 0)
            {
                EditorGUI.LabelField(position, label, new GUIContent("No sorting layers to choose from"));
                EditorGUI.EndProperty();
                return;
            }

            var mask = 0;
            for (var i = 0; i < Candidates.Count; i++)
            {
                if (!Contains(excluded, Candidates[i])) mask |= 1 << i;
            }
            if (mask == (int)((1L << Candidates.Count) - 1)) mask = -1;

            EditorGUI.BeginChangeCheck();
            var newMask = EditorGUI.MaskField(position, label, mask, Candidates.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                var kept = new List<string>();
                for (var i = 0; i < excluded.arraySize; i++)
                {
                    var name = excluded.GetArrayElementAtIndex(i).stringValue;
                    if (!Candidates.Contains(name)) kept.Add(name);
                }
                for (var i = 0; i < Candidates.Count; i++)
                {
                    if ((newMask & (1 << i)) == 0) kept.Add(Candidates[i]);
                }
                excluded.arraySize = kept.Count;
                for (var i = 0; i < kept.Count; i++) excluded.GetArrayElementAtIndex(i).stringValue = kept[i];
            }
            EditorGUI.EndProperty();
        }

        private static void CollectCandidates(bool allLayers)
        {
            Candidates.Clear();
            var surfaceValue = SortingLayer.GetLayerValueFromID(SpookyWaterSettings.Instance.SurfaceSortingLayerId);
            foreach (var layer in SortingLayer.layers)
            {
                if ((allLayers || layer.value < surfaceValue) && Candidates.Count < 32) Candidates.Add(layer.name);
            }
        }

        private static bool Contains(SerializedProperty array, string value)
        {
            for (var i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).stringValue == value) return true;
            }
            return false;
        }
    }
}
