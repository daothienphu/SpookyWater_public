#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    [CustomEditor(typeof(WaterLightShaft))]
    [CanEditMultipleObjects]
    public class WaterLightShaftEditor : GroupedInspector
    {
        private static readonly Color EdgeColor = new(0.45f, 0.85f, 1f, 0.8f);
        private static readonly Color TopColor = new(1f, 0.9f, 0.45f, 0.9f);

        protected override void DrawBeforeProperties()
        {
            DrawMissingAssets(serializedObject,
                "No material is assigned, so the shaft will not draw.",
                SpookyWaterDefaults.AssignLightShaftDefaults,
                SpookyWaterDefaults.LightShaftDefaultsMissing(serializedObject));
        }

        private void OnSceneGUI()
        {
            var shaft = (WaterLightShaft)target;
            var transform = shaft.transform;
            var half = shaft.Width * 0.5f;
            var top = shaft.LocalTop;
            var bottom = top - shaft.Height;
            var shift = shaft.Lean * shaft.Height;

            var topLeft = transform.TransformPoint(new Vector3(-half, top, 0f));
            var topRight = transform.TransformPoint(new Vector3(half, top, 0f));
            var bottomLeft = transform.TransformPoint(new Vector3(shift - half, bottom, 0f));
            var bottomRight = transform.TransformPoint(new Vector3(shift + half, bottom, 0f));

            Handles.color = TopColor;
            Handles.DrawLine(topLeft, topRight, 2f);
            Handles.color = EdgeColor;
            Handles.DrawLine(topLeft, bottomLeft);
            Handles.DrawLine(topRight, bottomRight);
            Handles.DrawLine(bottomLeft, bottomRight);

            var shaftObject = new SerializedObject(shaft);
            Handles.color = EdgeColor;
            var width = shaftObject.FindProperty("_width");
            var height = shaftObject.FindProperty("_height");

            var middle = shift * 0.5f;
            var right = Slide(transform, new Vector3(middle + half, (top + bottom) * 0.5f, 0f), Vector3.right);
            if (right.HasValue) width.floatValue = Mathf.Max(0.01f, (right.Value.x - middle) * 2f);
            var left = Slide(transform, new Vector3(middle - half, (top + bottom) * 0.5f, 0f), Vector3.left);
            if (left.HasValue) width.floatValue = Mathf.Max(0.01f, (middle - left.Value.x) * 2f);
            var down = Slide(transform, new Vector3(shift, bottom, 0f), Vector3.down);
            if (down.HasValue) height.floatValue = Mathf.Max(0.01f, top - down.Value.y);

            if (shaftObject.ApplyModifiedProperties()) EditorApplication.QueuePlayerLoopUpdate();
        }

        private static Vector3? Slide(Transform transform, Vector3 local, Vector3 localDirection)
        {
            var world = transform.TransformPoint(local);
            var size = HandleUtility.GetHandleSize(world) * 0.06f;
            EditorGUI.BeginChangeCheck();
            var moved = Handles.Slider(world, transform.TransformDirection(localDirection), size, Handles.CubeHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck()) return null;
            return transform.InverseTransformPoint(moved);
        }
    }
}
#endif
