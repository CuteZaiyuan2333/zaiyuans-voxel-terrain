using System.Collections.Generic;
using Godot;
using ZaiyuansVoxelWorld.Data;

namespace ZaiyuansVoxelWorld.Core;

/// <summary>
/// Interface for querying voxel world data (blocks, raycasts, collisions).
/// Implemented by VoxelWorld.
/// </summary>
public interface IVoxelQuery
{
    /// <summary>
    /// Get block type at global world coordinates.
    /// Returns Air (0) if unloaded or out of bounds.
    /// </summary>
    BlockId GetBlock(Vector3I globalPos);

    /// <summary>
    /// Cast a ray and return the first solid voxel hit.
    /// </summary>
    /// <param name="origin">Ray origin in world space.</param>
    /// <param name="direction">Ray direction (should be normalized).</param>
    /// <param name="maxDistance">Maximum distance to check.</param>
    /// <param name="result">Hit result when return is true.</param>
    /// <returns>True if a non-Air block was hit within maxDistance.</returns>
    bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out VoxelRaycastResult result);

    /// <summary>
    /// Get all solid voxel AABBs that intersect the given box.
    /// Each voxel is returned as an AABB with size (1,1,1) at integer coordinates.
    /// </summary>
    IReadOnlyList<Aabb> GetCollidingBoxes(Aabb box);
}
