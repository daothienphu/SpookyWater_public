using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SpookyWater.Editor
{
    internal sealed class MainCameraRequirement : ProjectRequirement
    {
        public override string Id => "main-camera";
        public override string Title => "Water camera";
        public override SetupScope Scope => SetupScope.Scene;

        public override string What => "The water knows which camera to follow (Water.Camera): one with a Water Camera Source component, one given by code through Water.SetCamera or Water.CameraResolver, or else the camera tagged MainCamera. A perspective camera is recommended.";

        public override string Why => "The reflection camera mirrors this camera, the ripple simulation window and contact-line culling follow its view, and ambient bubbles spawn inside it. A perspective camera gives the surface its depth and parallax; an orthographic camera works but looks flat. A camera spawned at runtime by your own camera system is fine: add Water Camera Source to its prefab, or set Water.CameraResolver from your code.";

        public override string ManualHelp => "Add Water Camera Source to your gameplay camera (or the camera prefab your camera system spawns), or tag it MainCamera, or set Water.CameraResolver in your bootstrap code.";

        public override SetupStatus Check()
        {
            var camera = Water.Camera;
            if (camera == null)
            {
                var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cameras.Length == 0)
                    return SetupStatus.Info("No camera in this scene. Fine if your camera is spawned at runtime with a Water Camera Source, or your code sets Water.CameraResolver.");
                return SetupStatus.Warning("The scene has cameras, but none is the water camera: none has a Water Camera Source" +
                                           (SpookyWaterSettings.Instance.mainCameraFallback ? " or is tagged MainCamera." : ", and the MainCamera fallback is off in the settings asset."));
            }
            if (camera.orthographic) return SetupStatus.Info($"'{camera.name}' is orthographic. It works, but a perspective camera shows the 2.5D depth.");
            return SetupStatus.Ok($"The water follows '{camera.name}'.");
        }

        public override bool Fix()
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length != 1) return false;
            if (!SpookyWaterSettings.Instance.mainCameraFallback)
            {
                Undo.AddComponent<WaterCameraSource>(cameras[0].gameObject);
                EditorSceneManager.MarkSceneDirty(cameras[0].gameObject.scene);
                Debug.Log(SetupUtility.Prefix + $"Added Water Camera Source to '{cameras[0].name}'.", cameras[0]);
                return true;
            }
            Undo.RecordObject(cameras[0].gameObject, "Tag MainCamera");
            cameras[0].gameObject.tag = "MainCamera";
            EditorSceneManager.MarkSceneDirty(cameras[0].gameObject.scene);
            Debug.Log(SetupUtility.Prefix + $"Tagged '{cameras[0].name}' as MainCamera.", cameras[0]);
            return true;
        }
    }

    internal sealed class GlobalLightRequirement : ProjectRequirement
    {
        public override string Id => "global-light";
        public override string Title => "Global Light 2D";
        public override SetupScope Scope => SetupScope.Scene;

        public override string What => "The scene contains an enabled Light2D of type Global.";

        public override string Why => "The water shaders are lit by URP 2D lights like any sprite. With no 2D light in the scene the water, its reflection and the sprites render dark. A Global Volume is optional and only needed for post effects such as bloom.";

        public override SetupStatus Check()
        {
            var lights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i].enabled && lights[i].lightType == Light2D.LightType.Global) return SetupStatus.Ok($"Found '{lights[i].name}'.");
            }
            return SetupStatus.Error("No enabled Global Light 2D in the scene.");
        }

        public override bool Fix()
        {
            var go = new GameObject("Global Light 2D");
            Undo.RegisterCreatedObjectUndo(go, "Create Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log(SetupUtility.Prefix + "Created a Global Light 2D in the active scene.", go);
            return true;
        }
    }

    internal sealed class WaterBuilderRequirement : ProjectRequirement
    {
        public override string Id => "water-builder";
        public override string Title => "Water level with all materials assigned";
        public override SetupScope Scope => SetupScope.Scene;

        public override string What => "Exactly one WaterTilemapBuilder in the scene, with its surface, front, horizon and ripple simulation materials assigned.";

        public override string Why => "The builder turns the painted tiles into the water meshes and owns the ripple simulation for the whole level. A second builder would fight the first over the shared shader globals, and an empty material slot leaves that part of the water invisible.";

        public override string ManualHelp => "Create water with GameObject > SpookyWater > Water.";

        public override SetupStatus Check()
        {
            var builders = Object.FindObjectsByType<WaterTilemapBuilder>(FindObjectsSortMode.None);
            if (builders.Length == 0) return SetupStatus.Info("No water in this scene yet.");
            if (builders.Length > 1) return SetupStatus.Error($"{builders.Length} WaterTilemapBuilders found. Only one per scene is supported.");
            var missing = ProjectSetup.MissingBuilderMaterials(builders[0]);
            if (missing.Count > 0) return SetupStatus.Error("Unassigned materials: " + string.Join(", ", missing) + ".");
            return SetupStatus.Ok($"'{builders[0].name}' is set up.");
        }

        public override bool Fix()
        {
            var builders = Object.FindObjectsByType<WaterTilemapBuilder>(FindObjectsSortMode.None);
            if (builders.Length != 1) return false;
            return ProjectSetup.AssignBuilderDefaults(builders[0]);
        }
    }

    internal sealed class SplashEffectsRequirement : ProjectRequirement
    {
        public override string Id => "splash-effects";
        public override string Title => "Splash effects (optional)";
        public override SetupScope Scope => SetupScope.Scene;

        public override string What => "One WaterSplashEffects in the scene, with its five particle materials assigned.";

        public override string Why => "It draws the splashes, spray, wake droplets and bubbles. The water works without it, but nothing splashes when objects enter it.";

        public override SetupStatus Check()
        {
            if (Object.FindObjectsByType<WaterTilemapBuilder>(FindObjectsSortMode.None).Length == 0) return SetupStatus.Info("Not needed until the scene has water.");
            var effects = Object.FindObjectsByType<WaterSplashEffects>(FindObjectsSortMode.None);
            if (effects.Length == 0) return SetupStatus.Warning("No WaterSplashEffects in the scene. Splashes and bubbles are off.");
            if (effects.Length > 1) return SetupStatus.Warning($"{effects.Length} WaterSplashEffects found. Keep one.");
            var missing = ProjectSetup.MissingSplashMaterials(effects[0]);
            if (missing.Count > 0) return SetupStatus.Warning("Unassigned materials: " + string.Join(", ", missing) + ".");
            return SetupStatus.Ok($"'{effects[0].name}' is set up.");
        }

        public override bool Fix()
        {
            var effects = Object.FindObjectsByType<WaterSplashEffects>(FindObjectsSortMode.None);
            if (effects.Length > 1) return false;
            WaterSplashEffects target;
            if (effects.Length == 1)
            {
                target = effects[0];
            }
            else
            {
                var go = new GameObject("Water Splash Effects");
                var builders = Object.FindObjectsByType<WaterTilemapBuilder>(FindObjectsSortMode.None);
                if (builders.Length == 1) go.transform.SetParent(builders[0].transform, false);
                Undo.RegisterCreatedObjectUndo(go, "Create Water Splash Effects");
                target = Undo.AddComponent<WaterSplashEffects>(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
                Debug.Log(SetupUtility.Prefix + "Created a Water Splash Effects object" + (go.transform.parent ? " under '" + go.transform.parent.name + "'." : " in the active scene."), go);
            }
            return ProjectSetup.AssignSplashDefaults(target);
        }
    }
}
