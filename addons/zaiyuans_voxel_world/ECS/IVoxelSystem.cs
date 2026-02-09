namespace ZaiyuansVoxelWorld.ECS;

public interface IVoxelSystem
{
    void Run(VoxelEcsWorld world, double delta, EcsRunContext ctx);
}
