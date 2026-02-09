using Godot;

namespace ZaiyuansVoxelWorld.ECS.Components;

/// <summary>
/// Holds the generated mesh for a chunk. Null or empty until ChunkMeshSystem runs.
/// </summary>
public sealed class ChunkMesh
{
    public Mesh Mesh { get; set; }
    public int VertexCount { get; set; }

    public bool IsEmpty => Mesh == null || VertexCount == 0;
}
