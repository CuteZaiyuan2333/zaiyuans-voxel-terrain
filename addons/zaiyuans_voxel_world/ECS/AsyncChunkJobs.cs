using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.Data;
using ZaiyuansVoxelWorld.ECS.Components;
using ZaiyuansVoxelWorld.Rendering;

namespace ZaiyuansVoxelWorld.ECS;

/// <summary>Result of async terrain generation; apply on main thread.</summary>
public sealed class TerrainGenResult
{
    public ChunkEntity Entity { get; set; }
    public byte[] Data { get; set; }
}

/// <summary>Result of async mesh build; apply on main thread (create ArrayMesh from arrays).</summary>
public sealed class MeshBuildResult
{
    public ChunkEntity Entity { get; set; }
    public Vector3[] Vertices { get; set; }
    public Vector3[] Normals { get; set; }
    public Vector2[] UVs { get; set; }
}

/// <summary>Runs terrain and mesh jobs on thread pool; main thread drains completed queues.</summary>
public static class AsyncChunkJobs
{
    public const int MaxTerrainInFlight = 2;
    public const int MaxMeshInFlight = 2;

    private static readonly ConcurrentQueue<TerrainGenResult> CompletedTerrain = new();
    private static readonly ConcurrentQueue<MeshBuildResult> CompletedMesh = new();

    public static void EnqueueCompletedTerrain(TerrainGenResult r) => CompletedTerrain.Enqueue(r);
    public static bool TryDequeueTerrain(out TerrainGenResult r) => CompletedTerrain.TryDequeue(out r);
    public static void EnqueueCompletedMesh(MeshBuildResult r) => CompletedMesh.Enqueue(r);
    public static bool TryDequeueMesh(out MeshBuildResult r) => CompletedMesh.TryDequeue(out r);

    public static void StartTerrainJob(ChunkEntity e, Vector3I chunkPos, int seed, IChunkGenerator generator)
    {
        Task.Run(() =>
        {
            try
            {
                var data = new VoxelData();
                generator.Generate(chunkPos, data, seed);
                var bytes = new byte[VoxelConstants.ChunkVolume];
                data.AsSpan().Slice(0, VoxelConstants.ChunkVolume).CopyTo(bytes);
                EnqueueCompletedTerrain(new TerrainGenResult { Entity = e, Data = bytes });
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[AsyncChunkJobs] Terrain gen failed for chunk {chunkPos}: {ex.Message}");
            }
        });
    }

    public static void StartMeshJob(ChunkEntity e, Vector3I chunkPos, ChunkMeshSnapshot snapshot, bool useGreedy)
    {
        Task.Run(() =>
        {
            try
            {
                var (v, n, u) = ChunkMesher.BuildMeshDataFromSnapshot(chunkPos, snapshot, useGreedy);
                if (v != null && v.Length > 0)
                    EnqueueCompletedMesh(new MeshBuildResult { Entity = e, Vertices = v, Normals = n, UVs = u });
                else
                    EnqueueCompletedMesh(new MeshBuildResult { Entity = e, Vertices = Array.Empty<Vector3>(), Normals = Array.Empty<Vector3>(), UVs = Array.Empty<Vector2>() });
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[AsyncChunkJobs] Mesh build failed for chunk {chunkPos}: {ex.Message}");
            }
        });
    }
}
