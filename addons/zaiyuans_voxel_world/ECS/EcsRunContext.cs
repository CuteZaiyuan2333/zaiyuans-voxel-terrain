using System.Collections.Generic;
using Godot;
using ZaiyuansVoxelWorld.Data;

namespace ZaiyuansVoxelWorld.ECS;

public sealed class EcsRunContext
{
    public Vector3 ObserverPosition { get; set; }
    public int ViewDistanceInChunks { get; set; } = 4;
    public int Seed { get; set; }
    public IChunkGenerator Generator { get; set; }
    public Node3D ChunkParent { get; set; }
    /// <summary>Optional. When set, chunks outside camera frustum are culled (Visible = false).</summary>
    public Camera3D Camera { get; set; }
    public string SaveDirectory { get; set; }
    public int MaxSpawnPerFrame { get; set; } = 2;
    public int MaxTerrainGenPerFrame { get; set; } = 2;
    public int MaxMeshBuildPerFrame { get; set; } = 4;
    public bool UseGreedyMeshing { get; set; } = true;
    public bool UseAsyncTerrain { get; set; } = false;
    public bool UseAsyncMesh { get; set; } = false;
    public int TerrainInFlightCount { get; set; }
    public int MeshInFlightCount { get; set; }
    public HashSet<ChunkEntity> TerrainSubmitted { get; } = new();
    public HashSet<ChunkEntity> MeshSubmitted { get; } = new();
    public Dictionary<ChunkEntity, MeshInstance3D> ChunkMeshInstances { get; } = new();
    public HashSet<ChunkEntity> DirtyChunksForSave { get; } = new();
    public List<(Vector3I WorldPos, byte BlockId)> PendingBlocks { get; } = new();
    /// <summary>Filled by systems; drain and emit from VoxelWorld after Run.</summary>
    public List<Vector3I> PendingChunkLoaded { get; } = new();
    public List<Vector3I> PendingChunkUnloaded { get; } = new();
    public List<(Vector3I WorldPos, byte OldId, byte NewId)> PendingBlockChanged { get; } = new();
}
