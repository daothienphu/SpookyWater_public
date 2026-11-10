using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

namespace SpookyWater
{
    [DisallowMultipleComponent]
    [AddComponentMenu("SpookyWater/Water Reflectable")]
    [HelpURL(SpookyWaterDocs.Reflectable)]
    public class WaterReflectable : MonoBehaviour
    {
        [Tooltip("Mirror this object's sprites, tilemaps and sprite shapes in the water surface while the camera is " +
                 "above it. Each source gets a hidden copy on the reflection layer, so the object's own layer " +
                 "stays free for physics and gameplay. Do not add this to fog sprites: a mirrored fog sheet " +
                 "starts abruptly at the waterline and draws a hard colour line across the water.")]
        [SerializeField] private bool _reflect = true;
        [Tooltip("Show this object through the water surface while the camera is below it. Only the parts above the " +
                 "surface show; a sprite entirely under water is skipped.")]
        [SerializeField] private bool _visibleFromBelow = true;
        [Tooltip("Also include the sprite, tilemap and sprite shape renderers on child objects. They are collected " +
                 "when this component is enabled.")]
        [SerializeField] private bool _includeChildren = true;

        [Header("Occasional Sync")]
        [Tooltip("Sprite, colour, flip and visibility are copied to the reflection every frame. These options " +
                 "keep re-copying the rarely changing state too. Off means it is copied once. Call SyncAllNow() " +
                 "from code instead when you know when the change happens. This one re-copies sorting layer, " +
                 "order in layer and sprite sort point.")]
        [SerializeField] private bool _syncSorting;
        [Tooltip("Frames between sorting checks. 1 checks every frame.")]
        [Range(1, 60)] [SerializeField] private int _sortingSyncInterval = 60;
        [Tooltip("Keep re-copying the material, for example when a hit-flash swaps it. Off means copied once.")]
        [SerializeField] private bool _syncMaterial;
        [Tooltip("Frames between material checks. 1 checks every frame.")]
        [Range(1, 60)] [SerializeField] private int _materialSyncInterval = 60;
        [Tooltip("Keep re-copying draw mode, sliced or tiled size, tile mode and mask interaction. Off means copied once.")]
        [SerializeField] private bool _syncDrawMode;
        [Tooltip("Frames between draw mode checks. 1 checks every frame.")]
        [Range(1, 60)] [SerializeField] private int _drawModeSyncInterval = 60;

        [System.Flags]
        private enum SyncGroups
        {
            None = 0,
            Sorting = 1,
            Material = 2,
            DrawMode = 4,
            All = Sorting | Material | DrawMode
        }

        private static readonly HashSet<WaterReflectable> ActiveReflectables = new();
        private static readonly HashSet<Renderer> AllProxies = new();
        private static readonly Dictionary<Tilemap, Tilemap> TilemapProxies = new();

        private readonly List<SpriteRenderer> _spriteSources = new();
        private readonly List<SpriteRenderer> _spriteProxies = new();
        private readonly List<TilemapRenderer> _tilemapSources = new();
        private readonly List<TilemapRenderer> _tilemapProxies = new();
        private readonly List<Tilemap> _tilemapSourceMaps = new();
        private readonly List<Tilemap> _tilemapProxyMaps = new();
        private readonly List<SpriteShapeRenderer> _shapeSources = new();
        private readonly List<SpriteShapeRenderer> _shapeProxies = new();
        private int _proxyLayer = -1;
        private bool _pendingFullSync;
        private int _syncFrameOffset;

        private void OnEnable()
        {
            _spriteSources.Clear();
            _tilemapSources.Clear();
            _shapeSources.Clear();
            if (_includeChildren)
            {
                foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!AllProxies.Contains(sr)) _spriteSources.Add(sr);
                }
                foreach (var tr in GetComponentsInChildren<TilemapRenderer>(true))
                {
                    if (!AllProxies.Contains(tr)) _tilemapSources.Add(tr);
                }
                foreach (var ssr in GetComponentsInChildren<SpriteShapeRenderer>(true))
                {
                    if (!AllProxies.Contains(ssr)) _shapeSources.Add(ssr);
                }
            }
            else
            {
                if (TryGetComponent<SpriteRenderer>(out var sr) && !AllProxies.Contains(sr)) _spriteSources.Add(sr);
                if (TryGetComponent<TilemapRenderer>(out var tr) && !AllProxies.Contains(tr)) _tilemapSources.Add(tr);
                if (TryGetComponent<SpriteShapeRenderer>(out var ssr) && !AllProxies.Contains(ssr)) _shapeSources.Add(ssr);
            }

            _syncFrameOffset = Random.Range(0, 60);
            if (ActiveReflectables.Count == 0) Tilemap.tilemapTileChanged += OnTilemapTileChanged;
            ActiveReflectables.Add(this);
        }

        private void OnDisable()
        {
            ActiveReflectables.Remove(this);
            if (ActiveReflectables.Count == 0) Tilemap.tilemapTileChanged -= OnTilemapTileChanged;
            DestroyProxies();
        }

        public static void PrepareAll(int proxyLayer, bool viewedFromBelow, float surfaceY)
        {
            foreach (var reflectable in ActiveReflectables)
            {
                reflectable.Prepare(proxyLayer, viewedFromBelow, surfaceY);
            }
        }

        public static void CollectWaterContacts(float waterY, List<Bounds> results)
        {
            foreach (var reflectable in ActiveReflectables)
            {
                if (!reflectable._reflect) continue;
                foreach (var sr in reflectable._spriteSources)
                {
                    if (!sr || !sr.enabled || !sr.sprite || !sr.gameObject.activeInHierarchy) continue;
                    var bounds = sr.bounds;
                    if (bounds.min.y <= waterY && bounds.max.y > waterY) SpriteWaterlineSpans.Collect(sr, waterY, results);
                }
            }
        }

        public void SyncAllNow()
        {
            _pendingFullSync = true;
        }

        private void Prepare(int proxyLayer, bool viewedFromBelow, float surfaceY)
        {
            if (_proxyLayer != proxyLayer)
            {
                DestroyProxies();
                CreateProxies(proxyLayer);
            }

            var due = _pendingFullSync ? SyncGroups.All : DueSyncGroups();
            _pendingFullSync = false;
            var show = viewedFromBelow ? _visibleFromBelow : _reflect;

            for (var i = 0; i < _spriteSources.Count; i++)
            {
                var source = _spriteSources[i];
                var proxy = _spriteProxies[i];
                if (!proxy) continue;

                var visible = show && source && source.enabled && source.gameObject.activeInHierarchy
                              && (!viewedFromBelow || source.bounds.max.y > surfaceY);
                var groups = visible && !proxy.enabled ? SyncGroups.All : due;
                if (proxy.enabled != visible) proxy.enabled = visible;
                if (visible) SyncSprite(source, proxy, groups);
            }

            for (var i = 0; i < _tilemapSources.Count; i++)
            {
                var source = _tilemapSources[i];
                var proxy = _tilemapProxies[i];
                if (!proxy) continue;
                if (!source)
                {
                    DestroyProxy(proxy);
                    _tilemapProxies[i] = null;
                    continue;
                }

                var visible = show && source.enabled && source.gameObject.activeInHierarchy
                              && (!viewedFromBelow || source.bounds.max.y > surfaceY);
                var groups = visible && !proxy.enabled ? SyncGroups.All : due;
                if (proxy.enabled != visible) proxy.enabled = visible;
                if (visible) SyncTilemap(source, proxy, _tilemapSourceMaps[i], _tilemapProxyMaps[i], groups);
            }

            for (var i = 0; i < _shapeSources.Count; i++)
            {
                var source = _shapeSources[i];
                var proxy = _shapeProxies[i];
                if (!proxy) continue;

                var visible = show && source && source.enabled && source.gameObject.activeInHierarchy
                              && (!viewedFromBelow || source.bounds.max.y > surfaceY);
                var groups = visible && !proxy.enabled ? SyncGroups.All : due;
                if (proxy.enabled != visible) proxy.enabled = visible;
                if (visible) SyncSpriteShape(source, proxy, groups);
            }
        }

        private SyncGroups DueSyncGroups()
        {
            var frame = Time.frameCount + _syncFrameOffset;
            var groups = SyncGroups.None;
            if (_syncSorting && frame % Mathf.Max(_sortingSyncInterval, 1) == 0) groups |= SyncGroups.Sorting;
            if (_syncMaterial && frame % Mathf.Max(_materialSyncInterval, 1) == 0) groups |= SyncGroups.Material;
            if (_syncDrawMode && frame % Mathf.Max(_drawModeSyncInterval, 1) == 0) groups |= SyncGroups.DrawMode;
            return groups;
        }

        private void CreateProxies(int proxyLayer)
        {
            _proxyLayer = proxyLayer;
            _pendingFullSync = true;

            foreach (var source in _spriteSources)
            {
                if (!source)
                {
                    _spriteProxies.Add(null);
                    continue;
                }

                var go = CreateProxyObject(source.name, proxyLayer);
                go.transform.SetParent(source.transform, false);
                var proxy = go.AddComponent<SpriteRenderer>();
                AllProxies.Add(proxy);
                _spriteProxies.Add(proxy);
            }

            foreach (var source in _tilemapSources)
            {
                if (!source)
                {
                    _tilemapProxies.Add(null);
                    _tilemapSourceMaps.Add(null);
                    _tilemapProxyMaps.Add(null);
                    continue;
                }

                var go = CreateProxyObject(source.name, proxyLayer);
                go.transform.SetParent(source.transform.parent, false);
                var sourceTilemap = source.GetComponent<Tilemap>();
                var proxyTilemap = go.AddComponent<Tilemap>();
                var proxy = go.AddComponent<TilemapRenderer>();
                proxy.mode = source.mode;
                proxy.sortOrder = source.sortOrder;
                proxy.detectChunkCullingBounds = source.detectChunkCullingBounds;
                proxy.chunkCullingBounds = source.chunkCullingBounds;
                proxyTilemap.orientation = sourceTilemap.orientation;
                proxyTilemap.orientationMatrix = sourceTilemap.orientationMatrix;
                proxyTilemap.tileAnchor = sourceTilemap.tileAnchor;
                CopyAllTiles(sourceTilemap, proxyTilemap);

                TilemapProxies[sourceTilemap] = proxyTilemap;
                AllProxies.Add(proxy);
                _tilemapProxies.Add(proxy);
                _tilemapSourceMaps.Add(sourceTilemap);
                _tilemapProxyMaps.Add(proxyTilemap);
                SyncTilemap(source, proxy, sourceTilemap, proxyTilemap, SyncGroups.All);
            }

            foreach (var source in _shapeSources)
            {
                var proxy = source ? CreateSpriteShapeProxy(source, proxyLayer) : null;
                if (proxy) AllProxies.Add(proxy);
                _shapeProxies.Add(proxy);
            }
        }

        private static SpriteShapeRenderer CreateSpriteShapeProxy(SpriteShapeRenderer source, int proxyLayer)
        {
            var holder = new GameObject("WaterReflectable Clone Holder") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);
            var go = Instantiate(source.gameObject, holder.transform, false);

            for (var i = go.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(go.transform.GetChild(i).gameObject);
            }
            foreach (var component in go.GetComponents<MonoBehaviour>())
            {
#if SPOOKYWATER_SPRITESHAPE
                if (!(component is SpriteShapeController)) DestroyImmediate(component);
#else
                DestroyImmediate(component);
#endif
            }
            foreach (var component in go.GetComponents<Joint2D>()) DestroyImmediate(component);
            foreach (var component in go.GetComponents<Effector2D>()) DestroyImmediate(component);
            foreach (var component in go.GetComponents<Collider2D>()) DestroyImmediate(component);
            foreach (var component in go.GetComponents<Component>())
            {
#if SPOOKYWATER_SPRITESHAPE
                if (component is Transform || component is SpriteShapeRenderer || component is SpriteShapeController) continue;
#else
                if (component is Transform || component is SpriteShapeRenderer) continue;
#endif
                DestroyImmediate(component);
            }
#if SPOOKYWATER_SPRITESHAPE
            if (go.TryGetComponent<SpriteShapeController>(out var controller)) controller.autoUpdateCollider = false;
#endif

            go.name = $"{source.name} (Water Reflection Proxy)";
            go.layer = proxyLayer;
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(source.transform, false);
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            go.SetActive(true);
            Destroy(holder);

            return go.GetComponent<SpriteShapeRenderer>();
        }

        private static GameObject CreateProxyObject(string sourceName, int proxyLayer)
        {
            return new GameObject($"{sourceName} (Water Reflection Proxy)")
            {
                layer = proxyLayer,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void DestroyProxies()
        {
            foreach (var proxy in _spriteProxies) DestroyProxy(proxy);
            foreach (var proxy in _tilemapProxies) DestroyProxy(proxy);
            foreach (var proxy in _shapeProxies) DestroyProxy(proxy);
            _spriteProxies.Clear();
            _tilemapProxies.Clear();
            _shapeProxies.Clear();
            _tilemapSourceMaps.Clear();
            _tilemapProxyMaps.Clear();
            _proxyLayer = -1;
        }

        private static void DestroyProxy(Renderer proxy)
        {
            if (!proxy) return;
            AllProxies.Remove(proxy);
            if (proxy is TilemapRenderer && proxy.TryGetComponent<Tilemap>(out var proxyTilemap))
            {
                foreach (var pair in TilemapProxies)
                {
                    if (pair.Value != proxyTilemap) continue;
                    TilemapProxies.Remove(pair.Key);
                    break;
                }
            }
            Destroy(proxy.gameObject);
        }

        private static void SyncSprite(SpriteRenderer source, SpriteRenderer proxy, SyncGroups groups)
        {
            if (proxy.sprite != source.sprite) proxy.sprite = source.sprite;
            if (proxy.color != source.color) proxy.color = source.color;
            if (proxy.flipX != source.flipX) proxy.flipX = source.flipX;
            if (proxy.flipY != source.flipY) proxy.flipY = source.flipY;
            if (groups == SyncGroups.None) return;

            if ((groups & SyncGroups.DrawMode) != 0)
            {
                if (proxy.drawMode != source.drawMode) proxy.drawMode = source.drawMode;
                if (source.drawMode != SpriteDrawMode.Simple)
                {
                    if (proxy.size != source.size) proxy.size = source.size;
                    if (proxy.tileMode != source.tileMode) proxy.tileMode = source.tileMode;
                }
                if (proxy.maskInteraction != source.maskInteraction) proxy.maskInteraction = source.maskInteraction;
            }
            if ((groups & SyncGroups.Sorting) != 0)
            {
                if (proxy.spriteSortPoint != source.spriteSortPoint) proxy.spriteSortPoint = source.spriteSortPoint;
            }
            SyncRendererCommon(source, proxy, groups);
        }

        private static void SyncTilemap(TilemapRenderer source, TilemapRenderer proxy, Tilemap sourceTilemap, Tilemap proxyTilemap, SyncGroups groups)
        {
            var sourceTransform = source.transform;
            var proxyTransform = proxy.transform;
            if (proxyTransform.parent != sourceTransform.parent) proxyTransform.SetParent(sourceTransform.parent, false);
            sourceTransform.GetLocalPositionAndRotation(out var localPosition, out var localRotation);
            proxyTransform.GetLocalPositionAndRotation(out var proxyPosition, out var proxyRotation);
            if (proxyPosition != localPosition || proxyRotation != localRotation)
            {
                proxyTransform.SetLocalPositionAndRotation(localPosition, localRotation);
            }
            if (proxyTransform.localScale != sourceTransform.localScale) proxyTransform.localScale = sourceTransform.localScale;

            if (proxyTilemap.color != sourceTilemap.color) proxyTilemap.color = sourceTilemap.color;
            if (!Mathf.Approximately(proxyTilemap.animationFrameRate, sourceTilemap.animationFrameRate))
            {
                proxyTilemap.animationFrameRate = sourceTilemap.animationFrameRate;
            }
            if (groups == SyncGroups.None) return;

            if ((groups & SyncGroups.DrawMode) != 0)
            {
                if (proxy.maskInteraction != source.maskInteraction) proxy.maskInteraction = source.maskInteraction;
            }
            SyncRendererCommon(source, proxy, groups);
        }

        private static void SyncSpriteShape(SpriteShapeRenderer source, SpriteShapeRenderer proxy, SyncGroups groups)
        {
            if (proxy.color != source.color) proxy.color = source.color;
            if (groups == SyncGroups.None) return;

            if ((groups & SyncGroups.DrawMode) != 0)
            {
                if (proxy.maskInteraction != source.maskInteraction) proxy.maskInteraction = source.maskInteraction;
            }
            if ((groups & SyncGroups.Material) != 0)
            {
                var sourceMaterials = source.sharedMaterials;
                var proxyMaterials = proxy.sharedMaterials;
                var same = sourceMaterials.Length == proxyMaterials.Length;
                for (var i = 0; same && i < sourceMaterials.Length; i++) same = sourceMaterials[i] == proxyMaterials[i];
                if (!same) proxy.sharedMaterials = sourceMaterials;
            }
            SyncRendererCommon(source, proxy, groups & ~SyncGroups.Material);
        }

        private static void SyncRendererCommon(Renderer source, Renderer proxy, SyncGroups groups)
        {
            if ((groups & SyncGroups.Material) != 0)
            {
                if (proxy.sharedMaterial != source.sharedMaterial) proxy.sharedMaterial = source.sharedMaterial;
            }
            if ((groups & SyncGroups.Sorting) != 0)
            {
                if (proxy.sortingLayerID != source.sortingLayerID) proxy.sortingLayerID = source.sortingLayerID;
                if (proxy.sortingOrder != source.sortingOrder) proxy.sortingOrder = source.sortingOrder;
            }
        }

        private static void CopyAllTiles(Tilemap source, Tilemap proxy)
        {
            var bounds = source.cellBounds;
            proxy.SetTilesBlock(bounds, source.GetTilesBlock(bounds));

            foreach (var position in bounds.allPositionsWithin)
            {
                if (!source.HasTile(position)) continue;
                CopyCellOverrides(source, proxy, position);
            }
        }

        private static void CopyCellOverrides(Tilemap source, Tilemap proxy, Vector3Int position)
        {
            var color = source.GetColor(position);
            var matrix = source.GetTransformMatrix(position);
            if (proxy.GetColor(position) == color && proxy.GetTransformMatrix(position) == matrix) return;

            var flags = source.GetTileFlags(position);
            proxy.SetTileFlags(position, TileFlags.None);
            proxy.SetColor(position, color);
            proxy.SetTransformMatrix(position, matrix);
            proxy.SetTileFlags(position, flags);
        }

        private static void OnTilemapTileChanged(Tilemap tilemap, Tilemap.SyncTile[] tiles)
        {
            if (!TilemapProxies.TryGetValue(tilemap, out var proxy) || !proxy) return;

            foreach (var syncTile in tiles)
            {
                proxy.SetTile(syncTile.position, syncTile.tile);
                if (syncTile.tile) CopyCellOverrides(tilemap, proxy, syncTile.position);
            }
        }
    }
}
