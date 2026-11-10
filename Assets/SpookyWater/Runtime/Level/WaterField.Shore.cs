using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    public partial class WaterField
    {
        public float SampleShoreDistance(Vector2 worldXZ)
        {
            if (_shoreDistancePixels == null || !_staticMask) return MaxShoreDistance;

            var width = _staticMask.width;
            var height = _staticMask.height;
            var u = (worldXZ.x - _staticMaskOrigin.x) / _staticMaskSize.x * width - 0.5f;
            var v = (worldXZ.y - _staticMaskOrigin.y) / _staticMaskSize.y * height - 0.5f;
            var x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, width - 1);
            var y0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, height - 1);
            var x1 = Mathf.Min(x0 + 1, width - 1);
            var y1 = Mathf.Min(y0 + 1, height - 1);
            var fx = Mathf.Clamp01(u - x0);
            var fy = Mathf.Clamp01(v - y0);
            var bottom = Mathf.Lerp(_shoreDistancePixels[y0 * width + x0], _shoreDistancePixels[y0 * width + x1], fx);
            var top = Mathf.Lerp(_shoreDistancePixels[y1 * width + x0], _shoreDistancePixels[y1 * width + x1], fx);
            return Mathf.Lerp(bottom, top, fy);
        }

        public void SetWaterMask(Func<Vector2, (bool isWater, float openEdge)> sample)
        {
            if (!_simMaterial || !_mask) return;

            var (width, height) = (_staticMask.width, _staticMask.height);
            DestroyImmediate(_staticMask);
            _staticMask = BakeStaticMask(width, height, sample);
            BakeShoreDistance();
            BindMaterialState();
            Graphics.Blit(null, _mask, _simMaterial, PassBuildMask);
        }

        private Texture2D BakeStaticMask(int width, int height, Func<Vector2, (bool isWater, float openEdge)> sample)
        {
            var texture = new Texture2D(width, height, TextureFormat.RG16, false, true)
            {
                name = "WaterField_RippleStaticMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new byte[width * height * 2];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var world = _staticMaskOrigin + new Vector2((x + 0.5f) / width * _staticMaskSize.x, (y + 0.5f) / height * _staticMaskSize.y);
                    var (isWater, openEdge) = sample?.Invoke(world) ?? (true, 0f);
                    var t = Mathf.Clamp01(openEdge);
                    var i = (y * width + x) * 2;
                    pixels[i] = isWater ? (byte)255 : (byte)0;
                    pixels[i + 1] = isWater ? (byte)Mathf.RoundToInt(t * t * 255f) : (byte)0;
                }
            }

            _staticMaskPixels = pixels;
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, true);
            return texture;
        }

        private void BakeShoreDistance()
        {
            if (_shoreDistance) DestroyImmediate(_shoreDistance);

            var width = _staticMask.width;
            var height = _staticMask.height;
            var texel = new Vector2(_staticMaskSize.x / width, _staticMaskSize.y / height);

            var toLand = new float[width * height];
            var toWater = new float[width * height];
            for (var i = 0; i < toLand.Length; i++)
            {
                var isWater = _staticMaskPixels[i * 2] >= 128;
                toLand[i] = isWater ? Unreachable : 0f;
                toWater[i] = isWater ? 0f : Unreachable;
            }
            SquaredDistanceTransform(toLand, width, height, texel);
            SquaredDistanceTransform(toWater, width, height, texel);

            var halfTexel = 0.25f * (texel.x + texel.y);
            var pixels = new ushort[width * height];
            _shoreDistancePixels = new float[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                var distance = toLand[i] > 0f
                    ? Mathf.Sqrt(toLand[i]) - halfTexel
                    : halfTexel - Mathf.Sqrt(toWater[i]);
                _shoreDistancePixels[i] = Mathf.Clamp(distance, -MaxShoreDistance, MaxShoreDistance);
                pixels[i] = Mathf.FloatToHalf(_shoreDistancePixels[i]);
            }

            _shoreDistance = new Texture2D(width, height, TextureFormat.RHalf, false, true)
            {
                name = "WaterField_ShoreDistance",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _shoreDistance.SetPixelData(pixels, 0);
            _shoreDistance.Apply(false, true);
            PublishShoreDistance();
        }

        private void PublishShoreDistance()
        {
            if (!_shoreDistance) return;
            PublishTexture(WaterShoreDistanceTexId, _shoreDistance);
            PublishVector(WaterShoreDistanceRectId,
                new Vector4(_staticMaskOrigin.x, _staticMaskOrigin.y, _staticMaskSize.x, _staticMaskSize.y));
        }

        private static void SquaredDistanceTransform(float[] grid, int width, int height, Vector2 texel)
        {
            var n = Mathf.Max(width, height);
            var f = new float[n];
            var d = new float[n];
            var v = new int[n];
            var z = new float[n + 1];

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++) f[y] = grid[y * width + x];
                DistanceTransform1D(f, height, texel.y, d, v, z);
                for (var y = 0; y < height; y++) grid[y * width + x] = d[y];
            }

            for (var y = 0; y < height; y++)
            {
                Array.Copy(grid, y * width, f, 0, width);
                DistanceTransform1D(f, width, texel.x, d, v, z);
                Array.Copy(d, 0, grid, y * width, width);
            }
        }

        private static void DistanceTransform1D(float[] f, int n, float spacing, float[] d, int[] v, float[] z)
        {
            var k = -1;
            for (var q = 0; q < n; q++)
            {
                if (f[q] >= Unreachable) continue;

                var s = float.NegativeInfinity;
                while (k >= 0)
                {
                    s = ParabolaIntersection(f, v[k], q, spacing);
                    if (s > z[k]) break;
                    k--;
                }
                if (k < 0) s = float.NegativeInfinity;

                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = float.PositiveInfinity;
            }

            if (k < 0)
            {
                for (var q = 0; q < n; q++) d[q] = Unreachable;
                return;
            }

            var j = 0;
            for (var q = 0; q < n; q++)
            {
                var x = q * spacing;
                while (z[j + 1] < x) j++;
                var dx = x - v[j] * spacing;
                d[q] = dx * dx + f[v[j]];
            }
        }

        private static float ParabolaIntersection(float[] f, int p, int q, float spacing)
        {
            var xp = p * spacing;
            var xq = q * spacing;
            return (f[q] + xq * xq - (f[p] + xp * xp)) / (2f * (xq - xp));
        }
    }
}
