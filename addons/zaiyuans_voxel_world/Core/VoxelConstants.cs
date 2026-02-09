using Godot;

namespace ZaiyuansVoxelWorld.Core;

/// <summary>
/// Constants for 32×32×32 chunk-based voxel world.
/// 1 world unit = 1 voxel edge.
/// </summary>
public static class VoxelConstants
{
    public const int ChunkSize = 32;
    public const int ChunkSizeShift = 5;
    public const int ChunkSizeMask = 31;
    public const int ChunkSizeSq = ChunkSize * ChunkSize;
    public const int ChunkVolume = ChunkSize * ChunkSize * ChunkSize;

    /// <summary>World position to chunk index (floor division).</summary>
    public static int WorldToChunkCoord(int worldCoord)
    {
        if (worldCoord >= 0) return worldCoord >> ChunkSizeShift;
        return (worldCoord + 1) / ChunkSize - 1;
    }

    /// <summary>World position to local position inside chunk [0..31].</summary>
    public static int WorldToLocalCoord(int worldCoord)
    {
        int local = worldCoord % ChunkSize;
        return local < 0 ? local + ChunkSize : local;
    }

    /// <summary>Chunk world origin (minimum corner) in world units.</summary>
    public static Vector3I ChunkToWorldOrigin(Vector3I chunkPos)
    {
        return new Vector3I(
            chunkPos.X * ChunkSize,
            chunkPos.Y * ChunkSize,
            chunkPos.Z * ChunkSize
        );
    }

    /// <summary>Linear index inside a chunk from local (lx, ly, lz).</summary>
    public static int LocalToIndex(int lx, int ly, int lz)
    {
        return lx + ly * ChunkSize + lz * ChunkSizeSq;
    }

    /// <summary>Six neighbor chunk positions: -X, +X, -Y, +Y, -Z, +Z. Caller can use as read-only.</summary>
    public static void GetNeighborChunkPositions(Vector3I chunkPos, Vector3I[] sixOut)
    {
        if (sixOut == null || sixOut.Length < 6) return;
        int cx = chunkPos.X, cy = chunkPos.Y, cz = chunkPos.Z;
        sixOut[0] = new Vector3I(cx - 1, cy, cz);
        sixOut[1] = new Vector3I(cx + 1, cy, cz);
        sixOut[2] = new Vector3I(cx, cy - 1, cz);
        sixOut[3] = new Vector3I(cx, cy + 1, cz);
        sixOut[4] = new Vector3I(cx, cy, cz - 1);
        sixOut[5] = new Vector3I(cx, cy, cz + 1);
    }
}
