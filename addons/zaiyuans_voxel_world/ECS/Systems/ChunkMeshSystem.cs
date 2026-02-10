using System.Collections.Generic;
using Godot;
using ZaiyuansVoxelWorld.Core;
using ZaiyuansVoxelWorld.ECS;
using ZaiyuansVoxelWorld.ECS.Components;
using ZaiyuansVoxelWorld.Rendering;

namespace ZaiyuansVoxelWorld.ECS.Systems;

public sealed class ChunkMeshSystem : IVoxelSystem
{
    private ChunkMesher _mesher;

    public ChunkMeshSystem()
    {
        _mesher = new ChunkMesher();
    }

    public void Run(VoxelEcsWorld world, double delta, EcsRunContext ctx)
    {
        // Drain completed async mesh results first (main thread only: create ArrayMesh and apply)
        while (AsyncChunkJobs.TryDequeueMesh(out var result))
        {
            ctx.MeshInFlightCount--;
            ctx.MeshSubmitted.Remove(result.Entity);
            if (!world.HasEntity(result.Entity)) continue;

            var meshComp = world.GetMesh(result.Entity);
            Mesh mesh = null;
            int vertexCount = 0;
            if (result.Vertices != null && result.Vertices.Length > 0)
            {
                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = result.Vertices;
                arrays[(int)Mesh.ArrayType.Normal] = result.Normals ?? System.Array.Empty<Vector3>();
                arrays[(int)Mesh.ArrayType.TexUV] = result.UVs ?? System.Array.Empty<Vector2>();
                var arrMesh = new ArrayMesh();
                arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                mesh = arrMesh;
                vertexCount = result.Vertices.Length;
            }
            meshComp.Mesh = mesh;
            meshComp.VertexCount = vertexCount;
            world.SetState(result.Entity, ChunkState.Ready);
        }

        int budget = ctx.MaxMeshBuildPerFrame <= 0 ? int.MaxValue : ctx.MaxMeshBuildPerFrame;

        if (ctx.UseAsyncMesh)
        {
            var dirtySorted = CollectDirtySorted(world, ctx);
            foreach (var e in dirtySorted)
            {
                if (budget <= 0 || ctx.MeshInFlightCount >= AsyncChunkJobs.MaxMeshInFlight) break;
                if (ctx.MeshSubmitted.Contains(e)) continue;

                var pos = world.GetPosition(e);
                var snapshot = ChunkMeshSnapshot.CreateFromWorld(world, e);
                if (snapshot == null) continue;

                AsyncChunkJobs.StartMeshJob(e, pos.Value, snapshot, ctx.UseGreedyMeshing);
                ctx.MeshSubmitted.Add(e);
                ctx.MeshInFlightCount++;
                budget--;
            }
            foreach (var e in dirtySorted)
            {
                if (budget <= 0) break;
                if (ctx.MeshSubmitted.Contains(e)) continue;

                var data = world.GetVoxelData(e);
                var pos = world.GetPosition(e);
                var meshComp = world.GetMesh(e);

                Mesh mesh = _mesher.Build(pos.Value, data, world, ctx);
                meshComp.Mesh = mesh;
                meshComp.VertexCount = mesh != null ? _mesher.LastVertexCount : 0;
                world.SetState(e, ChunkState.Ready);
                budget--;
            }
            return;
        }

        var syncDirty = CollectDirtySorted(world, ctx);
        foreach (var e in syncDirty)
        {
            if (budget <= 0) break;

            var data = world.GetVoxelData(e);
            var pos = world.GetPosition(e);
            var meshComp = world.GetMesh(e);

            Mesh mesh = _mesher.Build(pos.Value, data, world, ctx);
            meshComp.Mesh = mesh;
            meshComp.VertexCount = mesh != null ? _mesher.LastVertexCount : 0;
            world.SetState(e, ChunkState.Ready);
            budget--;
        }
    }

    private static List<ChunkEntity> CollectDirtySorted(VoxelEcsWorld world, EcsRunContext ctx)
    {
        const int S = VoxelConstants.ChunkSize;
        var list = new List<ChunkEntity>();
        foreach (var e in world.AllEntities())
        {
            if (world.GetState(e) != ChunkState.Dirty) continue;
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
}
