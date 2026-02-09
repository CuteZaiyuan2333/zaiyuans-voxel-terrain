using Godot;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld.Data;

/// <summary>
/// Generates voxel data for a chunk. Called by ChunkTerrainGenSystem.
/// When blockLibrary is non-null, generators can use GetIdByName etc.; otherwise use BlockId enum values.
/// </summary>
public interface IChunkGenerator
{
    void Generate(Vector3I chunkPos, VoxelData data, int seed, BlockLibrary blockLibrary = null);
}
