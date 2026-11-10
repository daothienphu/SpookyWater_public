using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    /// <summary>
    /// Draws a <see cref="WaterCrossing"/> inline (no extra foldout) and only shows the ripple settings
    /// while Override Ripple is on.
    /// </summary>
    [CustomPropertyDrawer(typeof(WaterCrossing))]
    public class WaterCrossingDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = 0f;
            foreach (var child in Children(property)) height += EditorGUI.GetPropertyHeight(child, true) + Spacing;
            return Mathf.Max(height - Spacing, 0f);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var rect = position;
            foreach (var child in Children(property))
            {
                rect.height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(rect, child, true);
                rect.y += rect.height + Spacing;
            }
            EditorGUI.EndProperty();
        }

        private static System.Collections.Generic.IEnumerable<SerializedProperty> Children(SerializedProperty property)
        {
            var overrideRipple = property.FindPropertyRelative(nameof(WaterCrossing.overrideRipple));
            yield return overrideRipple;
            if (overrideRipple.boolValue || overrideRipple.hasMultipleDifferentValues)
            {
                var ripple = property.FindPropertyRelative(nameof(WaterCrossing.ripple));
                ripple.isExpanded = true;
                yield return ripple;
            }
            yield return property.FindPropertyRelative(nameof(WaterCrossing.splashScale));
        }
    }
}
