using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.ECS.Components;
using ZaiyuansVoxelWorld.Rendering;

namespace ZaiyuansVoxelWorld.ECS.Systems;

public sealed class ChunkRenderSystem : IVoxelSystem
{
    public void Run(VoxelEcsWorld world, double delta, EcsRunContext ctx)
    {
        if (ctx.ChunkParent == null) return;

        foreach (var e in world.AllEntities())
        {
            if (world.GetState(e) != ChunkState.Ready) continue;

            var meshComp = world.GetMesh(e);
            if (meshComp.IsEmpty) continue;

            if (!ctx.ChunkMeshInstances.TryGetValue(e, out var mi))
            {
                mi = ChunkRenderer.CreateMeshInstance();
                mi.Name = $"Chunk_{e.ChunkPos.X}_{e.ChunkPos.Y}_{e.ChunkPos.Z}";
                ctx.ChunkParent.AddChild(mi);
                ctx.ChunkMeshInstances[e] = mi;
                ctx.PendingChunkLoaded.Add(e.ChunkPos);
            }

            var origin = VoxelConstants.ChunkToWorldOrigin(e.ChunkPos);
            mi.Position = new Vector3(origin.X, origin.Y, origin.Z);
            mi.Mesh = meshComp.Mesh;

            if (ctx.Camera != null)
            {
                var center = new Vector3(origin.X + 16, origin.Y + 16, origin.Z + 16);
                mi.Visible = ctx.Camera.IsPositionInFrustum(center);
            }
            else
            {
                mi.Visible = true;
            }
        }
    }
}
