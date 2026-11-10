using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    public static partial class Water
    {
        private const float SearchInterval = 0.5f;

        private static readonly List<WaterTilemapBuilder> Candidates = new();
        private static readonly List<WaterPocket> PocketList = new();
        private static WaterTilemapBuilder _level;
        private static float _nextSearchTime;

        public static WaterTilemapBuilder Level
        {
            get
            {
                if (_level) return _level;
                return FindLevel();
            }
        }

        public static WaterField Field
        {
            get
            {
                var level = Level;
                return level ? level.Field : null;
            }
        }

        public static WaterReflectionRenderer Reflection
        {
            get
            {
                var level = Level;
                return level ? level.Reflection : null;
            }
        }

        public static bool HasLevel => Level;

        public static IReadOnlyList<WaterPocket> Pockets => PocketList;

        internal static void RegisterPocket(WaterPocket pocket)
        {
            if (!PocketList.Contains(pocket)) PocketList.Add(pocket);
        }

        internal static void UnregisterPocket(WaterPocket pocket) => PocketList.Remove(pocket);

        public static WaterField FieldAt(Vector3 worldPos)
        {
            var main = Field;
            var best = main;
            var bestDistance = main ? Mathf.Abs(worldPos.y - main.SurfaceHeight) : float.PositiveInfinity;
            for (var i = 0; i < PocketList.Count; i++)
            {
                var pocket = PocketList[i];
                if (!pocket || !pocket.CoversColumn(worldPos.x)) continue;

                var distance = Mathf.Abs(worldPos.y - pocket.WaterLevelY);
                if (distance >= bestDistance) continue;
                best = pocket.Field;
                bestDistance = distance;
            }
            return best;
        }

        internal static bool Register(WaterTilemapBuilder builder)
        {
            if (!Candidates.Contains(builder)) Candidates.Add(builder);
            if (!Application.isPlaying)
            {
                _level = builder;
                return true;
            }

            if (_level && _level != builder) return false;
            _level = builder;
            return true;
        }

        internal static void Unregister(WaterTilemapBuilder builder)
        {
            Candidates.Remove(builder);
            if (_level != builder) return;

            _level = null;
            for (var i = 0; i < Candidates.Count; i++)
            {
                var next = Candidates[i];
                if (!next || !next.isActiveAndEnabled) continue;
                _level = next;
                next.OnPromoted();
                return;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _level = null;
            _nextSearchTime = 0f;
            Candidates.Clear();
            PocketList.Clear();
        }

        private static WaterTilemapBuilder FindLevel()
        {
            var now = Time.realtimeSinceStartup;
            if (now < _nextSearchTime) return null;
            _nextSearchTime = now + SearchInterval;

            var found = Object.FindAnyObjectByType<WaterTilemapBuilder>();
            if (!found || !found.isActiveAndEnabled) return null;

            _level = found;
            if (!Candidates.Contains(found)) Candidates.Add(found);
            return _level;
        }

        public static bool TryGetSurfaceY(Vector2 point, out float surfaceY)
        {
            surfaceY = 0f;
            for (var i = 0; i < PocketList.Count; i++)
            {
                if (PocketList[i] && PocketList[i].ContainsAir(point)) return false;
            }

            if (!TryGetTileSurfaceY(point, out surfaceY)) return false;

            for (var i = 0; i < PocketList.Count; i++)
            {
                var pocket = PocketList[i];
                if (!pocket || !pocket.CoversColumn(point.x)) continue;

                var level = pocket.WaterLevelY;
                if (level > point.y && level < surfaceY) surfaceY = level;
            }
            return true;
        }

        public static bool TryGetTileSurfaceY(Vector2 point, out float surfaceY)
        {
            surfaceY = 0f;
            var level = Level;
            if (!level) return false;

            var tilemap = level.FrontTilemap;
            if (!tilemap) return false;

            var cell = tilemap.WorldToCell(new Vector3(point.x, point.y, tilemap.transform.position.z));
            if (!tilemap.HasTile(cell)) return false;

            while (tilemap.HasTile(cell + Vector3Int.up)) cell += Vector3Int.up;
            surfaceY = tilemap.CellToWorld(cell + Vector3Int.up).y;
            return true;
        }

        public static bool IsUnderwater(Vector2 point)
        {
            return TryGetSurfaceY(point, out var surfaceY) && point.y < surfaceY;
        }

        public static bool TryGetWaterlineY(out float y)
        {
            var reflection = Reflection;
            y = reflection ? reflection.transform.position.y : 0f;
            return reflection;
        }

        public static void Ripple(Vector3 worldPos, float strength, bool pushUp = true, float radiusScale = 1f)
        {
            var field = FieldAt(worldPos);
            if (field) field.CreateContactRippleAt(worldPos, strength, pushUp, radiusScale);
        }

        public static void RippleAlong(Vector3 from, Vector3 to, float strength, bool pushUp = true, float radiusScale = 1f)
        {
            var field = FieldAt(from);
            if (field) field.CreateContactRippleAlong(from, to, strength, pushUp, radiusScale);
        }

        public static void Wake(Vector3 from, Vector3 to, float strength = 1f, float bulge = 1f)
        {
            var field = FieldAt(from);
            if (field) field.CreateWakeSegment(from, to, strength, bulge);
        }

        public static void Splash(Vector3 pos, float strength, Vector2 velocity)
        {
            var effects = WaterSplashEffects.Instance;
            if (effects && TryResolveSurfaceY(pos, out var surfaceY)) effects.Splash(pos, surfaceY, strength, velocity);
        }

        public static void Spray(Vector3 pos, float amount, Vector2 velocity)
        {
            var effects = WaterSplashEffects.Instance;
            if (effects && TryResolveSurfaceY(pos, out var surfaceY)) effects.Spray(pos, surfaceY, amount, velocity);
        }

        public static void Bubbles(Vector3 pos, int count, float spread, float driftX = 0f, float sizeScale = 1f)
        {
            var effects = WaterSplashEffects.Instance;
            if (effects) effects.Bubbles(pos, count, spread, driftX, sizeScale);
        }

        public static void BubbleTrail(Vector3 from, Vector3 to, int count, float spread, float driftX = 0f, float sizeScale = 1f)
        {
            var effects = WaterSplashEffects.Instance;
            if (effects) effects.BubbleTrail(from, to, count, spread, driftX, sizeScale);
        }

        private static bool TryResolveSurfaceY(Vector3 pos, out float surfaceY)
        {
            return TryGetSurfaceY(pos, out surfaceY) || TryGetWaterlineY(out surfaceY);
        }
    }
}
