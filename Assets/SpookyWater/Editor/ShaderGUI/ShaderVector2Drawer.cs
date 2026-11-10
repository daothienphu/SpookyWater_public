using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    /// <summary>
    /// [Vector2] on a Vector shader property: shows only X and Y. Z and W are left untouched.
    /// </summary>
    public class Vector2Drawer : MaterialPropertyDrawer
    {
        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
        {
            OnGUI(position, prop, new GUIContent(label), editor);
        }

        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            var value = prop.vectorValue;
            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            var field = EditorGUI.PrefixLabel(position, label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var xy = EditorGUI.Vector2Field(field, GUIContent.none, new Vector2(value.x, value.y));
            EditorGUI.indentLevel = indent;
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                prop.vectorValue = new Vector4(xy.x, xy.y, value.z, value.w);
        }
    }
}
