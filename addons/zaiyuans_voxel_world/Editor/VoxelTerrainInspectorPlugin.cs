#if TOOLS
using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.Data;
using ZaiyuansVoxelWorld.ECS;
using ZaiyuansVoxelWorld.ECS.Components;
using ZaiyuansVoxelWorld.Rendering;

namespace ZaiyuansVoxelWorld.Editor;

public partial class VoxelTerrainInspectorPlugin : EditorInspectorPlugin
{
    public override bool _CanHandle(GodotObject @object)
    {
        return @object is VoxelTerrain;
    }

    public override void _ParseEnd(GodotObject @object)
    {
        if (@object is not VoxelTerrain terrain)
            return;

        var vbox = new VBoxContainer();
        var previewBtn = new Button { Text = "Preview Terrain" };
        var clearBtn = new Button { Text = "Clear Preview" };
        previewBtn.Pressed += () => RunPreview(terrain);
        clearBtn.Pressed += () => ClearPreview(terrain);
        vbox.AddChild(previewBtn);
        vbox.AddChild(clearBtn);
        AddCustomControl(vbox);
    }

    private static void ClearPreview(VoxelTerrain terrain)
    {
        var preview = terrain.GetNodeOrNull<Node3D>("EditorPreview");
        if (preview != null)
            preview.QueueFree();
    }

    private static void RunPreview(VoxelTerrain terrain)
    {
        ClearPreview(terrain);

        int seed = terrain.Seed;
        const int sizeX = 2, sizeY = 1, sizeZ = 2;

        var world = new VoxelEcsWorld();
        var ctx = new EcsRunContext { UseGreedyMeshing = true };
        var generator = new DefaultTerrainGenerator();
        var mesher = new ChunkMesher();

        for (int cz = 0; cz < sizeZ; cz++)
        for (int cy = 0; cy < sizeY; cy++)
        for (int cx = 0; cx < sizeX; cx++)
        {
            var chunkPos = new Vector3I(cx, cy, cz);
            var e = new ChunkEntity(chunkPos);
            var pos = new ChunkPosition(chunkPos);
            var data = new VoxelData();
            generator.Generate(chunkPos, data, seed, null);
            world.AddEntity(e, pos, data, new ChunkMesh(), ChunkState.Ready);
        }

        var previewRoot = new Node3D { Name = "EditorPreview" };
        terrain.AddChild(previewRoot);

        for (int cz = 0; cz < sizeZ; cz++)
        for (int cy = 0; cy < sizeY; cy++)
        for (int cx = 0; cx < sizeX; cx++)
        {
            var chunkPos = new Vector3I(cx, cy, cz);
            var e = new ChunkEntity(chunkPos);
            var data = world.GetVoxelData(e);
            var mesh = mesher.Build(chunkPos, data, world, ctx);
            if (mesh == null) continue;

            var mi = ChunkRenderer.CreateMeshInstance();
            mi.Mesh = mesh;
            mi.Name = $"Chunk_{cx}_{cy}_{cz}";
            var origin = VoxelConstants.ChunkToWorldOrigin(chunkPos);
            mi.Position = new Vector3(origin.X, origin.Y, origin.Z);
            previewRoot.AddChild(mi);
        }
    }
}
#endif
