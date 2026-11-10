using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    [AddComponentMenu("SpookyWater/Water Surface Debris")]
    [HelpURL(SpookyWaterDocs.SurfaceDebris)]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterSurfaceDebris : MonoBehaviour
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly List<WaterSurfaceDebris> ActiveDebris = new();

        [Group("Look")]
        [Tooltip("Flat debris (lily pad, leaf, twig) lying on the water surface; it bobs and tilts with every ring that passes under it, and ripples shove it along the surface. Place it anywhere over Surface tiles and its height snaps to the water, in the editor too. This is what floats. Drawn lying flat on the water: the sprite's up (Y) runs into the screen " +
                 "(world Z), at the sprite's own size (pixels per unit), pivot at this object.")]
        [SerializeField] private Sprite _sprite;
        [Tooltip("A material using SpookyWater/Surface Debris (Materials/Water/SurfaceDebris.mat). Shared " +
                 "by every piece; the sprite's texture is set per piece.")]
        [SerializeField] private Material _material;
        [Tooltip("Color multiplied onto the sprite.")]
        [SerializeField] private Color _tint = Color.white;
        [Tooltip("Grid cells along each side. More = follows small ripples more closely; " +
                 "the surface shouldn't poke through between them.")]
        [SerializeField, Range(1, 16)] private int _segments = 6;
        [Tooltip("How far above the water (world units) it rests - just enough that the surface " +
                 "doesn't flicker through it.")]
        [SerializeField, Min(0f)] private float _lift = 0.01f;
        [SortingLayerDropdown]
        [Tooltip("Sorting layer it is drawn on. Use the surface sorting layer from the SpookyWater settings so it draws with the water.")]
        [SerializeField] private int _sortingLayer;
        [Tooltip("Order inside the sorting layer. It must be above the surface tiles, which use 0.")]
        [SerializeField] private int _sortingOrder = 10;

        [Group("Drift")]
        [Tooltip("How far ripples shove it. Light leaves/twigs ~1.5-2, heavy pads ~0.5. 0 = only bobs.")]
        [SerializeField, Min(0f)] private float _pushResponse = 1f;
        [Tooltip("How quickly it glides to a stop, per second.")]
        [SerializeField, Min(0f)] private float _waterDrag = 1.5f;
        [Tooltip("Fastest it can drift, world units/sec.")]
        [SerializeField, Min(0.01f)] private float _maxSpeed = 1.5f;
        [Tooltip("Degrees/sec of spin a ripple's kick adds (a unit kick).")]
        [SerializeField, Min(0f)] private float _spin = 120f;
        [Tooltip("How quickly spinning stops, per second.")]
        [SerializeField, Min(0f)] private float _spinDrag = 2f;
        [Tooltip("Size it bumps into shores, obstacles and other debris with (world units). " +
                 "0 = from the sprite (radius of its smaller side).")]
        [SerializeField, Min(0f)] private float _collisionRadius;
        [Tooltip("How much speed it keeps bouncing off a shore or another piece.")]
        [SerializeField, Range(0f, 1f)] private float _bounciness = 0.3f;

        [Group("Anchor")]
        [Tooltip("Pulls it back toward where it was placed, like a lily pad's stem. 0 = drifts freely.")]
        [SerializeField, Min(0f)] private float _anchorStrength;
        [Tooltip("How far (world units) it can drift before the anchor starts pulling.")]
        [SerializeField, Min(0f)] private float _anchorSlack = 0.25f;

        private MaterialPropertyBlock _propertyBlock;
        private Mesh _mesh;
        private bool _meshDirty = true;
        private Vector2 _velocity;
        private float _angularVelocity;
        private Vector2 _home;
        private float _lastTime;
        private float _radius;

        public Vector2 Velocity => _velocity;

        public void AddVelocity(Vector2 worldXZ) => _velocity += worldXZ;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveDebris.Clear();
        }

        private void Reset()
        {
            _sortingLayer = SpookyWaterSettings.Instance.SurfaceSortingLayerId;
            _sortingOrder = SpookyWaterSettings.Instance.debrisSortingOrder;
#if UNITY_EDITOR
            _material = WaterEditorDefaults.Load<Material>(WaterEditorDefaults.SurfaceDebrisMaterial);
            _sprite = WaterEditorDefaults.Load<Sprite>(WaterEditorDefaults.LilyPadASprite);
#endif
        }

        private void OnValidate() => _meshDirty = true;

        private void OnEnable()
        {
            ActiveDebris.Add(this);
            _home = new Vector2(transform.position.x, transform.position.z);
            _lastTime = Time.time;
            _velocity = Vector2.zero;
            _angularVelocity = 0f;
            _meshDirty = true;
            RebuildIfDirty();
        }

        private void OnDisable()
        {
            ActiveDebris.Remove(this);
            if (!_mesh) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
            _mesh = null;
        }

        private void Update()
        {
            RebuildIfDirty();

            if (!Application.isPlaying)
            {
                if (transform.hasChanged)
                {
                    SnapToWater();
                    transform.hasChanged = false;
                }
                return;
            }

            var now = Time.time;
            var dt = Time.deltaTime;
            var from = _lastTime;
            _lastTime = now;
            var field = Water.Field;
            if (!field || dt <= 0f) return;

            var position = new Vector2(transform.position.x, transform.position.z);
            var kick = field.AccumulateRippleKicks(position, from, now) * _pushResponse;
            if (kick.sqrMagnitude > 0f)
            {
                _velocity += kick;
                var facing = new Vector2(transform.right.x, transform.right.z);
                var handedness = Mathf.Sign(facing.x * kick.y - facing.y * kick.x);
                _angularVelocity += _spin * kick.magnitude * handedness;
            }

            if (_anchorStrength > 0f)
            {
                var fromHome = position - _home;
                var stretch = fromHome.magnitude - _anchorSlack;
                if (stretch > 0f) _velocity -= fromHome.normalized * (stretch * _anchorStrength * dt);
            }

            _velocity *= Mathf.Exp(-_waterDrag * dt);
            _velocity = Vector2.ClampMagnitude(_velocity, _maxSpeed);
            _angularVelocity *= Mathf.Exp(-_spinDrag * dt);

            var next = position + _velocity * dt;
            SeparateFromOthers(ref next);
            Confine(field, position, ref next);

            transform.position = new Vector3(next.x, transform.position.y, next.y);
            transform.Rotate(0f, _angularVelocity * dt, 0f, Space.World);
            SnapToWater();
        }

        private void SeparateFromOthers(ref Vector2 next)
        {
            for (var i = 0; i < ActiveDebris.Count; i++)
            {
                var other = ActiveDebris[i];
                if (other == this) continue;

                var otherPos = new Vector2(other.transform.position.x, other.transform.position.z);
                var offset = next - otherPos;
                var minDistance = _radius + other._radius;
                var distanceSq = offset.sqrMagnitude;
                if (distanceSq >= minDistance * minDistance) continue;

                var distance = Mathf.Sqrt(distanceSq);
                var normal = distance > 1e-4f ? offset / distance : Vector2.right;
                next += normal * ((minDistance - distance) * 0.5f);

                var closing = Vector2.Dot(_velocity - other._velocity, -normal);
                if (closing <= 0f) continue;
                var exchange = normal * (closing * 0.5f * (1f + _bounciness));
                _velocity += exchange;
                other._velocity -= exchange;
            }
        }

        private void Confine(WaterField field, Vector2 previous, ref Vector2 next)
        {
            var rect = field.FieldRect;
            var minZ = rect.yMin + _radius;
            var maxZ = rect.yMax - _radius;
            if (next.y < minZ || next.y > maxZ)
            {
                next.y = Mathf.Clamp(next.y, minZ, Mathf.Max(minZ, maxZ));
                _velocity.y = -_velocity.y * _bounciness;
            }

            var shore = field.SampleShoreDistance(next);
            if (shore < _radius)
            {
                const float step = 0.05f;
                var gradient = new Vector2(
                    field.SampleShoreDistance(next + new Vector2(step, 0f)) - field.SampleShoreDistance(next - new Vector2(step, 0f)),
                    field.SampleShoreDistance(next + new Vector2(0f, step)) - field.SampleShoreDistance(next - new Vector2(0f, step)));
                if (gradient.sqrMagnitude > 1e-10f)
                {
                    var normal = gradient.normalized;
                    next += normal * (_radius - shore);
                    var into = Vector2.Dot(_velocity, normal);
                    if (into < 0f) _velocity -= normal * (into * (1f + _bounciness));
                }
            }

            if (TouchesObstacle(field, next))
            {
                next = previous;
                _velocity = -_velocity * _bounciness;
            }
        }

        private bool TouchesObstacle(WaterField field, Vector2 centre)
        {
            if (field.IsInsideObstacle(centre)) return true;
            var r = _radius * 0.7f;
            return field.IsInsideObstacle(centre + new Vector2(r, 0f)) || field.IsInsideObstacle(centre - new Vector2(r, 0f))
                || field.IsInsideObstacle(centre + new Vector2(0f, r)) || field.IsInsideObstacle(centre - new Vector2(0f, r));
        }

        private void SnapToWater()
        {
            var level = Water.Level;
            if (!level || !level.FrontTilemap) return;

            var position = transform.position;
            var probe = new Vector2(position.x, position.y - _lift - level.FrontTilemap.cellSize.y * 0.5f);
            if (!Water.TryGetSurfaceY(probe, out var surfaceY)) return;

            var y = surfaceY + _lift;
            if (Mathf.Abs(position.y - y) > 1e-4f) transform.position = new Vector3(position.x, y, position.z);
        }

        private void RebuildIfDirty()
        {
            if (!_meshDirty) return;
            _meshDirty = false;

            var meshFilter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            if (!_mesh)
            {
                _mesh = new Mesh { name = "WaterSurfaceDebris", hideFlags = HideFlags.DontSave };
            }

            var bounds = _sprite ? new Rect(_sprite.bounds.min, _sprite.bounds.size) : new Rect(-0.5f, -0.5f, 1f, 1f);
            var uvMin = Vector2.zero;
            var uvMax = Vector2.one;
            if (_sprite)
            {
                uvMin = Vector2.positiveInfinity;
                uvMax = Vector2.negativeInfinity;
                foreach (var uv in _sprite.uv)
                {
                    uvMin = Vector2.Min(uvMin, uv);
                    uvMax = Vector2.Max(uvMax, uv);
                }
            }

            var n = Mathf.Max(_segments, 1);
            var vertices = new Vector3[(n + 1) * (n + 1)];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            for (var z = 0; z <= n; z++)
            {
                for (var x = 0; x <= n; x++)
                {
                    var t = new Vector2((float)x / n, (float)z / n);
                    var i = z * (n + 1) + x;
                    vertices[i] = new Vector3(bounds.xMin + t.x * bounds.width, 0f, bounds.yMin + t.y * bounds.height);
                    uvs[i] = uvMin + Vector2.Scale(uvMax - uvMin, t);
                    colors[i] = _tint;
                }
            }

            var triangles = new int[n * n * 6];
            for (int z = 0, k = 0; z < n; z++)
            {
                for (var x = 0; x < n; x++, k += 6)
                {
                    var i = z * (n + 1) + x;
                    triangles[k] = i;
                    triangles[k + 1] = i + n + 1;
                    triangles[k + 2] = i + 1;
                    triangles[k + 3] = i + 1;
                    triangles[k + 4] = i + n + 1;
                    triangles[k + 5] = i + n + 2;
                }
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.uv = uvs;
            _mesh.colors = colors;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            _mesh.bounds = new Bounds(new Vector3(bounds.center.x, 0f, bounds.center.y), new Vector3(bounds.width, 2f, bounds.height));
            meshFilter.sharedMesh = _mesh;

            if (_material) meshRenderer.sharedMaterial = _material;
            meshRenderer.sortingLayerID = _sortingLayer;
            meshRenderer.sortingOrder = _sortingOrder;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            _propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(_propertyBlock);
            if (_sprite) _propertyBlock.SetTexture(MainTexId, _sprite.texture);
            meshRenderer.SetPropertyBlock(_propertyBlock);

            var scale = transform.lossyScale;
            _radius = _collisionRadius > 0f
                ? _collisionRadius
                : 0.5f * Mathf.Min(bounds.width * Mathf.Abs(scale.x), bounds.height * Mathf.Abs(scale.z));
        }

    }
}
