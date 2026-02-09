using System.Collections.Generic;
using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.ECS.Components;
using ZaiyuansVoxelWorld.ECS.Systems;

namespace ZaiyuansVoxelWorld.ECS;

public sealed class VoxelEcsWorld
{
    private readonly Dictionary<ChunkEntity, ChunkPosition> _positions = new();
    private readonly Dictionary<ChunkEntity, VoxelData> _voxelData = new();
    private readonly Dictionary<ChunkEntity, ChunkMesh> _meshes = new();
    private readonly Dictionary<ChunkEntity, ChunkState> _states = new();
    private readonly List<IVoxelSystem> _systems = new();

    public IReadOnlyDictionary<ChunkEntity, ChunkPosition> Positions => _positions;
    public IReadOnlyDictionary<ChunkEntity, VoxelData> VoxelData => _voxelData;
    public IReadOnlyDictionary<ChunkEntity, ChunkMesh> Meshes => _meshes;
    public IReadOnlyDictionary<ChunkEntity, ChunkState> States => _states;

    public VoxelEcsWorld()
    {
        _systems.Add(new ChunkSpawnSystem());
        _systems.Add(new ChunkTerrainGenSystem());
        _systems.Add(new ChunkMeshSystem());
        _systems.Add(new ChunkRenderSystem());
        _systems.Add(new ChunkUnloadSystem());
    }

    public void Run(double delta, EcsRunContext ctx)
    {
        foreach (var system in _systems)
            system.Run(this, delta, ctx);
    }

    public bool HasEntity(ChunkEntity e) => _states.ContainsKey(e);

    public ChunkState GetState(ChunkEntity e) => _states.TryGetValue(e, out var s) ? s : ChunkState.Empty;
    public void SetState(ChunkEntity e, ChunkState s) => _states[e] = s;

    public ChunkPosition GetPosition(ChunkEntity e) => _positions[e];
    public VoxelData GetVoxelData(ChunkEntity e) => _voxelData[e];
    public ChunkMesh GetMesh(ChunkEntity e) => _meshes[e];

    public void AddEntity(ChunkEntity e, ChunkPosition pos, VoxelData data, ChunkMesh mesh, ChunkState state)
    {
        _positions[e] = pos;
        _voxelData[e] = data;
        _meshes[e] = mesh;
        _states[e] = state;
    }

    public void RemoveEntity(ChunkEntity e)
    {
        _positions.Remove(e);
        _voxelData.Remove(e);
        _meshes.Remove(e);
        _states.Remove(e);
    }

    public IEnumerable<ChunkEntity> AllEntities()
    {
        foreach (var kv in _states)
            yield return kv.Key;
    }

    /// <summary>Apply pending block writes that fall in chunk e; mark chunk Dirty and dirty for save.</summary>
    public void ApplyPendingBlocksForChunk(EcsRunContext ctx, ChunkEntity e)
    {
        if (ctx?.PendingBlocks == null || !_voxelData.TryGetValue(e, out var data)) return;
        var list = ctx.PendingBlocks;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var (worldPos, blockId) = list[i];
            int cx = VoxelConstants.WorldToChunkCoord(worldPos.X);
            int cy = VoxelConstants.WorldToChunkCoord(worldPos.Y);
            int cz = VoxelConstants.WorldToChunkCoord(worldPos.Z);
            if (cx != e.ChunkPos.X || cy != e.ChunkPos.Y || cz != e.ChunkPos.Z) continue;
            int lx = VoxelConstants.WorldToLocalCoord(worldPos.X);
            int ly = VoxelConstants.WorldToLocalCoord(worldPos.Y);
            int lz = VoxelConstants.WorldToLocalCoord(worldPos.Z);
            data.Set(lx, ly, lz, blockId);
            list.RemoveAt(i);
            ctx.DirtyChunksForSave.Add(e);
            SetState(e, ChunkState.Dirty);
        }
    }

    /// <summary>Get block at world position. Returns 0 (Air) if chunk not loaded.</summary>
    public bool TryGetBlockAtWorld(int wx, int wy, int wz, out byte blockId)
    {
        int cx = VoxelConstants.WorldToChunkCoord(wx);
        int cy = VoxelConstants.WorldToChunkCoord(wy);
        int cz = VoxelConstants.WorldToChunkCoord(wz);
        var e = new ChunkEntity(new Vector3I(cx, cy, cz));
        if (!_voxelData.TryGetValue(e, out var data))
        {
            blockId = 0;
            return false;
        }
        int lx = VoxelConstants.WorldToLocalCoord(wx);
        int ly = VoxelConstants.WorldToLocalCoord(wy);
        int lz = VoxelConstants.WorldToLocalCoord(wz);
        blockId = data.Get(lx, ly, lz);
        return true;
    }
}
