using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.ECS;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld.ECS.Systems;

public sealed class ChunkTerrainGenSystem : IVoxelSystem
{
    public void Run(VoxelEcsWorld world, double delta, EcsRunContext ctx)
    {
        if (ctx.Generator == null) return;

        while (AsyncChunkJobs.TryDequeueTerrain(out var result))
        {
            ctx.TerrainInFlightCount--;
            ctx.TerrainSubmitted.Remove(result.Entity);
            if (result.Data == null) continue; // 失败结果，保持 Generating 下一帧重试
            if (!world.HasEntity(result.Entity)) continue;
            world.GetVoxelData(result.Entity).SetFrom(result.Data);
            world.SetState(result.Entity, ChunkState.Dirty);
            world.ApplyPendingBlocksForChunk(ctx, result.Entity);
            MarkNeighborsDirty(world, result.Entity);
        }

        int budget = ctx.MaxTerrainGenPerFrame <= 0 ? int.MaxValue : ctx.MaxTerrainGenPerFrame;

        if (ctx.UseAsyncTerrain)
        {
            var generating = CollectGeneratingSorted(world, ctx);
            foreach (var e in generating)
            {
                if (budget <= 0 || ctx.TerrainInFlightCount >= AsyncChunkJobs.MaxTerrainInFlight) break;
                if (ctx.TerrainSubmitted.Contains(e)) continue;

                var pos = world.GetPosition(e);
                AsyncChunkJobs.StartTerrainJob(e, pos.Value, ctx.Seed, ctx.Generator, ctx.BlockLibrary);
                ctx.TerrainSubmitted.Add(e);
                ctx.TerrainInFlightCount++;
                budget--;
            }
            return;
        }

        var syncGenerating = CollectGeneratingSorted(world, ctx);
        foreach (var e in syncGenerating)
        {
            if (budget <= 0) break;

            var data = world.GetVoxelData(e);
            var pos = world.GetPosition(e);
            ctx.Generator.Generate(pos.Value, data, ctx.Seed, ctx.BlockLibrary);
            world.SetState(e, ChunkState.Dirty);
            world.ApplyPendingBlocksForChunk(ctx, e);
            MarkNeighborsDirty(world, e);
            budget--;
        }
    }

    /// <summary>按与观察者距离排序的 Generating 列表，避免字典迭代顺序导致部分区块长期得不到生成。</summary>
    private static System.Collections.Generic.List<ChunkEntity> CollectGeneratingSorted(VoxelEcsWorld world, EcsRunContext ctx)
    {
        const int S = VoxelConstants.ChunkSize;
        var list = new System.Collections.Generic.List<ChunkEntity>();
        foreach (var e in world.AllEntities())
        {
            if (world.GetState(e) != ChunkState.Generating) continue;
            list.Add(e);
        }
        list.Sort((a, b) =>
        {
            var pa = world.GetPosition(a).Value;
            var pb = world.GetPosition(b).Value;
            float da = (ctx.ObserverPosition - new Vector3(pa.X * S + S * 0.5f, pa.Y * S + S * 0.5f, pa.Z * S + S * 0.5f)).LengthSquared();
            float db = (ctx.ObserverPosition - new Vector3(pb.X * S + S * 0.5f, pb.Y * S + S * 0.5f, pb.Z * S + S * 0.5f)).LengthSquared();
            return da.CompareTo(db);
        });
        return list;
    }

    /// <summary>
    /// 邻块地形刚填满时，已存在的邻块可能已用“空邻块”建过网格，需标 Dirty 重算以剔除边界内的面。
    /// </summary>
    private static void MarkNeighborsDirty(VoxelEcsWorld world, ChunkEntity e)
    {
        var neighbors = new Vector3I[6];
        VoxelConstants.GetNeighborChunkPositions(e.ChunkPos, neighbors);
        foreach (var n in neighbors)
        {
            var ne = new ChunkEntity(n);
            if (world.HasEntity(ne))
                world.SetState(ne, ChunkState.Dirty);
        }
    }
}
