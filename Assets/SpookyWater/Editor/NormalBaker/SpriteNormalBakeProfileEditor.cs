using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    [CustomEditor(typeof(SpriteNormalBakeProfile))]
    public class SpriteNormalBakeProfileEditor : GroupedInspector
    {
        private const int PreviewMaxSize = 512;
        private const float PreviewHeight = 300f;
        private const string ModeKey = "SpookyWater.SpriteNormalBake.PreviewMode";
        private static readonly GUIContent[] Modes =
        {
            new("Lit + Normals", "The lit sprite and its normal map side by side. Drag in either to move the light."),
            new("Lit", "The sprite shaded by a point light using the normals. Drag to move the light."),
            new("Normals", "The normal map as it will be written (red = right, green = up, blue = facing you)."),
            new("Source", "The sprite as it is."),
            new("Baked File", "The normal map currently saved on disk, to compare with the live settings.")
        };

        private const int ModeBoth = 0;
        private const int ModeLit = 1;
        private const int ModeNormals = 2;
        private const int ModeSource = 3;

        private int _previewIndex;
        private Texture2D _previewSourceTexture;
        private SpriteNormalSource _source;
        private SpriteNormalSource _previewSource;
        private int _previewFactor = 1;
        private Vector3[] _normals;
        private bool _dirty = true;
        private bool _litDirty = true;
        private bool _imagesDirty = true;
        private Texture2D _litTexture;
        private Texture2D _normalTexture;
        private Texture2D _sourceTexture;
        private Texture2D _bakedTexture;
        private string _bakedPath;
        private System.DateTime _bakedTime;
        private Rect _dragRect;
        private Vector2 _light = new(0.2f, 0.8f);
        private float _lightHeight = 0.35f;
        private float _ambient = 0.2f;
        private string _loadError;

        private SpriteNormalBakeProfile Profile => (SpriteNormalBakeProfile)target;

        public Texture2D PreviewOverride { get; set; }

        public bool Embedded { get; set; }

        private void OnDisable()
        {
            foreach (var texture in new[] { _litTexture, _normalTexture, _sourceTexture, _bakedTexture })
                if (texture) DestroyImmediate(texture);
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            if (!EditorGUI.EndChangeCheck()) return;
            _dirty = true;
            Repaint();
        }

        protected override void DrawBeforeProperties()
        {
            if (Embedded) return;
            EditorGUILayout.HelpBox(
                "Bakes a normal map from each sprite's outline (rounded inward by the bevel), the lines drawn inside it and its painted detail, and assigns it as the sprite's _NormalMap secondary texture. " +
                "Use the baked sprites with Sprite-Lit-Default and 2D lights with Normal Maps enabled. On Sprite Lit Auto Normals or Sprite Lit Depth Write the map is added to their own guess: set Edge Bevel and Detail Bumps to 0 there.",
                MessageType.None);
        }

        protected override void DrawAfterProperties()
        {
            EditorGUILayout.Space(6f);
            serializedObject.ApplyModifiedProperties();
            if (!Embedded)
            {
                DrawBakeButtons();
                EditorGUILayout.Space(6f);
            }

            DrawPreview();
        }

        private void DrawBakeButtons()
        {
            var count = 0;
            foreach (var texture in Profile.Textures)
                if (texture) count++;

            using (new EditorGUI.DisabledScope(count == 0 || string.IsNullOrEmpty(Profile.Suffix)))
            {
                if (GUILayout.Button(count > 1 ? $"Bake All ({count})" : "Bake", GUILayout.Height(26f)))
                {
                    var baked = SpriteNormalBaker.BakeAll(Profile);
                    Debug.Log($"SpookyWater: baked {baked} of {count} sprite normal map(s) with {Profile.name}.", Profile);
                    GUIUtility.ExitGUI();
                }
            }

            if (string.IsNullOrEmpty(Profile.Suffix))
                EditorGUILayout.HelpBox("Suffix is empty: the normal map would overwrite the sprite.", MessageType.Error);
        }

        private void DrawPreview()
        {
            var names = new List<string>();
            var valid = new List<Texture2D>();
            if (PreviewOverride)
            {
                valid.Add(PreviewOverride);
                names.Add(PreviewOverride.name);
            }
            else
            {
                foreach (var texture in Profile.Textures)
                {
                    if (!texture) continue;
                    valid.Add(texture);
                    names.Add(texture.name);
                }
            }

            if (valid.Count == 0)
            {
                EditorGUILayout.HelpBox("Add sprite textures under Sources to preview and bake them.", MessageType.Info);
                return;
            }

            _previewIndex = Mathf.Clamp(_previewIndex, 0, valid.Count - 1);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            if (valid.Count > 1) _previewIndex = EditorGUILayout.Popup("Texture", _previewIndex, names.ToArray());
            var previewTexture = valid[_previewIndex];
            if (previewTexture != _previewSourceTexture) LoadPreviewSource(previewTexture);

            if (_loadError != null)
            {
                EditorGUILayout.HelpBox(_loadError, MessageType.Warning);
                return;
            }

            var mode = Mathf.Clamp(SessionState.GetInt(ModeKey, ModeBoth), 0, Modes.Length - 1);
            var nextMode = GUILayout.Toolbar(mode, Modes);
            if (nextMode != mode)
            {
                SessionState.SetInt(ModeKey, nextMode);
                mode = nextMode;
            }

            var showsLight = mode is ModeBoth or ModeLit or ModeNormals;
            using (new EditorGUI.DisabledScope(!showsLight))
            {
                EditorGUI.BeginChangeCheck();
                _lightHeight = EditorGUILayout.Slider(new GUIContent("Light Height", "Preview only: height of the point light above the sprite, as a fraction of its size. Low = grazing rim light, high = flat frontal light."), _lightHeight, 0.02f, 2f);
                _ambient = EditorGUILayout.Slider(new GUIContent("Ambient", "Preview only: light that reaches every pixel regardless of its normal."), _ambient, 0f, 1f);
                if (EditorGUI.EndChangeCheck()) _litDirty = true;
            }

            if (_dirty) Rebake();
            if (_imagesDirty) RefreshImages();
            if (_litDirty) RefreshLit();

            var output = SpriteNormalBaker.OutputPath(previewTexture, Profile.Suffix);
            var area = GUILayoutUtility.GetRect(10f, PreviewHeight, GUILayout.ExpandWidth(true));
            int w = _previewSource.Width, h = _previewSource.Height;
            switch (mode)
            {
                case ModeBoth:
                    var half = (area.width - 6f) * 0.5f;
                    var left = Fit(new Rect(area.x, area.y, half, area.height), w, h);
                    var right = Fit(new Rect(area.x + half + 6f, area.y, half, area.height), w, h);
                    Draw(left, _litTexture);
                    Draw(right, _normalTexture);
                    HandleLightDrag(left, right);
                    break;
                case ModeLit:
                case ModeNormals:
                    var rect = Fit(area, w, h);
                    Draw(rect, mode == ModeLit ? _litTexture : _normalTexture);
                    HandleLightDrag(rect, Rect.zero);
                    break;
                case ModeSource:
                    Draw(Fit(area, w, h), _sourceTexture);
                    break;
                default:
                    var baked = LoadBaked(output);
                    if (baked) Draw(Fit(area, baked.width, baked.height), baked);
                    else GUI.Label(area, "Not baked yet.", EditorStyles.centeredGreyMiniLabel);
                    break;
            }

            var existing = output != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(output) : null;
            var status = existing
                ? SpriteNormalBaker.HasSecondaryTexture(previewTexture, existing) ? "baked and assigned" : "baked, not assigned"
                : "not baked yet";
            var size = _previewFactor > 1 ? $" Preview at 1/{_previewFactor} size; the bake uses full resolution." : string.Empty;
            var hint = showsLight ? " Drag in the preview to move the light." : string.Empty;
            EditorGUILayout.LabelField($"{output} ({status}).{size}{hint}", EditorStyles.wordWrappedMiniLabel);
        }

        private static void Draw(Rect rect, Texture2D texture)
        {
            if (texture) EditorGUI.DrawTextureTransparent(rect, texture, ScaleMode.StretchToFill);
        }

        private Texture2D LoadBaked(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
            var time = System.IO.File.GetLastWriteTimeUtc(path);
            if (_bakedTexture && _bakedPath == path && _bakedTime == time) return _bakedTexture;
            if (_bakedTexture) DestroyImmediate(_bakedTexture);
            _bakedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!_bakedTexture.LoadImage(System.IO.File.ReadAllBytes(path)))
            {
                DestroyImmediate(_bakedTexture);
                return null;
            }

            _bakedTexture.hideFlags = HideFlags.HideAndDontSave;
            _bakedPath = path;
            _bakedTime = time;
            return _bakedTexture;
        }

        private void LoadPreviewSource(Texture2D texture)
        {
            _previewSourceTexture = texture;
            _loadError = null;
            _source = null;
            _previewSource = null;
            if (!SpriteNormalBaker.TryLoadSource(texture, out _source, out var error))
            {
                _loadError = error;
                return;
            }

            _previewFactor = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(_source.Width, _source.Height) / (float)PreviewMaxSize));
            _previewSource = _source.Downsampled(_previewFactor);
            _dirty = true;
        }

        private void Rebake()
        {
            _dirty = false;
            _litDirty = true;
            _imagesDirty = true;
            if (_previewSource == null) return;
            _normals = SpriteNormalBaker.Bake(_previewSource, SpriteNormalBakeSettings.From(Profile).ScaledDown(_previewFactor));
        }

        private void RefreshImages()
        {
            _imagesDirty = false;
            if (_previewSource == null || _normals == null) return;
            var source = _previewSource.Pixels;
            var pixels = new Color[source.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var n = _normals[i];
                pixels[i] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, source[i].a);
            }

            Upload(ref _normalTexture, pixels);
            Upload(ref _sourceTexture, source);
        }

        private void RefreshLit()
        {
            _litDirty = false;
            if (_previewSource == null || _normals == null) return;
            int w = _previewSource.Width, h = _previewSource.Height;
            var source = _previewSource.Pixels;
            var pixels = new Color[w * h];
            var lx = _light.x * w;
            var ly = _light.y * h;
            var lz = _lightHeight * Mathf.Max(w, h);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                var c = source[i];
                var dir = new Vector3(lx - x, ly - y, lz).normalized;
                var light = _ambient + (1f - _ambient) * Mathf.Max(0f, Vector3.Dot(_normals[i], dir));
                pixels[i] = new Color(c.r * light, c.g * light, c.b * light, c.a);
            }

            Upload(ref _litTexture, pixels);
        }

        private void Upload(ref Texture2D texture, Color[] pixels)
        {
            int w = _previewSource.Width, h = _previewSource.Height;
            if (!texture || texture.width != w || texture.height != h)
            {
                if (texture) DestroyImmediate(texture);
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            }

            texture.SetPixels(pixels);
            texture.Apply();
        }

        private void HandleLightDrag(Rect first, Rect second)
        {
            var e = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button == 0 && (first.Contains(e.mousePosition) || second.Contains(e.mousePosition)):
                    GUIUtility.hotControl = id;
                    _dragRect = first.Contains(e.mousePosition) ? first : second;
                    MoveLight(_dragRect, e.mousePosition);
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    MoveLight(_dragRect, e.mousePosition);
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
            }

            EditorGUIUtility.AddCursorRect(first, MouseCursor.MoveArrow);
            if (second.width > 0f) EditorGUIUtility.AddCursorRect(second, MouseCursor.MoveArrow);
        }

        private void MoveLight(Rect rect, Vector2 mouse)
        {
            _light = new Vector2((mouse.x - rect.x) / rect.width, 1f - (mouse.y - rect.y) / rect.height);
            _litDirty = true;
            Repaint();
        }

        private static Rect Fit(Rect area, int width, int height)
        {
            var scale = Mathf.Min(area.width / width, area.height / height);
            var w = width * scale;
            var h = height * scale;
            return new Rect(area.x + (area.width - w) * 0.5f, area.y + (area.height - h) * 0.5f, w, h);
        }
    }
}
