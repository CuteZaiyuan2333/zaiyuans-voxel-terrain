using Godot;

namespace ZaiyuansVoxelWorld;

public partial class VoxelTerrain : Node3D
{
    /// <summary>If set, observer position is taken from this node's GlobalPosition. Otherwise uses this VoxelTerrain's position.</summary>
    [Export] public Node3D ObserverNode { get; set; }

    private VoxelWorld _voxelWorld;

    public override void _Ready()
    {
        _voxelWorld = GetNodeOrNull<VoxelWorld>("%VoxelWorld");
        if (_voxelWorld == null)
        {
            _voxelWorld = GetTree().Root.GetNodeOrNull<VoxelWorld>("VoxelWorld");
        }
        if (_voxelWorld == null)
        {
            _voxelWorld = new VoxelWorld();
            _voxelWorld.Name = "VoxelWorld";
            AddChild(_voxelWorld);
        }
    }

    public override void _Process(double delta)
    {
        if (_voxelWorld == null) return;

        if (ObserverNode != null)
            _voxelWorld.ObserverPosition = ObserverNode.GlobalPosition;
        else
            _voxelWorld.ObserverPosition = GlobalPosition;

        var camera = (ObserverNode as Camera3D) ?? GetViewport().GetCamera3D();
        _voxelWorld.RunEcs(delta, this, camera);
    }

    /// <summary>Convenience: SetBlock on the world singleton (if this terrain's world is active).</summary>
    public void SetBlock(Vector3I worldPos, Core.BlockId blockId)
    {
        _voxelWorld?.SetBlock(worldPos, blockId);
    }

    /// <summary>Convenience: GetBlock from the world singleton.</summary>
    public Core.BlockId GetBlock(Vector3I worldPos)
    {
        return _voxelWorld != null ? _voxelWorld.GetBlock(worldPos) : Core.BlockId.Air;
    }
}
