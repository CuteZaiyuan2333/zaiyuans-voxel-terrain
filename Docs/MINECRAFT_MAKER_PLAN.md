# 类 Minecraft 游戏构建工具包 (Minecraft-Maker) 开发规划

> **目标**：打造一套模块化、即插即用的 Godot 插件组，允许用户通过简单的“拖拽节点 + 少量脚本”方式，快速构建高质量的类 Minecraft Voxel 游戏。

---

## 1. 总体构想与评价

用户的计划非常有价值且符合 Godot 的核心设计哲学（组合优于继承）。将复杂功能拆解为独立插件，不仅降低了单一模块的复杂度，也让最终用户（游戏开发者）能灵活裁剪功能。

### 核心优势
*   **低代码/零代码起步**：用户只需在场景中拖入 `VoxelTerrain`, `VoxelPlayer`, `VoxelSky` 即可运行。
*   **高度解耦**：流体、物理、AI 互不强耦合，通过标准接口（如“体素查询 API”）交互。
*   **生态兼容**：不仅仅支持 C#，还通过适配层支持 GDScript，极大地扩展了受众。

---

## 2. 组件/插件规划 (The Plugin Suite)

为了实现“大计划”，建议将功能拆分为以下独立插件（或单一插件下的独立子模块）：

### 2.1 核心层 (Core)
*   **插件名**: `zaiyuans_voxel_terrain` (现有插件升级版)
*   **职责**:
    *   体素数据的存储、管理、LOD。
    *   高效网格生成 (Greedy Meshing)。
    *   多线程调度与存档系统。
    *   **新增**: 提供通用的 `IVoxelQuery` 接口，供其他插件查询地形（碰撞、寻路、光照）。

### 2.2 实体层 (Entities)
*   **插件名**: `zaiyuans_voxel_entity`
*   **职责**:
    *   **VoxelPhysicsEngine**: 处理 AABB 与体素地形的碰撞检测与响应（滑动、台阶自动跳跃）。
    *   **VoxelMob**: 基于行为树或 GOAP 的生物 AI，能够识别体素地形进行寻路（A* 或 JPS）。
    *   **VoxelPlayer**: 预制的 FPS/TPS 玩家控制器，开箱即用。

### 2.3 环境层 (Environment)
*   **插件名**: `zaiyuans_voxel_atmosphere`
*   **职责**:
    *   **VolumetricClouds**: 基于噪声的体积云渲染。
    *   **VoxelFluid**: 简化的流体模拟（水、熔岩），基于元胞自动机，与地形网格分离或合并渲染。
    *   **DayNightCycle**: 动态天空盒、太阳/月亮轨迹、环境光调节。

### 2.4 交互与工具 (Interaction & Tools)
*   **插件名**: `zaiyuans_voxel_gameplay`
*   **职责**:
    *   **BlockRayCast**: 高效的体素射线检测（用于放置/破坏方块）。
    *   **InventorySystem**: 物品栏与掉落物系统。
    *   **StructureBlock**: 结构方块，用于复制/粘贴建筑结构。

---

## 3. 现有体素插件改造计划：支持多语言生成 (Polyglot Generation)

目前的瓶颈在于 `IChunkGenerator` 是纯 C# 接口。为了让 GDScript 或其他语言用户能编写地形，我们需要实现一个**跨语言桥接层**。

### 3.1 架构改动：引入 `ScriptableVoxelGenerator`

我们需要在 C# 层实现一个通用的生成器，它不产生具体地形，而是将任务“代理”给用户的 Godot 资源 (Resource) 或脚本对象。

#### 1. 新增 `VoxelGeneratorResource` (Godot Resource)
这是一个基类（继承自 `Resource`），暴露给 GDScript 继承。

```gdscript
# 用户侧代码示例 (GDScript)
class_name MyCustomTerrain extends VoxelGeneratorResource

@export var base_height: int = 64
@export var noise: FastNoiseLite

# 核心回调函数
func _generate_chunk(buffer: VoxelBufferWrapper, chunk_pos: Vector3i):
    # 下面这行代码如果是 GDScript 循环调用 32768 次会很慢
    # 所以我们需要提供高效的 C++ 或 C# 侧批量操作 API
    
    # 方案 A: 全局噪声填充 (快)
    buffer.fill_with_noise(noise, base_height, BlockType.Stone, BlockType.Dirt)
    
    # 方案 B: 局部细节修饰 (慢，但灵活)
    if chunk_pos.y == 0:
        buffer.set_block(0, 0, 0, BlockType.Bedrock)
```

#### 2. 新增 `VoxelBufferWrapper` (API 对象)
C# 中的原生数组 (Array/Span) 不能高效直接暴露给 GDScript。我们需要封装一个 `GodotObject`，提供批量操作 API。

*   **API 建议**:
    *   `fill_solid(block_id)`: 填充整个区块。
    *   `fill_surface(noise, min_h, max_h, block_id)`: 基于噪声填充地表。
    *   `set_block_v(local_pos, block_id)`: 设置单个方块。
    *   `get_block_v(local_pos)`: 获取单个方块。

### 3.2 性能优化策略 (C# <-> GDScript)

直接在 GDScript 中遍历 `32*32*32` 循环极其缓慢。为了解决这个问题，我们必须提供**“宏观指令” (Macro Instructions)**。

*   **指令式生成**： GDScript 不直接写 `for` 循环，而是通过调用 wrapper 的 C++ 绑定函数来执行耗时操作。
    *   *Bad*: `for x in 32: buffer.set_block(...)`
    *   *Good*: `buffer.apply_heightmap(noise_texture)`

### 3.3 具体实施步骤

1.  **定义数据交换协议**：
    在 `Core/Data` 中创建一个 `VoxelBuffer` 类，继承自 `Godot.RefCounted`。它内部持有一个指向当前正在生成的 `BlockId[]` 的指针或引用。

2.  **实现 C# 代理生成器 (`ProxyGenerator`)**：
    实现 `IChunkGenerator` 接口。
    ```csharp
    public class ProxyGenerator : IChunkGenerator {
        public Resource UserScript; // 用户拖拽进来的 Resource
        
        public void Generate(Vector3i chunkPos, VoxelData data) {
            // 1. 封装 VoxelData 为 VoxelBufferWrapper (Godot.Object)
            var wrapper = new VoxelBufferWrapper(data);
            
            // 2. 调用 GDScript 方法
            if (UserScript.HasMethod("_generate_chunk")) {
                UserScript.Call("_generate_chunk", wrapper, chunkPos);
            }
        }
    }
    ```

3.  **编辑器集成**：
    在 `VoxelWorld` 节点上，增加一个 `GeneratorResource` 导出变量。
    如果用户赋值了这个变量，系统自动使用 `ProxyGenerator` 并注入该资源。

---

## 4. 开发路线图 (Roadmap)

### 4.0 全局实施顺序（依赖关系）

*   **Terrain**：先完成 Bridge（VoxelBufferWrapper + ScriptableVoxelGenerator）+ **IVoxelQuery**（Raycast、GetCollidingBoxes），再考虑 LOD/光照等。
*   **Entity**：在 IVoxelQuery 可用后做 Physics（Swept AABB）→ VoxelPlayer → 寻路与 AI。
*   **Gameplay**：在 IVoxelQuery（含 Raycast）可用后做 VoxelRaycast 与放置/破坏 → Structure → Inventory。
*   **Atmosphere**：Sky/云可与前几项并行；流体在「流体数据归属」确定后排期（见 DESIGN_VOXEL_ATMOSPHERE）。

### 阶段一：插件基础设施改造 (Current Focus)
1.  **VoxelBufferWrapper 实现**：创建一个对 GDScript 友好的体素操作类。
2.  **ProxyGenerator 实现**：打通 C# 到 GDScript 的调用链路。
3.  **API 扩充**：为 wrapper 增加 `fill_noise`, `replace_blocks` 等高性能 helper 方法。

### 阶段二：实体与交互 (Next Step)
1.  **VoxelPhysics**: 实现简单的 AABB vs Voxel 碰撞检测。
2.  **PlayerController**: 基于上述物理的一个 CharacterBody3D 实现。
3.  **Raycast**: 实现 `VoxelWorld.Raycast(ray_origin, ray_dir)`。

### 阶段三：环境增强 (Future)
1.  **流体支持**: 在 BlockId 中预留流体位，或增加独立的 FluidLayer。
2.  **天空盒/云**: 移植开源的 Godot 体积云 Shader 并适配体素风格。

---

## 5. 总结

你的计划完全可行。目前的当务之急是**打通 C# 底层与 Godot 上层脚本的隔阂**。通过实现一个基于 `Resource` 的代理生成器模式，你可以让用户享受 C# 的高性能核心，同时使用 GDScript 快速定义游戏玩法和地形特征。
