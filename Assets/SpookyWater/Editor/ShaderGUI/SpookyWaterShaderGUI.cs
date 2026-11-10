using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyWater.Editor
{
    public class SpookyWaterShaderGUI : ShaderGUI
    {
        private const string KeyPrefix = "SpookyWater.ShaderGUI.";
        private const string AdvancedId = "_Advanced";

        private static GUIStyle _headerStyle;
        private static GUIStyle _boxStyle;
        private static GUIStyle _dimLabel;

        private string _search = string.Empty;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor.target is not Material material || material.shader == null)
            {
                base.OnGUI(materialEditor, properties);
                return;
            }

            EnsureStyles();
            var shader = material.shader;
            var shaderName = shader.name;
            var layout = ShaderLayoutCache.Get(shader);
            var lookup = new Dictionary<string, MaterialProperty>(properties.Length);
            foreach (var property in properties)
            {
                if (property != null) lookup[property.name] = property;
            }

            materialEditor.SetDefaultGUIWidths();

            if (ShaderPropertyDocs.TryGetIntro(shaderName, out var intro))
                EditorGUILayout.HelpBox(intro, MessageType.None);

            var searching = !string.IsNullOrWhiteSpace(_search);
            var visible = CollectVisible(materialEditor, shaderName, layout, lookup, searching);

            DrawToolbar(shaderName, layout);
            EditorGUILayout.Space(2);

            var flat = visible.Count == 1 && visible[0].section.id == ShaderPropertyDocs.GeneralSectionId;
            foreach (var group in visible)
            {
                if (flat)
                {
                    DrawEntries(materialEditor, shaderName, group.entries, lookup);
                    continue;
                }

                ShaderPropertyDocs.TryGetSection(group.section.id, out var title, out var sectionTip);
                var open = searching || GetOpen(shaderName, group.section.id);
                var toggled = DrawHeader(new GUIContent(title, sectionTip), open);
                if (!searching && toggled != open) SetOpen(shaderName, group.section.id, toggled);
                if (!(searching || toggled)) continue;

                EditorGUILayout.BeginVertical(_boxStyle);
                DrawEntries(materialEditor, shaderName, group.entries, lookup);
                EditorGUILayout.EndVertical();
            }

            if (visible.Count == 0 && searching)
                EditorGUILayout.LabelField("No settings match \"" + _search + "\".", _dimLabel);

            EditorGUILayout.Space(4);
            var advancedContent = new GUIContent("Advanced", "Render queue, GPU instancing and global illumination flags for this material.");
            var advancedOpen = DrawHeader(advancedContent, GetOpen(shaderName, AdvancedId, false));
            SetOpen(shaderName, AdvancedId, advancedOpen);
            if (advancedOpen)
            {
                EditorGUILayout.BeginVertical(_boxStyle);
                materialEditor.RenderQueueField();
                materialEditor.EnableInstancingField();
                materialEditor.DoubleSidedGIField();
                EditorGUILayout.EndVertical();
            }
        }

        private sealed class VisibleGroup
        {
            public ShaderSection section;
            public List<ShaderEntry> entries = new List<ShaderEntry>();
        }

        private List<VisibleGroup> CollectVisible(MaterialEditor editor, string shaderName, ShaderLayout layout, Dictionary<string, MaterialProperty> lookup, bool searching)
        {
            var result = new List<VisibleGroup>();
            var needle = searching ? _search.Trim().ToLowerInvariant() : null;

            foreach (var section in layout.sections)
            {
                VisibleGroup group = null;
                foreach (var entry in section.entries)
                {
                    if (entry.hidden || !lookup.ContainsKey(entry.name)) continue;
                    if (!PassesShowIf(editor.targets, entry)) continue;
                    if (searching && !Matches(shaderName, entry, needle)) continue;

                    if (group == null)
                    {
                        group = new VisibleGroup { section = section };
                        result.Add(group);
                    }

                    group.entries.Add(entry);
                }
            }

            return result;
        }

        private static bool Matches(string shaderName, ShaderEntry entry, string needle)
        {
            if (entry.displayName.ToLowerInvariant().Contains(needle)) return true;
            if (entry.name.ToLowerInvariant().Contains(needle)) return true;
            return ShaderPropertyDocs.TryGetTooltip(shaderName, entry.name, out var tip) && tip.ToLowerInvariant().Contains(needle);
        }

        private void DrawToolbar(string shaderName, ShaderLayout layout)
        {
            EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField(GUIContent.none, _search, EditorStyles.toolbarSearchField);
            if (GUILayout.Button(new GUIContent("Expand All", "Open every group."), EditorStyles.miniButtonLeft, GUILayout.Width(80)))
                SetAll(shaderName, layout, true);
            if (GUILayout.Button(new GUIContent("Collapse All", "Close every group."), EditorStyles.miniButtonRight, GUILayout.Width(80)))
                SetAll(shaderName, layout, false);
            EditorGUILayout.EndHorizontal();
        }

        private static void SetAll(string shaderName, ShaderLayout layout, bool open)
        {
            foreach (var section in layout.sections) SetOpen(shaderName, section.id, open);
            SetOpen(shaderName, AdvancedId, open);
        }

        private static void DrawEntries(MaterialEditor editor, string shaderName, List<ShaderEntry> entries, Dictionary<string, MaterialProperty> lookup)
        {
            foreach (var entry in entries)
            {
                var property = lookup[entry.name];
                ShaderPropertyDocs.TryGetTooltip(shaderName, entry.name, out var tip);
                var text = string.IsNullOrEmpty(tip) ? entry.name : tip + "\n(" + entry.name + ")";
                if (entry.perRenderer) text += "\nUsually overridden per renderer at runtime.";
                editor.ShaderProperty(property, new GUIContent(entry.displayName, text));
                if (entry.name == "_DebugDistortionMask" && property.floatValue > 0.5f) DrawDistortionMaskStatus();
            }
        }

        private static void DrawDistortionMaskStatus()
        {
            if (!SpookyWaterSettings.Instance.distortedSortingLayers.IsEverything) return;
            EditorGUILayout.HelpBox("The distortion mask is off: every sorting layer is ticked in Distorted Sorting Layers, so nothing is kept sharp and this view is plain dark blue. Untick a layer (e.g. Player) to see it.", MessageType.Info);
            if (GUILayout.Button("Open Distorted Sorting Layers"))
                SettingsService.OpenProjectSettings("Project/SpookyWater");
        }

        private static bool PassesShowIf(Object[] targets, ShaderEntry entry)
        {
            if (entry.showIf.Count == 0) return true;
            foreach (var rule in entry.showIf)
            {
                var any = false;
                foreach (var target in targets)
                {
                    if (target is Material m && RuleHolds(m, rule))
                    {
                        any = true;
                        break;
                    }
                }

                if (!any) return false;
            }

            return true;
        }

        private static bool RuleHolds(Material material, ShowIfRule rule)
        {
            var shader = material.shader;
            var index = shader.FindPropertyIndex(rule.property);
            if (index < 0) return true;

            switch (shader.GetPropertyType(index))
            {
                case ShaderPropertyType.Color:
                {
                    var a = material.GetColor(rule.property).a;
                    return rule.hasValue ? Mathf.Approximately(a, rule.value) : a > 0f;
                }
                case ShaderPropertyType.Vector:
                {
                    var v = material.GetVector(rule.property);
                    return rule.hasValue ? Mathf.Approximately(v.x, rule.value) : v.sqrMagnitude > 0f;
                }
                case ShaderPropertyType.Texture:
                    return material.GetTexture(rule.property) != null;
                default:
                {
                    var f = material.GetFloat(rule.property);
                    return rule.hasValue ? Mathf.Approximately(f, rule.value) : f > 0f;
                }
            }
        }

        private static bool DrawHeader(GUIContent content, bool open)
        {
            var rect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.07f) : new Color(0f, 0f, 0f, 0.09f));
            var foldRect = new Rect(rect.x + 4f, rect.y, rect.width - 4f, rect.height);
            return EditorGUI.Foldout(foldRect, open, content, true, _headerStyle);
        }

        private static string Key(string shaderName, string id)
        {
            return KeyPrefix + shaderName + "." + id;
        }

        private static bool GetOpen(string shaderName, string id)
        {
            return GetOpen(shaderName, id, id != "Debug");
        }

        private static bool GetOpen(string shaderName, string id, bool fallback)
        {
            return EditorPrefs.GetBool(Key(shaderName, id), fallback);
        }

        private static void SetOpen(string shaderName, string id, bool open)
        {
            var key = Key(shaderName, id);
            if (EditorPrefs.HasKey(key) && EditorPrefs.GetBool(key) == open) return;
            EditorPrefs.SetBool(key, open);
        }

        private static void EnsureStyles()
        {
            if (_headerStyle != null) return;
            _headerStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fixedHeight = 22f,
                alignment = TextAnchor.MiddleLeft,
            };
            _boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 6, 6),
            };
            _dimLabel = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }
    }
}
