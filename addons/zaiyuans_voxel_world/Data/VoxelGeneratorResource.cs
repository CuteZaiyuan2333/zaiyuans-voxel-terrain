using Godot;
using System;

namespace ZaiyuansVoxelWorld.Data;

/// <summary>
/// Base class for user-defined voxel generators in GDScript.
/// Users should extend this naming it e.g. MyTerrainGenerator.
/// </summary>
[GlobalClass]
public partial class VoxelGeneratorResource : Resource
{
    /// <summary>
    /// Called by the system to generate a chunk.
    /// </summary>
    /// <param name="buffer">The VoxelBufferWrapper to write blocks to.</param>
    /// <param name="chunkPos">The chunk coordinates (x, y, z).</param>
    public virtual void _GenerateChunk(VoxelBufferWrapper buffer, Vector3I chunkPos)
    {
        // To be implemented by GDScript
    }
}
