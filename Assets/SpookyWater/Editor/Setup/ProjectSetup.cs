using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    public static class ProjectSetup
    {
        public const string MenuPath = "Tools/SpookyWater/Project Setup";

        private static readonly ProjectRequirement[] AllRequirements =
        {
            new RenderPipelineRequirement(),
            new SettingsAssetRequirement(),
            new ReflectionLayerRequirement(),
            new SortingLayersRequirement(),
            new CameraSortingTextureRequirement(),
            new MainCameraRequirement(),
            new GlobalLightRequirement(),
            new WaterBuilderRequirement(),
            new SplashEffectsRequirement()
        };

        public static IReadOnlyList<ProjectRequirement> Requirements => AllRequirements;

        public static string PackageRoot => SetupUtility.PackageRoot;

        public static void Open()
        {
            ProjectSetupWindow.Open();
        }

        public static bool IsComplete(bool includeScene = false)
        {
            for (var i = 0; i < AllRequirements.Length; i++)
            {
                var requirement = AllRequirements[i];
                if (requirement.Scope == SetupScope.Scene && !includeScene) continue;
                if (requirement.Check().State == SetupState.Error) return false;
            }
            return true;
        }

        public static List<SetupIssue> GetIssues(bool includeScene = false)
        {
            var issues = new List<SetupIssue>();
            for (var i = 0; i < AllRequirements.Length; i++)
            {
                var requirement = AllRequirements[i];
                if (requirement.Scope == SetupScope.Scene && !includeScene) continue;
                var status = requirement.Check();
                if (status.IsOk) continue;
                issues.Add(new SetupIssue
                {
                    Id = requirement.Id,
                    Title = requirement.Title,
                    Message = status.Message,
                    Scope = requirement.Scope,
                    State = status.State,
                    CanFix = requirement.CanFix
                });
            }
            return issues;
        }

        public static string Summarize(bool includeScene = false)
        {
            var issues = GetIssues(includeScene);
            if (issues.Count == 0) return "Project setup is complete.";
            var lines = new List<string>();
            for (var i = 0; i < issues.Count; i++) lines.Add("- " + issues[i].Title + ": " + issues[i].Message);
            return string.Join("\n", lines);
        }

        public static bool Fix(ProjectRequirement requirement)
        {
            if (requirement == null || !requirement.CanFix) return false;
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            var fixedIt = false;
            try
            {
                fixedIt = requirement.Fix();
            }
            catch (Exception exception)
            {
                Debug.LogError(SetupUtility.Prefix + "Fixing '" + requirement.Title + "' failed: " + exception.Message);
            }
            Undo.CollapseUndoOperations(group);
            return fixedIt;
        }

        public static int FixAll(SetupScope scope)
        {
            var count = 0;
            for (var i = 0; i < AllRequirements.Length; i++)
            {
                var requirement = AllRequirements[i];
                if (requirement.Scope != scope || !requirement.CanFix) continue;
                var status = requirement.Check();
                if (status.IsOk) continue;
                if (Fix(requirement)) count++;
            }
            Debug.Log(SetupUtility.Prefix + (count == 0 ? "Nothing needed fixing." : "Fixed " + count + " item(s)."));
            return count;
        }

        public static List<string> MissingBuilderMaterials(WaterTilemapBuilder builder)
        {
            var missing = new List<string>();
            var so = new SerializedObject(builder);
            CollectMissing(so, missing, "_surfaceMeshMaterial", "Surface Mesh");
            CollectMissing(so, missing, "_lineMeshMaterial", "Line Mesh");
            CollectMissing(so, missing, "_bodyMeshMaterial", "Body Mesh");
            CollectMissing(so, missing, "_horizonMaterial", "Horizon");
            CollectMissing(so, missing, "_rippleSimulationMaterial", "Ripple Simulation");
            return missing;
        }

        public static List<string> MissingSplashMaterials(WaterSplashEffects effects)
        {
            var missing = new List<string>();
            var so = new SerializedObject(effects);
            CollectMissing(so, missing, "_dropletMaterial", "Droplet");
            CollectMissing(so, missing, "_mistMaterial", "Mist");
            CollectMissing(so, missing, "_crownMaterial", "Crown");
            CollectMissing(so, missing, "_bubbleMaterial", "Bubble");
            CollectMissing(so, missing, "_ringMaterial", "Ring");
            return missing;
        }

        public static bool AssignBuilderDefaults(WaterTilemapBuilder builder)
        {
            var so = new SerializedObject(builder);
            return FinishAssign(so, builder, SpookyWaterDefaults.AssignBuilderMaterials(so), "WaterTilemapBuilder");
        }

        public static bool AssignSplashDefaults(WaterSplashEffects effects)
        {
            var so = new SerializedObject(effects);
            return FinishAssign(so, effects, SpookyWaterDefaults.AssignSplashMaterials(so), "WaterSplashEffects");
        }

        private static void CollectMissing(SerializedObject so, List<string> missing, string field, string label)
        {
            var property = so.FindProperty(field);
            if (property != null && property.objectReferenceValue == null) missing.Add(label);
        }

        private static bool FinishAssign(SerializedObject so, UnityEngine.Object target, int changed, string typeName)
        {
            if (changed > 0)
            {
                Undo.RecordObject(target, "Assign default " + typeName + " materials");
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                if (target is Component component) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
            return true;
        }
    }
}
