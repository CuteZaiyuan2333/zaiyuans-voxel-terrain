# 体素地形插件 — 开发计划与架构说明

> 目标：基于 Godot 4.6 Mono (C#)，开发每区块 **32×32×32** 方块、使用 **C# + ECS** 管理区块的 3D 体素地形插件。

---

## 一、可行性结论

**结论：可行。**

- **Godot 4.6 Mono**：C# 支持成熟，适合写高性能逻辑与数据结构；GDExtension 也可后续扩展。
- **32³ 区块**：单区块 32,768 体素，在内存与网格生成量上平衡良好，业界常见（如 Minecraft 16³、部分引擎 32³）。
- **ECS 管理区块**：区块作为“实体”、体素数据/网格/状态作为“组件”，用“系统”做生成、网格重算、LOD、保存等，结构清晰、易扩展、便于多线程/Job 化。
- **插件形式**：放在 `addons/` 下，用 C# 脚本 + 可选 `plugin.gd`/`plugin.csharp` 暴露编辑器与运行时 API，与现有 Godot 4.6 工作流兼容。

下文为建议的**内部架构**与**开发者使用方式**，用于在编码前统一认知并指导实现。

---

## 二、内部架构设想

### 2.1 整体分层

```
┌─────────────────────────────────────────────────────────────┐
│  开发者 API（VoxelTerrain 节点、VoxelWorld 单例、扩展方法）   │
├─────────────────────────────────────────────────────────────┤
│  ECS 层：World / ChunkEntity / Components / Systems          │
├─────────────────────────────────────────────────────────────┤
│  数据层：Chunk 数据（体素数组、光照、元数据）、序列化         │
├─────────────────────────────────────────────────────────────┤
│  渲染层：MeshInstance3D 生成、材质、LOD（可选）               │
└─────────────────────────────────────────────────────────────┘
```

- **开发者**只接触最上层 API。
- **ECS** 负责“何时生成/卸载区块、何时重算网格、何时存盘”。
- **数据层**只关心“一块 32³ 里每个格子的 BlockId / 光照等”。
- **渲染层**只关心“由体素数据生成 Mesh + 挂到场景树”。

### 2.2 ECS 映射关系

- **实体 (Entity)**：每个**区块 (Chunk)** 对应一个实体。  
  实体 ID 可用“区块坐标”派生（如 `(cx, cy, cz)` 哈希或线性索引），便于 O(1) 查找。

- **组件 (Component)**（按需拆分）：
  - **ChunkPosition**：区块世界坐标 `(cx, cy, cz)`，单位是“区块数”。
  - **VoxelData**：32×32×32 的体素数组（BlockId、可选光照/湿度等）。  
    建议用 `Span<T>` / 一维数组 + 索引函数，保证缓存友好。
  - **ChunkMesh**：当前区块生成的 `Mesh` 或 `ArrayMesh` 的引用，以及顶点数等元数据（用于 LOD/剔除）。
  - **ChunkState**：枚举如 `Empty / Generating / Ready / Dirty / Unloading`，驱动系统逻辑。
  - **ChunkLoadPriority**（可选）：用于按距离/重要性排序加载顺序。

- **系统 (System)**（已实现）：
  - **ChunkSpawnSystem**：根据观察者位置与视距生成区块实体；优先从磁盘 `ChunkStorage.TryLoad` 加载，否则进入 `Generating`；每帧 Spawn 数量受 `MaxSpawnPerFrame` 限制，按距离排序。
  - **ChunkTerrainGenSystem**：对 `Generating` 的区块填充 `VoxelData`（默认或自定义 `IChunkGenerator`）；支持同步或 `UseAsyncTerrain` 工作线程；每帧数量受 `MaxTerrainGenPerFrame` 限制。
  - **ChunkMeshSystem**：对 `Dirty` 区块从 `VoxelData` 生成网格并写入 `ChunkMesh`；支持 `UseGreedyMeshing` 与 `UseAsyncMesh`；每帧数量受 `MaxMeshBuildPerFrame` 限制。
  - **ChunkRenderSystem**：将 `ChunkMesh` 同步到场景树（创建/更新 `MeshInstance3D`）；可选相机视锥剔除（`RunContext.Camera`）。
  - **ChunkUnloadSystem**：卸载超出视距的区块；若配置了 `SaveDirectory` 且区块在脏表中，卸载前通过 `ChunkStorage.Save` 写回磁盘，再销毁实体与 MeshInstance。
  - **ChunkSaveSystem**：由 Unload 时写盘承担；无独立定时 Save 系统（可选后续）。

这样，**区块的生成 → 网格 → 渲染 → 卸载** 全部由系统驱动，逻辑集中、易于加 LOD 或异步 Job。

### 2.3 区块与体素数据布局

- **区块尺寸**：固定 **32×32×32** 体素。  
  世界坐标 `(wx, wy, wz)` 与区块坐标及块内坐标：
  - `cx = wx >> 5`, `cy = wy >> 5`, `cz = wz >> 5`（假设 1 单位 = 1 体素）
  - 块内：`lx = wx & 31`, `ly = wy & 31`, `lz = wz & 31`
- **体素存储**：
  - 至少一个 **BlockId**（byte/ushort 等）数组，一维：`index = lx + ly * 32 + lz * 32 * 32`。
  - 可选：光照、法线/AO、流体高度等，可单独数组或打包到更少字节。
- **邻接**：生成网格时需要访问相邻区块的边界体素，可通过“区块邻居引用”或“临时边界拷贝”实现，避免跨区块耦合。

### 2.4 网格生成（Meshing）（已实现）

- **Greedy Meshing (Culling)**：已实现；`ChunkMesher` 支持按面合并（`UseGreedyMeshing`）与逐面 Naive 两种模式。
- **输入**：本区块 32³ + 六邻区块边界体素（通过 `VoxelEcsWorld.TryGetBlockAtWorld` 或 `ChunkMeshSnapshot` 只读访问）。
- **输出**：`ArrayMesh`，带 UV/法线；默认单一材质，可按 BlockId 扩展。
- **线程**：可选 `UseAsyncMesh`，工作线程生成网格数据，主线程排出结果并提交到 `MeshInstance3D`。

### 2.5 坐标与尺度约定

- 建议：**1 世界单位 = 1 体素边长**，这样 32 单位 = 1 区块边长，与 Godot 默认单位一致。
- 区块原点：例如区块 `(cx, cy, cz)` 的世界原点为 `(cx*32, cy*32, cz*32)`，便于与 `MeshInstance3D.GlobalPosition` 对齐。

---

## 三、插件目录与文件结构建议

```
addons/
  voxel_terrain/
    plugin.csharp              # 插件入口（可选，用于编辑器工具）
    VoxelTerrain.cs            # 主节点脚本，挂到场景
    VoxelWorld.cs              # 单例/服务：全局配置、区块范围、种子等
    Core/
      VoxelConstants.cs        # 32, 32*32, 32*32*32 等常量
      BlockId.cs               # 体素类型枚举或表
    ECS/
      VoxelEcsWorld.cs         # ECS World，持有所有 Chunk 实体
      ChunkEntity.cs           # 实体 ID / 区块坐标
      Components/
        ChunkPosition.cs
        VoxelData.cs
        ChunkMesh.cs
        ChunkState.cs
      Systems/
        ChunkSpawnSystem.cs
        ChunkTerrainGenSystem.cs
        ChunkMeshSystem.cs
        ChunkRenderSystem.cs
        ChunkUnloadSystem.cs
    Data/
      ChunkData.cs             # 32³ 数组封装、序列化
      IChunkGenerator.cs       # 地形生成接口
      DefaultTerrainGenerator.cs
    Rendering/
      ChunkMesher.cs           # 体素 → Mesh（含 Greedy Meshing）
      ChunkRenderer.cs         # Mesh → MeshInstance3D 管理
    Docs/                      # 可放 addon 自带小文档
```

- 根目录的 `Docs/` 用于**项目级**计划与设计（即本文）；`addons/voxel_terrain/Docs/` 可放插件使用说明。

---

## 四、开发者使用方式（API 设想）

### 4.1 场景与节点（已实现）

- 在场景中放置一个 **VoxelTerrain** 节点（继承 `Node3D`）；可选指定 **ObserverNode**（如 Camera3D）用于观察者位置与视锥剔除。
- 可选：挂载或由 VoxelTerrain 自动创建 **VoxelWorld** 节点，配置 **Seed**、**ViewDistanceInChunks**、**SaveDirectory**、**MaxSpawnPerFrame** / **MaxTerrainGenPerFrame** / **MaxMeshBuildPerFrame**、**UseGreedyMeshing** / **UseAsyncTerrain** / **UseAsyncMesh**、**MaxChunkRadius**（0 表示不限制）等。

### 4.2 初始化与运行（已实现）

- VoxelTerrain 的 `_Ready` 查找或创建 VoxelWorld；VoxelWorld 的 `_Ready` 创建 ECS World 与 RunContext。
- 每帧 VoxelTerrain 的 `_Process` 将 Observer 位置与相机传入并调用 `VoxelWorld.RunEcs(delta, this, camera)`，顺序执行 Spawn → Gen → Mesh → Render → Unload；ChunkParent 变化时自动清理旧 MeshInstance 并绑定新父节点。

### 4.3 读写体素（对外 API）（已实现）

- **设置体素**：`VoxelWorld.SetBlock(worldPos, blockId)` 返回 `bool`。若区块已加载：写 `VoxelData`，当前块与六邻标 `Dirty`，脏块加入存盘集合；若区块未加载：写入 `PendingBlocks`，待 Spawn/Gen 后应用。若 `MaxChunkRadius > 0` 且超出范围则返回 false。
- **读取体素**：`VoxelWorld.GetBlock(worldPos)` 返回 `BlockId`。区块未加载或超出 `MaxChunkRadius` 时返回 `Air`。

### 4.4 地形生成扩展（已实现）

- **IChunkGenerator**：`Generate(chunkPos, data, seed)`；由 RunContext 注入，ChunkTerrainGenSystem 调用。
- **DefaultTerrainGenerator**：噪声 + 地表高度 + 草地/泥土/石头分层；内部按 seed 复用 FastNoiseLite 实例。
- 将自定义 Generator 赋给 `VoxelWorld.Generator` 即可。

### 4.5 事件与回调（已实现）

- **VoxelWorld** 信号：`ChunkLoaded(cx, cy, cz)`、`ChunkUnloaded(cx, cy, cz)`、`BlockChanged(wx, wy, wz, oldId, newId)`。  
- 在 RunEcs 结束后统一排出并发出，用于玩法、音效、存档脏标记等。

---

## 五、性能与注意事项

- **内存**：每区块体素数据控制在 KB 级（如 1 byte/体素 ≈ 32KB），数千区块在百 MB 量级，可接受。
- **网格**：使用 Greedy Meshing 和按材质/纹理合批，减少 Draw Call 与顶点数。
- **多线程**：地形生成与网格生成放工作线程，仅主线程操作场景树与 Mesh 提交。
- **LOD**：后续可为远区块提供简化网格或体素聚合，由 **ChunkState** 或距离驱动。
- **保存/加载**：已实现。`ChunkStorage` 按区块文件 `{SaveDir}/chunks/cx_cy_cz.chunk` 存储（4 字节 magic、4 字节 version、Deflate 压缩的 32³ 字节）；仅卸载时对脏区块写盘，Spawn 时优先从磁盘 TryLoad。

---

## 六、实施阶段建议

1. **阶段一**：数据层 + 单区块  
   - 实现 `ChunkData`（32³）、坐标换算、`BlockId`。  
   - 实现简单 `ChunkMesher`（可先不用 Greedy，用立方体即可），在场景中显示一个区块。
2. **阶段二**：ECS 骨架  
   - 引入 ECS（可先用简单字典/列表实现，不必上完整 ECS 框架）。  
   - 实现 Chunk 实体、Position/State/VoxelData 组件，以及 Spawn/Mesh/Render/Unload 系统，支持多区块。
3. **阶段三**：地形与 API  
   - 实现默认地形生成器、`SetBlock`/`GetBlock`、VoxelTerrain 节点与 VoxelWorld 配置。  
   - 完善 Greedy Meshing 与材质。
4. **阶段四**：优化与扩展  
   - 异步生成、LOD、存档、事件回调、编辑器小工具（如画笔）等。

---

## 七、文档与后续

- 本文档作为**项目级**体素地形插件的架构与计划说明，放在 `Docs/VOXEL_TERRAIN_PLAN.md`。  
- 插件实现后，可在 `addons/voxel_terrain/Docs/` 下补充**用户手册**（如何挂节点、如何改生成器、如何做 Mod 等）。  
- 开发过程中若调整 ECS 粒度、区块尺寸或 API 命名，建议同步更新本文档。

---

*文档版本：1.1 | 适用于 Godot 4.6 Mono | 区块尺寸 32×32×32 | C# + ECS | 已实现生产级特性：存档、帧预算、Greedy/异步、视锥剔除、信号、SetBlock 返回值与边界*
