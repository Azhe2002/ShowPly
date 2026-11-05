# PLY Point Cloud Viewer — Unity Editor

## 简介

将 `.ply` 点云文件拖入 Unity 编辑器，自动导入并以深度灰度（近白远黑）渲染，支持 Unity 内置场景工具自由旋转、平移、缩放查看。

## 环境

- Unity 2022.3 LTS
- URP（Universal Render Pipeline）
- Windows 11

## 快速使用

1. 将 `.ply` 文件拖入 `Assets/PointClouds/` 目录
2. Unity 自动导入，生成 Prefab（点云居中 + 深度灰度材质）
3. 将生成的 Prefab 拖入 Scene
4. 选中点云，按 **F** 聚焦
5. 使用 Unity Scene View 工具查看：
   - **右键拖拽** — 旋转视角
   - **中键拖拽** — 平移
   - **滚轮** — 缩放

## 深度着色说明

- **近处（Z 小）** → 白色
- **远处（Z 大）** → 黑色
- 深度范围从 Mesh 包围盒自动计算，无需手动设置

## DAT 点云

`.dat` 文件同样可以直接拖入 Unity，由 `DatImporter` 自动导入：

- 格式：二进制 little-endian，每点 3 个 `float32`（x, y, z），无颜色
- 建议放在 `Assets/PointCloudDat/` 下
- 导入后生成与 PLY 相同的 Prefab（点云居中 + 深度灰度材质），拖入 Scene 即可查看

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
    └── PointCloudCamera.cs    ← 相机辅助脚本 (可选)
```

## 构建

当前仅支持 Unity Editor 内使用。构建独立应用的功能已移除。

## 依赖

- [Pcx4D](https://github.com/romanesco/Pcx4D) — PLY 导入框架 (MIT)
  - `Assets/Pcx/` 下所有文件
  - 已针对 Unity 2022.3 URP 适配修复：
    - Shader CGPROGRAM → HLSLPROGRAM
    - ScriptedImporter 命名空间修正
    - ASCII PLY `\r\n` 换行兼容
    - Mesh 自动居中
