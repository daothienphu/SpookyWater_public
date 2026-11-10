using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpookyWater.Editor
{
    public sealed class SpriteNormalSource
    {
        public int Width;
        public int Height;
        public Color[] Pixels;
        public RectInt[] Rects;

        public SpriteNormalSource Downsampled(int factor)
        {
            if (factor <= 1) return this;
            var w = Mathf.Max(1, Width / factor);
            var h = Mathf.Max(1, Height / factor);
            var pixels = new Color[w * h];
            var weight = 1f / (factor * factor);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var sum = new Color(0f, 0f, 0f, 0f);
                for (var j = 0; j < factor; j++)
                for (var i = 0; i < factor; i++)
                    sum += Pixels[Mathf.Min(y * factor + j, Height - 1) * Width + Mathf.Min(x * factor + i, Width - 1)];
                pixels[y * w + x] = sum * weight;
            }

            var rects = new RectInt[Rects.Length];
            for (var r = 0; r < Rects.Length; r++)
            {
                var x0 = Rects[r].xMin / factor;
                var y0 = Rects[r].yMin / factor;
                var x1 = Mathf.Min(w, Mathf.CeilToInt(Rects[r].xMax / (float)factor));
                var y1 = Mathf.Min(h, Mathf.CeilToInt(Rects[r].yMax / (float)factor));
                rects[r] = new RectInt(x0, y0, Mathf.Max(1, x1 - x0), Mathf.Max(1, y1 - y0));
            }

            return new SpriteNormalSource { Width = w, Height = h, Pixels = pixels, Rects = rects };
        }
    }

    [Serializable]
    public struct SpriteNormalBakeSettings
    {
        public float AlphaThreshold;
        public SpriteNormalBevelShape BevelShape;
        public float BevelWidth;
        public float BevelHeight;
        public float ShapeSmoothing;
        public bool BevelTextureBorders;
        public float InnerEdges;
        public float InnerEdgeThreshold;
        public float InnerEdgeWidth;
        public float DetailBumps;
        public float DetailWidth;
        public float DetailSoftness;
        public bool InvertDetail;
        public float NormalStrength;
        public bool FlipX;
        public bool FlipY;
        public int Padding;

        public static SpriteNormalBakeSettings From(SpriteNormalBakeProfile p) => new()
        {
            AlphaThreshold = p.AlphaThreshold,
            BevelShape = p.BevelShape,
            BevelWidth = p.BevelWidth,
            BevelHeight = p.BevelHeight,
            ShapeSmoothing = p.ShapeSmoothing,
            BevelTextureBorders = p.BevelTextureBorders,
            InnerEdges = p.InnerEdges,
            InnerEdgeThreshold = p.InnerEdgeThreshold,
            InnerEdgeWidth = p.InnerEdgeWidth,
            DetailBumps = p.DetailBumps,
            DetailWidth = p.DetailWidth,
            DetailSoftness = p.DetailSoftness,
            InvertDetail = p.InvertDetail,
            NormalStrength = p.NormalStrength,
            FlipX = p.FlipX,
            FlipY = p.FlipY,
            Padding = p.Padding
        };

        public SpriteNormalBakeSettings ScaledDown(int factor)
        {
            if (factor <= 1) return this;
            var s = this;
            var k = 1f / factor;
            s.BevelWidth = Mathf.Max(0.5f, BevelWidth * k);
            s.ShapeSmoothing *= k;
            s.InnerEdgeWidth = Mathf.Max(0.5f, InnerEdgeWidth * k);
            s.DetailWidth = Mathf.Max(0.5f, DetailWidth * k);
            s.DetailSoftness *= k;
            s.Padding = Mathf.CeilToInt(Padding * k);
            return s;
        }
    }

    public readonly struct SpriteNormalBakeJob
    {
        public readonly Texture2D Texture;
        public readonly SpriteNormalBakeProfile Profile;

        public SpriteNormalBakeJob(Texture2D texture, SpriteNormalBakeProfile profile)
        {
            Texture = texture;
            Profile = profile;
        }
    }

    public static class SpriteNormalBaker
    {
        public const string SecondaryTextureName = "_NormalMap";

        private const double Far = 1e20;

        public static string OutputPath(Texture2D texture, string suffix)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return null;
            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            return Path.Combine(directory, Path.GetFileNameWithoutExtension(path) + suffix + ".png").Replace('\\', '/');
        }

        public static bool TryLoadSource(Texture2D texture, out SpriteNormalSource source, out string error)
        {
            source = null;
            error = null;
            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                error = $"{(texture ? texture.name : "Texture")} is not an asset.";
                return false;
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter { textureType: TextureImporterType.NormalMap })
            {
                error = $"{path} is a normal map, not a sprite.";
                return false;
            }

            Texture2D readable = null;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".png" or ".jpg" or ".jpeg")
            {
                readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!readable.LoadImage(File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.DestroyImmediate(readable);
                    readable = null;
                }
            }

            if (!readable) readable = ReadBack(texture);

            source = new SpriteNormalSource
            {
                Width = readable.width,
                Height = readable.height,
                Pixels = readable.GetPixels()
            };
            UnityEngine.Object.DestroyImmediate(readable);
            source.Rects = SpriteRects(path, texture, source.Width, source.Height);
            return true;
        }

        public static Vector3[] Bake(SpriteNormalSource source, in SpriteNormalBakeSettings settings)
        {
            var normals = new Vector3[source.Width * source.Height];
            for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.forward;
            foreach (var rect in source.Rects) BakeRect(source, rect, settings, normals);
            return normals;
        }

        public static Color32[] Encode(Vector3[] normals)
        {
            var pixels = new Color32[normals.Length];
            for (var i = 0; i < normals.Length; i++)
            {
                var n = normals[i];
                pixels[i] = new Color32(ToByte(n.x), ToByte(n.y), ToByte(n.z), 255);
            }

            return pixels;
        }

        public static int BakeAll(SpriteNormalBakeProfile profile)
        {
            var jobs = new List<SpriteNormalBakeJob>();
            var seen = new HashSet<Texture2D>();
            foreach (var texture in profile.Textures)
                if (texture && seen.Add(texture)) jobs.Add(new SpriteNormalBakeJob(texture, profile));
            return Bake(jobs);
        }

        public static int Bake(IReadOnlyList<SpriteNormalBakeJob> jobs)
        {
            var written = new List<(SpriteNormalBakeJob job, string sourcePath, string outputPath, bool existed)>();
            var assigned = 0;
            try
            {
                for (var i = 0; i < jobs.Count; i++)
                {
                    var job = jobs[i];
                    if (!job.Texture || !job.Profile) continue;
                    if (EditorUtility.DisplayCancelableProgressBar("Baking sprite normals", job.Texture.name, 0.8f * i / jobs.Count)) break;
                    if (TryWrite(job, out var sourcePath, out var outputPath, out var existed, out var error))
                        written.Add((job, sourcePath, outputPath, existed));
                    else
                        Debug.LogError("SpookyWater: " + error, job.Texture);
                }

                if (written.Count == 0) return 0;

                EditorUtility.DisplayProgressBar("Baking sprite normals", "Importing normal maps", 0.85f);
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var w in written) AssetDatabase.ImportAsset(w.outputPath, ImportAssetOptions.ForceUpdate);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                EditorUtility.DisplayProgressBar("Baking sprite normals", "Assigning normal maps", 0.95f);
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var w in written)
                    {
                        var sourceImporter = AssetImporter.GetAtPath(w.sourcePath) as TextureImporter;
                        if (AssetImporter.GetAtPath(w.outputPath) is TextureImporter normalImporter)
                        {
                            var configure = !w.existed || normalImporter.textureType != TextureImporterType.NormalMap;
                            if (configure) ConfigureNormalImporter(normalImporter, sourceImporter);
                            var hash = SettingsHash(w.job.Profile);
                            var rehash = normalImporter.userData != hash;
                            if (rehash) normalImporter.userData = hash;
                            if (configure || rehash) normalImporter.SaveAndReimport();
                        }

                        if (w.job.Profile.AssignToSprite && sourceImporter && sourceImporter.textureType == TextureImporterType.Sprite &&
                            AssignSecondaryTexture(sourceImporter, AssetDatabase.LoadAssetAtPath<Texture2D>(w.outputPath)))
                            assigned++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"SpookyWater: baked {written.Count} sprite normal map(s); {assigned} assigned as the sprite's {SecondaryTextureName}.");
            return written.Count;
        }

        private static bool TryWrite(SpriteNormalBakeJob job, out string sourcePath, out string outputPath, out bool existed, out string error)
        {
            existed = false;
            sourcePath = AssetDatabase.GetAssetPath(job.Texture);
            outputPath = OutputPath(job.Texture, job.Profile.Suffix);
            if (string.IsNullOrEmpty(job.Profile.Suffix))
            {
                error = $"{job.Profile.name}'s Suffix is empty; the normal map would overwrite {sourcePath}.";
                return false;
            }

            if (outputPath == null || string.Equals(outputPath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                error = $"can't bake {sourcePath}: the output path would overwrite it.";
                return false;
            }

            if (!IsWritable(sourcePath))
            {
                error = $"can't bake {sourcePath}: it is in a read-only package.";
                return false;
            }

            if (!TryLoadSource(job.Texture, out var source, out error)) return false;

            var normals = Bake(source, SpriteNormalBakeSettings.From(job.Profile));
            var output = new Texture2D(source.Width, source.Height, TextureFormat.RGBA32, false, true);
            output.SetPixels32(Encode(normals));
            output.Apply();
            var bytes = output.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(output);

            existed = File.Exists(outputPath);
            File.WriteAllBytes(outputPath, bytes);
            return true;
        }

        public static string SettingsHash(SpriteNormalBakeProfile profile) =>
            Hash128.Compute(JsonUtility.ToJson(SpriteNormalBakeSettings.From(profile))).ToString();

        public static bool IsUpToDate(Texture2D texture, Texture2D normalMap, SpriteNormalBakeProfile profile)
        {
            if (!normalMap || !profile) return false;
            var sourcePath = AssetDatabase.GetAssetPath(texture);
            var outputPath = AssetDatabase.GetAssetPath(normalMap);
            if (AssetImporter.GetAtPath(outputPath) is not TextureImporter importer || importer.userData != SettingsHash(profile)) return false;
            return File.GetLastWriteTimeUtc(sourcePath) <= File.GetLastWriteTimeUtc(outputPath);
        }

        public static bool IsWritable(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal)) return true;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            return info != null && info.source is UnityEditor.PackageManager.PackageSource.Embedded or UnityEditor.PackageManager.PackageSource.Local;
        }

        public static Texture2D SecondaryNormalMap(Texture2D texture)
        {
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) is not TextureImporter importer) return null;
            var current = importer.secondarySpriteTextures;
            if (current == null) return null;
            foreach (var entry in current)
                if (entry.name == SecondaryTextureName) return entry.texture;
            return null;
        }

        public static bool HasSecondaryTexture(Texture2D texture, Texture2D normalMap) =>
            normalMap && SecondaryNormalMap(texture) == normalMap;

        private static bool AssignSecondaryTexture(TextureImporter importer, Texture2D normalMap)
        {
            if (!normalMap) return false;
            var list = new List<SecondarySpriteTexture>(importer.secondarySpriteTextures ?? Array.Empty<SecondarySpriteTexture>());
            var index = list.FindIndex(s => s.name == SecondaryTextureName);
            if (index >= 0 && list[index].texture == normalMap) return true;
            var entry = new SecondarySpriteTexture { name = SecondaryTextureName, texture = normalMap };
            if (index >= 0) list[index] = entry;
            else list.Add(entry);
            importer.secondarySpriteTextures = list.ToArray();
            importer.SaveAndReimport();
            return true;
        }

        private static void ConfigureNormalImporter(TextureImporter normal, TextureImporter source)
        {
            normal.textureType = TextureImporterType.NormalMap;
            normal.npotScale = TextureImporterNPOTScale.None;
            if (!source)
            {
                normal.mipmapEnabled = false;
                return;
            }

            normal.mipmapEnabled = source.mipmapEnabled;
            normal.filterMode = source.filterMode;
            normal.wrapMode = source.wrapMode;
            normal.maxTextureSize = source.maxTextureSize;
            normal.textureCompression = source.textureCompression;
        }

        private static RectInt[] SpriteRects(string path, Texture2D texture, int width, int height)
        {
            var rects = new List<RectInt>();
            var sx = width / (float)Mathf.Max(1, texture.width);
            var sy = height / (float)Mathf.Max(1, texture.height);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not Sprite sprite) continue;
                var r = sprite.rect;
                var x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin * sx), 0, width - 1);
                var y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin * sy), 0, height - 1);
                var x1 = Mathf.Clamp(Mathf.CeilToInt(r.xMax * sx), x0 + 1, width);
                var y1 = Mathf.Clamp(Mathf.CeilToInt(r.yMax * sy), y0 + 1, height);
                rects.Add(new RectInt(x0, y0, x1 - x0, y1 - y0));
            }

            if (rects.Count == 0) rects.Add(new RectInt(0, 0, width, height));
            return rects.ToArray();
        }

        private static Texture2D ReadBack(Texture2D texture)
        {
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        private static void BakeRect(SpriteNormalSource source, RectInt rect, in SpriteNormalBakeSettings s, Vector3[] normals)
        {
            int w = rect.width, h = rect.height, n = w * h;
            var mask = new float[n];
            var empty = new bool[n];
            var lum = new float[n];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var c = source.Pixels[(rect.y + y) * source.Width + rect.x + x];
                var i = y * w + x;
                var inside = c.a >= s.AlphaThreshold;
                mask[i] = inside ? 1f : 0f;
                empty[i] = !inside;
                lum[i] = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            }

            var tmp = new float[n];
            var soft = MaskedBlur(lum, mask, w, h, s.DetailSoftness, tmp);

            var outline = Distance(empty, w, h, s.BevelTextureBorders);
            float[] inner = null;
            if (s.InnerEdges > 0f) inner = Distance(DetectEdges(soft, mask, w, h, s.InnerEdgeThreshold), w, h, false);

            var shape = new float[n];
            var bevelWidth = Mathf.Max(0.5f, s.BevelWidth);
            var innerWidth = Mathf.Max(0.5f, s.InnerEdgeWidth);
            for (var i = 0; i < n; i++)
            {
                if (mask[i] <= 0f) continue;
                var height = Profile(s.BevelShape, (outline[i] - 0.5f) / bevelWidth) * bevelWidth;
                if (inner != null) height -= s.InnerEdges * innerWidth * (1f - Profile(s.BevelShape, (inner[i] + 0.5f) / innerWidth));
                shape[i] = Mathf.Max(0f, height) * s.BevelHeight;
            }

            Blur(shape, w, h, s.ShapeSmoothing, tmp);

            float[] detail = null;
            if (s.DetailBumps > 0f)
            {
                var low = MaskedBlur(soft, mask, w, h, s.DetailWidth, tmp);
                var sign = s.InvertDetail ? -s.DetailBumps : s.DetailBumps;
                detail = new float[n];
                for (var i = 0; i < n; i++) detail[i] = (soft[i] - low[i]) * sign;
            }

            var sx = s.FlipX ? -1f : 1f;
            var sy = s.FlipY ? -1f : 1f;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                if (mask[i] <= 0f) continue;
                var gx = (ShapeAt(shape, w, h, x + 1, y, s.BevelTextureBorders) - ShapeAt(shape, w, h, x - 1, y, s.BevelTextureBorders)) * 0.5f;
                var gy = (ShapeAt(shape, w, h, x, y + 1, s.BevelTextureBorders) - ShapeAt(shape, w, h, x, y - 1, s.BevelTextureBorders)) * 0.5f;
                if (detail != null)
                {
                    var d = detail[i];
                    gx += (DetailAt(detail, mask, w, h, x + 1, y, d) - DetailAt(detail, mask, w, h, x - 1, y, d)) * 0.5f;
                    gy += (DetailAt(detail, mask, w, h, x, y + 1, d) - DetailAt(detail, mask, w, h, x, y - 1, d)) * 0.5f;
                }

                var normal = new Vector3(-gx * s.NormalStrength * sx, -gy * s.NormalStrength * sy, 1f).normalized;
                normals[(rect.y + y) * source.Width + rect.x + x] = normal;
            }

            Pad(normals, source.Width, rect, mask, s.Padding);
        }

        private static float ShapeAt(float[] shape, int w, int h, int x, int y, bool borders)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
            {
                if (borders) return 0f;
                x = Mathf.Clamp(x, 0, w - 1);
                y = Mathf.Clamp(y, 0, h - 1);
            }

            return shape[y * w + x];
        }

        private static float DetailAt(float[] detail, float[] mask, int w, int h, int x, int y, float fallback)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return fallback;
            var i = y * w + x;
            return mask[i] > 0f ? detail[i] : fallback;
        }

        private static float Profile(SpriteNormalBevelShape shape, float x)
        {
            x = Mathf.Clamp01(x);
            switch (shape)
            {
                case SpriteNormalBevelShape.Smooth:
                    return x * x * (3f - 2f * x);
                case SpriteNormalBevelShape.Linear:
                    return x;
                default:
                    var t = 1f - x;
                    return Mathf.Sqrt(1f - t * t);
            }
        }

        private static bool[] DetectEdges(float[] lum, float[] mask, int w, int h, float threshold)
        {
            var edges = new bool[w * h];
            for (var y = 1; y < h - 1; y++)
            for (var x = 1; x < w - 1; x++)
            {
                var inside = true;
                for (var j = -1; j <= 1 && inside; j++)
                for (var i = -1; i <= 1; i++)
                    if (mask[(y + j) * w + x + i] <= 0f)
                    {
                        inside = false;
                        break;
                    }

                if (!inside) continue;

                float L(int dx, int dy) => lum[(y + dy) * w + x + dx];
                var gx = L(1, -1) + 2f * L(1, 0) + L(1, 1) - L(-1, -1) - 2f * L(-1, 0) - L(-1, 1);
                var gy = L(-1, 1) + 2f * L(0, 1) + L(1, 1) - L(-1, -1) - 2f * L(0, -1) - L(1, -1);
                edges[y * w + x] = Mathf.Sqrt(gx * gx + gy * gy) * 0.25f >= threshold;
            }

            return edges;
        }

        private static float[] Distance(bool[] feature, int w, int h, bool borderIsFeature)
        {
            int pw = w + 2, ph = h + 2;
            var grid = new double[pw * ph];
            for (var y = 0; y < ph; y++)
            for (var x = 0; x < pw; x++)
            {
                var inside = x > 0 && x <= w && y > 0 && y <= h;
                var isFeature = inside ? feature[(y - 1) * w + x - 1] : borderIsFeature;
                grid[y * pw + x] = isFeature ? 0.0 : Far;
            }

            var length = Mathf.Max(pw, ph);
            var f = new double[length];
            var d = new double[length];
            var v = new int[length];
            var z = new double[length + 1];

            for (var x = 0; x < pw; x++)
            {
                for (var y = 0; y < ph; y++) f[y] = grid[y * pw + x];
                Transform1D(f, d, v, z, ph);
                for (var y = 0; y < ph; y++) grid[y * pw + x] = d[y];
            }

            for (var y = 0; y < ph; y++)
            {
                Array.Copy(grid, y * pw, f, 0, pw);
                Transform1D(f, d, v, z, pw);
                Array.Copy(d, 0, grid, y * pw, pw);
            }

            var result = new float[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                result[y * w + x] = (float)Math.Sqrt(grid[(y + 1) * pw + x + 1]);
            return result;
        }

        private static void Transform1D(double[] f, double[] d, int[] v, double[] z, int n)
        {
            var k = 0;
            v[0] = 0;
            z[0] = double.NegativeInfinity;
            z[1] = double.PositiveInfinity;
            for (var q = 1; q < n; q++)
            {
                var s = Intersection(f, q, v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = Intersection(f, q, v[k]);
                }

                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = double.PositiveInfinity;
            }

            k = 0;
            for (var q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                var dq = q - v[k];
                d[q] = (double)dq * dq + f[v[k]];
            }
        }

        private static double Intersection(double[] f, int q, int p) =>
            (f[q] + (double)q * q - (f[p] + (double)p * p)) / (2.0 * (q - p));

        private static float[] MaskedBlur(float[] values, float[] mask, int w, int h, float sigma, float[] tmp)
        {
            var n = values.Length;
            var weighted = new float[n];
            var weights = (float[])mask.Clone();
            for (var i = 0; i < n; i++) weighted[i] = values[i] * mask[i];
            Blur(weighted, w, h, sigma, tmp);
            Blur(weights, w, h, sigma, tmp);
            var result = new float[n];
            for (var i = 0; i < n; i++) result[i] = weights[i] > 1e-4f ? weighted[i] / weights[i] : values[i];
            return result;
        }

        private static void Blur(float[] data, int w, int h, float sigma, float[] tmp)
        {
            if (sigma <= 0.05f) return;
            var radius = (Mathf.Sqrt(1f + 4f * sigma * sigma) - 1f) * 0.5f;
            for (var pass = 0; pass < 3; pass++)
            {
                for (var y = 0; y < h; y++) BoxLine(data, tmp, y * w, 1, w, radius);
                for (var x = 0; x < w; x++) BoxLine(tmp, data, x, w, h, radius);
            }
        }

        private static void BoxLine(float[] src, float[] dst, int start, int stride, int n, float radius)
        {
            var whole = (int)radius;
            var fraction = radius - whole;
            var norm = 1f / (2 * whole + 1 + 2f * fraction);
            var last = n - 1;
            var sum = 0f;
            for (var k = -whole; k <= whole; k++) sum += src[start + Mathf.Clamp(k, 0, last) * stride];
            for (var x = 0; x < n; x++)
            {
                var outer = fraction > 0f
                    ? src[start + Mathf.Clamp(x - whole - 1, 0, last) * stride] + src[start + Mathf.Clamp(x + whole + 1, 0, last) * stride]
                    : 0f;
                dst[start + x * stride] = (sum + fraction * outer) * norm;
                sum += src[start + Mathf.Clamp(x + whole + 1, 0, last) * stride] - src[start + Mathf.Clamp(x - whole, 0, last) * stride];
            }
        }

        private static void Pad(Vector3[] normals, int stride, RectInt rect, float[] mask, int passes)
        {
            int w = rect.width, h = rect.height;
            var known = new bool[w * h];
            for (var i = 0; i < known.Length; i++) known[i] = mask[i] > 0f;
            var added = new List<(int index, Vector3 normal)>();
            for (var pass = 0; pass < passes; pass++)
            {
                added.Clear();
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    if (known[y * w + x]) continue;
                    var sum = Vector3.zero;
                    for (var j = -1; j <= 1; j++)
                    for (var i = -1; i <= 1; i++)
                    {
                        int nx = x + i, ny = y + j;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h || !known[ny * w + nx]) continue;
                        sum += normals[(rect.y + ny) * stride + rect.x + nx];
                    }

                    if (sum != Vector3.zero) added.Add((y * w + x, sum.normalized));
                }

                if (added.Count == 0) break;
                foreach (var (index, normal) in added)
                {
                    known[index] = true;
                    normals[(rect.y + index / w) * stride + rect.x + index % w] = normal;
                }
            }
        }

        private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt((v * 0.5f + 0.5f) * 255f), 0, 255);
    }
}
