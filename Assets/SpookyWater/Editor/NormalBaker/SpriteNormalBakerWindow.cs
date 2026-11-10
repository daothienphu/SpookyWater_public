using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace SpookyWater.Editor
{
    public class SpriteNormalBakerWindow : EditorWindow
    {
        private enum Scope
        {
            OpenScenes,
            SelectedObjects
        }

        private enum Status
        {
            NotBaked,
            NotAssigned,
            OutOfDate,
            Baked,
            OwnNormalMap,
            ReadOnly
        }

        private sealed class Row
        {
            public Texture2D Texture;
            public int Users;
            public bool Checked;
            public SpriteNormalBakeProfile Owner;
            public Texture2D Baked;
            public Status Status;
        }

        private const float RowHeight = 36f;
        private const string DefaultProfileKey = "SpookyWater.SpriteNormalBaker.DefaultProfile.";
        private const string ScopeKey = "SpookyWater.SpriteNormalBaker.Scope";
        private const string InactiveKey = "SpookyWater.SpriteNormalBaker.IncludeInactive";
        private const string TilemapsKey = "SpookyWater.SpriteNormalBaker.IncludeTilemaps";
        private const string LitLayersKey = "SpookyWater.SpriteNormalBaker.OnlyLitLayers";

        private static readonly GUIContent[] ScopeLabels =
        {
            new("Open Scenes", "Every sprite and tilemap in the loaded scenes (or the open prefab)."),
            new("Selected Objects", "Only the selected GameObjects and their children.")
        };

        private readonly List<Row> _rows = new();
        private SpriteNormalBakeProfile _defaultProfile;
        private Scope _scope;
        private bool _includeInactive = true;
        private bool _includeTilemaps = true;
        private bool _onlyLitLayers = true;
        private bool _filteringLayers;
        private int _hiddenUnlit;
        private string _unlitLayerNames = string.Empty;
        private Vector2 _listScroll;
        private Vector2 _settingsScroll;
        private Texture2D _selected;
        private UnityEditor.Editor _profileEditor;

        [MenuItem("Tools/SpookyWater/Sprite Normal Baker")]
        public static void Open()
        {
            var window = GetWindow<SpriteNormalBakerWindow>();
            window.titleContent = new GUIContent("Sprite Normal Baker");
            window.minSize = new Vector2(420f, 420f);
            window.Show();
        }

        private static string ProfilePrefKey => DefaultProfileKey + PlayerSettings.productGUID;

        private void OnEnable()
        {
            _scope = (Scope)EditorPrefs.GetInt(ScopeKey, 0);
            _includeInactive = EditorPrefs.GetBool(InactiveKey, true);
            _includeTilemaps = EditorPrefs.GetBool(TilemapsKey, true);
            _onlyLitLayers = EditorPrefs.GetBool(LitLayersKey, true);
            var guid = EditorPrefs.GetString(ProfilePrefKey, string.Empty);
            if (!string.IsNullOrEmpty(guid)) _defaultProfile = AssetDatabase.LoadAssetAtPath<SpriteNormalBakeProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (!_defaultProfile)
            {
                var all = AllProfiles();
                if (all.Count > 0) SetDefaultProfile(all[0]);
            }

            EditorSceneManager.sceneOpened += OnSceneOpened;
            Selection.selectionChanged += OnSelectionChanged;
            Scan();
        }

        private void OnDisable()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            Selection.selectionChanged -= OnSelectionChanged;
            if (_profileEditor) DestroyImmediate(_profileEditor);
        }

        private void OnFocus() => Scan();

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            Scan();
            Repaint();
        }

        private void OnSelectionChanged()
        {
            if (_scope != Scope.SelectedObjects) return;
            Scan();
            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawList();
            DrawBakeBar();
            DrawSettings();
            if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4f);
            EditorGUI.BeginChangeCheck();
            var profile = (SpriteNormalBakeProfile)EditorGUILayout.ObjectField(
                new GUIContent("Default Profile", "Bake settings for every texture that isn't in a profile yet. Baking adds such textures to it, so later bakes and the profile's own Bake All find them again."),
                _defaultProfile, typeof(SpriteNormalBakeProfile), false);
            if (EditorGUI.EndChangeCheck())
            {
                SetDefaultProfile(profile);
                RefreshStatuses();
            }

            if (!_defaultProfile)
            {
                EditorGUILayout.HelpBox("No default profile. Create one to bake textures that aren't in a profile yet.", MessageType.Warning);
                if (GUILayout.Button("Create Default Profile")) CreateDefaultProfile();
            }

            EditorGUI.BeginChangeCheck();
            _scope = (Scope)GUILayout.Toolbar((int)_scope, ScopeLabels);
            using (new EditorGUILayout.HorizontalScope())
            {
                _includeInactive = EditorGUILayout.ToggleLeft(new GUIContent("Inactive Objects", "Include disabled GameObjects and renderers."), _includeInactive, GUILayout.Width(130f));
                _includeTilemaps = EditorGUILayout.ToggleLeft(new GUIContent("Tilemaps", "Include every sprite painted on Tilemaps."), _includeTilemaps, GUILayout.Width(90f));
                _onlyLitLayers = EditorGUILayout.ToggleLeft(new GUIContent("Only Lit Layers", "Skip sprites on sorting layers that no normal-mapped 2D light in the scene targets (for example distant backgrounds that only get the Global Light 2D): their normals would never be used. Which layers a light reaches is its Target Sorting Layers (on a Water Glow Light: Lit Layers)."), _onlyLitLayers, GUILayout.Width(115f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", GUILayout.Width(70f))) Scan();
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt(ScopeKey, (int)_scope);
                EditorPrefs.SetBool(InactiveKey, _includeInactive);
                EditorPrefs.SetBool(TilemapsKey, _includeTilemaps);
                EditorPrefs.SetBool(LitLayersKey, _onlyLitLayers);
                Scan();
            }
        }

        private void DrawList()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{_rows.Count} sprite texture(s)", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(40f))) SetChecked(_ => true);
                if (GUILayout.Button("None", EditorStyles.miniButtonMid, GUILayout.Width(44f))) SetChecked(_ => false);
                if (GUILayout.Button("Needing Bake", EditorStyles.miniButtonRight, GUILayout.Width(90f))) SetChecked(NeedsBake);
            }

            if (_onlyLitLayers && !_filteringLayers)
                EditorGUILayout.LabelField("No 2D light with Normal Maps enabled in the scene: showing every layer.", EditorStyles.wordWrappedMiniLabel);
            else if (_filteringLayers && _hiddenUnlit > 0)
                EditorGUILayout.LabelField($"{_hiddenUnlit} texture(s) hidden on layers no normal-mapped light reaches: {_unlitLayerNames}.", EditorStyles.wordWrappedMiniLabel);

            if (_rows.Count == 0)
            {
                EditorGUILayout.HelpBox(_scope == Scope.SelectedObjects
                    ? "No sprites in the selected objects."
                    : "No sprites in the open scenes.", MessageType.Info);
                return;
            }

            var height = Mathf.Clamp(_rows.Count * RowHeight + 6f, RowHeight + 6f, Mathf.Max(RowHeight * 3f, position.height * 0.4f));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(height));
            foreach (var row in _rows) DrawRow(row);
            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(Row row)
        {
            var rect = EditorGUILayout.GetControlRect(false, RowHeight - 2f);
            var selected = row.Texture == _selected;
            if (Event.current.type == EventType.Repaint && selected)
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.24f, 0.37f, 0.59f, 0.6f) : new Color(0.55f, 0.7f, 0.95f, 0.5f));

            var toggleRect = new Rect(rect.x + 2f, rect.y + (rect.height - 16f) * 0.5f, 16f, 16f);
            var thumbRect = new Rect(toggleRect.xMax + 4f, rect.y + 1f, rect.height - 2f, rect.height - 2f);
            const float profileWidth = 150f;
            var profileRect = new Rect(rect.xMax - profileWidth - 2f, rect.y + (rect.height - 18f) * 0.5f, profileWidth, 18f);
            var normalRect = new Rect(thumbRect.xMax + 2f, thumbRect.y, thumbRect.width, thumbRect.height);
            var textRect = new Rect(normalRect.xMax + 6f, rect.y, profileRect.x - normalRect.xMax - 10f, rect.height);

            using (new EditorGUI.DisabledScope(row.Status == Status.ReadOnly))
                row.Checked = EditorGUI.Toggle(toggleRect, row.Checked) && row.Status != Status.ReadOnly;

            var thumb = AssetPreview.GetAssetPreview(row.Texture) ?? AssetPreview.GetMiniThumbnail(row.Texture);
            if (thumb) GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit);
            if (row.Baked)
            {
                var normalThumb = AssetPreview.GetAssetPreview(row.Baked) ?? AssetPreview.GetMiniThumbnail(row.Baked);
                if (normalThumb) GUI.DrawTexture(normalRect, normalThumb, ScaleMode.ScaleToFit);
                GUI.Label(normalRect, new GUIContent(string.Empty, $"{row.Baked.name}: the baked normal map. Click the name to preview it live."));
            }
            else if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(normalRect, new Color(0.5f, 0.5f, 0.5f, 0.12f));
            }

            var users = row.Users == 1 ? "1 use" : $"{row.Users} uses";
            GUI.Label(new Rect(textRect.x, textRect.y + 1f, textRect.width, 16f), row.Texture.name, EditorStyles.label);
            var statusStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = StatusColor(row.Status) } };
            GUI.Label(new Rect(textRect.x, textRect.y + 17f, textRect.width, 14f), $"{StatusText(row.Status)} | {users}" + (row.Owner ? string.Empty : " | default profile"), statusStyle);

            EditorGUI.BeginChangeCheck();
            var owner = (SpriteNormalBakeProfile)EditorGUI.ObjectField(profileRect, row.Owner, typeof(SpriteNormalBakeProfile), false);
            if (EditorGUI.EndChangeCheck()) SetOwner(row, owner);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && textRect.Contains(e.mousePosition))
            {
                _selected = row.Texture;
                if (e.clickCount == 2) EditorGUIUtility.PingObject(row.Texture);
                e.Use();
                Repaint();
            }
        }

        private void DrawBakeBar()
        {
            var count = 0;
            foreach (var row in _rows)
                if (row.Checked && (row.Owner || _defaultProfile)) count++;

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(count == 0))
            {
                if (GUILayout.Button(count == 1 ? "Bake 1 Texture" : $"Bake {count} Textures", GUILayout.Height(28f)))
                {
                    BakeChecked();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawSettings()
        {
            var row = _rows.Find(r => r.Texture == _selected);
            var profile = row != null && row.Owner ? row.Owner : _defaultProfile;
            EditorGUILayout.Space(6f);
            if (!profile)
            {
                EditorGUILayout.HelpBox("Pick a default profile to see its settings.", MessageType.None);
                return;
            }

            var label = row != null ? $"{profile.name} - previewing {row.Texture.name}" : $"{profile.name} - click a texture above to preview it";
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (!_profileEditor || _profileEditor.target != profile)
            {
                if (_profileEditor) DestroyImmediate(_profileEditor);
                _profileEditor = UnityEditor.Editor.CreateEditor(profile, typeof(SpriteNormalBakeProfileEditor));
            }

            var editor = (SpriteNormalBakeProfileEditor)_profileEditor;
            editor.Embedded = true;
            editor.PreviewOverride = row?.Texture;

            _settingsScroll = EditorGUILayout.BeginScrollView(_settingsScroll);
            EditorGUI.BeginChangeCheck();
            editor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) RefreshStatuses();
            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            var previous = new Dictionary<Texture2D, bool>();
            foreach (var row in _rows) previous[row.Texture] = row.Checked;

            var litLayers = LitLayers();
            _filteringLayers = _onlyLitLayers && litLayers != null;
            var users = new Dictionary<Texture2D, int>();
            var unlit = new HashSet<Texture2D>();
            var unlitLayers = new SortedSet<string>();
            foreach (var root in Roots())
            {
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(_includeInactive))
                    if (_includeInactive || renderer.enabled) Count(renderer.sprite, renderer, users, unlit, unlitLayers, litLayers);

                if (!_includeTilemaps) continue;
                foreach (var tilemap in root.GetComponentsInChildren<Tilemap>(_includeInactive))
                {
                    var renderer = tilemap.GetComponent<TilemapRenderer>();
                    if (!renderer) continue;
                    var sprites = new Sprite[tilemap.GetUsedSpritesCount()];
                    tilemap.GetUsedSpritesNonAlloc(sprites);
                    foreach (var sprite in sprites) Count(sprite, renderer, users, unlit, unlitLayers, litLayers);
                }
            }

            unlit.ExceptWith(users.Keys);
            _hiddenUnlit = unlit.Count;
            _unlitLayerNames = string.Join(", ", unlitLayers);

            var owners = Owners();
            _rows.Clear();
            foreach (var pair in users)
            {
                var row = new Row { Texture = pair.Key, Users = pair.Value };
                owners.TryGetValue(pair.Key, out row.Owner);
                row.Status = StatusOf(row);
                row.Checked = previous.TryGetValue(pair.Key, out var was) ? was && row.Status != Status.ReadOnly : NeedsBake(row);
                _rows.Add(row);
            }

            _rows.Sort((a, b) => string.CompareOrdinal(a.Texture.name, b.Texture.name));
        }

        private IEnumerable<GameObject> Roots()
        {
            if (_scope == Scope.SelectedObjects)
            {
                foreach (var go in Selection.gameObjects)
                    if (!EditorUtility.IsPersistent(go)) yield return go;
                yield break;
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                yield return stage.prefabContentsRoot;
                yield break;
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var go in scene.GetRootGameObjects()) yield return go;
            }
        }

        private void Count(Sprite sprite, Renderer renderer, Dictionary<Texture2D, int> users, HashSet<Texture2D> unlit, SortedSet<string> unlitLayers, HashSet<int> litLayers)
        {
            if (!sprite) return;
            var texture = sprite.texture;
            if (!texture || !AssetDatabase.Contains(texture)) return;
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) is TextureImporter { textureType: TextureImporterType.NormalMap }) return;
            if (_filteringLayers)
            {
                var group = renderer.GetComponentInParent<SortingGroup>(true);
                var layer = group ? group.sortingLayerID : renderer.sortingLayerID;
                if (!litLayers.Contains(layer))
                {
                    unlit.Add(texture);
                    unlitLayers.Add(SortingLayer.IDToName(layer));
                    return;
                }
            }

            users[texture] = users.TryGetValue(texture, out var count) ? count + 1 : 1;
        }

        private static IEnumerable<GameObject> SceneRoots()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) yield return stage.prefabContentsRoot;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var go in scene.GetRootGameObjects()) yield return go;
            }
        }

        private HashSet<int> LitLayers()
        {
            HashSet<int> layers = null;
            foreach (var root in SceneRoots())
            foreach (var light in root.GetComponentsInChildren<Light2D>(_includeInactive))
            {
                if (!_includeInactive && !light.isActiveAndEnabled) continue;
                if (light.lightType == Light2D.LightType.Global || light.normalMapQuality == Light2D.NormalMapQuality.Disabled) continue;
                layers ??= new HashSet<int>();
                var targets = light.targetSortingLayers;
                if (targets != null) layers.UnionWith(targets);
            }

            return layers;
        }

        private void RefreshStatuses()
        {
            foreach (var row in _rows)
            {
                var before = row.Status;
                row.Status = StatusOf(row);
                if (before != row.Status && NeedsBake(row)) row.Checked = true;
            }
        }

        private Status StatusOf(Row row)
        {
            var path = AssetDatabase.GetAssetPath(row.Texture);
            row.Baked = null;
            if (!SpriteNormalBaker.IsWritable(path)) return Status.ReadOnly;
            var profile = row.Owner ? row.Owner : _defaultProfile;
            var outputPath = SpriteNormalBaker.OutputPath(row.Texture, profile ? profile.Suffix : "_n");
            var baked = outputPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath) : null;
            row.Baked = baked;
            var assigned = SpriteNormalBaker.SecondaryNormalMap(row.Texture);
            if (assigned && assigned != baked) return Status.OwnNormalMap;
            if (!baked) return Status.NotBaked;
            if (assigned != baked && (!profile || profile.AssignToSprite)) return Status.NotAssigned;
            return SpriteNormalBaker.IsUpToDate(row.Texture, baked, profile) ? Status.Baked : Status.OutOfDate;
        }

        private static bool NeedsBake(Row row) => row.Status is Status.NotBaked or Status.NotAssigned or Status.OutOfDate;

        private void SetChecked(System.Predicate<Row> predicate)
        {
            foreach (var row in _rows) row.Checked = row.Status != Status.ReadOnly && predicate(row);
        }

        private void BakeChecked()
        {
            var jobs = new List<SpriteNormalBakeJob>();
            var added = false;
            foreach (var row in _rows)
            {
                if (!row.Checked || row.Status == Status.ReadOnly) continue;
                var profile = row.Owner;
                if (!profile)
                {
                    if (!_defaultProfile) continue;
                    profile = _defaultProfile;
                    if (!profile.Textures.Contains(row.Texture))
                    {
                        if (!added) Undo.RecordObject(profile, "Add Textures To Sprite Normal Bake Profile");
                        profile.Textures.Add(row.Texture);
                        added = true;
                    }
                }

                jobs.Add(new SpriteNormalBakeJob(row.Texture, profile));
            }

            if (added)
            {
                EditorUtility.SetDirty(_defaultProfile);
                AssetDatabase.SaveAssetIfDirty(_defaultProfile);
            }

            SpriteNormalBaker.Bake(jobs);
            Scan();
        }

        private void SetOwner(Row row, SpriteNormalBakeProfile owner)
        {
            foreach (var profile in AllProfiles())
            {
                if (profile == owner || !profile.Textures.Contains(row.Texture)) continue;
                Undo.RecordObject(profile, "Change Sprite Normal Bake Profile");
                profile.Textures.RemoveAll(t => t == row.Texture);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }

            if (owner && !owner.Textures.Contains(row.Texture))
            {
                Undo.RecordObject(owner, "Change Sprite Normal Bake Profile");
                owner.Textures.Add(row.Texture);
                EditorUtility.SetDirty(owner);
                AssetDatabase.SaveAssetIfDirty(owner);
            }

            row.Owner = owner;
            row.Status = StatusOf(row);
            if (NeedsBake(row)) row.Checked = true;
        }

        private void SetDefaultProfile(SpriteNormalBakeProfile profile)
        {
            _defaultProfile = profile;
            var guid = profile ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile)) : string.Empty;
            EditorPrefs.SetString(ProfilePrefKey, guid);
        }

        private void CreateDefaultProfile()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Sprite Normal Bake Profile", "SpriteNormalBakeProfile", "asset", "Where to save the default bake profile.");
            if (string.IsNullOrEmpty(path)) return;
            var profile = CreateInstance<SpriteNormalBakeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            SetDefaultProfile(profile);
            RefreshStatuses();
            GUIUtility.ExitGUI();
        }

        private static List<SpriteNormalBakeProfile> AllProfiles()
        {
            var result = new List<SpriteNormalBakeProfile>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(SpriteNormalBakeProfile)))
            {
                var profile = AssetDatabase.LoadAssetAtPath<SpriteNormalBakeProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile) result.Add(profile);
            }

            return result;
        }

        private static Dictionary<Texture2D, SpriteNormalBakeProfile> Owners()
        {
            var owners = new Dictionary<Texture2D, SpriteNormalBakeProfile>();
            foreach (var profile in AllProfiles())
            foreach (var texture in profile.Textures)
                if (texture && !owners.ContainsKey(texture)) owners[texture] = profile;
            return owners;
        }

        private static string StatusText(Status status) => status switch
        {
            Status.NotBaked => "Not baked",
            Status.NotAssigned => "Baked, not assigned to the sprite",
            Status.OutOfDate => "Out of date (sprite or settings changed)",
            Status.Baked => "Baked",
            Status.OwnNormalMap => "Has its own normal map (baking replaces it)",
            _ => "In a read-only package"
        };

        private static Color StatusColor(Status status)
        {
            var pro = EditorGUIUtility.isProSkin;
            return status switch
            {
                Status.Baked => pro ? new Color(0.5f, 0.85f, 0.5f) : new Color(0.1f, 0.5f, 0.1f),
                Status.NotBaked or Status.NotAssigned or Status.OutOfDate => pro ? new Color(0.95f, 0.75f, 0.35f) : new Color(0.6f, 0.4f, 0f),
                _ => pro ? new Color(0.65f, 0.65f, 0.65f) : new Color(0.35f, 0.35f, 0.35f)
            };
        }
    }
}
