using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpookyWater.Editor
{
    public sealed class ProjectSetupWindow : EditorWindow
    {
        public const string AutoOpenKey = "SpookyWater.ProjectSetup.AutoOpen";

        private sealed class Row
        {
            public ProjectRequirement Requirement;
            public SetupStatus Status;
        }

        private readonly List<Row> _rows = new List<Row>();
        private Vector2 _scroll;
        private bool _dirty = true;

        [MenuItem(ProjectSetup.MenuPath, false, 100)]
        public static void Open()
        {
            var window = GetWindow<ProjectSetupWindow>(false, "SpookyWater Setup", true);
            window.minSize = new Vector2(480f, 420f);
            window.Show();
            window.Refresh();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += MarkDirty;
            EditorApplication.hierarchyChanged += MarkDirty;
            EditorApplication.projectChanged += MarkDirty;
            _dirty = true;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= MarkDirty;
            EditorApplication.hierarchyChanged -= MarkDirty;
            EditorApplication.projectChanged -= MarkDirty;
        }

        private void OnFocus()
        {
            _dirty = true;
        }

        private void MarkDirty()
        {
            _dirty = true;
            Repaint();
        }

        private void Refresh()
        {
            _rows.Clear();
            var requirements = ProjectSetup.Requirements;
            for (var i = 0; i < requirements.Count; i++)
            {
                _rows.Add(new Row { Requirement = requirements[i], Status = requirements[i].Check() });
            }
            _dirty = false;
            Repaint();
        }

        private void OnGUI()
        {
            if (_dirty || _rows.Count == 0) Refresh();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("SpookyWater Project Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("SpookyWater needs a few project settings to render and sort correctly. This list acts as a sanity check when you first import the porject. ", MessageType.Info);

            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSection(SetupScope.Project, "Project");
            DrawSection(SetupScope.Scene, "Active Scene: " + SceneManager.GetActiveScene().name);
            EditorGUILayout.EndScrollView();

            var auto = EditorPrefs.GetBool(AutoOpenKey, true);
            var toggled = EditorGUILayout.ToggleLeft("Open this window automatically when a project is missing settings", auto);
            if (toggled != auto) EditorPrefs.SetBool(AutoOpenKey, toggled);
            EditorGUILayout.Space(4f);
        }

        private void DrawToolbar()
        {
            var projectFixable = CountFixable(SetupScope.Project);
            var sceneFixable = CountFixable(SetupScope.Scene);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(projectFixable == 0))
                {
                    if (GUILayout.Button("Fix All Project Items (" + projectFixable + ")", GUILayout.Height(24f)))
                    {
                        ProjectSetup.FixAll(SetupScope.Project);
                        Refresh();
                    }
                }
                using (new EditorGUI.DisabledScope(sceneFixable == 0))
                {
                    if (GUILayout.Button("Fix All Scene Items (" + sceneFixable + ")", GUILayout.Height(24f)))
                    {
                        ProjectSetup.FixAll(SetupScope.Scene);
                        Refresh();
                    }
                }
                if (GUILayout.Button("Refresh", GUILayout.Width(70f), GUILayout.Height(24f))) Refresh();
            }
        }

        private int CountFixable(SetupScope scope)
        {
            var count = 0;
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Requirement.Scope == scope && row.Requirement.CanFix && !row.Status.IsOk) count++;
            }
            return count;
        }

        private void DrawSection(SetupScope scope, string title)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Requirement.Scope == scope) DrawRow(_rows[i]);
            }
        }

        private void DrawRow(Row row)
        {
            var requirement = row.Requirement;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(IconFor(row.Status.State), GUILayout.Width(20f), GUILayout.Height(20f));
                    EditorGUILayout.LabelField(requirement.Title, EditorStyles.boldLabel);
                    if (requirement.SecondaryLabel != null && GUILayout.Button(requirement.SecondaryLabel, GUILayout.Width(140f)))
                    {
                        requirement.Secondary();
                    }
                    if (requirement.CanFix && !row.Status.IsOk && GUILayout.Button("Fix", GUILayout.Width(60f)))
                    {
                        ProjectSetup.Fix(requirement);
                        Refresh();
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.LabelField(row.Status.Message, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(requirement.What, EditorStyles.wordWrappedLabel);
                if (!row.Status.IsOk && requirement.ManualHelp != null)
                {
                    EditorGUILayout.HelpBox(requirement.ManualHelp, MessageType.None);
                }
            }
        }

        private static Texture IconFor(SetupState state)
        {
            switch (state)
            {
                case SetupState.Ok: return EditorGUIUtility.IconContent("TestPassed").image;
                case SetupState.Info: return EditorGUIUtility.IconContent("console.infoicon.sml").image;
                case SetupState.Warning: return EditorGUIUtility.IconContent("console.warnicon.sml").image;
                default: return EditorGUIUtility.IconContent("console.erroricon.sml").image;
            }
        }
    }
}
