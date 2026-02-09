using Godot;

namespace ZaiyuansVoxelWorld;

public partial class VoxelTerrain : Node3D
{
    /// <summary>If set, observer position is taken from this node's GlobalPosition. Otherwise uses this VoxelTerrain's position.</summary>
    [Export] public Node3D ObserverNode { get; set; }

    [ExportGroup("World Settings")]
    [Export] public int Seed { get; set; } = 12345;
    [Export] public int ViewDistanceInChunks { get; set; } = 4;
    [Export] public string SaveDirectory { get; set; } = "";
    [Export] public int MaxSpawnPerFrame { get; set; } = 2;
    [Export] public int MaxTerrainGenPerFrame { get; set; } = 2;
    [Export] public int MaxMeshBuildPerFrame { get; set; } = 4;
    [Export] public bool UseGreedyMeshing { get; set; } = true;
    [Export] public bool UseAsyncTerrain { get; set; } = false;
    [Export] public bool UseAsyncMesh { get; set; } = false;
    [Export] public int MaxChunkRadius { get; set; } = 0;

    private VoxelWorld _voxelWorld;
    private bool _voxelWorldCreatedByUs;

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
            _voxelWorldCreatedByUs = true;
        }
        else
        {
            _voxelWorldCreatedByUs = false;
        }

        if (_voxelWorldCreatedByUs)
        {
            _voxelWorld.Seed = Seed;
            _voxelWorld.ViewDistanceInChunks = ViewDistanceInChunks;
            _voxelWorld.SaveDirectory = SaveDirectory ?? "";
            _voxelWorld.MaxSpawnPerFrame = MaxSpawnPerFrame;
            _voxelWorld.MaxTerrainGenPerFrame = MaxTerrainGenPerFrame;
            _voxelWorld.MaxMeshBuildPerFrame = MaxMeshBuildPerFrame;
            _voxelWorld.UseGreedyMeshing = UseGreedyMeshing;
            _voxelWorld.UseAsyncTerrain = UseAsyncTerrain;
            _voxelWorld.UseAsyncMesh = UseAsyncMesh;
            _voxelWorld.MaxChunkRadius = MaxChunkRadius;
        }

        var editorPreview = GetNodeOrNull<Node3D>("EditorPreview");
        if (editorPreview != null)
            editorPreview.QueueFree();
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
