using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    public class SectionDecorator : MaterialPropertyDrawer
    {
        public readonly string id;

        public SectionDecorator(string id)
        {
            this.id = id;
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return 0f;
        }

        public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
        {
        }
    }

    public class ShowIfDecorator : MaterialPropertyDrawer
    {
        public readonly string property;
        public readonly float value;
        public readonly bool hasValue;

        public ShowIfDecorator(string property)
        {
            this.property = property;
        }

        public ShowIfDecorator(string property, float value)
        {
            this.property = property;
            this.value = value;
            hasValue = true;
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return 0f;
        }

        public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
        {
        }
    }
}
