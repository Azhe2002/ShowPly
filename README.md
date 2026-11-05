# DAT / PLY Point Cloud Viewer

## 简介

Unity 2022.3 + URP 点云查看器。独立 Windows 程序运行后输入外部 `.dat` / `.ply` 文件路径即可查看，以 Z 深度灰度渲染。相机操作由运行时代码实现，在 Game View 和打包程序中都可使用。

## 环境

- Unity 2022.3 LTS
- URP（Universal Render Pipeline）
- Windows 11

## 快速使用

1. 打开 `Assets/Scenes/PointCloudViewer.unity`，点击 Play；或运行 `Builds/Windows/ShowPly.exe`。
2. 点击 **Import** 打开文件选择窗口，选择 DAT / PLY 后自动填入完整路径；点击 **Open** 或按回车加载。也可直接粘贴路径，支持中文路径和带空格的路径。取消选择会保留原路径与当前点云。
3. 加载成功后自动居中、以正交 XY 视图完整显示点云。文件读取在后台执行，失败时保留上一份点云。
4. 点击视图区域后操作：

| 操作 | 功能 |
| --- | --- |
| 右键拖拽 | 绕当前观察中心旋转 |
| 中键拖拽 | 沿屏幕方向平移 |
| 滚轮 | 缩放 |
| 右键 + W / S | 向前推近 / 向后拉远（正交模式连续缩放，透视模式沿视线移动） |
| 右键 + A / D | 左右移动 |
| 右键 + Q / E | 沿世界 Y 轴下降 / 上升 |
| 同时按 Shift | 加速移动 |
| F / Focus | 保持当前方向，重新聚焦点云 |
| Home / Reset | 恢复初始正交 XY 视图 |
| F1 | 显示 / 隐藏帮助 |
| Esc | 结束当前拖拽 |

路径输入框获得焦点时暂停相机操作，点击视图后继续。顶部可调整点大小。鼠标拖拽范围为应用窗口内。

也可通过命令行启动：

```powershell
& .\Builds\Windows\ShowPly.exe --file 'D:\Point Clouds\sample.ply'
```

在自己的场景中，只需将 `PointCloudCamera` 挂到 Camera 上。可在 Inspector 指定点云 `MeshRenderer` 或父物体；不指定时会查找点拓扑 Mesh。运行时加载后调用 `SetTarget(cloud.transform)`，也可调用 `FitTopDown(bounds)` / `FitBounds(bounds)`。

## 深度着色说明

- **模型局部 Z 小** → 白色
- **模型局部 Z 大** → 黑色
- 深度范围从 Mesh 包围盒自动计算，无需手动设置
- 旋转相机不会改变深度着色基准；目前不显示 PLY 自带 RGB 颜色
- Windows 使用几何着色器将点扩展为屏幕像素方块，使点大小设置在 D3D11 下生效

## DAT 点云

运行时 DAT 支持项目现有约定：

- 格式：二进制 little-endian，每点 3 个 `float32`（x, y, z），无颜色
- 不含文件头，文件长度必须是 12 的正整数倍
- DAT 并非统一标准；其他 DAT 布局需要修改 `PointCloudFile.ReadDat`

运行时 PLY 支持 ASCII、小端二进制、大端二进制 PLY 1.0，支持不同顺序的 XYZ 标量及 float / double 等常用类型。顶点必须在其他非空 element 之前，顶点内部的 list 属性不支持；顶点后的面数据忽略。NaN / Infinity 坐标会跳过，无有效点时报告错误。

默认最多加载 500 万个点，可在 `PointCloudViewer` Inspector 修改上限。读取完成后会在主线程生成 Mesh，大文件此阶段可能短暂卡顿。点坐标使用 Unity float 精度。

原有编辑器导入方式仍可使用：把 `.dat` / `.ply` 拖入 Assets，再将生成的 Prefab 放到场景中。

## 项目结构

```
Assets/
├── PointCloudsPly/           ← 放 .ply 文件
├── PointCloudDat/            ← 放 .dat 文件
├── Pcx/                      ← Pcx 点云导入框架 (Pcx4D + URP 适配)
│   ├── Editor/PlyImporter.cs ← .ply ScriptedImporter
│   ├── Editor/DatImporter.cs ← .dat ScriptedImporter
│   └── Shaders/              ← Pcx 原始 shader (已弃用)
├── Shaders/
│   └── PointCloudUnlit.shader ← 深度灰度着色器（已加入 Always Included Shaders）
└── Scripts/
    ├── PointCloudCamera.cs    ← 运行时相机导航、聚焦和裁剪范围
    ├── PointCloudFile.cs      ← 运行时 DAT / PLY 解析
    ├── PointCloudViewer.cs    ← 路径输入、后台读取和资源管理
    └── Editor/               ← 场景生成、Windows 构建和校验
```

## 构建

Unity 菜单 **Tools → Point Cloud Viewer → Build Windows** 生成 `Builds/Windows/ShowPly.exe`。分发时复制整个 `Builds/Windows` 文件夹。

新查看场景是默认构建入口，原有场景保留。菜单 **Create Runtime Scene** 可重新生成查看场景；会重置该场景内容及 Build Settings 的场景列表。

菜单 **Validate** 校验 DAT、ASCII / 二进制 PLY、CRLF、属性顺序、大小端、错误输入、相机完整聚焦，以及本机样本。批处理校验并构建：

```powershell
& 'D:\Program Files\Unity 2022.3.62f3c1\Editor\Unity.exe' -batchmode -quit -projectPath 'E:\Unity\ShowPly' -executeMethod PointCloudViewerBuild.ValidateAndBuild -logFile 'E:\Unity\ShowPly\Logs\PointCloudViewerBuild.log'
```

目标平台为 Windows 桌面，显卡需支持几何着色器；使用项目当前的旧版 Input Manager（Active Input Handling = Input Manager 或 Both）。

## 依赖

- [Pcx4D](https://github.com/romanesco/Pcx4D) — PLY 导入框架 (MIT)
  - `Assets/Pcx/` 下所有文件
  - 已针对 Unity 2022.3 URP 适配修复：
    - Shader CGPROGRAM → HLSLPROGRAM
    - ScriptedImporter 命名空间修正
    - ASCII PLY `\r\n` 换行兼容
    - Mesh 自动居中
