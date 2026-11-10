using SpookyWater.Samples;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpookyWater.Demo
{
    [AddComponentMenu("SpookyWater/Demo/Demo Controls Overlay")]
    [DisallowMultipleComponent]
    public class DemoControlsOverlay : MonoBehaviour
    {
        private const string DemoSceneName = "Demo_TiledWater";

        [Tooltip("Color of a key cap at rest.")]
        [SerializeField] private Color _keyColor = new(1f, 1f, 1f, 0.14f);
        [Tooltip("Color of a key cap while its key is held.")]
        [SerializeField] private Color _keyHeldColor = new(0.55f, 0.85f, 1f, 0.75f);
        [Tooltip("Color of the panel behind the controls.")]
        [SerializeField] private Color _panelColor = new(0f, 0.03f, 0.06f, 0.45f);
        [Tooltip("Color of the key letters and action labels.")]
        [SerializeField] private Color _textColor = new(1f, 1f, 1f, 0.9f);

        private struct KeyCap
        {
            public KeyCode Key;
            public Image Image;
        }

        private KeyCap[] _caps = System.Array.Empty<KeyCap>();
        private Font _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SpawnIfDemo(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SpawnIfDemo(scene);

        private static void SpawnIfDemo(Scene scene)
        {
            if (scene.name != DemoSceneName) return;
            if (FindAnyObjectByType<DemoControlsOverlay>()) return;

            var go = new GameObject("Demo Controls Overlay");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<DemoControlsOverlay>();
        }

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void Update()
        {
            for (var i = 0; i < _caps.Length; i++)
            {
                _caps[i].Image.color = SampleInput.GetKey(_caps[i].Key) ? _keyHeldColor : _keyColor;
            }
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = CreateRect("Panel", transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(32f, 32f);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = _panelColor;
            background.raycastTarget = false;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 24, 18, 18);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var caps = new System.Collections.Generic.List<KeyCap>();
            AddRow(panel, caps, "Move", KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D);
            AddRow(panel, caps, "Jump", KeyCode.Space);
            AddRow(panel, caps, "Dash", KeyCode.K);
            AddRow(panel, caps, "Leap / Slam", KeyCode.L);
            _caps = caps.ToArray();
        }

        private void AddRow(RectTransform parent, System.Collections.Generic.List<KeyCap> caps, string action, params KeyCode[] keys)
        {
            var row = CreateRect(action, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            foreach (var key in keys)
            {
                var cap = CreateRect(key.ToString(), row);
                var image = cap.gameObject.AddComponent<Image>();
                image.color = _keyColor;
                image.raycastTarget = false;
                var keyName = key.ToString();
                var size = cap.gameObject.AddComponent<LayoutElement>();
                size.preferredHeight = 44f;
                size.preferredWidth = keyName.Length > 1 ? 44f + 14f * keyName.Length : 44f;
                var letter = CreateText(cap, keyName, keyName.Length > 1 ? 20 : 24, FontStyle.Bold, TextAnchor.MiddleCenter);
                letter.rectTransform.anchorMin = Vector2.zero;
                letter.rectTransform.anchorMax = Vector2.one;
                letter.rectTransform.offsetMin = letter.rectTransform.offsetMax = Vector2.zero;
                caps.Add(new KeyCap { Key = key, Image = image });
            }

            var spacer = CreateRect("Gap", row).gameObject.AddComponent<LayoutElement>();
            spacer.preferredWidth = 10f;

            var label = CreateText(row, action, 26, FontStyle.Normal, TextAnchor.MiddleLeft);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        }

        private Text CreateText(Transform parent, string content, int size, FontStyle style, TextAnchor alignment)
        {
            var text = CreateRect("Text", parent).gameObject.AddComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = _textColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
