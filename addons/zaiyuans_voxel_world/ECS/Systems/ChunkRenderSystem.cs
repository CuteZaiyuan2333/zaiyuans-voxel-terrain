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
                var originV = new Vector3(origin.X, origin.Y, origin.Z);
                mi.Visible = IsChunkAabbInFrustum(ctx.Camera, originV);
            }
            else
            {
                mi.Visible = true;
            }
        }
    }

    /// <summary>
    /// 用区块的 AABB 与相机视锥做相交检测，避免仅用中心点判断时在区块边缘错误剔除。
    /// </summary>
    private static bool IsChunkAabbInFrustum(Camera3D camera, Vector3 chunkOrigin)
    {
        var size = new Vector3(VoxelConstants.ChunkSize, VoxelConstants.ChunkSize, VoxelConstants.ChunkSize);
        var aabb = new Aabb(chunkOrigin, size);
        var planes = camera.GetFrustum();
        foreach (Variant planeVar in planes)
        {
            var plane = planeVar.As<Plane>();
            var n = plane.Normal;
            // 视锥平面法线朝外，视锥内为负半空间。取 AABB 在法线“负方向”上最远的顶点（最靠近视锥内的点）。
            // 若该点都在平面正侧 (DistanceTo > 0)，说明整块在视锥外，应剔除。
            var p = new Vector3(
                n.X <= 0 ? aabb.End.X : aabb.Position.X,
                n.Y <= 0 ? aabb.End.Y : aabb.Position.Y,
                n.Z <= 0 ? aabb.End.Z : aabb.Position.Z);
            if (plane.DistanceTo(p) > 0)
                return false;
        }
        return true;
    }
}
