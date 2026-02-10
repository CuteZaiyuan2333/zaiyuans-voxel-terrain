# 插件架构分析与推进建议

## 本次已完成（2025-02-11）

1. **IVoxelQuery 补全**：在 `zaiyuans_voxel_world` 中实现了 `Raycast`（DDA 体素射线，返回命中点/法线/BlockPosition/BlockId）和 `GetCollidingBoxes`（给定 AABB 返回相交固体体素 AABB 列表）。新增 `Core/VoxelRaycastResult.cs`。
2. **Entity 接口化解耦**：`VoxelPhysicsEngine`、`VoxelPathfinder` 改为接收 `IVoxelQuery`；未注入时回退到 `VoxelWorld.Singleton`。`VoxelCharacterBody` 增加可选 `VoxelQuerySourcePath`，可指向任意实现 `IVoxelQuery` 的节点，便于多世界或自定义体素后端复用。

---

## 分析结论概览

### 当前状态
1.  **zaiyuans_voxel_world（地形）**：已到生产可用水平；**IVoxelQuery** 已包含 GetBlock、Raycast、GetCollidingBoxes。
2.  **zaiyuans_voxel_entity（实体）**：已有物理、玩家、生物、寻路；**已改为依赖 IVoxelQuery**，支持可选注入。
3.  **zaiyuans_voxel_gameplay**：尚未创建（设计里包含 BlockRaycast、放置/破坏、物品栏、结构方块等）。
4.  **zaiyuans_voxel_atmosphere**：尚未创建（设计里包含昼夜循环、流体；目前云渲染临时使用了 `SunshineClouds2`）。

### 和「全插件化」的剩余差距

#### 1. 玩法插件缺失
*   没有独立的 gameplay 插件，就没有「射线选块 + 放置/破坏」的标准化交互层。
*   **影响**：距离「类 Minecraft」的可玩闭环还差这一步。

**结论**：接口与 Entity 解耦已完成；距离目标 ≈ 差「一个最小 Gameplay 插件（Raycast 选块 + 放置/破坏）」。

---

## 推荐下一步（按优先级）

### 下一步推荐：新建 `zaiyuans_voxel_gameplay` 插件（最小闭环）

*   实现「Raycast 选块 + 左键破坏 + 右键放置」：使用 `IVoxelQuery.Raycast` 做准星选块，调用 `VoxelWorld.SetBlock`/`GetBlock` 做破坏/放置。
*   可选：在场景中通过 `VoxelQuerySourcePath` 指定体素世界节点，使 Gameplay 与 Entity 一样支持多世界或自定义体素后端。
