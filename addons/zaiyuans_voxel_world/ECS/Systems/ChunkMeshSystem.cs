using Godot;
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
            // Submit async mesh jobs for Dirty chunks (budget and in-flight limit)
            foreach (var e in world.AllEntities())
            {
                if (budget <= 0 || ctx.MeshInFlightCount >= AsyncChunkJobs.MaxMeshInFlight) break;
                if (world.GetState(e) != ChunkState.Dirty) continue;
                if (ctx.MeshSubmitted.Contains(e)) continue;

                var pos = world.GetPosition(e);
                var snapshot = ChunkMeshSnapshot.CreateFromWorld(world, e);
                if (snapshot == null) continue;

                AsyncChunkJobs.StartMeshJob(e, pos.Value, snapshot, ctx.UseGreedyMeshing);
                ctx.MeshSubmitted.Add(e);
                ctx.MeshInFlightCount++;
                budget--;
            }
            // Remaining budget: sync build for Dirty chunks not yet submitted
            foreach (var e in world.AllEntities())
            {
                if (budget <= 0) break;
                if (world.GetState(e) != ChunkState.Dirty) continue;
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

        // Sync-only path
        foreach (var e in world.AllEntities())
        {
            if (budget <= 0) break;
            if (world.GetState(e) != ChunkState.Dirty) continue;

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
}
