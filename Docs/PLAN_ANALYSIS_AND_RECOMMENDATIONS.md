# Docs 计划合理性分析与修改建议

> 对 `Docs/` 下 Minecraft-Maker 相关设计与计划文档的交叉分析，以及应修改或细化的部分建议。

---

## 一、总体结论

**计划整体合理**：四模块拆分（Terrain / Entity / Atmosphere / Gameplay）清晰，与 Godot「组合优于继承」一致；Terrain 的 ECS + 32³ 区块在 VOXEL_TERRAIN_PLAN 与 PRODUCTION_READINESS_GAP 中已对齐且多数已实现。  
建议在**跨模块接口、命名与引用、流体数据归属、全局路线图**上做少量修改与细化，以便后续实体/玩法/大气插件落地时不踩坑。

---

## 二、应修改或统一的部分

### 2.1 文档引用错误

- **MINECRAFT_MAKER_OVERVIEW.md** 第 48 行写的是「See [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md)」，该文件在 Docs 中**不存在**。
- **建议**：改为指向实际路线文档，例如：
  - `[MINECRAFT_MAKER_PLAN.md](./MINECRAFT_MAKER_PLAN.md)`（总规划与阶段），以及
  - `[VOXEL_TERRAIN_PLAN.md](./VOXEL_TERRAIN_PLAN.md)`（地形实施阶段）。

### 2.2 插件/模块命名与现状一致

- **OVERVIEW** 中核心地形插件写的是 `zaiyuans_voxel_terrain`，当前代码库中实际 addon 名为 **`zaiyuans_voxel_world`**（且已包含地形 ECS、存档、Greedy 等）。
- **建议**：在 OVERVIEW 或 PLAN 中明确说明：
  - 当前是**单一 addon** `zaiyuans_voxel_world`，内含「核心地形」逻辑；
  - 未来若拆分为多 addon（terrain / entity / atmosphere / gameplay），再使用 `zaiyuans_voxel_terrain` 等命名；或统一写成「地形模块 (zaiyuans_voxel_world 内)」避免混淆。

### 2.3 结构文件扩展名统一

- **DESIGN_VOXEL_GAMEPLAY.md** 使用 **`.struct`**，**MINECRAFT_MAKER_OVERVIEW.md** 使用 **`.structure`**。
- **建议**：在 DESIGN_VOXEL_GAMEPLAY 或共享术语表中统一为一种（例如 `.structure`），并在全文档中统一。

### 2.4 跨插件接口：IVoxelQuery 与实现状态

- **DESIGN_VOXEL_TERRAIN.md** 和 **MINECRAFT_MAKER_PLAN.md** 都要求地形提供 **`IVoxelQuery`**（GetBlock、Raycast、GetCollidingBoxes），供 Entity（物理、寻路）与 Gameplay（射线选块）使用。
- **VOXEL_TERRAIN_PLAN.md** 与 **PRODUCTION_READINESS_GAP.md** 均**未提及** IVoxelQuery 的实现或计划；当前代码中**未发现** IVoxelQuery 接口或实现。
- **建议**：
  1. 在 **VOXEL_TERRAIN_PLAN.md** 的「实施阶段」中增加一条：**实现 IVoxelQuery（GetBlock 已有，补充 Raycast、GetCollidingBoxes）**，并标明为 Entity/Gameplay 的**前置依赖**。
  2. 在 **PRODUCTION_READINESS_GAP.md** 的「可选/后续」或「接口」小节中简要写上：对外提供 **IVoxelQuery** 以解耦物理与玩法模块。

### 2.5 ScriptableVoxelGenerator / VoxelBufferWrapper 与代码现状

- **VoxelWorld.cs** 中已出现 `ScriptableVoxelGenerator` 与 `GeneratorResource` 的用法，但 addon 内**没有** `ScriptableVoxelGenerator`、**VoxelBufferWrapper** 的类定义；若在编辑器中为 `GeneratorResource` 赋值，很可能编译或运行时报错。
- **建议**：
  1. 在 **MINECRAFT_MAKER_PLAN.md** 或 **DESIGN_VOXEL_TERRAIN.md** 中明确：**阶段一** 的交付物包括 `ScriptableVoxelGenerator.cs` 与 `VoxelBufferWrapper.cs`，且 VoxelWorld 仅在两者就绪后启用 `GeneratorResource` 路径。
  2. 若当前不打算实现 GDScript 生成器，则暂时从 VoxelWorld 中移除对 `ScriptableVoxelGenerator` 的引用，或改为空实现/默认回退到 `DefaultTerrainGenerator`，避免未实现代码路径。

---

## 三、建议细化的部分

### 3.1 流体数据归属与查询（Atmosphere ↔ Terrain）

- **DESIGN_VOXEL_ATMOSPHERE.md** 描述了流体的元胞自动机、Level 1–8、独立网格渲染，但**未说明流体体素数据存在哪里**：是存在地形区块内（如扩展 VoxelData 的「流体层」），还是由大气插件维护独立网格并通过某接口告知地形「此处为流体」。
- 若流体与地形分离，则「流动规则」里「block below is Air/Fluid」需要地形与流体双方可查询（例如 Terrain 提供 `GetBlock`，Atmosphere 提供 `GetFluidLevel`，或 Terrain 统一提供 `GetBlockAndFluid`）。
- **建议**：在 **DESIGN_VOXEL_ATMOSPHERE.md** 中增加一小节「流体数据与地形的关系」：
  - 方案 A：流体作为地形的一部分（VoxelData 中预留 FluidType + Level），Terrain 负责存储与区块保存；Atmosphere 只负责流动逻辑与渲染。
  - 方案 B：流体由 Atmosphere 插件独立存储，Terrain 提供只读查询（如 `GetBlock`），Atmosphere 提供 `GetFluid(pos)`，流动逻辑在 Atmosphere 内查询两者。
  - 选定方案后，在 Terrain 与 Atmosphere 的实施计划中写出对应任务（例如 Terrain：预留/扩展流体字段；Atmosphere：实现 FluidSimulator 并调用 Terrain/流体查询 API）。

### 3.2 Entity 寻路与「可行走」定义

- **DESIGN_VOXEL_ENTITY.md** 提到 A* 在体素格子上，节点为「Center of block above solid ground」，但未定义何谓 **solid**（例如 `GetBlock != Air`？是否排除草、雪等？），以及**台阶/跳跃**在图中是「相邻格 +1 高度」还是多格高。
- **建议**：在 Entity 设计文档中补充：
  - **Solid**：明确为「Terrain.GetBlock(pos) 属于可站立类型」（可引用 Terrain 的 BlockId 或可配置表）。
  - **移动与跳跃**：水平 4 邻 + 4 对角是否允许、最大步高（如 1 格）、是否允许「跳上 2 格高」等，便于后续实现 A* 的边与代价。

### 3.3 Gameplay 与 Entity 的「当前选中方块」接口

- **DESIGN_VOXEL_ENTITY.md** 中 VoxelPlayer 的「放置」依赖 Gameplay 的 **InventorySystem** 提供「当前选中的方块」；未说明是直接依赖 Gameplay 的类，还是通过接口/信号获取。
- **建议**：在 **DESIGN_VOXEL_GAMEPLAY.md** 或 **DESIGN_VOXEL_ENTITY.md** 中二选一写清：
  - 定义一个小型接口（如 `ISelectedBlockProvider`）或全局/组单例（如 `InventoryService.GetSelectedBlockId()`），由 Gameplay 实现，Entity 的 VoxelPlayer 只依赖该接口；或
  - 明确「VoxelPlayer 与 Inventory UI 同属 Gameplay 模块」，Entity 只提供移动与射线，放置时由 Gameplay 脚本取选中物品并调用 Terrain.SetBlock。  
 这样避免后续在 Entity 与 Gameplay 之间出现循环依赖或隐式耦合。

### 3.4 BlockId 与 ItemID 的约定

- Terrain 使用 **BlockId**；Gameplay 的 Inventory 使用 **ItemID**。多数体素游戏中「可放置方块」是物品的子集，存在 BlockId ↔ ItemID 的映射。
- **建议**：在 **DESIGN_VOXEL_GAMEPLAY.md** 或一份共享的「数据契约」中简短约定：
  - BlockId 与 ItemID 是否同一套 ID（例如 BlockId 即 ItemID），或
  - 存在 BlockLibrary/ItemTable 做映射，放置时由 ItemID 查 BlockId。  
 便于后续实现物品栏与放置逻辑时一致。

### 3.5 全局路线图与阶段顺序

- **MINECRAFT_MAKER_PLAN.md** 有阶段一（Bridge）、二（Entity + Raycast）、三（Fluids/Sky）；各 DESIGN_*.md 又有各自 Phase 1/2/3，但**没有**一份「按时间/依赖排序」的全局路线图。
- **建议**：在 **MINECRAFT_MAKER_PLAN.md** 或 **MINECRAFT_MAKER_OVERVIEW.md** 中增加「全局实施顺序」小节，例如：
  - **Terrain**：Bridge（VoxelBufferWrapper + ScriptableVoxelGenerator）+ **IVoxelQuery**（Raycast + GetCollidingBoxes）→ 再考虑 LOD/光照等。
  - **Entity**：在 IVoxelQuery 可用后做 Physics（Swept AABB）→ VoxelPlayer → 寻路与 AI。
  - **Gameplay**：在 IVoxelQuery（含 Raycast）可用后做 VoxelRaycast 与放置/破坏 → Structure → Inventory。
  - **Atmosphere**：Sky/云可与前几项并行；流体依赖「流体数据归属」决策后再排期。  
 这样各模块的 Phase 1 与「当前焦点」能对齐，减少重复实现（例如 Raycast 只做一次并放在 Terrain 的 IVoxelQuery 中）。

### 3.6 昼夜与大气信号

- **DESIGN_VOXEL_ATMOSPHERE.md** 提到 `DayStarted` / `NightStarted` 信号，未提到**归一化时间**（如 0–1 的 day phase）供 Shader 或逻辑使用。
- **建议**：在 Atmosphere 设计里补充一句：提供 `DayPhase`（0–1）或 `TimeOfDay` 属性，便于天空 Shader 与游戏逻辑（生物生成、睡觉等）复用。

---

## 四、无需大改、仅需留意之处

- **坐标与尺度**：VOXEL_TERRAIN_PLAN 已约定 1 单位 = 1 体素；Entity/Gameplay/Atmosphere 设计中对「格点」「射线步进」等均隐含同一假设，无需改设计，只需在 Entity/Gameplay 文档中**明确引用**「与 Terrain 坐标约定一致（1 unit = 1 voxel）」即可。
- **PRODUCTION_READINESS_GAP**：与 VOXEL_TERRAIN_PLAN 及当前实现一致；P0/P1/P2 已标注清晰，可选扩展（LOD、光照、编辑器工具）与 DESIGN 文档无冲突，保持现状即可。
- **行为树 vs GOAP**：Entity 设计中对 AI 的「行为树或 GOAP」二选一可后续再定，不必在计划阶段锁死，只需在实现计划中先选一种（如行为树）做 Phase 1。

---

## 五、建议的文档修改清单（可逐条执行）

| 序号 | 文档 | 修改类型 | 内容摘要 |
|------|------|----------|----------|
| 1 | MINECRAFT_MAKER_OVERVIEW.md | 修改 | 将 IMPLEMENTATION_PLAN.md 改为 MINECRAFT_MAKER_PLAN.md（及可选 VOXEL_TERRAIN_PLAN.md） |
| 2 | MINECRAFT_MAKER_OVERVIEW.md 或 PLAN | 修改 | 明确 addon 名称与「四模块」是逻辑划分还是未来多 addon |
| 3 | DESIGN_VOXEL_GAMEPLAY.md / OVERVIEW | 统一 | 结构文件扩展名统一为 .structure 或 .struct |
| 4 | VOXEL_TERRAIN_PLAN.md | 细化 | 实施阶段中增加「实现 IVoxelQuery（Raycast、GetCollidingBoxes）」并标为 Entity/Gameplay 依赖 |
| 5 | PRODUCTION_READINESS_GAP.md | 细化 | 在接口/扩展点处注明「对外提供 IVoxelQuery」 |
| 6 | MINECRAFT_MAKER_PLAN 或 DESIGN_VOXEL_TERRAIN | 细化 | 明确 ScriptableVoxelGenerator/VoxelBufferWrapper 为阶段一交付物；或代码中暂时禁用/回退该路径 |
| 7 | DESIGN_VOXEL_ATMOSPHERE.md | 细化 | 增加「流体数据归属与查询」小节（Terrain 存 vs Atmosphere 存 + 查询约定） |
| 8 | DESIGN_VOXEL_ENTITY.md | 细化 | 明确 A* 的 solid 定义与步高/跳跃规则 |
| 9 | DESIGN_VOXEL_ENTITY / GAMEPLAY | 细化 | VoxelPlayer 与 Inventory 的接口或模块边界（谁提供「当前选中方块」） |
| 10 | DESIGN_VOXEL_GAMEPLAY 或术语 | 细化 | BlockId 与 ItemID 的映射或统一约定 |
| 11 | MINECRAFT_MAKER_PLAN 或 OVERVIEW | 细化 | 增加「全局实施顺序」小节（Terrain → IVoxelQuery → Entity/Gameplay → Atmosphere） |
| 12 | DESIGN_VOXEL_ATMOSPHERE.md | 细化 | DayPhase / TimeOfDay 供 Shader 与逻辑使用 |

---

## 六、总结

- **计划本身**：模块划分、技术选型（ECS、Greedy、异步、Scriptable 生成器）合理，与现有实现和 PRODUCTION_READINESS_GAP 基本一致。
- **最值得尽快改的**：修正 OVERVIEW 中失效的 IMPLEMENTATION_PLAN 链接；在 Terrain 计划中显式加入 **IVoxelQuery** 与 **ScriptableVoxelGenerator/VoxelBufferWrapper** 的实现与依赖关系；统一结构文件扩展名与 addon/模块命名。
- **细化后收益最大**：流体数据归属（Atmosphere + Terrain）、Entity 寻路规则、Gameplay–Entity 的选中方块接口、BlockId/ItemID 约定、以及一份简短的全局实施顺序。按上表逐条更新后，后续开发 Entity、Gameplay、Atmosphere 时会更少返工与接口歧义。
