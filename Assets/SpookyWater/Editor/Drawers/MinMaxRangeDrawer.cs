#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace SpookyWater.Editor
{
    [CustomPropertyDrawer(typeof(MinMaxRangeAttribute), true)]
    public class MinMaxRangeDrawer : PropertyDrawer
    {
        private const float FieldWidth = 48f;
        private const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var isInt = property.propertyType == SerializedPropertyType.Vector2Int;
            if (!isInt && property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.LabelField(position, label.text, "MinMaxRange needs a Vector2 or Vector2Int");
                return;
            }

            var range = (MinMaxRangeAttribute)attribute;
            EditorGUI.BeginProperty(position, label, property);

            var content = EditorGUI.PrefixLabel(position, label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var minRect = new Rect(content.x, content.y, FieldWidth, content.height);
            var maxRect = new Rect(content.xMax - FieldWidth, content.y, FieldWidth, content.height);
            var sliderRect = new Rect(minRect.xMax + Spacing, content.y,
                Mathf.Max(0f, maxRect.x - minRect.xMax - Spacing * 2f), content.height);

            float min, max;
            if (isInt)
            {
                min = property.vector2IntValue.x;
                max = property.vector2IntValue.y;
            }
            else
            {
                min = property.vector2Value.x;
                max = property.vector2Value.y;
            }

            EditorGUI.BeginChangeCheck();
            min = EditorGUI.FloatField(minRect, min);
            EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, range.Min, range.Max);
            max = EditorGUI.FloatField(maxRect, max);
            if (EditorGUI.EndChangeCheck())
            {
                min = Mathf.Clamp(min, range.Min, range.Max);
                max = Mathf.Clamp(max, min, range.Max);
                if (isInt) property.vector2IntValue = new Vector2Int(Mathf.RoundToInt(min), Mathf.RoundToInt(max));
                else property.vector2Value = new Vector2(min, max);
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
#endif
