using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace SpookyWater
{
    public partial class WaterTilemapBuilder
    {
        private sealed class ChunkMesh
        {
            public readonly Vector3 Origin;
            public readonly List<Vector3> Vertices = new();
            public readonly List<Vector2> Uvs = new();
            public readonly List<Vector4> SurfaceInfo = new();
            public readonly List<int> Triangles = new();

            public ChunkMesh(Vector3 origin) => Origin = origin;

            public void AddVertex(Vector3 worldPosition, Vector2 uv)
            {
                Vertices.Add(worldPosition - Origin);
                Uvs.Add(uv);
            }

            public void AddVertex(Vector3 worldPosition, Vector2 uv, Vector4 surfaceInfo)
            {
                AddVertex(worldPosition, uv);
                SurfaceInfo.Add(surfaceInfo);
            }

            public void AddGridTriangles(int baseIndex, int columns, int rows, bool frontFacing)
            {
                var stride = columns + 1;
                for (var r = 0; r < rows; r++)
                {
                    for (var c = 0; c < columns; c++)
                    {
                        var i = baseIndex + c + r * stride;
                        if (frontFacing)
                        {
                            Triangles.Add(i + 1); Triangles.Add(i + stride); Triangles.Add(i);
                            Triangles.Add(i + 1 + stride); Triangles.Add(i + stride); Triangles.Add(i + 1);
                        }
                        else
                        {
                            Triangles.Add(i); Triangles.Add(i + stride); Triangles.Add(i + 1);
                            Triangles.Add(i + 1); Triangles.Add(i + stride); Triangles.Add(i + stride + 1);
                        }
                    }
                }
            }

            public Mesh Build(string name, float verticalPadding)
            {
                var mesh = new Mesh { name = name };
                if (Vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(Vertices);
                mesh.SetUVs(0, Uvs);
                if (SurfaceInfo.Count == Vertices.Count) mesh.SetUVs(1, SurfaceInfo);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                if (verticalPadding > 0f)
                {
                    var bounds = mesh.bounds;
                    bounds.Expand(new Vector3(0f, verticalPadding * 2f, 0f));
                    mesh.bounds = bounds;
                }
                return mesh;
            }
        }

        private static ChunkMesh GetChunk(Dictionary<Vector2Int, ChunkMesh> chunks, int x, int y, int chunkSize, Vector3 originIfNew)
        {
            var key = new Vector2Int(Mathf.FloorToInt((float)x / chunkSize), Mathf.FloorToInt((float)y / chunkSize));
            if (!chunks.TryGetValue(key, out var chunk))
            {
                chunk = new ChunkMesh(originIfNew);
                chunks.Add(key, chunk);
            }
            return chunk;
        }

        private static void CreateChunkObject(string name, ChunkMesh chunk, Material material, int sortingLayer,
            WaterCellKind kind, WaterField field, Transform parent, MaterialPropertyBlock block, float verticalPadding)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = chunk.Origin;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            var sg = go.AddComponent<SortingGroup>();
            sg.sortingLayerID = sortingLayer;
            mf.sharedMesh = chunk.Build(name, verticalPadding);
            if (block != null) mr.SetPropertyBlock(block);

            go.AddComponent<WaterTile>().Initialize(kind, field);
        }

        private void AddLineCell(ChunkMesh chunk, Vector3 cellCenter, Vector3 cellSize, float surfaceWorldY)
        {
            var xSegments = Mathf.Max(_surfaceSegmentsPerCell, 1);
            var ySegments = Mathf.Max(_verticalSegmentsPerCell, 1);
            var halfW = cellSize.x * 0.5f;
            var halfH = cellSize.y * 0.5f;
            var info = new Vector4(surfaceWorldY, 1f, 1f, 0f);

            var baseIndex = chunk.Vertices.Count;
            for (var yi = 0; yi <= ySegments; yi++)
            {
                var ty = (float)yi / ySegments;
                for (var xi = 0; xi <= xSegments; xi++)
                {
                    var tx = (float)xi / xSegments;
                    var local = new Vector3(Mathf.Lerp(-halfW, halfW, tx), Mathf.Lerp(-halfH, halfH, ty), 0f);
                    chunk.AddVertex(cellCenter + local, new Vector2(tx, ty), info);
                }
            }
            chunk.AddGridTriangles(baseIndex, xSegments, ySegments, true);
        }

        private static void AddBodyCell(ChunkMesh chunk, Vector3 cellCenter, Vector3 cellSize, float surfaceWorldY)
        {
            var halfW = cellSize.x * 0.5f;
            var halfH = cellSize.y * 0.5f;
            var info = new Vector4(surfaceWorldY, 0f, 1f, 0f);

            var baseIndex = chunk.Vertices.Count;
            chunk.AddVertex(cellCenter + new Vector3(-halfW, -halfH, 0f), new Vector2(0f, 0f), info);
            chunk.AddVertex(cellCenter + new Vector3(halfW, -halfH, 0f), new Vector2(1f, 0f), info);
            chunk.AddVertex(cellCenter + new Vector3(-halfW, halfH, 0f), new Vector2(0f, 0f), info);
            chunk.AddVertex(cellCenter + new Vector3(halfW, halfH, 0f), new Vector2(1f, 0f), info);
            chunk.AddGridTriangles(baseIndex, 1, 1, true);
        }

        private static void AddSurfaceCell(ChunkMesh chunk, Vector3 corner, float cellSizeX, float cellSizeZ, Vector2Int segments)
        {
            var xSegments = Mathf.Max(segments.x, 1);
            var zSegments = Mathf.Max(segments.y, 1);

            var baseIndex = chunk.Vertices.Count;
            for (var z = 0; z <= zSegments; z++)
            {
                for (var x = 0; x <= xSegments; x++)
                {
                    var tx = (float)x / xSegments;
                    var tz = (float)z / zSegments;
                    chunk.AddVertex(corner + new Vector3(tx * cellSizeX, 0f, tz * cellSizeZ), new Vector2(tx, tz));
                }
            }
            chunk.AddGridTriangles(baseIndex, xSegments, zSegments, false);
        }

        private void BuildContactBands(List<Rect> bands, WaterField field)
        {
            if (bands.Count == 0) return;

            var go = new GameObject("WaterContactBands");
            go.transform.SetParent(_generatedRoot, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<WaterTile>().Initialize(WaterCellKind.Line, field);

            foreach (var band in bands)
            {
                var contact = go.AddComponent<BoxCollider2D>();
                contact.isTrigger = true;
                contact.size = band.size;
                contact.offset = band.center;
            }
            go.AddComponent<WaterContactRipple>().Initialize(_contactRipple, _contactLayers);
        }

        private static void BuildSurfaceColliders(Dictionary<Vector2Int, float> cellY, BoundsInt bounds,
            System.Func<int, float> columnCenterX, float cellSizeX, float cellSizeZ, WaterField field)
        {
            if (cellY.Count == 0) return;

            var go = new GameObject("WaterSurfaceColliders");
            go.transform.SetParent(field.transform, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<WaterTile>().Initialize(WaterCellKind.Surface, field);

            void AddBox((int firstCol, int lastCol, float y) span, int firstRow, int lastRow)
            {
                var xMin = columnCenterX(span.firstCol) - cellSizeX * 0.5f;
                var xMax = columnCenterX(span.lastCol) + cellSizeX * 0.5f;
                var zMin = (firstRow - bounds.yMin) * cellSizeZ;
                var zMax = (lastRow + 1 - bounds.yMin) * cellSizeZ;

                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3((xMin + xMax) * 0.5f, span.y, (zMin + zMax) * 0.5f);
                box.size = new Vector3(xMax - xMin, 0.1f, zMax - zMin);
            }

            var open = new Dictionary<(int, int, float), int>();
            for (var row = bounds.yMin; row <= bounds.yMax; row++)
            {
                var current = new Dictionary<(int, int, float), int>();
                for (var col = bounds.xMin; row < bounds.yMax && col < bounds.xMax; col++)
                {
                    if (!cellY.TryGetValue(new Vector2Int(col, row), out var y)) continue;

                    var last = col;
                    while (cellY.TryGetValue(new Vector2Int(last + 1, row), out var nextY) && Mathf.Approximately(nextY, y)) last++;
                    var span = (col, last, y);
                    current[span] = open.TryGetValue(span, out var firstRow) ? firstRow : row;
                    col = last;
                }

                foreach (var (span, firstRow) in open)
                {
                    if (!current.ContainsKey(span)) AddBox(span, firstRow, row - 1);
                }
                open = current;
            }
        }
    }
}
