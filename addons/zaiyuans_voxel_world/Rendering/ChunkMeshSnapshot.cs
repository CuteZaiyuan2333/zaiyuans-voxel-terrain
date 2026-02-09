using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.ECS;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld.Rendering;

/// <summary>Read-only snapshot of chunk + 6 neighbor faces for async mesh build. lx, ly, lz in [-1, 32].</summary>
public sealed class ChunkMeshSnapshot
{
    /// <summary>Stored in neighbor face when that chunk is not loaded; mesher treats as solid (no face drawn).</summary>
    public const byte NeighborNotLoaded = 0xFF;

    public const int S = VoxelConstants.ChunkSize;
    public const int FaceSize = S * S;

    public byte[] Chunk { get; set; }
    public byte[] FaceMinX { get; set; }
    public byte[] FaceMaxX { get; set; }
    public byte[] FaceMinY { get; set; }
    public byte[] FaceMaxY { get; set; }
    public byte[] FaceMinZ { get; set; }
    public byte[] FaceMaxZ { get; set; }

    public byte GetBlock(int lx, int ly, int lz)
    {
        if (lx >= 0 && lx < S && ly >= 0 && ly < S && lz >= 0 && lz < S)
            return Chunk != null && Chunk.Length >= VoxelConstants.ChunkVolume
                ? Chunk[VoxelConstants.LocalToIndex(lx, ly, lz)]
                : (byte)0;
        if (lx == -1 && FaceMinX != null && ly >= 0 && ly < S && lz >= 0 && lz < S)
            return FaceMinX[ly * S + lz];
        if (lx == S && FaceMaxX != null && ly >= 0 && ly < S && lz >= 0 && lz < S)
            return FaceMaxX[ly * S + lz];
        if (ly == -1 && FaceMinY != null && lx >= 0 && lx < S && lz >= 0 && lz < S)
            return FaceMinY[lx * S + lz];
        if (ly == S && FaceMaxY != null && lx >= 0 && lx < S && lz >= 0 && lz < S)
            return FaceMaxY[lx * S + lz];
        if (lz == -1 && FaceMinZ != null && lx >= 0 && lx < S && ly >= 0 && ly < S)
            return FaceMinZ[lx * S + ly];
        if (lz == S && FaceMaxZ != null && lx >= 0 && lx < S && ly >= 0 && ly < S)
            return FaceMaxZ[lx * S + ly];
        return 0;
    }

    public static ChunkMeshSnapshot CreateFromWorld(VoxelEcsWorld world, ChunkEntity e)
    {
        if (!world.VoxelData.TryGetValue(e, out var data))
            return null;
        var snapshot = new ChunkMeshSnapshot
        {
            Chunk = new byte[VoxelConstants.ChunkVolume],
            FaceMinX = new byte[FaceSize],
            FaceMaxX = new byte[FaceSize],
            FaceMinY = new byte[FaceSize],
            FaceMaxY = new byte[FaceSize],
            FaceMinZ = new byte[FaceSize],
            FaceMaxZ = new byte[FaceSize],
        };
        data.AsSpan().Slice(0, VoxelConstants.ChunkVolume).CopyTo(snapshot.Chunk);
        int ox = e.ChunkPos.X * S, oy = e.ChunkPos.Y * S, oz = e.ChunkPos.Z * S;
        for (int ly = 0; ly < S; ly++)
        for (int lz = 0; lz < S; lz++)
        {
            snapshot.FaceMinX[ly * S + lz] = world.TryGetBlockAtWorld(ox - 1, oy + ly, oz + lz, out var b) ? b : NeighborNotLoaded;
            snapshot.FaceMaxX[ly * S + lz] = world.TryGetBlockAtWorld(ox + S, oy + ly, oz + lz, out b) ? b : NeighborNotLoaded;
        }
        for (int lx = 0; lx < S; lx++)
        for (int lz = 0; lz < S; lz++)
        {
            snapshot.FaceMinY[lx * S + lz] = world.TryGetBlockAtWorld(ox + lx, oy - 1, oz + lz, out var b) ? b : NeighborNotLoaded;
            snapshot.FaceMaxY[lx * S + lz] = world.TryGetBlockAtWorld(ox + lx, oy + S, oz + lz, out b) ? b : NeighborNotLoaded;
        }
        for (int lx = 0; lx < S; lx++)
        for (int ly = 0; ly < S; ly++)
        {
            snapshot.FaceMinZ[lx * S + ly] = world.TryGetBlockAtWorld(ox + lx, oy + ly, oz - 1, out var b) ? b : NeighborNotLoaded;
            snapshot.FaceMaxZ[lx * S + ly] = world.TryGetBlockAtWorld(ox + lx, oy + ly, oz + S, out b) ? b : NeighborNotLoaded;
        }
        return snapshot;
    }
}
