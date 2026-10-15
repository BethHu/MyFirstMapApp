# MyFirstMapApp

基于 **ArcGIS Runtime SDK for .NET (100.13)** 与 **WPF** 开发的地图工具应用，实现了二维地图显示、多图层管理、地理要素绘制与测量、地址搜索、路径规划以及三维场景浏览等功能。

## 功能特性

- **底图切换**：支持街道地图、地形图、影像图、浅色图、深色图、导航图等多种底图。
- **图层管理**：加载 Shapefile / GeoJSON 等数据源，支持图层添加、删除、拖拽排序、可见性控制与颜色样式设置。
- **要素绘制**：绘制点、线、面，支持通过地图点击或输入坐标两种方式。
- **距离 / 面积测量**：在地图上实时测量两点距离与多边形面积。
- **属性查看**：点击要素查看其属性信息。
- **地址搜索**：基于地理编码服务实现地址搜索与快速定位。
- **路径规划**：支持驾车 / 步行的起点终点路径计算，显示距离与耗时。
- **三维场景**：MapView / SceneView 切换，支持 Web Scene、3D 底图、俯视 / 倾斜 / 旋转等视角控制。
- **数据导出**：将绘制的图形导出为 GeoJSON。

## 技术栈

| 项目 | 版本 |
|------|------|
| .NET Framework | 4.7.2 |
| ArcGIS Runtime SDK | 100.13 |
| Newtonsoft.Json | 13.0.4 |
| Visual Studio | 2019+ |

## 运行环境

- Windows 10/11 64 位
- Visual Studio 2019（含 .NET 桌面开发工作负载）
- 稳定的互联网连接（访问 ArcGIS Online 在线服务）

## 快速开始

1. 使用 Visual Studio 打开 `MyFirstMapApp.sln`。
2. 通过 NuGet 还原依赖包（`Esri.ArcGISRuntime`、`Esri.ArcGISRuntime.WPF`、`Newtonsoft.Json`）。
3. 在 `MyFirstMapApp/App.xaml.cs` 中设置你的 ArcGIS API Key：

   ```csharp
   ArcGISRuntimeEnvironment.ApiKey = "YOUR_API_KEY";
   ```

4. 生成并运行项目（F5）。

## 项目结构

```
MyFirstMapApp/
├── App.xaml / App.xaml.cs              # 应用入口、API Key 初始化
├── MainWindow.xaml / .xaml.cs          # 主界面与核心逻辑
├── LayerStyleDialog.xaml / .xaml.cs    # 图层样式设置对话框
├── ColorPickerDialog.xaml / .xaml.cs   # 颜色选择对话框
├── InputDialog.xaml / .xaml.cs         # 通用输入对话框
├── CoordinateInputDialog.xaml / .cs    # 坐标输入对话框
├── SelectionDialog.xaml / .xaml.cs     # 选择对话框
├── AddressResultDialog.xaml / .xaml.cs # 地址搜索结果对话框
├── QuickLocateDialog.xaml / .xaml.cs   # 快速定位对话框
└── Properties/                         # 程序集与资源信息
```

## 安全提示

`App.xaml.cs` 中当前硬编码了 API Key。请勿将真实密钥提交到公开仓库，建议改用环境变量或本地配置文件的方式管理密钥，并在公开前轮换（撤销）已泄露的密钥。

## 许可

本项目仅用于学习与教学目的。
