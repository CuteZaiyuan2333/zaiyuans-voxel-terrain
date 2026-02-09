using Godot;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld.Data;

/// <summary>
/// Generates voxel data for a chunk. Called by ChunkTerrainGenSystem.
/// </summary>
public interface IChunkGenerator
{
    void Generate(Vector3I chunkPos, VoxelData data, int seed);
}
