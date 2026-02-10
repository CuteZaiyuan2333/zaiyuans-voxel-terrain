using Godot;

namespace ZaiyuansVoxelWorld.Core;

/// <summary>
/// Result of a voxel raycast hit: world position, face normal, block coordinate and block type.
/// </summary>
public readonly struct VoxelRaycastResult
{
    /// <summary>World position where the ray hit the voxel surface.</summary>
    public Vector3 Position { get; }
    /// <summary>Outward normal of the hit face (e.g. (1,0,0) for +X face).</summary>
    public Vector3 Normal { get; }
    /// <summary>Block/voxel coordinate that was hit.</summary>
    public Vector3I BlockPosition { get; }
    /// <summary>Block type at the hit voxel.</summary>
    public BlockId BlockId { get; }

    public VoxelRaycastResult(Vector3 position, Vector3 normal, Vector3I blockPosition, BlockId blockId)
    {
        Position = position;
        Normal = normal;
        BlockPosition = blockPosition;
        BlockId = blockId;
    }
}
