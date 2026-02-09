# 体素插件与生产就绪的差距分析

> 基于当前 `addons/zaiyuans_voxel_world` 实现，对比「可全面投入生产」所需能力所做的差距分析。  
> **更新**：P0/P1/P2 核心项（存档、跨块 Dirty、帧预算、Greedy、异步地形/网格、视锥剔除、信号、SetBlock 返回值与边界、RunContext 生命周期、PendingBlocks）已实现；下表与各节中已闭环项已标注「已实现」。
---

## 一、结论概览

| 维度         | 当前状态       | 生产就绪大致差距 |
|--------------|----------------|------------------|
| 功能完整性   | 生产级已闭环   | 可选扩展         |
| 性能与规模   | 帧预算+异步+剔除 | LOD/遮挡为可选   |
| 稳定性与健壮 | 边界/返回值/事件 | 测试覆盖可加强   |
| 内容与表现   | 最小可用       | 中（材质/LOD 等）|
| 运维与交付   | 文档与测试已补 | 可选编辑器工具   |

**总体**：核心生产能力已具备；LOD、体素光照、世界元数据文件、编辑器画笔等为可选/后续迭代。

---

## 二、功能与逻辑缺口

### 2.1 存档与持久化（关键）— **已实现**

- **实现**：`ChunkStorage` 提供区块格式 `{SaveDir}/chunks/cx_cy_cz.chunk`（magic + version + Deflate 压缩 32³）；`ChunkUnloadSystem` 在卸载前对脏区块调用 `ChunkStorage.Save`；Spawn 时优先 `ChunkStorage.TryLoad`，成功则直接进入 Dirty 并应用 PendingBlocks，否则走 TerrainGen。
- **可选后续**：世界元数据文件（种子/版本/生成器 ID）；独立 ChunkSaveSystem 定时写盘。

### 2.2 跨区块编辑与网格一致性 — **已实现**

- **实现**：`VoxelWorld.SetBlock` 对当前块与六邻已加载区块均 `SetState(Dirty)`，边界破面已修复。

### 2.3 SetBlock 时区块未加载 — **已实现**

- **实现**：区块未加载时写入 `RunContext.PendingBlocks`；ChunkSpawnSystem / ChunkTerrainGenSystem 在区块加入或生成后调用 `ApplyPendingBlocksForChunk` 应用待办写入。

### 2.4 多世界 / 多 VoxelTerrain — **已实现**

- **实现**：`VoxelWorld._Ready` 清空 `ChunkMeshInstances`；`RunEcs` 内若 `chunkParent != RunContext.ChunkParent` 则对旧 MI 全部 `QueueFree` 并 Clear，再绑定新 ChunkParent，避免悬空引用。

### 2.5 事件与扩展点 — **已实现**

- **实现**：VoxelWorld 信号 `ChunkLoaded(cx,cy,cz)`、`ChunkUnloaded(cx,cy,cz)`、`BlockChanged(wx,wy,wz,oldId,newId)`；RunEcs 结束后统一排出并发出。

### 2.6 对外查询接口（IVoxelQuery）

- **目标**：地形插件对外提供 **IVoxelQuery**（GetBlock、Raycast、GetCollidingBoxes），供 Entity/Gameplay 解耦使用。
- **现状**：GetBlock 已实现；Raycast、GetCollidingBoxes 为计划中扩展（见 VOXEL_TERRAIN_PLAN 阶段三）。

---

## 三、性能与规模

### 3.1 网格生成（顶点与 Draw Call）— **已实现**

- **实现**：ChunkMesher 支持 `UseGreedyMeshing`（按面合并四边形）与 Naive 两种模式；默认开启 Greedy。

### 3.2 主线程阻塞 — **已实现**

- **实现**：`UseAsyncTerrain` / `UseAsyncMesh` 时，地形与网格在工作线程（AsyncChunkJobs）生成，主线程排出结果并提交 Mesh/场景树。

### 3.3 加载策略与帧预算 — **已实现**

- **实现**：`MaxSpawnPerFrame`、`MaxTerrainGenPerFrame`、`MaxMeshBuildPerFrame` 限制每帧数量；Spawn 按到观察者距离排序。

### 3.4 视锥剔除与遮挡 — **已实现**

- **实现**：RunContext.Camera 由 VoxelTerrain 传入（ObserverNode 或 Viewport 相机）；ChunkRenderSystem 用 `Camera.IsPositionInFrustum(区块中心)` 设置 `MeshInstance3D.Visible`。遮挡剔除为可选后续。

### 3.5 LOD（ Level of Detail）

- **现状**：无 LOD；ChunkMesh 有 VertexCount 但未使用。
- **生产需求**：远区块用简化网格或体素聚合，减少顶点与 overdraw。
- **建议**：后续阶段为远距离区块生成简化网格或降低分辨率，由 ChunkState 或距离带驱动。

---

## 四、稳定性与健壮性

### 4.1 边界与空引用

- **现状**：`GetBlock` 在区块未加载时返回 Air；`SetBlock` 在区块未加载时静默 no-op。未对世界坐标做范围限制（理论上可无限大）。
- **风险**：若上层传入异常坐标（如极大值），依赖 VoxelConstants 与字典查找，一般不会崩，但缺少「世界边界」或「最大区块范围」的明确约定与校验。
- **建议**：可选配置「世界边界」（如最大 chunk 半径），超出则 GetBlock 固定返回 Air、SetBlock 忽略；对关键 API 做参数校验与文档说明。

### 4.2 生成器与依赖

- **现状**：`DefaultTerrainGenerator` 使用 Godot 的 `FastNoiseLite`（GDScript/C# API），每区块新建一个实例；无依赖注入或配置化。
- **生产需求**：不同世界/关卡可能用不同噪声或表，需要可配置、可替换，且避免每块 new 带来的分配与参数不一致。
- **建议**：生成器由 VoxelWorld 或 RunContext 统一持有并注入；噪声等可复用实例或通过参数传入，避免每块重建。

### 4.3 错误处理与日志

- **现状**：几乎无 try-catch、无结构化日志；生成或网格失败时无明确反馈。
- **生产需求**：在异常或无效状态时能定位问题（如某区块生成失败、磁盘满导致保存失败）。
- **建议**：对生成、序列化、Mesh 构建等做基本异常捕获与日志（含区块坐标）；对外 API 可返回 bool 或 Result 表示成功/失败。

---

## 五、内容与表现

### 5.1 方块类型与材质

- **现状**：`BlockId` 仅 Air/Grass/Dirt/Stone；ChunkRenderer 单一 StandardMaterial3D，无纹理、无按 BlockId 区分。
- **生产需求**：多种方块、纹理图集（Atlas）或每面纹理、不同材质（透明、双面、发光等）。
- **建议**：扩展 BlockId 或 BlockDef 表；ChunkMesher 输出带材质/UV 的 Mesh，支持 Atlas UV 或每类型材质；ChunkRenderer 支持多材质或材质数组。

### 5.2 光照与环境

- **现状**：无体素光照；场景依赖场景中的 DirectionalLight。
- **生产需求**：若要做「洞穴变暗、火把照明」等，需要体素光照或光照贴图。
- **建议**：属后续扩展；可先预留光照数据（如每体素 1 byte 亮度）与光照传播接口。

### 5.3 地形丰富度

- **现状**：DefaultTerrainGenerator 仅噪声高度 + 草地/泥土/石头三层，无洞穴、无结构、无生物群系。
- **生产需求**：根据目标玩法需要洞穴、矿脉、树木、建筑等。
- **建议**：在 IChunkGenerator 上扩展；地形复杂度与性能（含帧预算）需一起考虑。

---

## 六、运维、交付与工程化

### 6.1 配置与调优

- **现状**：视距、种子等通过代码或节点属性设置，无配置文件、无运行时热更。
- **生产需求**：不同平台或画质档位需要不同视距/加载预算；便于运营调参。
- **建议**：关键参数（视距、每帧生成/网格预算、是否启用 Greedy 等）可通过 ProjectSettings 或外部配置读取，并支持运行时修改（需注意线程与缓存一致性）。

### 6.2 调试与编辑器工具

- **现状**：仅注册 VoxelTerrain 节点类型，无画笔、无区块边界显示、无性能面板。
- **生产需求**：开发期需要「点击放置/删除方块、查看区块边界、查看加载/网格统计」等。
- **建议**：在 EditorPlugin 中增加简单工具（如射线检测 + SetBlock(0)、绘制区块线框、显示当前加载区块数/顶点数）。

### 6.3 文档与 API 契约

- **现状**：代码内注释尚可，无对外「用户手册」、无版本化 API 说明、无「最低 Godot 版本」的明确声明。
- **生产需求**：第三方或团队协作需要稳定 API 与迁移指南。
- **建议**：在 `addons/zaiyuans_voxel_world/Docs/` 增加使用说明（如何挂节点、如何换生成器、SetBlock/GetBlock 语义、未加载行为）；对主要公开 API 标注版本与弃用策略。

### 6.4 测试与自动化

- **现状**：无单元测试、无自动化场景测试。
- **生产需求**：重构或加功能时需回归保证（坐标换算、序列化、边界行为等）。
- **建议**：对 VoxelConstants（坐标/索引）、ChunkData 序列化、SetBlock 邻块 Dirty 等编写单元测试；可选简单集成测试（加载一屏区块、SetBlock 再 GetBlock 校验）。

---

## 七、优先级建议（若按生产目标推进）

| 优先级 | 项目                         | 状态 |
|--------|------------------------------|------|
| P0     | 跨区块 SetBlock 标 Dirty     | 已实现 |
| P0     | 存档：区块序列化 + Unload 写盘 | 已实现 |
| P1     | 每帧加载/生成/网格预算       | 已实现 |
| P1     | Greedy Meshing               | 已实现 |
| P1     | RunContext/ChunkParent 与多场景 | 已实现 |
| P2     | 地形/网格工作线程            | 已实现（UseAsyncTerrain/UseAsyncMesh） |
| P2     | SetBlock 未加载时的策略      | 已实现（PendingBlocks） |
| P2     | 视锥剔除                     | 已实现 |
| P3     | 事件/回调、配置化、调试工具  | 事件已实现；配置为 Export；调试工具可选 |
| P3     | 方块/材质扩展、LOD、光照     | 可选/后续 |

---

## 八、总结

当前插件**已具备**：32³ 区块、ECS 流程、坐标与数据布局、SetBlock/GetBlock（含返回值与 MaxChunkRadius）、可扩展生成器、存档（ChunkStorage + Unload 写盘 + Spawn 优先加载）、帧预算与异步地形/网格、Greedy Meshing、视锥剔除、RunContext/ChunkParent 生命周期、PendingBlocks、信号（ChunkLoaded/ChunkUnloaded/BlockChanged）、单元测试（VoxelConstants、ChunkStorage）与文档，**已达生产级完成度**。

**可选/后续**：LOD、体素光照、世界元数据文件、多材质/纹理图集、编辑器画笔与调试面板。按项目需求裁剪即可。
