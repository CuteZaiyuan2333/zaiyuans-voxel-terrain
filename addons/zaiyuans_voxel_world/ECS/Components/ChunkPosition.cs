using Godot;

namespace ZaiyuansVoxelWorld.ECS.Components;

public readonly struct ChunkPosition
{
    public Vector3I Value { get; }

    public ChunkPosition(Vector3I value) => Value = value;
    public int X => Value.X;
    public int Y => Value.Y;
    public int Z => Value.Z;
}
