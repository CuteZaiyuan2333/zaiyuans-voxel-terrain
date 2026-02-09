using Godot;

namespace ZaiyuansVoxelWorld;

[Tool]
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

#if TOOLS
    [ExportGroup("Editor")]
    [Export] public bool PreviewTerrain
    {
        get => _previewTerrain;
        set
        {
            _previewTerrain = value;
            if (value)
            {
                _previewTerrain = false;
                CallDeferred(nameof(EditorRunPreviewDeferred));
            }
        }
    }

    [Export] public bool ClearPreview
    {
        get => _clearPreview;
        set
        {
            _clearPreview = value;
            if (value)
            {
                _clearPreview = false;
                CallDeferred(nameof(EditorClearPreviewDeferred));
            }
        }
    }

    private bool _previewTerrain;
    private bool _clearPreview;

    private void EditorRunPreviewDeferred() => EditorRunPreview();
    private void EditorClearPreviewDeferred() => EditorClearPreview();
#endif

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

#if TOOLS
    private void EditorClearPreview()
    {
        var preview = GetNodeOrNull<Node3D>("EditorPreview");
        if (preview != null)
            preview.QueueFree();
    }

    private void EditorRunPreview()
    {
        try
        {
            EditorClearPreview();

            const int sizeX = 2, sizeY = 1, sizeZ = 2;
            int seed = Seed;

            var world = new ECS.VoxelEcsWorld();
            var ctx = new ECS.EcsRunContext { UseGreedyMeshing = true };
            var generator = new Data.DefaultTerrainGenerator();
            var mesher = new Rendering.ChunkMesher();

            for (int cz = 0; cz < sizeZ; cz++)
            for (int cy = 0; cy < sizeY; cy++)
            for (int cx = 0; cx < sizeX; cx++)
            {
                var chunkPos = new Vector3I(cx, cy, cz);
                var e = new ECS.ChunkEntity(chunkPos);
                var pos = new ECS.Components.ChunkPosition(chunkPos);
                var data = new ECS.Components.VoxelData();
                generator.Generate(chunkPos, data, seed, null);
                world.AddEntity(e, pos, data, new ECS.Components.ChunkMesh(), ECS.Components.ChunkState.Ready);
            }

            var previewRoot = new Node3D { Name = "EditorPreview" };
            AddChild(previewRoot);

            for (int cz = 0; cz < sizeZ; cz++)
            for (int cy = 0; cy < sizeY; cy++)
            for (int cx = 0; cx < sizeX; cx++)
            {
                var chunkPos = new Vector3I(cx, cy, cz);
                var e = new ECS.ChunkEntity(chunkPos);
                var data = world.GetVoxelData(e);
                var mesh = mesher.Build(chunkPos, data, world, ctx);
                if (mesh == null) continue;

                var mi = Rendering.ChunkRenderer.CreateMeshInstance();
                mi.Mesh = mesh;
                mi.Name = $"Chunk_{cx}_{cy}_{cz}";
                var origin = Core.VoxelConstants.ChunkToWorldOrigin(chunkPos);
                mi.Position = new Vector3(origin.X, origin.Y, origin.Z);
                previewRoot.AddChild(mi);
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr("[VoxelTerrain] Editor preview failed: ", ex.Message, "\n", ex.StackTrace);
        }
    }
#endif
}
