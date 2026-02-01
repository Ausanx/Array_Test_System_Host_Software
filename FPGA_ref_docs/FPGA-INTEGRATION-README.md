# FPGA 集成指南 - 完整文档

本目录包含FPGA板卡与C# ArrayCamera软件集成所需的所有文档和代码参考。

## 📚 文档结构

### 1. **快速开始** (5分钟)

- [FPGA-QUICK-REFERENCE.md](FPGA-QUICK-REFERENCE.md) ⭐ **从这里开始**
  - 网络配置
  - 指令包/图像包格式
  - 关键工作流程
  - 常见错误

### 2. **详细规范** (阅读)

- [FPGA-PROTOCOL.md](FPGA-PROTOCOL.md)
  - 完整的UDP通信协议规范
  - 数据包格式详解
  - 参考Python实现
  - 测试步骤

### 3. **代码参考** (实现)

- [FPGA-VERILOG-REFERENCE.md](FPGA-VERILOG-REFERENCE.md)
  - Verilog参考代码框架
  - UDP接收/发送模块
  - 命令执行逻辑
  - 图像打包模块

- [fpga_reference_implementation.py](fpga_reference_implementation.py)
  - 完整的Python参考实现
  - 可直接运行测试
  - 包含所有协议逻辑

### 4. **集成检查清单** (验证)

- [FPGA-INTEGRATION-CHECKLIST.md](FPGA-INTEGRATION-CHECKLIST.md)
  - 网络基础检查
  - 代码集成检查
  - 通信流程验证
  - 错误排查指南
  - 性能指标验证

### 5. **现有代码** (参考)

- [ArrayCamera/NetworkDriver.cs](ArrayCamera/NetworkDriver.cs)
  - C#网络驱动
  - 已实现智能网卡绑定
  - 已实现异常处理

- [udp_device_sim.py](udp_device_sim.py)
  - Python模拟器（已修复）
  - 用于测试C#连接

---

## 🎯 集成流程

### 第一步：理解协议 (15分钟)

1. 阅读 [FPGA-QUICK-REFERENCE.md](FPGA-QUICK-REFERENCE.md)
2. 理解4个关键点：
   - 指令包28字节 (Header=0x55AAAA55)
   - 图像包524字节 (Magic=0xAA55AA55)
   - **必须立即回复ACK**
   - 单帧2048包 @ 30FPS

### 第二步：选择实现方式 (选一个)

**选项A：参考Python实现**

```bash
# 运行Python模拟器测试
python fpga_reference_implementation.py

# 启动C# ArrayCamera
.\ArrayCamera\bin\Release\net6.0-windows\ArrayCamera.exe

# 点击"连接/绑定"测试
# 预期：连接成功，可以看到握手日志
```

**选项B：参考Verilog实现**

1. 阅读 [FPGA-VERILOG-REFERENCE.md](FPGA-VERILOG-REFERENCE.md)
2. 使用你的FPGA工具链（Vivado/Quartus等）
3. 实现UDP收发模块
4. 集成到你的FPGA设计中

**选项C：直接在FPGA代码中实现**

1. 使用FPGA提供的UDP/TCP IP库（如Xilinx LWIP）
2. 按照 [FPGA-PROTOCOL.md](FPGA-PROTOCOL.md) 的协议规范实现
3. 关键：接收指令后立即回复ACK

### 第三步：集成到你的FPGA

基本步骤：

1. 配置FPGA网络接口 (IP: 192.168.2.88)
2. 实现UDP接收模块（监听端口8080）
3. 实现命令解析和ACK回复
4. 实现ADC数据采集
5. 实现图像数据打包（524字节格式）
6. 实现UDP发送模块（发送到端口8081）

### 第四步：验证集成

使用 [FPGA-INTEGRATION-CHECKLIST.md](FPGA-INTEGRATION-CHECKLIST.md)：

- [ ] 网络基础检查
- [ ] 代码集成检查
- [ ] 通信流程验证
- [ ] 错误排查
- [ ] 性能指标验证

---

## 📦 关键数据格式速记

### 指令包 (28字节)

```
0x55AAAA55  |  CmdID  |  Mode  |  Param1  |  Param2  |  Res1  |  Res2
4字节       |  4字节  | 4字节  | 4字节    | 4字节    | 4字节  | 4字节
```

**命令类型**:

- 0: PING (握手测试) → **立即回复相同28字节**
- 1: RESET (系统复位)
- 2: CONFIG (配置参数)
- 3: START (启动扫描)
- 4: STOP (停止扫描)

### 图像包 (524字节)

```
0xAA55AA55  | FrameNum | RowNum | SegNum | Type | PixelData(128×uint32)
4字节       | 4字节    | 2字节  | 1字节  | 1字节| 512字节
```

**参数范围**:

- FrameNum: 0 → 递增
- RowNum: 0-511
- SegNum: 0-3 (512像素 ÷ 4段)
- PixelData: 0-10000 (ADC值)

---

## 🚀 快速测试

### 使用Python模拟器测试

```bash
# 终端1: 启动FPGA模拟器
python fpga_reference_implementation.py

# 输出应该显示:
# ============================================================
# FPGA UDP 服务器模拟器
# ============================================================
# 配置:
#   监听地址: 0.0.0.0:8080
#   发送端口: 8081
#
# 等待PC连接...
```

```bash
# 终端2: 启动C#程序
cd ArrayCamera
dotnet build -c Release
.\bin\Release\net6.0-windows\ArrayCamera.exe

# 或直接运行:
.\bin\Release\net6.0-windows\ArrayCamera.exe
```

```
# C#窗口中:
1. 点击"连接/绑定"按钮
2. 预期日志:
   [INFO] 正在搜索192.168.2.x网段的网卡...
   [SUCCESS] ✓ 找到目标网卡: 以太网 (192.168.2.100)
   [SUCCESS] ✓ 指令Socket已绑定: 192.168.2.100:8080
   [SUCCESS] ✓ 数据Socket已绑定: 192.168.2.100:8081 (10MB缓冲)
   [INFO] → 发送Ping指令到 192.168.2.88:8080 (28字节)
   [SUCCESS] [8080 收到ACK] CmdID=0, Mode=0
   [SUCCESS] ✓ 握手成功，连接已建立

3. 点击"开始扫描"按钮
4. 预期: 图形区显示实时波形, PacketCount快速递增
5. 点击"停止扫描"按钮
6. 预期: 数据停止, 可以点击"断开连接"
```

---

## 🔧 Wireshark调试

### 安装Wireshark

```bash
# Windows
choco install wireshark -y
# 或从 https://www.wireshark.org 下载

# Linux
sudo apt-get install wireshark
```

### 过滤并查看数据包

```
# 打开Wireshark → Capture → Start
# 在过滤框输入:
udp.port == 8080 or udp.port == 8081

# 应该看到:
# 1. PC → FPGA: 28字节指令包 (从192.168.2.100:xxxxx到192.168.2.88:8080)
# 2. FPGA → PC: 28字节ACK (从192.168.2.88:xxxxx到192.168.2.100:8080)
# 3. FPGA → PC: 524字节图像包 (从192.168.2.88:xxxxx到192.168.2.100:8081)
```

---

## ❌ 常见问题

### Q1: 连接超时，显示"握手失败"

**原因**: FPGA没有回复ACK

**检查**:

1. FPGA是否接收到Ping包？
   ```bash
   用Wireshark查看是否有包发出
   ```
2. FPGA是否检查了Header？
   ```python
   if header != 0x55AAAA55:
       continue
   ```
3. FPGA是否立即发送了回复？
   ```python
   socket.sendto(data, addr)  # 必须在接收后立即发送
   ```

### Q2: C#接收到部分包但数据不完整

**原因**: 网络丢包或缓冲区配置

**解决**:

1. 确保Socket接收缓冲区足够大 (10MB)
2. 检查网线质量和网络稳定性
3. 降低FPGA发送速率（减少帧率）

### Q3: 显示错误的波形或颜色

**原因**: 像素数据格式或顺序错误

**检查**:

1. 像素数据类型: 必须是uint32 (4字节)
2. 数据顺序: Little Endian (0x2710 = 10000)
3. SegNum顺序: 必须0→1→2→3 (不能乱序)

---

## 📞 获取帮助

| 问题             | 查看文档                         |
| ---------------- | -------------------------------- |
| 网络配置不对     | FPGA-QUICK-REFERENCE.md          |
| 不知道包格式     | FPGA-PROTOCOL.md                 |
| 不知道怎么写代码 | FPGA-VERILOG-REFERENCE.md        |
| Python参考实现   | fpga_reference_implementation.py |
| 集成过程中出错   | FPGA-INTEGRATION-CHECKLIST.md    |
| C#代码问题       | ArrayCamera/NetworkDriver.cs     |

---

## ✅ 最终检查清单

在向FPGA工程师交付前，确保:

- [ ] 所有文档都已审阅
- [ ] Python模拟器能与C#连接成功
- [ ] 理解了Echo ACK的重要性
- [ ] 理解了28字节指令包格式
- [ ] 理解了524字节图像包格式
- [ ] 有可靠的Wireshark抓包工具
- [ ] 知道如何调试UDP通信

---

## 📄 版本历史

| 版本 | 日期       | 说明     |
| ---- | ---------- | -------- |
| 1.0  | 2026-01-29 | 初版发布 |

---

**准备好集成了吗？从 [FPGA-QUICK-REFERENCE.md](FPGA-QUICK-REFERENCE.md) 开始！** 🚀
