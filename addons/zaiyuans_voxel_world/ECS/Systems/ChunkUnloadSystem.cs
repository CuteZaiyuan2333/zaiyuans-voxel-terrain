using System.Collections.Generic;
using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.Data;
using ZaiyuansVoxelWorld.ECS.Components;

namespace ZaiyuansVoxelWorld.ECS.Systems;

public sealed class ChunkUnloadSystem : IVoxelSystem
{
	public void Run(VoxelEcsWorld world, double delta, EcsRunContext ctx)
	{
		int cx0 = VoxelConstants.WorldToChunkCoord((int)ctx.ObserverPosition.X);
		int cy0 = VoxelConstants.WorldToChunkCoord((int)ctx.ObserverPosition.Y);
		int cz0 = VoxelConstants.WorldToChunkCoord((int)ctx.ObserverPosition.Z);
		int r = ctx.ViewDistanceInChunks;

		var toRemove = new List<ChunkEntity>();
		foreach (var e in world.AllEntities())
		{
			int dx = Mathf.Abs(e.ChunkPos.X - cx0);
			int dy = Mathf.Abs(e.ChunkPos.Y - cy0);
			int dz = Mathf.Abs(e.ChunkPos.Z - cz0);
			if (dx <= r && dy <= r && dz <= r) continue;
			toRemove.Add(e);
		}

		foreach (var e in toRemove)
		{
			ctx.PendingChunkUnloaded.Add(e.ChunkPos);
			if (!string.IsNullOrEmpty(ctx.SaveDirectory) && ctx.DirtyChunksForSave.Remove(e))
			{
				var data = world.GetVoxelData(e);
				ChunkStorage.Save(ctx.SaveDirectory, e.ChunkPos, data.AsSpan());
			}
			if (ctx.ChunkMeshInstances.TryGetValue(e, out var mi))
			{
				mi.QueueFree();
				ctx.ChunkMeshInstances.Remove(e);
			}
			world.RemoveEntity(e);
		}
	}
}
