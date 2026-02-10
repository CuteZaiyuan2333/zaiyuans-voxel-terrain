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
public partial class VoxelWorld : Node, IVoxelQuery
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
    [Export] public Resource GeneratorResource { get; set; }
    public Vector3 ObserverPosition { get; set; }
    public IChunkGenerator Generator { get; set; }

    internal VoxelEcsWorld EcsWorld { get; private set; }
    internal EcsRunContext RunContext { get; private set; }

    public override void _Ready()
    {
        Singleton = this;
        EcsWorld = new VoxelEcsWorld();

        if (Generator == null)
        {
            if (GeneratorResource != null)
            {
                Generator = new ScriptableVoxelGenerator { GeneratorResource = GeneratorResource };
            }
            else
            {
                Generator = new DefaultTerrainGenerator();
            }
        }
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

    /// <inheritdoc />
    public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out VoxelRaycastResult result)
    {
        result = default;
        if (EcsWorld == null || direction.LengthSquared() < 1e-10f) return false;
        Vector3 dir = direction.Normalized();
        int ix = Mathf.FloorToInt(origin.X);
        int iy = Mathf.FloorToInt(origin.Y);
        int iz = Mathf.FloorToInt(origin.Z);
        int stepX = dir.X >= 0 ? 1 : -1;
        int stepY = dir.Y >= 0 ? 1 : -1;
        int stepZ = dir.Z >= 0 ? 1 : -1;
        float tDeltaX = Mathf.Abs(dir.X) >= 1e-8f ? 1f / Mathf.Abs(dir.X) : float.MaxValue;
        float tDeltaY = Mathf.Abs(dir.Y) >= 1e-8f ? 1f / Mathf.Abs(dir.Y) : float.MaxValue;
        float tDeltaZ = Mathf.Abs(dir.Z) >= 1e-8f ? 1f / Mathf.Abs(dir.Z) : float.MaxValue;
        float tMaxX = dir.X >= 0 ? (ix + 1 - origin.X) * tDeltaX : (origin.X - ix) * tDeltaX;
        float tMaxY = dir.Y >= 0 ? (iy + 1 - origin.Y) * tDeltaY : (origin.Y - iy) * tDeltaY;
        float tMaxZ = dir.Z >= 0 ? (iz + 1 - origin.Z) * tDeltaZ : (origin.Z - iz) * tDeltaZ;
        float t = 0;
        Vector3 normal = Vector3.Zero;
        while (t < maxDistance)
        {
            var blockId = GetBlock(new Vector3I(ix, iy, iz));
            if (blockId != BlockId.Air)
            {
                Vector3 hitPos = origin + t * dir;
                Vector3 n = normal;
                if (n.LengthSquared() < 0.01f) // first voxel hit: normal = face most opposite to ray
                {
                    if (Mathf.Abs(dir.X) >= Mathf.Abs(dir.Y) && Mathf.Abs(dir.X) >= Mathf.Abs(dir.Z))
                        n = new Vector3(-Mathf.Sign(dir.X), 0, 0);
                    else if (Mathf.Abs(dir.Y) >= Mathf.Abs(dir.Z))
                        n = new Vector3(0, -Mathf.Sign(dir.Y), 0);
                    else
                        n = new Vector3(0, 0, -Mathf.Sign(dir.Z));
                }
                result = new VoxelRaycastResult(hitPos, n, new Vector3I(ix, iy, iz), blockId);
                return true;
            }
            float tNext;
            if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
            {
                tNext = tMaxX;
                normal = new Vector3(-stepX, 0, 0);
                ix += stepX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY <= tMaxZ)
            {
                tNext = tMaxY;
                normal = new Vector3(0, -stepY, 0);
                iy += stepY;
                tMaxY += tDeltaY;
            }
            else
            {
                tNext = tMaxZ;
                normal = new Vector3(0, 0, -stepZ);
                iz += stepZ;
                tMaxZ += tDeltaZ;
            }
            t = tNext;
        }
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<Aabb> GetCollidingBoxes(Aabb box)
    {
        var list = new List<Aabb>();
        if (EcsWorld == null) return list;
        int minX = Mathf.FloorToInt(box.Position.X);
        int minY = Mathf.FloorToInt(box.Position.Y);
        int minZ = Mathf.FloorToInt(box.Position.Z);
        int maxX = Mathf.FloorToInt(box.Position.X + box.Size.X);
        int maxY = Mathf.FloorToInt(box.Position.Y + box.Size.Y);
        int maxZ = Mathf.FloorToInt(box.Position.Z + box.Size.Z);
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            if (GetBlock(new Vector3I(x, y, z)) == BlockId.Air) continue;
            list.Add(new Aabb(new Vector3(x, y, z), Vector3.One));
        }
        return list;
    }
}
