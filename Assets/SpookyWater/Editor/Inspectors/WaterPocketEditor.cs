using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    [CustomEditor(typeof(WaterPocket))]
    public class WaterPocketEditor : GroupedInspector
    {
        private static readonly Color OutlineColor = new(0.3f, 0.9f, 1f, 1f);
        private static readonly Color TangentColor = new(1f, 0.8f, 0.3f, 1f);
        private static readonly Color LevelColor = new(0.4f, 0.6f, 1f, 1f);
        private static readonly string[] CopiedFromBuilder = { "_rippleGrid", "_ambientRipples", "_frontRipples", "_wind" };

        private int _selected = -1;

        protected override void DrawBeforeProperties()
        {
            DrawNoBuilderWarning();
            EditorGUILayout.HelpBox("Cuts an air pocket out of the front water along a bezier outline and gives the water under " +
                                    "its Water Level its own waterline, a shallow surface strip, ripple simulation and wind. " +
                                    "Drag the anchors (dots), handles (circles, hold Alt to break a smooth corner) and the " +
                                    "Water Level arrow in the Scene view.", MessageType.None);
        }

        protected override void DrawAfterProperties()
        {
            var points = serializedObject.FindProperty("_points");
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Add Point", "Splits the curve after the selected anchor (or the last one) without changing its shape.")))
                    AddPoint(points);
                using (new EditorGUI.DisabledScope(points.arraySize <= 3))
                {
                    if (GUILayout.Button(new GUIContent("Remove Point", "Removes the selected anchor (or the last one). A pocket needs at least three.")))
                        RemovePoint(points);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Copy Ripples From Builder", "Copies the scene's Water Tilemap Builder's Ripple Grid, Ambient Ripples, Front Ripples and Wind into this pocket as a starting point.")))
                    CopyFromBuilder();
                if (GUILayout.Button(new GUIContent("Regenerate", "Rebuilds the pocket's meshes and simulation. Happens on its own after any change.")))
                {
                    foreach (var t in targets) ((WaterPocket)t).Generate();
                }
            }
        }

        private void AddPoint(SerializedProperty points)
        {
            serializedObject.ApplyModifiedProperties();
            var count = points.arraySize;
            var index = _selected >= 0 && _selected < count ? _selected : count - 1;
            var next = (index + 1) % count;

            var a = points.GetArrayElementAtIndex(index);
            var b = points.GetArrayElementAtIndex(next);
            var p0 = a.FindPropertyRelative("position").vector2Value;
            var p1 = p0 + a.FindPropertyRelative("outTangent").vector2Value;
            var p3 = b.FindPropertyRelative("position").vector2Value;
            var p2 = p3 + b.FindPropertyRelative("inTangent").vector2Value;
            var q0 = (p0 + p1) * 0.5f;
            var q1 = (p1 + p2) * 0.5f;
            var q2 = (p2 + p3) * 0.5f;
            var r0 = (q0 + q1) * 0.5f;
            var r1 = (q1 + q2) * 0.5f;
            var mid = (r0 + r1) * 0.5f;

            a.FindPropertyRelative("outTangent").vector2Value = q0 - p0;
            b.FindPropertyRelative("inTangent").vector2Value = q2 - p3;
            points.InsertArrayElementAtIndex(index + 1);
            var inserted = points.GetArrayElementAtIndex(index + 1);
            inserted.FindPropertyRelative("position").vector2Value = mid;
            inserted.FindPropertyRelative("inTangent").vector2Value = r0 - mid;
            inserted.FindPropertyRelative("outTangent").vector2Value = r1 - mid;
            _selected = index + 1;
            serializedObject.ApplyModifiedProperties();
        }

        private void RemovePoint(SerializedProperty points)
        {
            var index = _selected >= 0 && _selected < points.arraySize ? _selected : points.arraySize - 1;
            points.DeleteArrayElementAtIndex(index);
            _selected = -1;
            serializedObject.ApplyModifiedProperties();
        }

        private void CopyFromBuilder()
        {
            var builder = Object.FindAnyObjectByType<WaterTilemapBuilder>();
            if (!builder)
            {
                EditorUtility.DisplayDialog("Water Pocket", "There is no Water Tilemap Builder in this scene to copy from.", "OK");
                return;
            }

            var source = new SerializedObject(builder);
            foreach (var path in CopiedFromBuilder)
            {
                var property = source.FindProperty(path);
                if (property != null) serializedObject.CopyFromSerializedProperty(property);
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var pocket = (WaterPocket)target;
            serializedObject.Update();
            var points = serializedObject.FindProperty("_points");
            if (points.arraySize < 2) return;

            Vector2 origin = pocket.transform.position;
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            for (var i = 0; i < points.arraySize; i++)
            {
                var element = points.GetArrayElementAtIndex(i);
                var position = element.FindPropertyRelative("position");
                var inTangent = element.FindPropertyRelative("inTangent");
                var outTangent = element.FindPropertyRelative("outTangent");
                var anchor = origin + position.vector2Value;
                minX = Mathf.Min(minX, anchor.x);
                maxX = Mathf.Max(maxX, anchor.x);

                DrawTangent(anchor, inTangent, outTangent);
                DrawTangent(anchor, outTangent, inTangent);

                var size = HandleUtility.GetHandleSize(anchor) * 0.08f;
                Handles.color = i == _selected ? Color.white : OutlineColor;
                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(anchor, size, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    position.vector2Value = (Vector2)moved - origin;
                    _selected = i;
                }
            }

            DrawCurve(pocket, points.arraySize);

            var levelProperty = serializedObject.FindProperty("_waterLevel");
            var levelPoint = new Vector3((minX + maxX) * 0.5f, origin.y + levelProperty.floatValue, 0f);
            Handles.color = LevelColor;
            Handles.DrawDottedLine(new Vector3(minX, levelPoint.y, 0f), new Vector3(maxX, levelPoint.y, 0f), 4f);
            EditorGUI.BeginChangeCheck();
            var level = Handles.Slider(levelPoint, Vector3.up, HandleUtility.GetHandleSize(levelPoint) * 0.6f, Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck()) levelProperty.floatValue = level.y - origin.y;

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawTangent(Vector2 anchor, SerializedProperty tangent, SerializedProperty opposite)
        {
            var end = anchor + tangent.vector2Value;
            Handles.color = TangentColor;
            Handles.DrawLine(anchor, end);

            var size = HandleUtility.GetHandleSize(end) * 0.06f;
            EditorGUI.BeginChangeCheck();
            var moved = (Vector2)Handles.FreeMoveHandle(end, size, Vector3.zero, Handles.CircleHandleCap);
            if (!EditorGUI.EndChangeCheck()) return;

            tangent.vector2Value = moved - anchor;
            var other = opposite.vector2Value;
            if (!Event.current.alt && other.sqrMagnitude > 1e-6f && tangent.vector2Value.sqrMagnitude > 1e-6f)
                opposite.vector2Value = -tangent.vector2Value.normalized * other.magnitude;
        }

        private static void DrawCurve(WaterPocket pocket, int count)
        {
            Handles.color = OutlineColor;
            var steps = pocket.CurveSteps;
            var previous = pocket.Evaluate(0, 0f);
            for (var span = 0; span < count; span++)
            {
                for (var i = 1; i <= steps; i++)
                {
                    var next = pocket.Evaluate(span, (float)i / steps);
                    Handles.DrawLine(previous, next, 2f);
                    previous = next;
                }
            }
        }
    }
}
