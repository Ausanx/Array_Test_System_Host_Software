# Project Context

## Purpose

交叉阵列测试系统控制软件（Array Test System Host Software）是一款用于 512×512 忆阻器阵列的高速成像与控制软件。

**核心目标:**

- 实时采集和可视化 512×512 忆阻器阵列的电流数据
- 通过 Gigabit Ethernet (UDP) 与 FPGA 硬件通信
- 提供低延迟（<10ms）的热力图渲染性能
- 支持科研实验的多种工作模式（预览/高精度）

**应用场景:**

- 忆阻器阵列特性测试
- 神经形态计算研究
- 新型存储器件评估

## Tech Stack

### 核心技术

- **.NET 6.0** - 应用框架
- **C# 10** - 编程语言
- **WinForms** - 桌面UI框架
- **Windows (6.1+)** - 目标平台

### 通信协议

- **UDP Socket** - 千兆以太网通信
- **自定义协议** - FPGA 数据包格式（固定包头 0xAA55AA55）
- **端口**: RX=8081, TX=8080

### 性能优化技术

- **LockBits** - 直接内存操作，unsafe代码
- **生产者-消费者模式** - 多线程解耦
- **ConcurrentQueue** - 线程安全队列
- **内存池复用** - 减少GC压力

### 第三方库

- `System.Drawing.Common 8.0.0` - 图像处理
- `System.Collections.Concurrent 4.3.0` - 并发集合

## Project Conventions

### 代码风格

**命名约定:**

- 类名: PascalCase (`HeatmapRenderer`, `UdpReceiver`)
- 方法名: PascalCase (`ProcessPacket`, `RenderTo`)
- 私有字段: \_camelCase (`_udpReceiver`, `_currentFrame`)
- 常量: UPPER_SNAKE_CASE (`ARRAY_SIZE`, `VALID_HEADER`)

**文件组织:**

- 每个类一个文件
- Partial class 分离 UI 逻辑和设计器代码
  - `MainForm.cs` - 业务逻辑
  - `MainForm.Designer.cs` - UI布局代码

**注释规范:**

- 使用 XML 文档注释（`///`）描述公共API
- 中文注释解释复杂逻辑
- 区域标记 (`// ==================== 区域名 ====================`)

### 架构模式

**生产者-消费者模式:**

```
[UDP线程] → [FrameReassembler] → [ConcurrentQueue] → [UI Timer] → [渲染]
   生产者          重组器              缓冲区         消费者        显示
```

**模块分层:**

1. **Driver层** - 硬件通信（无UI依赖）
2. **Core层** - 算法核心（无UI依赖）
3. **UI层** - 用户界面（依赖Driver和Core）

**错误处理策略:**

- UDP丢包: 不崩溃，显示上一帧或黑色坏线
- 连接断开: 3秒超时自动提示，支持重连
- 异常捕获: 不阻塞UI线程

### 性能要求

**硬性指标:**

- 热力图渲染: <10ms/帧
- 目标帧率: 30 FPS
- 最大丢包率: <5%
- UI响应: 始终流畅（不卡顿）

**内存管理:**

- 复用 Bitmap 对象避免频繁分配
- 帧队列限制为10帧防止溢出
- 避免在热路径中使用 LINQ

### 测试策略

**当前状态:**

- 无自动化测试（科研原型阶段）
- 手动测试为主

**未来计划:**

- 单元测试: Driver 层协议解析
- 集成测试: UDP 模拟数据流
- 性能测试: 渲染帧率基准

### Git 工作流

**分支策略:**

- `main` - 稳定版本
- `feature/*` - 功能开发
- `fix/*` - Bug修复

**提交规范:**

- 使用中英文混合commit message
- 示例: `feat: 添加多帧平均算法`
- 类型: feat, fix, docs, style, refactor, perf, test

## Domain Context

### 忆阻器阵列知识

**硬件架构:**

- 512×512 交叉阵列（crossbar array）
- TIA（跨阻放大器）电流-电压转换
- FPGA 控制扫描和ADC采样

**数据格式:**

- 原始数据: Int32（4字节/像素）
- 数据含义: 电流值（单位取决于TIA量程）
- 扫描方式: 逐行扫描（512行）

**工作模式:**

- **Preview模式**: 低Wait_Cycles，快速预览用于阵列对齐
- **Precision模式**: 高Wait_Cycles，支持多帧平均降噪

### UDP 协议细节

**接收包结构 (RX - Port 8081):**

```
[Header: 4B][FrameID: 4B][RowIndex: 2B][SegIndex: 1B][DataType: 1B][Data: 512B]
- Header: 0xAA55AA55（固定）
- 一行 512 像素 = 4 个分段（每段 128 像素）
- 完整帧 = 512 行 × 4 分段 = 2048 个 UDP 包
```

**发送命令结构 (TX - Port 8080):**

```
[Header: 4B][CmdID: 4B][Mode: 4B][Param1: 4B][Param2: 4B][Reserved: 12B]
- Header: 0x55AAAA55（固定）
- CmdID: 1=Reset, 2=Config, 3=Start, 4=Stop
- 总长度: 32 字节
```

### 伪彩色映射

**映射方式:**

- 输入: Int32 电流值
- 归一化: [Min, Max] → [0, 255]
- 查找表: 256色预计算RGB

**支持的colormap:**

- Jet: 蓝→青→黄→红（经典科学可视化）
- Parula: 柔和渐变（MATLAB默认）
- Gray: 灰度图
- Hot: 黑→红→黄→白（热分布）
- Viridis: 感知均匀（色盲友好）

## Important Constraints

### 技术约束

- **仅支持 Windows 平台** - 使用 GDI+ 和 WinForms
- **必须使用 unsafe 代码** - LockBits 性能优化
- **单线程UI** - WinForms 限制，需用 Invoke 跨线程
- **.NET 6.0 目标** - 不向下兼容 .NET Framework

### 硬件约束

- **需要千兆以太网** - UDP吞吐量要求
- **FPGA固定协议** - 无法修改包格式
- **512×512 固定分辨率** - 硬编码在多处

### 业务约束

- **预留功能锁定** - 神经形态/IV扫描需许可证
- **科研工具定位** - 非商业产品级要求

## External Dependencies

### 硬件依赖

- **FPGA控制板** - 阵列扫描控制器
- **忆阻器阵列芯片** - 512×512 crossbar
- **千兆以太网交换机** - 低延迟网络

### 网络配置

- FPGA 默认 IP: `192.168.1.100`
- 主机需配置同网段IP（如 `192.168.1.10`）
- 防火墙需开放 UDP 8080/8081 端口

### 开发环境

- **Visual Studio 2022** - 推荐IDE
- **.NET 6.0 SDK** - 必需
- **Windows 10/11** - 开发和运行平台

### 可选外部系统（未实现）

- Keithley 源表 - IV曲线测试
- STDP训练模块 - 神经形态计算
- 数据库 - 实验数据存储
