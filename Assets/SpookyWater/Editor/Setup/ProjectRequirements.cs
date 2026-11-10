using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpookyWater.Editor
{
    internal sealed class RenderPipelineRequirement : ProjectRequirement
    {
        public override string Id => "pipeline";
        public override string Title => "Universal Render Pipeline with a 2D Renderer";
        public override SetupScope Scope => SetupScope.Project;
        public override bool CanFix => false;

        public override string What => "The project must render with the Universal Render Pipeline (URP), and the active pipeline asset must use a 2D Renderer (Renderer2DData).";

        public override string Why => "The water shaders are built on URP's 2D lit sprite pass and read the Camera Sorting Layer Texture, which only the 2D Renderer provides.";

        public override string ManualHelp => "Create a URP asset (Assets > Create > Rendering > URP Asset with 2D Renderer), then assign it in Project Settings > Graphics and in every Quality level.";

        public override string SecondaryLabel => "Select Pipeline Asset";

        public override void Secondary()
        {
            var assets = SetupUtility.CollectPipelineAssets();
            if (assets.Count > 0)
            {
                Selection.activeObject = assets[0];
                EditorGUIUtility.PingObject(assets[0]);
                return;
            }
            SettingsService.OpenProjectSettings("Project/Graphics");
        }

        public override SetupStatus Check()
        {
            var current = GraphicsSettings.currentRenderPipeline;
            if (current == null) return SetupStatus.Error("The project uses the Built-in render pipeline.");
            if (!(current is UniversalRenderPipelineAsset urp)) return SetupStatus.Error($"The active pipeline asset '{current.name}' is not a URP asset.");
            var list = new SerializedObject(urp).FindProperty("m_RendererDataList");
            if (list != null)
            {
                for (var i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is Renderer2DData renderer)
                    {
                        return SetupStatus.Ok($"Pipeline '{urp.name}' uses the 2D Renderer '{renderer.name}'.");
                    }
                }
            }
            return SetupStatus.Error($"The URP asset '{urp.name}' has no 2D Renderer in its renderer list.");
        }
    }

    internal sealed class SettingsAssetRequirement : ProjectRequirement
    {
        public override string Id => "settings";
        public override string Title => "SpookyWater settings asset";
        public override SetupScope Scope => SetupScope.Project;

        public override string What => "A SpookyWaterSettings asset in a Resources folder. It holds the names of the reflection layer and the two sorting layers, and the sorting orders for debris, bubbles and spray.";

        public override string Why => "Without it the package silently uses built-in defaults and you cannot rename the layers to fit your project. Creating it makes every name visible and editable in one place.";

        public override string SecondaryLabel => "Select Asset";

        public override void Secondary()
        {
            var asset = Resources.Load<SpookyWaterSettings>(SpookyWaterSettings.ResourceName);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public override SetupStatus Check()
        {
            var asset = Resources.Load<SpookyWaterSettings>(SpookyWaterSettings.ResourceName);
            if (asset != null) return SetupStatus.Ok($"Found at {AssetDatabase.GetAssetPath(asset)}.");
            return SetupStatus.Warning("Not found. The package is running on built-in defaults.");
        }

        public override bool Fix()
        {
            var folder = SetupUtility.PackageRoot + "/Resources";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(SetupUtility.PackageRoot, "Resources");
            var path = folder + "/" + SpookyWaterSettings.ResourceName + ".asset";
            if (File.Exists(path)) return true;
            var asset = ScriptableObject.CreateInstance<SpookyWaterSettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Debug.Log(SetupUtility.Prefix + "Created the settings asset at " + path + ". This cannot be undone with Ctrl+Z; delete the file to remove it.", asset);
            return true;
        }
    }

    internal sealed class ReflectionLayerRequirement : ProjectRequirement
    {
        public override string Id => "reflection-layer";
        public override string Title => "Reflection physics layer";
        public override SetupScope Scope => SetupScope.Project;

        public override string What => "A user layer (a physics/GameObject layer, not a sorting layer) whose name comes from the settings asset. The default is SpookyWater_Reflections.";

        public override string Why => "Water Reflectable creates hidden copies of your sprites on this layer, and only the reflection camera renders it. You never put your own objects on it. The URP 2D Renderer can only filter by GameObject layer, so a layer is unavoidable.";

        public override SetupStatus Check()
        {
            var name = SetupUtility.Settings.reflectionLayer;
            var tagManager = SetupUtility.LoadTagManager();
            if (tagManager == null) return SetupStatus.Error("Could not read ProjectSettings/TagManager.asset.");
            var slot = SetupUtility.FindLayerSlot(tagManager, name);
            if (slot >= 0) return SetupStatus.Ok($"Layer '{name}' exists at slot {slot}.");
            return SetupStatus.Error($"Layer '{name}' does not exist.");
        }

        public override bool Fix()
        {
            var name = SetupUtility.Settings.reflectionLayer;
            var tagManager = SetupUtility.LoadTagManager();
            if (tagManager == null) return false;
            if (SetupUtility.FindLayerSlot(tagManager, name) >= 0) return true;
            var slot = SetupUtility.FindFreeLayerSlot(tagManager);
            if (slot < 0)
            {
                Debug.LogError(SetupUtility.Prefix + $"All user layers (8-31) are in use, so '{name}' cannot be added. Free a layer in Project Settings > Tags and Layers, then press Fix again.");
                return false;
            }
            Undo.RecordObject(tagManager.targetObject, "Add SpookyWater reflection layer");
            tagManager.FindProperty("layers").GetArrayElementAtIndex(slot).stringValue = name;
            SetupUtility.SaveTagManager(tagManager);
            Debug.Log(SetupUtility.Prefix + $"Added layer '{name}' at user layer slot {slot} in ProjectSettings/TagManager.asset.");
            return true;
        }
    }

    internal sealed class SortingLayersRequirement : ProjectRequirement
    {
        public override string Id => "sorting-layers";
        public override string Title => "Water sorting layers in the right order";
        public override SetupScope Scope => SetupScope.Project;

        public override string What => "Two sorting layers named in the settings asset (defaults SpookyWater_Surface and SpookyWater_Front), placed after Default and your actor layers, with the surface first and the front glass after it.";

        public override string Why => "Draw order comes from sorting layers. Everything on layers before the surface layer is captured and refracted by the front glass. Layers placed after SpookyWater_Front (foreground, UI) draw over the water untouched. Sprites that must appear under the water belong before the surface layer.";

        public override SetupStatus Check()
        {
            var settings = SetupUtility.Settings;
            var tagManager = SetupUtility.LoadTagManager();
            if (tagManager == null) return SetupStatus.Error("Could not read ProjectSettings/TagManager.asset.");
            var surface = settings.surfaceSortingLayer;
            var front = settings.frontSortingLayer;
            if (surface == front) return SetupStatus.Error("The surface and front sorting layers must have different names.");
            var surfaceIndex = SetupUtility.FindSortingLayerIndex(tagManager, surface);
            var frontIndex = SetupUtility.FindSortingLayerIndex(tagManager, front);
            if (surfaceIndex < 0 && frontIndex < 0) return SetupStatus.Error($"Sorting layers '{surface}' and '{front}' do not exist.");
            if (surfaceIndex < 0) return SetupStatus.Error($"Sorting layer '{surface}' does not exist.");
            if (frontIndex < 0) return SetupStatus.Error($"Sorting layer '{front}' does not exist.");
            var defaultIndex = SetupUtility.FindSortingLayerIndex(tagManager, "Default");
            if (surfaceIndex <= defaultIndex) return SetupStatus.Error($"'{surface}' must come after Default, otherwise Default sprites draw over the water.");
            if (frontIndex <= surfaceIndex) return SetupStatus.Error($"'{front}' must come after '{surface}'.");
            var playerIndex = SetupUtility.FindSortingLayerIndex(tagManager, "Player");
            if (playerIndex > surfaceIndex) return SetupStatus.Warning($"The Player layer comes after '{surface}', so characters on it draw over the water and are not refracted.");
            return SetupStatus.Ok($"Order is Default, ..., {surface}, {front}.");
        }

        public override bool Fix()
        {
            var settings = SetupUtility.Settings;
            var tagManager = SetupUtility.LoadTagManager();
            if (tagManager == null) return false;
            if (settings.surfaceSortingLayer == settings.frontSortingLayer)
            {
                Debug.LogError(SetupUtility.Prefix + "The surface and front sorting layers have the same name in the settings asset. Rename one of them first.");
                return false;
            }
            Undo.RecordObject(tagManager.targetObject, "Add SpookyWater sorting layers");
            Place(tagManager, settings.surfaceSortingLayer, SetupUtility.ActorAnchorIndex(tagManager));
            Place(tagManager, settings.frontSortingLayer, SetupUtility.FindSortingLayerIndex(tagManager, settings.surfaceSortingLayer));
            SetupUtility.SaveTagManager(tagManager);
            return true;
        }

        private static void Place(SerializedObject tagManager, string layerName, int anchor)
        {
            var layers = tagManager.FindProperty("m_SortingLayers");
            var index = SetupUtility.FindSortingLayerIndex(tagManager, layerName);
            if (index < 0)
            {
                var position = anchor < 0 ? layers.arraySize : anchor + 1;
                SetupUtility.InsertSortingLayer(tagManager, layerName, position);
                tagManager.ApplyModifiedProperties();
                Debug.Log(SetupUtility.Prefix + $"Added sorting layer '{layerName}' at position {position}.");
                return;
            }
            if (index <= anchor)
            {
                layers.MoveArrayElement(index, anchor);
                tagManager.ApplyModifiedProperties();
                Debug.Log(SetupUtility.Prefix + $"Moved sorting layer '{layerName}' from position {index} to {anchor}, after its required predecessor.");
            }
        }
    }

    internal sealed class CameraSortingTextureRequirement : ProjectRequirement
    {
        public override string Id => "camera-sorting-texture";
        public override string Title => "Camera Sorting Layer Texture on the 2D Renderer";
        public override SetupScope Scope => SetupScope.Project;

        public override string What => "On every 2D Renderer, 'Camera Sorting Layers Texture' is enabled and its foremost layer is the water surface sorting layer.";

        public override string Why => "The front glass of the water refracts and tints whatever is behind it by sampling this texture. The texture holds everything drawn up to and including the surface layer, so objects behind the water bend and darken, while layers after it are not captured.";

        public override string ManualHelp => "Open your 2D Renderer asset and, under Camera Sorting Layers Texture, enable it and set the Foremost Sorting Layer to the surface layer.";

        public override SetupStatus Check()
        {
            var renderers = SetupUtility.Find2DRenderers();
            if (renderers.Count == 0) return SetupStatus.Error("No 2D Renderer found. Set up the render pipeline first.");
            var tagManager = SetupUtility.LoadTagManager();
            var surface = SetupUtility.Settings.surfaceSortingLayer;
            if (tagManager == null || !SetupUtility.TryGetSortingLayerId(tagManager, surface, out var id)) return SetupStatus.Error($"Sorting layer '{surface}' does not exist yet. Fix the sorting layers first.");
            var failing = new List<string>();
            for (var i = 0; i < renderers.Count; i++)
            {
                var so = new SerializedObject(renderers[i]);
                var use = so.FindProperty("m_UseCameraSortingLayersTexture");
                var bound = so.FindProperty("m_CameraSortingLayersTextureBound");
                if (use == null || bound == null) return SetupStatus.Error("This URP version does not expose the Camera Sorting Layers Texture settings.");
                if (!use.boolValue || bound.intValue != id) failing.Add(renderers[i].name);
            }
            if (failing.Count == 0) return SetupStatus.Ok($"Enabled on {renderers.Count} renderer(s), bound to '{surface}'.");
            return SetupStatus.Error("Not set up on: " + string.Join(", ", failing) + ".");
        }

        public override bool Fix()
        {
            var renderers = SetupUtility.Find2DRenderers();
            var tagManager = SetupUtility.LoadTagManager();
            var surface = SetupUtility.Settings.surfaceSortingLayer;
            if (renderers.Count == 0 || tagManager == null || !SetupUtility.TryGetSortingLayerId(tagManager, surface, out var id))
            {
                Debug.LogError(SetupUtility.Prefix + "Cannot set the Camera Sorting Layers Texture: it needs a 2D Renderer and the surface sorting layer to exist first.");
                return false;
            }
            for (var i = 0; i < renderers.Count; i++)
            {
                var so = new SerializedObject(renderers[i]);
                var use = so.FindProperty("m_UseCameraSortingLayersTexture");
                var bound = so.FindProperty("m_CameraSortingLayersTextureBound");
                if (use.boolValue && bound.intValue == id) continue;
                Undo.RecordObject(renderers[i], "Set Camera Sorting Layers Texture");
                use.boolValue = true;
                bound.intValue = id;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(renderers[i]);
                Debug.Log(SetupUtility.Prefix + $"Enabled Camera Sorting Layers Texture on '{renderers[i].name}' and set its foremost layer to '{surface}'.", renderers[i]);
            }
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
