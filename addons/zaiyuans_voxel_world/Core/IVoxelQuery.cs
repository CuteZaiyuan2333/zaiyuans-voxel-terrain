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

    // TODO: Add Raycast and GetCollidingBoxes methods
    // bool Raycast(...);
    // IEnumerable<AABB> GetCollidingBoxes(AABB box);
}
