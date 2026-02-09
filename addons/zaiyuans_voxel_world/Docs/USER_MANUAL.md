# zaiyuan's voxel world — 用户手册

> 适用于 **Godot 4.6 Mono (C#)**。区块尺寸 32×32×32，1 世界单位 = 1 体素边长。

---

## 一、快速开始

1. 在场景中添加节点：**节点** → **其他节点** → 搜索 **VoxelTerrain**，添加。
2. （可选）将相机或玩家节点拖到 VoxelTerrain 的 **Observer Node** 属性，用于观察者位置与视锥剔除；不设则使用 VoxelTerrain 自身位置。
3. 运行场景：地形会按观察者周围自动加载区块；若配置了 **Save Directory**，会从磁盘加载已存区块并保存修改过的区块。

---

## 二、节点与配置

### VoxelTerrain（Node3D）

- **Observer Node**：用于驱动加载范围的观察者（如 Camera3D）。若不设置，使用本节点 `GlobalPosition`。
- 每帧会查找或自动创建 **VoxelWorld**，并调用其 ECS 与渲染；区块的 `MeshInstance3D` 会挂在本节点下。

### VoxelWorld（Node）

可单独挂到场景（如根节点下命名为 `VoxelWorld`），或由 VoxelTerrain 自动创建。主要属性：

| 属性 | 说明 | 默认 |
|------|------|------|
| **Seed** | 地形生成种子 | 12345 |
| **View Distance In Chunks** | 以观察者为中心加载的区块半径（区块数） | 4 |
| **Save Directory** | 存档目录（空则不存盘）。例：`user://saves/world1` | "" |
| **Max Spawn Per Frame** | 每帧最多新生成的区块数 | 2 |
| **Max Terrain Gen Per Frame** | 每帧最多地形生成的区块数 | 2 |
| **Max Mesh Build Per Frame** | 每帧最多网格重算的区块数 | 4 |
| **Use Greedy Meshing** | 是否使用 Greedy Meshing 合并面 | true |
| **Use Async Terrain** | 地形生成是否在工作线程执行 | false |
| **Use Async Mesh** | 网格生成是否在工作线程执行 | false |
| **Max Chunk Radius** | 世界边界（0=不限制）。超出时 GetBlock 返回 Air，SetBlock 返回 false | 0 |

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

## 五、自定义地形生成

- 实现接口 **IChunkGenerator**（方法 `Generate(chunkPos, data, seed)`），在 `data` 中按区块内坐标写入体素（byte）。
- 将实例赋给 **VoxelWorld.Generator**；若不赋，使用默认 **DefaultTerrainGenerator**（噪声 + 地表 + 草地/泥土/石头分层）。

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
