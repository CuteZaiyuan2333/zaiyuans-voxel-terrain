using System.Collections.Generic;
using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.Data;
using ZaiyuansVoxelWorld.ECS;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld;

/// <summary>
/// Singleton / service: global voxel world config and SetBlock/GetBlock API.
/// </summary>
public partial class VoxelWorld : Node
{
    public static VoxelWorld Singleton { get; private set; }

    [Signal] public delegate void ChunkLoadedEventHandler(int cx, int cy, int cz);
    [Signal] public delegate void ChunkUnloadedEventHandler(int cx, int cy, int cz);
    [Signal] public delegate void BlockChangedEventHandler(int wx, int wy, int wz, int oldId, int newId);

    [Export] public int Seed { get; set; } = 12345;
    [Export] public int ViewDistanceInChunks { get; set; } = 4;
    [Export] public string SaveDirectory { get; set; } = "";
    [Export] public int MaxSpawnPerFrame { get; set; } = 2;
    [Export] public int MaxTerrainGenPerFrame { get; set; } = 2;
    [Export] public int MaxMeshBuildPerFrame { get; set; } = 4;
    [Export] public bool UseGreedyMeshing { get; set; } = true;
    [Export] public bool UseAsyncTerrain { get; set; } = false;
    [Export] public bool UseAsyncMesh { get; set; } = false;
    /// <summary>Max chunk radius from origin (0 = no limit). GetBlock returns Air and SetBlock returns false outside.</summary>
    [Export] public int MaxChunkRadius { get; set; } = 0;
    /// <summary>Optional block list for generators (look up by name). When null, generators use built-in BlockId.</summary>
    [Export] public BlockLibrary BlockLibrary { get; set; }
    public Vector3 ObserverPosition { get; set; }
    public IChunkGenerator Generator { get; set; }

    internal VoxelEcsWorld EcsWorld { get; private set; }
    internal EcsRunContext RunContext { get; private set; }

    public override void _Ready()
    {
        Singleton = this;
        EcsWorld = new VoxelEcsWorld();
        RunContext = new EcsRunContext
        {
            Seed = Seed,
            ViewDistanceInChunks = ViewDistanceInChunks,
            SaveDirectory = SaveDirectory ?? "",
            Generator = Generator ?? new DefaultTerrainGenerator(),
        };
        RunContext.ChunkMeshInstances.Clear();
    }

    public override void _ExitTree()
    {
        if (Singleton == this)
            Singleton = null;
    }

    /// <summary>Update context from current config and run ECS. Call from VoxelTerrain._Process.</summary>
    /// <param name="camera">Optional. Used for frustum culling; if null, all loaded chunks are visible.</param>
    public void RunEcs(double delta, Node3D chunkParent, Camera3D camera = null)
    {
        if (EcsWorld == null || RunContext == null) return;
        RunContext.Camera = camera;
        if (chunkParent != RunContext.ChunkParent)
        {
            foreach (var kv in RunContext.ChunkMeshInstances)
                kv.Value.QueueFree();
            RunContext.ChunkMeshInstances.Clear();
            RunContext.ChunkParent = chunkParent;
        }
        RunContext.ObserverPosition = ObserverPosition;
        RunContext.ViewDistanceInChunks = ViewDistanceInChunks;
        RunContext.Seed = Seed;
        RunContext.SaveDirectory = SaveDirectory ?? "";
        RunContext.MaxSpawnPerFrame = MaxSpawnPerFrame;
        RunContext.MaxTerrainGenPerFrame = MaxTerrainGenPerFrame;
        RunContext.MaxMeshBuildPerFrame = MaxMeshBuildPerFrame;
        RunContext.UseGreedyMeshing = UseGreedyMeshing;
        RunContext.UseAsyncTerrain = UseAsyncTerrain;
        RunContext.UseAsyncMesh = UseAsyncMesh;
        RunContext.Generator = Generator ?? new DefaultTerrainGenerator();
        RunContext.BlockLibrary = BlockLibrary;
        EcsWorld.Run(delta, RunContext);

        foreach (var p in RunContext.PendingChunkLoaded)
        {
            EmitSignal(SignalName.ChunkLoaded, p.X, p.Y, p.Z);
        }
        RunContext.PendingChunkLoaded.Clear();
        foreach (var p in RunContext.PendingChunkUnloaded)
        {
            EmitSignal(SignalName.ChunkUnloaded, p.X, p.Y, p.Z);
        }
        RunContext.PendingChunkUnloaded.Clear();
        foreach (var (worldPos, oldId, newId) in RunContext.PendingBlockChanged)
        {
            EmitSignal(SignalName.BlockChanged, worldPos.X, worldPos.Y, worldPos.Z, (int)oldId, (int)newId);
        }
        RunContext.PendingBlockChanged.Clear();
    }

    /// <summary>Set block at world position. Returns true if written or queued; false if world null or outside MaxChunkRadius.</summary>
    public bool SetBlock(Vector3I worldPos, BlockId blockId)
    {
        if (EcsWorld == null) return false;
        int cx = VoxelConstants.WorldToChunkCoord(worldPos.X);
        int cy = VoxelConstants.WorldToChunkCoord(worldPos.Y);
        int cz = VoxelConstants.WorldToChunkCoord(worldPos.Z);
        if (MaxChunkRadius > 0)
        {
            if (Mathf.Abs(cx) > MaxChunkRadius || Mathf.Abs(cy) > MaxChunkRadius || Mathf.Abs(cz) > MaxChunkRadius)
                return false;
        }
        var chunkPos = new Vector3I(cx, cy, cz);
        var e = new ChunkEntity(chunkPos);
        byte newId = (byte)blockId;
        if (!EcsWorld.HasEntity(e))
        {
            if (RunContext != null)
                RunContext.PendingBlocks.Add((worldPos, newId));
            return true;
        }
        byte oldId = EcsWorld.GetVoxelData(e).Get(
            VoxelConstants.WorldToLocalCoord(worldPos.X),
            VoxelConstants.WorldToLocalCoord(worldPos.Y),
            VoxelConstants.WorldToLocalCoord(worldPos.Z));
        var neighbors = new Godot.Vector3I[6];
        VoxelConstants.GetNeighborChunkPositions(chunkPos, neighbors);
        EcsWorld.SetState(e, ChunkState.Dirty);
        if (RunContext != null)
        {
            RunContext.DirtyChunksForSave.Add(e);
            if (oldId != newId)
                RunContext.PendingBlockChanged.Add((worldPos, oldId, newId));
        }
        for (int i = 0; i < 6; i++)
        {
            var ne = new ChunkEntity(neighbors[i]);
            if (EcsWorld.HasEntity(ne))
                EcsWorld.SetState(ne, ChunkState.Dirty);
        }
        int lx = VoxelConstants.WorldToLocalCoord(worldPos.X);
        int ly = VoxelConstants.WorldToLocalCoord(worldPos.Y);
        int lz = VoxelConstants.WorldToLocalCoord(worldPos.Z);
        EcsWorld.GetVoxelData(e).Set(lx, ly, lz, newId);
        return true;
    }

    /// <summary>Get block at world position. Returns Air if chunk not loaded or outside MaxChunkRadius.</summary>
    public BlockId GetBlock(Vector3I worldPos)
    {
        if (EcsWorld == null) return BlockId.Air;
        int cx = VoxelConstants.WorldToChunkCoord(worldPos.X);
        int cy = VoxelConstants.WorldToChunkCoord(worldPos.Y);
        int cz = VoxelConstants.WorldToChunkCoord(worldPos.Z);
        if (MaxChunkRadius > 0)
        {
            if (Mathf.Abs(cx) > MaxChunkRadius || Mathf.Abs(cy) > MaxChunkRadius || Mathf.Abs(cz) > MaxChunkRadius)
                return BlockId.Air;
        }
        if (!EcsWorld.TryGetBlockAtWorld(worldPos.X, worldPos.Y, worldPos.Z, out byte id))
            return BlockId.Air;
        return (BlockId)id;
    }
}
