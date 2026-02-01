# 📋 FPGA 集成文档 - 完整索引

> 本项目包含完整的FPGA-PC通信协议文档。按照下面的顺序阅读。

## 🎯 5分钟快速开始

**第一步** → 打开这个文件了解基本架构:

- [FPGA-QUICK-REFERENCE.md](FPGA-QUICK-REFERENCE.md)
  - 网络配置 (IP/端口)
  - 28字节指令包格式
  - 524字节图像包格式
  - 工作流程图

**第二步** → 测试Python参考实现:

```bash
python fpga_reference_implementation.py
```

**第三步** → 启动C#程序测试连接:

```bash
.\ArrayCamera\bin\Release\net6.0-windows\ArrayCamera.exe
```

---

## 📚 完整文档清单

### 📖 核心文档

| 文档                                                               | 用途            | 阅读时间 |
| ------------------------------------------------------------------ | --------------- | -------- |
| **[FPGA-QUICK-REFERENCE.md](FPGA-QUICK-REFERENCE.md)**             | 快速参考卡片    | 5分钟    |
| **[FPGA-PROTOCOL.md](FPGA-PROTOCOL.md)**                           | 详细协议规范    | 30分钟   |
| **[FPGA-VERILOG-REFERENCE.md](FPGA-VERILOG-REFERENCE.md)**         | Verilog代码参考 | 1小时    |
| **[FPGA-INTEGRATION-CHECKLIST.md](FPGA-INTEGRATION-CHECKLIST.md)** | 集成检查清单    | 20分钟   |
| **[FPGA-INTEGRATION-README.md](FPGA-INTEGRATION-README.md)**       | 集成指南总览    | 15分钟   |

### 💻 代码参考

| 文件                                                                 | 说明                            | 语言   |
| -------------------------------------------------------------------- | ------------------------------- | ------ |
| [fpga_reference_implementation.py](fpga_reference_implementation.py) | **完整的参考实现** (可直接运行) | Python |
| [ArrayCamera/NetworkDriver.cs](ArrayCamera/NetworkDriver.cs)         | C#网络驱动（PC侧）              | C#     |
| [udp_device_sim.py](udp_device_sim.py)                               | Python模拟器                    | Python |

### 🛠️ 工具脚本

| 脚本                                       | 功能           |
| ------------------------------------------ | -------------- |
| [start_fpga_sim.bat](start_fpga_sim.bat)   | 启动FPGA模拟器 |
| [test_connection.bat](test_connection.bat) | 一键测试连接   |

---

## 🗺️ 文档导航图

```
开始
  ↓
阅读FPGA-QUICK-REFERENCE.md (5分钟)
  ├─ 理解网络配置
  ├─ 理解指令包格式
  ├─ 理解图像包格式
  └─ 理解工作流程
  ↓
运行fpga_reference_implementation.py
  ├─ 测试FPGA模拟器
  └─ 与C#程序连接
  ↓
连接成功?
  ├─ YES → 阅读FPGA-PROTOCOL.md (深入理解)
  └─ NO  → 查看FPGA-INTEGRATION-CHECKLIST.md (调试)
  ↓
实现你的FPGA代码
  ├─ 参考: FPGA-VERILOG-REFERENCE.md (Verilog代码框架)
  └─ 参考: fpga_reference_implementation.py (完整实现逻辑)
  ↓
集成到你的FPGA项目
  ├─ 按照FPGA-PROTOCOL.md规范实现
  └─ 按照FPGA-INTEGRATION-CHECKLIST.md检查
  ↓
通过所有测试 ✓
  └─ 集成完成！
```

---

## 🎓 学习路线

### 初级 (第1天)

1. 阅读 [FPGA-QUICK-REFERENCE.md](FPGA-QUICK-REFERENCE.md) (5分钟)
2. 运行 `python fpga_reference_implementation.py` (2分钟)
3. 运行C#程序并点击"连接/绑定" (3分钟)
4. **目标**: 理解协议基础，完成握手测试

### 中级 (第2-3天)

1. 阅读 [FPGA-PROTOCOL.md](FPGA-PROTOCOL.md) (30分钟)
2. 理解数据包格式和命令列表 (20分钟)
3. 使用Wireshark抓包验证 (30分钟)
4. **目标**: 深入理解协议细节

### 高级 (第4-7天)

1. 阅读 [FPGA-VERILOG-REFERENCE.md](FPGA-VERILOG-REFERENCE.md) (1小时)
2. 实现UDP收发模块 (4小时)
3. 实现命令处理逻辑 (2小时)
4. 实现图像数据打包 (2小时)
5. **目标**: 完整的FPGA实现

### 测试与验证 (最后1天)

1. 按照 [FPGA-INTEGRATION-CHECKLIST.md](FPGA-INTEGRATION-CHECKLIST.md) 检查 (2小时)
2. 运行完整的集成测试 (2小时)
3. **目标**: 所有检查项通过，准备上线

---

## 🔑 关键概念速记

### 网络配置

```
FPGA IP:          192.168.2.88
PC IP:            192.168.2.100 (或其他192.168.2.x)
FPGA RX 端口:      8080 (接收指令)
FPGA TX 端口:      8081 (发送图像)
```

### 指令包 (28字节)

```
Header: 0x55AAAA55 → 验证合法性
CmdID:  0-4 → 具体命令
关键：收到任何指令后立即回复相同的28字节到发送方:8080
```

### 图像包 (524字节)

```
Magic:  0xAA55AA55 → 验证图像包
Data:   128×uint32 像素数据
总量：  512行 × 4段 × 524字节 = 1MB/帧
```

### 帧率计算

```
30FPS × 1MB/帧 = 30MB/s
占用千兆网: 30MB/s ÷ 125MB/s = 24% ✓
```

---

## 🧪 快速测试

### 方式1: 使用Python模拟器

```bash
# 终端1
python fpga_reference_implementation.py

# 终端2
.\ArrayCamera\bin\Release\net6.0-windows\ArrayCamera.exe

# C#窗口
点击"连接/绑定" → 预期连接成功
点击"开始扫描"  → 预期显示波形
```

### 方式2: 使用真实FPGA板卡

需要先在FPGA上实现UDP协议，参考:

- [FPGA-PROTOCOL.md](FPGA-PROTOCOL.md) 完整规范
- [FPGA-VERILOG-REFERENCE.md](FPGA-VERILOG-REFERENCE.md) 代码框架

---

## ❓ 常见问题

### Q: 从哪里开始？

**A**: 打开 [FPGA-QUICK-REFERENCE.md](FPGA-QUICK-REFERENCE.md)

### Q: 怎样验证协议是否正确？

**A**: 使用Wireshark抓包，参考 [FPGA-PROTOCOL.md](FPGA-PROTOCOL.md) 中的"测试步骤"

### Q: 有Python参考代码吗？

**A**: 是的，[fpga_reference_implementation.py](fpga_reference_implementation.py) 包含完整实现

### Q: 有Verilog参考代码吗？

**A**: 是的，[FPGA-VERILOG-REFERENCE.md](FPGA-VERILOG-REFERENCE.md) 包含详细框架

### Q: 集成中出现问题怎么办？

**A**: 查看 [FPGA-INTEGRATION-CHECKLIST.md](FPGA-INTEGRATION-CHECKLIST.md) 的错误排查部分

---

## 📞 文档关联

```
快速了解 → FPGA-QUICK-REFERENCE.md
      ↓
深入学习 → FPGA-PROTOCOL.md
      ↓
开始编码 → FPGA-VERILOG-REFERENCE.md + fpga_reference_implementation.py
      ↓
集成测试 → FPGA-INTEGRATION-CHECKLIST.md
      ↓
遇到问题 → FPGA-INTEGRATION-CHECKLIST.md → 错误排查
```

---

## 📊 文件大小速览

| 文件                             | 大小  | 说明       |
| -------------------------------- | ----- | ---------- |
| FPGA-QUICK-REFERENCE.md          | ~4KB  | 快速参考   |
| FPGA-PROTOCOL.md                 | ~25KB | 详细规范   |
| FPGA-VERILOG-REFERENCE.md        | ~20KB | 代码框架   |
| FPGA-INTEGRATION-CHECKLIST.md    | ~18KB | 检查清单   |
| fpga_reference_implementation.py | ~12KB | Python实现 |

---

## ✅ 开始前检查表

- [ ] 已下载所有文档
- [ ] 已安装Python 3.6+
- [ ] 已安装.NET 6.0 SDK
- [ ] 有Wireshark进行抓包分析
- [ ] FPGA板卡IP配置为192.168.2.88
- [ ] 网线连接正常

---

## 🎉 准备好了吗？

**立即开始**: 打开 [FPGA-QUICK-REFERENCE.md](FPGA-QUICK-REFERENCE.md)

**或者先测试**: 运行 `python fpga_reference_implementation.py`

**或者先看全景**: 阅读 [FPGA-PROTOCOL.md](FPGA-PROTOCOL.md)

---

**最后更新**: 2026-01-29
