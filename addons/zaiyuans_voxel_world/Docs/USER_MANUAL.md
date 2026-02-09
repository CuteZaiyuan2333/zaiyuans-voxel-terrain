# zaiyuan's voxel world — 用户手册

> 适用于 **Godot 4.6 Mono (C#)**。区块尺寸 32×32×32，1 世界单位 = 1 体素边长。

---

## 一、快速开始

1. 在场景中添加节点：**节点** → **其他节点** → 搜索 **VoxelTerrain**，添加。
2. 在检查器中可调整 **World Settings** 分组下的种子、视距、存档目录、每帧预算等（见下文）；可选将相机或玩家节点拖到 **Observer Node**，用于观察者位置与视锥剔除。
3. **编辑器预览**：选中 VoxelTerrain 后，在检查器 **Editor** 分组下勾选 **Preview Terrain** 可在不运行游戏的情况下生成并显示一小块地形（勾选后会自动取消勾选）；勾选 **Clear Preview** 可清除预览。运行场景时预览会自动移除。预览使用当前 **Seed**，仅用于布局与调试，不写入存档。
4. 运行场景：地形会按观察者周围自动加载区块；若配置了 **Save Directory**，会从磁盘加载已存区块并保存修改过的区块。

---

## 二、节点与配置

### VoxelTerrain（Node3D）

选中 VoxelTerrain 即可在检查器中调整以下内容，无需单独添加 VoxelWorld 节点（未找到 VoxelWorld 时会自动创建并应用这些参数）。

- **Observer Node**：用于驱动加载范围的观察者（如 Camera3D）。若不设置，使用本节点 `GlobalPosition`。
- **World Settings**（分组）：
  - **Seed**：地形生成种子（默认 12345）。
  - **View Distance In Chunks**：以观察者为中心加载的区块半径（默认 4）。
  - **Save Directory**：存档目录，空则不存盘；例：`user://saves/world1`。
  - **Max Spawn Per Frame** / **Max Terrain Gen Per Frame** / **Max Mesh Build Per Frame**：每帧生成与网格预算（默认 2 / 2 / 4）。
  - **Use Greedy Meshing** / **Use Async Terrain** / **Use Async Mesh**：网格合并与异步选项（默认 true / false / false）。
  - **Max Chunk Radius**：世界边界，0 表示不限制；超出时 GetBlock 返回 Air，SetBlock 返回 false。

每帧会查找或自动创建 **VoxelWorld**，并调用其 ECS 与渲染；区块的 `MeshInstance3D` 会挂在本节点下。若场景中已有 **VoxelWorld**（如通过唯一名 `%VoxelWorld` 或根节点下名为 `VoxelWorld`），则使用该节点且**不会**用 VoxelTerrain 上的 World Settings 覆盖它；此时需在 VoxelWorld 节点上改生成器与高级选项。

### VoxelWorld（Node）

可单独挂到场景（如根节点下命名为 `VoxelWorld`），或由 VoxelTerrain 自动创建。主要属性与 VoxelTerrain 的 World Settings 一致，此外：

| 属性 | 说明 |
|------|------|
| **Block Library** | 可选。方块列表资源，供世界生成器按名称查 ID（见第五节）。 |
| **Generator** | 自定义地形生成器（代码中赋值）；不赋则使用 DefaultTerrainGenerator。 |

---

## 三、读写体素

- **设置体素**：`VoxelWorld.SetBlock(worldPos, blockId)` 或 `VoxelTerrain.SetBlock(worldPos, blockId)`。  
  - 返回 `bool`（仅 VoxelWorld 的 API）：已写入或已加入待办返回 true；EcsWorld 为空或超出 `MaxChunkRadius` 返回 false。  
  - 若该区块尚未加载，修改会进入 **PendingBlocks**，待区块生成后自动应用。  
  - 当前块与六邻已加载区块会被标为 Dirty，下一帧重算网格。
- **读取体素**：`VoxelWorld.GetBlock(worldPos)` 或 `VoxelTerrain.GetBlock(worldPos)`。  
  - 区块未加载或超出 `MaxChunkRadius` 时返回 `BlockId.Air`。

世界坐标为整数（Vector3I），1 单位 = 1 体素边长。

---

## 四、存档

- 在 VoxelWorld 上设置 **Save Directory**（如 `user://saves/world1`）。
- 区块在**卸载时**若被标记为脏（玩家修改过），会写入 `{SaveDirectory}/chunks/cx_cy_cz.chunk`（二进制 + Deflate 压缩）。
- 再次加载时，Spawn 会优先从该路径 TryLoad；若文件存在且合法则直接填充体素并应用 PendingBlocks，否则走地形生成。

---

## 五、方块列表（Block Library）与自定义地形生成

### 方块列表（供生成器使用）

- 新建 **BlockLibrary** 资源：在文件系统中右键 → 新建资源 → 搜索 **BlockLibrary**，创建并保存（如 `block_library.tres`）。
- 在 BlockLibrary 的 **Blocks** 数组中添加 **BlockLibraryEntry**，每条设置 **Id**（byte）和 **Name**（string）。Id 0 约定为空气；名称供生成器按名查 ID。
- 将 BlockLibrary 资源拖到 **VoxelWorld** 的 **Block Library** 属性。世界生成器会收到该引用，可通过 `blockLibrary.GetIdByName("Grass")` 等写入体素；未配置时生成器使用内置 `BlockId` 枚举数值。

### 自定义地形生成

- 实现接口 **IChunkGenerator**，方法签名为：  
  `void Generate(Vector3I chunkPos, VoxelData data, int seed, BlockLibrary blockLibrary = null)`  
  在 `data` 中按区块内坐标写入体素（byte）。当 `blockLibrary != null` 时可用 `blockLibrary.GetIdByName("名称")` 获取方块 ID；为 null 时使用 `BlockId` 枚举值。
- 将实例赋给 **VoxelWorld.Generator**；若不赋，使用默认 **DefaultTerrainGenerator**（噪声 + 地表 + 草地/泥土/石头分层；若提供了 BlockLibrary 则按名称解析 Grass/Dirt/Stone，否则用枚举）。

---

## 六、信号（事件）

在 VoxelWorld 上可连接以下信号（参数为 int，便于 GDScript 等使用）：

- **ChunkLoaded(cx, cy, cz)**：某区块首次完成网格并挂上 MeshInstance 时触发。
- **ChunkUnloaded(cx, cy, cz)**：某区块即将卸载时触发。
- **BlockChanged(wx, wy, wz, oldId, newId)**：某格体素被 SetBlock 修改时触发（oldId/newId 为 BlockId 整数值）。

用于玩法逻辑、音效、粒子、存档脏标记等。

---

## 七、性能建议

- 视距不要过大（如 4～8 区块），并配合 **Max Spawn/Terrain Gen/Mesh Build Per Frame** 限制每帧负载。
- 开启 **Use Greedy Meshing** 可显著减少顶点与 Draw Call。
- 大视距或低端设备可尝试 **Use Async Terrain** 与 **Use Async Mesh**，将生成放到工作线程；注意 Godot 的 Mesh/Node 仅在主线程提交。
- 将 **Observer Node** 设为相机时，会自动做视锥剔除，背后与地下区块不绘制。

---

## 八、单元测试

项目内 **Tests/** 下提供对 **VoxelConstants**（坐标/索引）与 **ChunkStorage**（存盘/读盘 roundtrip）的单元测试。在项目根目录执行：

```bash
dotnet test Tests/zaiyuan-s-voxel-world.Tests.csproj
```

（需已安装 .NET SDK；若主项目依赖 Godot 运行时，可在 Godot 编辑器中运行项目后，再在外部执行上述命令或通过 CI 配置。）

---

*最低 Godot 版本：4.6 | 插件路径：addons/zaiyuans_voxel_world*
