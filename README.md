# 交叉阵列测试系统控制软件

## 项目概述

512x512 忆阻器阵列的高速成像与控制软件，支持 Gigabit Ethernet (UDP) 通信协议。

## 技术架构

### 1. 软件架构

- **通信层 (Producer)**: 独立后台线程，UDP 数据接收、包重组、校验
- **数据层 (Buffer)**: 线程安全的双缓冲队列存储完整帧数据
- **渲染层 (Consumer)**: UI 定时器 (30ms/帧) 伪彩色转换和热力图绘制
- **控制层 (Command)**: 封装指令协议，下发给 FPGA

### 2. 模块划分

#### ArrayCamera.Driver (通信驱动库)

- `Protocol.cs` - UDP 协议结构体定义
- `FrameReassembler.cs` - 帧重组逻辑（512行 × 4分段）
- `UdpReceiver.cs` - UDP 接收器（生产者模式）

#### ArrayCamera.Core (渲染核心库)

- `HeatmapRenderer.cs` - 高性能热力图渲染（LockBits技术）

#### ArrayCamera.UI (用户界面)

- `MainForm.cs` - 主窗体（生产者-消费者模式）
- `Program.cs` - 程序入口

## 功能特性

### ✅ 已实现

- [x] UDP 数据接收与分包重组（512×512 像素，4分段/行）
- [x] 生产者-消费者模式确保 UI 不卡顿
- [x] 高性能热力图渲染（LockBits，<10ms）
- [x] 5种伪彩色映射（Jet, Parula, Gray, Hot, Viridis）
- [x] 预览模式与高精模式切换
- [x] 多帧平均降噪算法
- [x] 丢包容错与自动重连
- [x] 实时 FPS、丢包率显示
- [x] 鼠标悬停显示像素电流值

### 🔒 预留功能（需许可证）

- [ ] 神经形态训练模块
- [ ] 独立器件测试（IV 曲线扫描）
- [ ] 外部源表控制（Keithley）

## 快速开始

### 环境要求

- .NET 6.0 或更高版本
- Windows 10/11
- Visual Studio 2022 或 VS Code

### 编译运行

```bash
# 还原依赖包
dotnet restore

# 编译整个解决方案
dotnet build

# 运行程序
dotnet run --project src/ArrayCamera.UI/ArrayCamera.UI.csproj
```

### 使用 Visual Studio

1. 双击打开 `ArrayCameraHost.sln`
2. 设置 `ArrayCamera.UI` 为启动项目
3. 按 F5 运行

## 通信协议

### 接收端口: 8081 (RX)

- 包头: `0xAA55AA55`
- 一帧 = 512行，一行 = 4个分段（每段128像素）
- 数据格式: Int32 (4 bytes/pixel)

### 发送端口: 8080 (TX)

- 包头: `0x55AAAA55`
- 固定 32 字节
- 命令: Reset(1), Config(2), Start(3), Stop(4)

## 性能指标

- **渲染性能**: <10ms/帧 (512×512像素)
- **目标帧率**: 30 FPS
- **内存管理**: 复用内存池，减少 GC 压力
- **丢包容错**: 支持乱序、丢包恢复

## 交付物

- ✅ Visual Studio 解决方案（完整源码）
- ⏳ 独立 DLL 库（ArrayCamera.Driver.dll）
- ⏳ 安装包（Setup.exe）

## 作者

Ausan + Gemini + Github copilot
© 2026
