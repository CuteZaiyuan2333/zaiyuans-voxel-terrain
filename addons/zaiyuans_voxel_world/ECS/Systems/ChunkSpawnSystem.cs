using System.Collections.Generic;
using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.Data;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld.ECS.Systems;

public sealed class ChunkSpawnSystem : IVoxelSystem
{
    private static readonly byte[] LoadBuffer = new byte[VoxelConstants.ChunkVolume];

    public void Run(VoxelEcsWorld world, double delta, EcsRunContext ctx)
    {
        int cx0 = VoxelConstants.WorldToChunkCoord((int)ctx.ObserverPosition.X);
        int cy0 = VoxelConstants.WorldToChunkCoord((int)ctx.ObserverPosition.Y);
        int cz0 = VoxelConstants.WorldToChunkCoord((int)ctx.ObserverPosition.Z);
        int r = ctx.ViewDistanceInChunks;

        var shouldExist = new HashSet<ChunkEntity>();
        for (int cz = cz0 - r; cz <= cz0 + r; cz++)
        for (int cy = cy0 - r; cy <= cy0 + r; cy++)
        for (int cx = cx0 - r; cx <= cx0 + r; cx++)
            shouldExist.Add(new ChunkEntity(new Vector3I(cx, cy, cz)));

        var toSpawn = new List<ChunkEntity>();
        foreach (var e in shouldExist)
        {
            if (!world.HasEntity(e))
                toSpawn.Add(e);
        }
        toSpawn.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.ChunkPos.X - cx0) + Mathf.Abs(a.ChunkPos.Y - cy0) + Mathf.Abs(a.ChunkPos.Z - cz0);
            int db = Mathf.Abs(b.ChunkPos.X - cx0) + Mathf.Abs(b.ChunkPos.Y - cy0) + Mathf.Abs(b.ChunkPos.Z - cz0);
            return da.CompareTo(db);
        });
        int spawnBudget = ctx.MaxSpawnPerFrame <= 0 ? int.MaxValue : ctx.MaxSpawnPerFrame;
        foreach (var e in toSpawn)
        {
            if (spawnBudget-- <= 0) break;
            var pos = new ChunkPosition(new Vector3I(e.ChunkPos.X, e.ChunkPos.Y, e.ChunkPos.Z));
            var data = new VoxelData();
            if (!string.IsNullOrEmpty(ctx.SaveDirectory) && ChunkStorage.TryLoad(ctx.SaveDirectory, e.ChunkPos, LoadBuffer))
            {
                data.SetFrom(LoadBuffer);
                world.AddEntity(e, pos, data, new ChunkMesh(), ChunkState.Dirty);
                world.ApplyPendingBlocksForChunk(ctx, e);
            }
            else
            {
                world.AddEntity(e, pos, data, new ChunkMesh(), ChunkState.Generating);
            }
        }
    }
}
