using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    public class GroupedInspector : UnityEditor.Editor
    {
        private const double SceneScanSeconds = 1.0;

        private static readonly Dictionary<Type, Dictionary<string, string>> GroupCache = new();
        private static double _builderScanAt = double.NegativeInfinity;
        private static int _builderCount;

        protected static int BuilderCount
        {
            get
            {
                var now = EditorApplication.timeSinceStartup;
                if (now - _builderScanAt > SceneScanSeconds || now < _builderScanAt)
                {
                    _builderScanAt = now;
                    _builderCount = UnityEngine.Object.FindObjectsByType<WaterTilemapBuilder>(FindObjectsSortMode.None).Length;
                }

                return _builderCount;
            }
        }

        protected virtual bool IsGroupVisible(string group) => true;

        protected virtual bool IsGroupOpenByDefault(string group) =>
            !string.Equals(group, "Advanced", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(group, "Debug", StringComparison.OrdinalIgnoreCase);

        protected virtual void DrawBeforeProperties() { }

        protected virtual void DrawAfterProperties() { }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawBeforeProperties();
            DrawGroupedProperties();
            DrawAfterProperties();
            serializedObject.ApplyModifiedProperties();
        }

        protected void DrawGroupedProperties()
        {
            var groups = GroupsOf(target.GetType());
            var property = serializedObject.GetIterator();
            var enterChildren = true;
            string currentGroup = null;
            var groupOpen = true;
            var groupVisible = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property, true);
                    continue;
                }

                if (groups.TryGetValue(property.name, out var group))
                {
                    currentGroup = group;
                    groupVisible = IsGroupVisible(group);
                    groupOpen = groupVisible && DrawFoldout(group);
                }

                if (!groupVisible || !groupOpen) continue;

                if (currentGroup != null) EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(property, true);
                if (currentGroup != null) EditorGUI.indentLevel--;
            }
        }

        private bool DrawFoldout(string group)
        {
            var key = "SpookyWater." + target.GetType().Name + "." + group;
            var open = SessionState.GetBool(key, IsGroupOpenByDefault(group));
            EditorGUILayout.Space(2f);
            var rect = EditorGUILayout.GetControlRect();
            var next = EditorGUI.Foldout(rect, open, group, true, EditorStyles.foldoutHeader);
            if (next != open) SessionState.SetBool(key, next);
            return next;
        }

        private static Dictionary<string, string> GroupsOf(Type type)
        {
            if (GroupCache.TryGetValue(type, out var cached)) return cached;

            var result = new Dictionary<string, string>();
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (var field in t.GetFields(flags))
                {
                    if (result.ContainsKey(field.Name)) continue;
                    var groupAttribute = field.GetCustomAttribute<GroupAttribute>();
                    if (groupAttribute != null) result[field.Name] = groupAttribute.Name;
                }
            }

            GroupCache[type] = result;
            return result;
        }

        protected static void DrawNoBuilderWarning()
        {
            if (BuilderCount > 0) return;
            EditorGUILayout.HelpBox("There is no water in this scene. Use GameObject > SpookyWater > Water to create it.", MessageType.Warning);
        }

        protected static void DrawSetupWarning()
        {
            if (!SetupLink.TryGetProblem(out var problem)) return;
            EditorGUILayout.HelpBox(problem, MessageType.Warning);
            if (GUILayout.Button("Open Project Setup")) SetupLink.Open();
        }

        protected static bool DrawMissingAssets(SerializedObject serializedObject, string message, Func<SerializedObject, int> assignDefaults, bool missing)
        {
            if (!missing) return false;
            EditorGUILayout.HelpBox(message, MessageType.Warning);
            if (!GUILayout.Button("Assign Defaults")) return false;
            serializedObject.ApplyModifiedProperties();
            var so = new SerializedObject(serializedObject.targetObjects);
            assignDefaults(so);
            so.ApplyModifiedProperties();
            serializedObject.Update();
            return true;
        }
    }
}
