using Godot;
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
            if (!world.HasEntity(result.Entity)) continue;
            world.GetVoxelData(result.Entity).SetFrom(result.Data);
            world.SetState(result.Entity, ChunkState.Dirty);
            world.ApplyPendingBlocksForChunk(ctx, result.Entity);
        }

        int budget = ctx.MaxTerrainGenPerFrame <= 0 ? int.MaxValue : ctx.MaxTerrainGenPerFrame;

        if (ctx.UseAsyncTerrain)
        {
            foreach (var e in world.AllEntities())
            {
                if (budget <= 0 || ctx.TerrainInFlightCount >= AsyncChunkJobs.MaxTerrainInFlight) break;
                if (world.GetState(e) != ChunkState.Generating) continue;
                if (ctx.TerrainSubmitted.Contains(e)) continue;

                var pos = world.GetPosition(e);
                AsyncChunkJobs.StartTerrainJob(e, pos.Value, ctx.Seed, ctx.Generator);
                ctx.TerrainSubmitted.Add(e);
                ctx.TerrainInFlightCount++;
                budget--;
            }
            return;
        }

        foreach (var e in world.AllEntities())
        {
            if (budget <= 0) break;
            if (world.GetState(e) != ChunkState.Generating) continue;

            var data = world.GetVoxelData(e);
            var pos = world.GetPosition(e);
            ctx.Generator.Generate(pos.Value, data, ctx.Seed);
            world.SetState(e, ChunkState.Dirty);
            world.ApplyPendingBlocksForChunk(ctx, e);
            budget--;
        }
    }
}
