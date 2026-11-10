using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    internal static class SpriteWaterlineSpans
    {
        private const int MaxCacheWidth = 512;
        private const int MaxCacheHeight = 256;
        private const byte OpaqueAlpha = 128;
        private const float TouchHeight = 0.15f;
        private const int MinGapTexels = 2;
        private const int MinRunTexels = 1;

        private sealed class AlphaGrid
        {
            public int Width;
            public int Height;
            public byte[] Alpha;
        }

        private static readonly Dictionary<Sprite, AlphaGrid> Cache = new();

        public static void Collect(SpriteRenderer sr, float waterY, List<Bounds> results)
        {
            var sprite = sr.sprite;
            var grid = GetAlphaGrid(sprite);
            if (grid == null) return;

            var t = sr.transform;
            var ppu = sprite.pixelsPerUnit;
            var tileSize = sprite.rect.size / ppu;
            var pivot01 = sprite.pivot / sprite.rect.size;
            var drawSize = sr.drawMode == SpriteDrawMode.Simple ? tileSize : sr.size;
            var drawMin = -pivot01 * drawSize;
            var tiled = sr.drawMode == SpriteDrawMode.Tiled;

            var localY = t.InverseTransformPoint(new Vector3(t.position.x, waterY, t.position.z)).y;
            var fy = (localY - drawMin.y) / drawSize.y;
            if (fy < 0f || fy >= 1f) return;

            var rowHeight = (tiled ? tileSize.y : drawSize.y) / grid.Height;
            var touchRows = Mathf.CeilToInt(TouchHeight / Mathf.Max(Mathf.Abs(t.lossyScale.y), 1e-4f) / rowHeight);
            var v0 = tiled ? Mathf.Repeat(localY - drawMin.y, tileSize.y) / tileSize.y : fy;
            if (sr.flipY) v0 = 1f - v0;
            var row0 = Mathf.Clamp(Mathf.FloorToInt(v0 * grid.Height), 0, grid.Height - 1);
            var rowStep = sr.flipY ? -1 : 1;

            var tileWidth = tiled ? tileSize.x : drawSize.x;
            var step = tileWidth / grid.Width;
            var steps = Mathf.CeilToInt(drawSize.x / step);
            var depth = sr.bounds.min.z;

            var runStart = -1;
            var lastOpaque = -1;
            for (var i = 0; i <= steps; i++)
            {
                var opaque = false;
                if (i < steps)
                {
                    var lx = (i + 0.5f) * step;
                    var u = tiled ? Mathf.Repeat(lx, tileSize.x) / tileSize.x : lx / drawSize.x;
                    if (sr.flipX) u = 1f - u;
                    var col = Mathf.Clamp((int)(u * grid.Width), 0, grid.Width - 1);
                    for (var r = 0; r <= touchRows && !opaque; r++)
                    {
                        var row = row0 + r * rowStep;
                        if (row < 0 || row >= grid.Height) break;
                        opaque = grid.Alpha[row * grid.Width + col] >= OpaqueAlpha;
                    }
                }

                if (opaque)
                {
                    if (runStart < 0) runStart = i;
                    lastOpaque = i;
                    continue;
                }
                if (runStart < 0 || (i < steps && i - lastOpaque <= MinGapTexels)) continue;

                if (lastOpaque - runStart + 1 >= MinRunTexels)
                {
                    var a = t.TransformPoint(new Vector3(drawMin.x + runStart * step, localY, 0f)).x;
                    var b = t.TransformPoint(new Vector3(drawMin.x + (lastOpaque + 1) * step, localY, 0f)).x;
                    var min = Mathf.Min(a, b);
                    var max = Mathf.Max(a, b);
                    results.Add(new Bounds(new Vector3((min + max) * 0.5f, waterY, depth),
                        new Vector3(max - min, 0.01f, 0.01f)));
                }
                runStart = -1;
            }
        }

        private static AlphaGrid GetAlphaGrid(Sprite sprite)
        {
            if (Cache.TryGetValue(sprite, out var grid)) return grid;

            var tex = sprite.texture;
            if (!tex)
            {
                Cache[sprite] = null;
                return null;
            }

            var rect = sprite.textureRect;
            var width = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, MaxCacheWidth);
            var height = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, MaxCacheHeight);
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt, new Vector2(rect.width / tex.width, rect.height / tex.height),
                new Vector2(rect.x / tex.width, rect.y / tex.height));

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = readback.GetPixels32();
            if (Application.isPlaying) Object.Destroy(readback);
            else Object.DestroyImmediate(readback);

            grid = new AlphaGrid { Width = width, Height = height, Alpha = new byte[pixels.Length] };
            for (var i = 0; i < pixels.Length; i++) grid.Alpha[i] = pixels[i].a;
            Cache[sprite] = grid;
            return grid;
        }
    }
}
