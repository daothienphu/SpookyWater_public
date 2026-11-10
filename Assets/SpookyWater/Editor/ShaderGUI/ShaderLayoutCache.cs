using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyWater.Editor
{
    public sealed class ShowIfRule
    {
        public string property;
        public bool hasValue;
        public float value;
    }

    public sealed class ShaderEntry
    {
        public string name;
        public string displayName;
        public bool hidden;
        public bool perRenderer;
        public List<ShowIfRule> showIf = new List<ShowIfRule>();
    }

    public sealed class ShaderSection
    {
        public string id;
        public List<ShaderEntry> entries = new List<ShaderEntry>();
    }

    public sealed class ShaderLayout
    {
        public List<ShaderSection> sections = new List<ShaderSection>();
    }

    public static class ShaderLayoutCache
    {
        private static readonly Dictionary<int, ShaderLayout> Layouts = new Dictionary<int, ShaderLayout>();

        public static void Clear()
        {
            Layouts.Clear();
        }

        public static ShaderLayout Get(Shader shader)
        {
            var key = shader.GetInstanceID();
            if (Layouts.TryGetValue(key, out var cached)) return cached;
            var layout = Build(shader);
            Layouts[key] = layout;
            return layout;
        }

        private static ShaderLayout Build(Shader shader)
        {
            var layout = new ShaderLayout();
            var byId = new Dictionary<string, ShaderSection>();
            var currentId = ShaderPropertyDocs.GeneralSectionId;
            var count = shader.GetPropertyCount();

            for (var i = 0; i < count; i++)
            {
                var entry = new ShaderEntry
                {
                    name = shader.GetPropertyName(i),
                    displayName = shader.GetPropertyDescription(i),
                };

                var flags = shader.GetPropertyFlags(i);
                entry.hidden = (flags & ShaderPropertyFlags.HideInInspector) != 0;
                entry.perRenderer = (flags & ShaderPropertyFlags.PerRendererData) != 0;

                var attributes = shader.GetPropertyAttributes(i);
                if (attributes != null)
                {
                    foreach (var attribute in attributes)
                    {
                        if (!TrySplit(attribute, out var attributeName, out var args)) continue;
                        if (attributeName == "Section" && args.Length > 0 && args[0].Length > 0)
                            currentId = args[0];
                        else if (attributeName == "ShowIf" && args.Length > 0 && args[0].Length > 0)
                            entry.showIf.Add(MakeRule(args));
                    }
                }

                if (!byId.TryGetValue(currentId, out var section))
                {
                    section = new ShaderSection { id = currentId };
                    byId[currentId] = section;
                    layout.sections.Add(section);
                }

                section.entries.Add(entry);
            }

            return layout;
        }

        private static ShowIfRule MakeRule(string[] args)
        {
            var rule = new ShowIfRule { property = args[0] };
            if (args.Length > 1 && float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                rule.hasValue = true;
                rule.value = value;
            }

            return rule;
        }

        private static bool TrySplit(string attribute, out string name, out string[] args)
        {
            name = null;
            args = null;
            if (string.IsNullOrEmpty(attribute)) return false;

            var open = attribute.IndexOf('(');
            if (open < 0)
            {
                name = attribute.Trim();
                args = new string[0];
                return true;
            }

            var close = attribute.LastIndexOf(')');
            if (close < open) return false;

            name = attribute.Substring(0, open).Trim();
            var inner = attribute.Substring(open + 1, close - open - 1);
            args = inner.Split(',');
            for (var i = 0; i < args.Length; i++) args[i] = args[i].Trim();
            return true;
        }
    }

    public class ShaderLayoutInvalidator : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            ShaderLayoutCache.Clear();
        }
    }
}
