#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpookyWater.Editor
{
    public class DepthClipMaterialConverter : EditorWindow
    {
        private const string DepthWriteShaderName = "SpookyWater/Sprite Lit Depth Write";
        private const string SpriteLitDefaultShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default";
        private const string AlphaClipProperty = "_AlphaClipThreshold";
        private const string CopySuffix = "_DepthClip";

        private Material _material;
        private float _alphaClipThreshold = 0.5f;

        [MenuItem("Tools/SpookyWater/Convert Material To Depth Clipping")]
        private static void Open()
        {
            var window = GetWindow<DepthClipMaterialConverter>(true, "Convert Material To Depth Clipping");
            window.minSize = new Vector2(420f, 220f);
            if (Selection.activeObject is Material selected) window._material = selected;
        }

        private static bool IsEditable(Material material)
        {
            var path = AssetDatabase.GetAssetPath(material);
            return path.StartsWith("Assets/");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            _material = (Material)EditorGUILayout.ObjectField("Material", _material, typeof(Material), false);
            _alphaClipThreshold = EditorGUILayout.Slider(new GUIContent("Depth Alpha Clip",
                "Pixels fainter than this are discarded, so soft edges don't punch holes in the water."), _alphaClipThreshold, 0f, 1f);
            EditorGUILayout.Space();

            var depthShader = Shader.Find(DepthWriteShaderName);
            if (depthShader == null)
            {
                EditorGUILayout.HelpBox($"Shader '{DepthWriteShaderName}' not found.", MessageType.Error);
                return;
            }

            if (_material == null)
            {
                EditorGUILayout.HelpBox("Project materials (under Assets/) are converted in place - every renderer using them updates.\n\n" +
                                        "Read-only materials (from a package or built in, e.g. URP's Sprite-Lit-Default) can't be edited. " +
                                        "For those, a converted copy is saved into the project and swapped onto every renderer using the " +
                                        "original in the open scenes / prefab stage. Renderers in other scenes or in prefab assets that " +
                                        "aren't open keep the original.", MessageType.None);
            }
            else
            {
                var editable = IsEditable(_material);
                if (_material.shader == depthShader)
                {
                    EditorGUILayout.HelpBox("This material already uses the depth-clipping shader. Converting again only updates the alpha clip.", MessageType.Info);
                }
                else if (_material.shader == null || _material.shader.name != SpriteLitDefaultShaderName)
                {
                    var isShaderGraph = _material.shader != null && AssetDatabase.GetAssetPath(_material.shader).EndsWith(".shadergraph");
                    EditorGUILayout.HelpBox($"Source shader is '{(_material.shader != null ? _material.shader.name : "none")}', not Sprite-Lit-Default. " +
                                            "Main texture and tint are carried over, but anything else the shader did (custom effects, unlit shading) will be lost." +
                                            (isShaderGraph ? "\n\nThis is a Shader Graph: to keep its effects, open the graph instead and in Graph Settings set " +
                                                             "Depth Write to Force Enabled and turn on Alpha Clipping." : ""),
                                            MessageType.Warning);
                }

                if (!editable)
                {
                    EditorGUILayout.HelpBox($"'{_material.name}' is read-only (package or built-in), so it can't be converted in place.\n\n" +
                                            $"Convert will save a converted copy ('{_material.name}{CopySuffix}') into the project and swap it onto every " +
                                            "renderer using the original in the open scenes / prefab stage (undoable). Prefab instances get it as an " +
                                            "override - apply it to the prefab if you want it everywhere. Renderers in scenes or prefabs that aren't " +
                                            "open keep the original.", MessageType.Warning);
                }
            }

            using (new EditorGUI.DisabledScope(_material == null))
            {
                if (GUILayout.Button("Convert", GUILayout.Height(28f)))
                {
                    if (IsEditable(_material)) Convert(_material, depthShader, _alphaClipThreshold);
                    else ConvertCopy(_material, depthShader, _alphaClipThreshold);
                }
            }
        }

        private static void Convert(Material material, Shader depthShader, float alphaClipThreshold)
        {
            Undo.RecordObject(material, "Convert Material To Depth Clipping");
            ApplyDepthClipping(material, depthShader, alphaClipThreshold);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            Debug.Log($"Converted '{material.name}' to {DepthWriteShaderName}.", material);
        }

        private static void ConvertCopy(Material source, Shader depthShader, float alphaClipThreshold)
        {
            var path = EditorUtility.SaveFilePanelInProject("Save Converted Material Copy", source.name + CopySuffix, "mat",
                "Where to save the depth-clipping copy of this read-only material.");
            if (string.IsNullOrEmpty(path)) return;

            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                copy = new Material(source);
                ApplyDepthClipping(copy, depthShader, alphaClipThreshold);
                AssetDatabase.CreateAsset(copy, path);
            }
            else
            {
                Undo.RecordObject(copy, "Convert Material To Depth Clipping");
                copy.CopyPropertiesFromMaterial(source);
                ApplyDepthClipping(copy, depthShader, alphaClipThreshold);
                EditorUtility.SetDirty(copy);
            }
            AssetDatabase.SaveAssetIfDirty(copy);

            var swapped = SwapOnOpenRenderers(source, copy);
            Debug.Log($"'{source.name}' is read-only - saved a converted copy at {path} and swapped it onto {swapped} renderer(s) " +
                      "in the open scenes / prefab stage.", copy);
        }

        private static void ApplyDepthClipping(Material material, Shader depthShader, float alphaClipThreshold)
        {
            var mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex")
                        : material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            var color = material.HasProperty("_Color") ? material.GetColor("_Color")
                      : material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;

            material.shader = depthShader;
            if (mainTex != null) material.SetTexture("_MainTex", mainTex);
            material.SetColor("_Color", color);
            material.SetFloat(AlphaClipProperty, alphaClipThreshold);
        }

        private static int SwapOnOpenRenderers(Material source, Material replacement)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            Renderer[] renderers;
            if (prefabStage != null)
            {
                renderers = prefabStage.prefabContentsRoot.GetComponentsInChildren<Renderer>(true);
            }
            else
            {
                var list = new System.Collections.Generic.List<Renderer>();
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    foreach (var root in scene.GetRootGameObjects())
                        list.AddRange(root.GetComponentsInChildren<Renderer>(true));
                }
                renderers = list.ToArray();
            }

            var swapped = 0;
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != source) continue;
                    materials[i] = replacement;
                    changed = true;
                }
                if (!changed) continue;

                Undo.RecordObject(renderer, "Swap Depth Clipping Material");
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                swapped++;
            }

            if (prefabStage != null) EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            return swapped;
        }
    }
}
#endif
