using System;
using System.Collections.Generic;
using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.ECS;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld.Rendering;

/// <summary>
/// Builds an ArrayMesh from chunk voxel data. Only draws faces between solid and air.
/// </summary>
public sealed class ChunkMesher
{
    /// <summary>Vertex count of the last built mesh (for LOD/culling metadata).</summary>
    public int LastVertexCount { get; private set; }

    private static readonly Vector3[] FaceNormals =
    {
        new(-1, 0, 0), new(1, 0, 0),
        new(0, -1, 0), new(0, 1, 0),
        new(0, 0, -1), new(0, 0, 1),
    };

    private static readonly int[] FaceDx = { -1, 1, 0, 0, 0, 0 };
    private static readonly int[] FaceDy = { 0, 0, -1, 1, 0, 0 };
    private static readonly int[] FaceDz = { 0, 0, 0, 0, -1, 1 };

    public Mesh Build(Vector3I chunkPos, VoxelData data, VoxelEcsWorld world, EcsRunContext ctx)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        if (ctx != null && ctx.UseGreedyMeshing)
            BuildGreedy(chunkPos, data, world, vertices, normals, uvs);
        else
            BuildNaive(chunkPos, data, world, vertices, normals, uvs);

        if (vertices.Count == 0)
        {
            LastVertexCount = 0;
            return null;
        }

        LastVertexCount = vertices.Count;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();

        var arrMesh = new ArrayMesh();
        arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return arrMesh;
    }

    private void BuildNaive(Vector3I chunkPos, VoxelData data, VoxelEcsWorld world,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs)
    {
        int ox = chunkPos.X * VoxelConstants.ChunkSize;
        int oy = chunkPos.Y * VoxelConstants.ChunkSize;
        int oz = chunkPos.Z * VoxelConstants.ChunkSize;
        for (int lz = 0; lz < VoxelConstants.ChunkSize; lz++)
        for (int ly = 0; ly < VoxelConstants.ChunkSize; ly++)
        for (int lx = 0; lx < VoxelConstants.ChunkSize; lx++)
        {
            byte b = data.Get(lx, ly, lz);
            if (b == (byte)BlockId.Air) continue;
            int wx = ox + lx, wy = oy + ly, wz = oz + lz;
            for (int f = 0; f < 6; f++)
            {
                byte neighbor = 0;
                if (world.TryGetBlockAtWorld(wx + FaceDx[f], wy + FaceDy[f], wz + FaceDz[f], out var nb))
                    neighbor = nb;
                if (neighbor != (byte)BlockId.Air) continue;
                AddFace(vertices, normals, uvs, lx, ly, lz, f, FaceNormals[f]);
            }
        }
    }

    private void BuildGreedy(Vector3I chunkPos, VoxelData data, VoxelEcsWorld world,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs)
    {
        int ox = chunkPos.X * VoxelConstants.ChunkSize;
        int oy = chunkPos.Y * VoxelConstants.ChunkSize;
        int oz = chunkPos.Z * VoxelConstants.ChunkSize;
        const int S = VoxelConstants.ChunkSize;
        var slice = new byte[S, S];
        var merged = new bool[S, S];

        for (int f = 0; f < 6; f++)
        {
            int dx = FaceDx[f], dy = FaceDy[f], dz = FaceDz[f];
            Vector3 normal = FaceNormals[f];
            for (int sliceIndex = 0; sliceIndex < S; sliceIndex++)
            {
                for (int a = 0; a < S; a++)
                for (int b = 0; b < S; b++)
                {
                    merged[a, b] = false;
                    int lx, ly, lz, nx, ny, nz;
                    GetSliceCoords(f, sliceIndex, a, b, out lx, out ly, out lz);
                    nx = lx + dx; ny = ly + dy; nz = lz + dz;
                    byte block = data.Get(lx, ly, lz);
                    if (block == (byte)BlockId.Air) { slice[a, b] = 0; continue; }
                    byte neighbor = 0;
                    if (world.TryGetBlockAtWorld(ox + nx, oy + ny, oz + nz, out var nb))
                        neighbor = nb;
                    slice[a, b] = (neighbor == (byte)BlockId.Air) ? block : (byte)0;
                }
                for (int a = 0; a < S; a++)
                for (int b = 0; b < S; b++)
                {
                    if (merged[a, b] || slice[a, b] == 0) continue;
                    byte id = slice[a, b];
                    int w = 1;
                    while (b + w < S && slice[a, b + w] == id) w++;
                    int h = 1;
                    while (a + h < S)
                    {
                        bool same = true;
                        for (int bb = 0; bb < w && same; bb++)
                            same = slice[a + h, b + bb] == id;
                        if (!same) break;
                        h++;
                    }
                    for (int aa = 0; aa < h; aa++)
                    for (int bb = 0; bb < w; bb++)
                        merged[a + aa, b + bb] = true;
                    int lx0, ly0, lz0, lx1, ly1, lz1;
                    GetSliceCoords(f, sliceIndex, a, b, out lx0, out ly0, out lz0);
                    GetSliceCoords(f, sliceIndex, a + h - 1, b + w - 1, out lx1, out ly1, out lz1);
                    AddGreedyQuad(vertices, normals, uvs, f, lx0, ly0, lz0, lx1, ly1, lz1);
                }
            }
        }
    }

    private static void GetSliceCoords(int face, int sliceIndex, int a, int b, out int lx, out int ly, out int lz)
    {
        switch (face)
        {
            case 0: case 1: lx = sliceIndex; ly = a; lz = b; break;
            case 2: case 3: ly = sliceIndex; lx = a; lz = b; break;
            default: lz = sliceIndex; lx = a; ly = b; break;
        }
    }

    private static void AddGreedyQuad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, int face,
        int lx0, int ly0, int lz0, int lx1, int ly1, int lz1)
    {
        float u0 = 0, v0 = 0, u1 = 1, v1 = 1;
        Vector3 o0 = new Vector3(lx0, ly0, lz0);
        Vector3 o1 = new Vector3(lx1 + 1, ly1 + 1, lz1 + 1);
        Vector3[] quad;
        if (face == 0)
            quad = new[] { new Vector3(o0.X, o0.Y, o0.Z), new Vector3(o0.X, o1.Y, o0.Z), new Vector3(o0.X, o1.Y, o1.Z), new Vector3(o0.X, o0.Y, o1.Z) };
        else if (face == 1)
            quad = new[] { new Vector3(o1.X, o0.Y, o1.Z), new Vector3(o1.X, o1.Y, o1.Z), new Vector3(o1.X, o1.Y, o0.Z), new Vector3(o1.X, o0.Y, o0.Z) };
        else if (face == 2)
            quad = new[] { new Vector3(o0.X, o0.Y, o1.Z), new Vector3(o1.X, o0.Y, o1.Z), new Vector3(o1.X, o0.Y, o0.Z), new Vector3(o0.X, o0.Y, o0.Z) };
        else if (face == 3)
            quad = new[] { new Vector3(o0.X, o1.Y, o0.Z), new Vector3(o1.X, o1.Y, o0.Z), new Vector3(o1.X, o1.Y, o1.Z), new Vector3(o0.X, o1.Y, o1.Z) };
        else if (face == 4)
            quad = new[] { new Vector3(o1.X, o0.Y, o0.Z), new Vector3(o1.X, o1.Y, o0.Z), new Vector3(o0.X, o1.Y, o0.Z), new Vector3(o0.X, o0.Y, o0.Z) };
        else
            quad = new[] { new Vector3(o0.X, o0.Y, o1.Z), new Vector3(o0.X, o1.Y, o1.Z), new Vector3(o1.X, o1.Y, o1.Z), new Vector3(o1.X, o0.Y, o1.Z) };
        Vector3 norm = FaceNormals[face];
        v.Add(quad[0]); v.Add(quad[1]); v.Add(quad[2]);
        v.Add(quad[0]); v.Add(quad[2]); v.Add(quad[3]);
        for (int i = 0; i < 6; i++) n.Add(norm);
        uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u0, v1)); uv.Add(new Vector2(u1, v1));
        uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2(u1, v0));
    }

    /// <summary>Build mesh arrays from snapshot (thread-safe, no Godot world). Returns (vertices, normals, uvs).</summary>
    public static (Vector3[] v, Vector3[] n, Vector2[] u) BuildMeshDataFromSnapshot(Vector3I chunkPos, ChunkMeshSnapshot snapshot, bool useGreedy)
    {
        if (snapshot?.Chunk == null) return (Array.Empty<Vector3>(), Array.Empty<Vector3>(), Array.Empty<Vector2>());
        byte GetBlock(int lx, int ly, int lz) => snapshot.GetBlock(lx, ly, lz);
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        if (useGreedy)
            BuildGreedyWithGetter(GetBlock, vertices, normals, uvs);
        else
            BuildNaiveWithGetter(GetBlock, vertices, normals, uvs);
        return (vertices.ToArray(), normals.ToArray(), uvs.ToArray());
    }

    private static void BuildNaiveWithGetter(Func<int, int, int, byte> getBlock,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs)
    {
        for (int lz = 0; lz < VoxelConstants.ChunkSize; lz++)
        for (int ly = 0; ly < VoxelConstants.ChunkSize; ly++)
        for (int lx = 0; lx < VoxelConstants.ChunkSize; lx++)
        {
            byte b = getBlock(lx, ly, lz);
            if (b == (byte)BlockId.Air) continue;
            for (int f = 0; f < 6; f++)
            {
                byte neighbor = getBlock(lx + FaceDx[f], ly + FaceDy[f], lz + FaceDz[f]);
                if (neighbor != (byte)BlockId.Air) continue;
                AddFace(vertices, normals, uvs, lx, ly, lz, f, FaceNormals[f]);
            }
        }
    }

    private static void BuildGreedyWithGetter(Func<int, int, int, byte> getBlock,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs)
    {
        const int S = VoxelConstants.ChunkSize;
        var slice = new byte[S, S];
        var merged = new bool[S, S];
        for (int f = 0; f < 6; f++)
        {
            int dx = FaceDx[f], dy = FaceDy[f], dz = FaceDz[f];
            for (int sliceIndex = 0; sliceIndex < S; sliceIndex++)
            {
                for (int a = 0; a < S; a++)
                for (int b = 0; b < S; b++)
                {
                    merged[a, b] = false;
                    GetSliceCoords(f, sliceIndex, a, b, out int lx, out int ly, out int lz);
                    int nx = lx + dx, ny = ly + dy, nz = lz + dz;
                    byte block = getBlock(lx, ly, lz);
                    slice[a, b] = (block != (byte)BlockId.Air && getBlock(nx, ny, nz) == (byte)BlockId.Air) ? block : (byte)0;
                }
                for (int a = 0; a < S; a++)
                for (int b = 0; b < S; b++)
                {
                    if (merged[a, b] || slice[a, b] == 0) continue;
                    byte id = slice[a, b];
                    int w = 1;
                    while (b + w < S && slice[a, b + w] == id) w++;
                    int h = 1;
                    while (a + h < S)
                    {
                        bool same = true;
                        for (int bb = 0; bb < w && same; bb++) same = slice[a + h, b + bb] == id;
                        if (!same) break;
                        h++;
                    }
                    for (int aa = 0; aa < h; aa++)
                    for (int bb = 0; bb < w; bb++)
                        merged[a + aa, b + bb] = true;
                    GetSliceCoords(f, sliceIndex, a, b, out int lx0, out int ly0, out int lz0);
                    GetSliceCoords(f, sliceIndex, a + h - 1, b + w - 1, out int lx1, out int ly1, out int lz1);
                    AddGreedyQuad(vertices, normals, uvs, f, lx0, ly0, lz0, lx1, ly1, lz1);
                }
            }
        }
    }

    private static void AddFace(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
        int lx, int ly, int lz, int face, Vector3 normal)
    {
        Vector3 o = new Vector3(lx, ly, lz);
        float u0 = 0, v0 = 0, u1 = 1, v1 = 1;
        Vector3[] quad;
        if (face == 0) // -X
            quad = new[] { o + new Vector3(0, 0, 0), o + new Vector3(0, 1, 0), o + new Vector3(0, 1, 1), o + new Vector3(0, 0, 1) };
        else if (face == 1) // +X
            quad = new[] { o + new Vector3(1, 0, 1), o + new Vector3(1, 1, 1), o + new Vector3(1, 1, 0), o + new Vector3(1, 0, 0) };
        else if (face == 2) // -Y
            quad = new[] { o + new Vector3(0, 0, 1), o + new Vector3(1, 0, 1), o + new Vector3(1, 0, 0), o + new Vector3(0, 0, 0) };
        else if (face == 3) // +Y
            quad = new[] { o + new Vector3(0, 1, 0), o + new Vector3(1, 1, 0), o + new Vector3(1, 1, 1), o + new Vector3(0, 1, 1) };
        else if (face == 4) // -Z
            quad = new[] { o + new Vector3(1, 0, 0), o + new Vector3(1, 1, 0), o + new Vector3(0, 1, 0), o + new Vector3(0, 0, 0) };
        else // +Z
            quad = new[] { o + new Vector3(0, 0, 1), o + new Vector3(0, 1, 1), o + new Vector3(1, 1, 1), o + new Vector3(1, 0, 1) };

        vertices.Add(quad[0]); vertices.Add(quad[1]); vertices.Add(quad[2]);
        vertices.Add(quad[0]); vertices.Add(quad[2]); vertices.Add(quad[3]);
        for (int i = 0; i < 6; i++)
            normals.Add(normal);
        uvs.Add(new Vector2(u0, v0)); uvs.Add(new Vector2(u0, v1)); uvs.Add(new Vector2(u1, v1));
        uvs.Add(new Vector2(u0, v0)); uvs.Add(new Vector2(u1, v1)); uvs.Add(new Vector2(u1, v0));
    }
}
