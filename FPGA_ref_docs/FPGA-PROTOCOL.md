# FPGA 侧 UDP 通信协议规范

## 📋 概述

本文档描述FPGA板卡应如何实现UDP通信功能，以便与C# ArrayCamera主机软件建立连接并传输512×512阵列图像数据。

**硬件配置**：

- FPGA板卡 IP: `192.168.2.88` (固定)
- FPGA 监听端口: `8080` (接收PC的指令)
- FPGA 发送端口: `8081` (不需要绑定，UDP自动选择源端口)
- PC IP: `192.168.2.100` (或通过ARP探测)
- PC 接收指令回复端口: `8080`
- PC 接收图像数据端口: `8081`

---

## 📡 通信流程

```
PC                              FPGA Board
|                               |
+------ Ping (28字节) ------>   |
|    192.168.2.88:8080          |
|                               | 接收Ping，解析Header
|                      <-- ACK (28字节) ----+
|    从192.168.2.100收到      | 回复相同的Ping包
|                               |
+------ Start (28字节) ------>   |
|                               | CmdID=3, 启动扫描
|                               | 开始生成图像数据
|                               |
|    <-- 图像数据 (524字节) ---- | 发送512×512图像
|    循环接收262144包           |
|    (512行×512列÷128像素)      |
|                               |
+------ Stop (28字节) ------>   |
|                               | CmdID=4, 停止扫描
```

---

## 📦 数据包格式

### 1. **指令包** (Command Packet) - 28字节

**C# 侧发送格式** (Little Endian):

| 字段      | 偏移 | 大小   | 说明                              |
| --------- | ---- | ------ | --------------------------------- |
| Header    | 0    | 4 字节 | 固定值: `0x55AAAA55`              |
| CmdID     | 4    | 4 字节 | 命令ID (0-4)                      |
| Mode      | 8    | 4 字节 | 扫描模式 (0=Preview, 1=Precision) |
| Param1    | 12   | 4 字节 | TIA增益范围 (0-7)                 |
| Param2    | 16   | 4 字节 | 等待周期                          |
| Reserved1 | 20   | 4 字节 | 保留                              |
| Reserved2 | 24   | 4 字节 | 保留                              |

**总计：28 字节**

#### **命令定义**：

| CmdID | 命令   | 动作     | Mode | Param1 | 备注                         |
| ----- | ------ | -------- | ---- | ------ | ---------------------------- |
| 0     | PING   | 握手测试 | 忽略 | 忽略   | FPGA需要立即回复相同的28字节 |
| 1     | RESET  | 复位系统 | 忽略 | 忽略   | 清空缓冲区，复位状态         |
| 2     | CONFIG | 配置参数 | 设置 | 设置   | 配置扫描参数                 |
| 3     | START  | 开始扫描 | 设置 | 设置   | 启动图像采集，开始发送数据   |
| 4     | STOP   | 停止扫描 | 设置 | 设置   | 停止采集和发送               |

**FPGA 应该做的**：

```
对于任何命令 (0-4):
  1. 接收28字节的指令包
  2. 验证Header == 0x55AAAA55
  3. 立即将相同的28字节回复到发送者的IP:8080
  4. 根据CmdID执行对应的动作

关键: 必须立即回复(Echo ACK)，否则C#认为连接失败
```

---

### 2. **图像数据包** (Image Data Packet) - 524字节

**发送到PC的格式** (Little Endian):

| 字段          | 偏移   | 大小         | 说明                      |
| ------------- | ------ | ------------ | ------------------------- |
| Magic         | 0      | 4 字节       | 固定值: `0xAA55AA55`      |
| FrameNum      | 4      | 4 字节       | 帧号 (递增)               |
| RowNum        | 8      | 2 字节       | 行号 (0-511)              |
| SegNum        | 10     | 1 字节       | 段号 (0-3) 512÷128=4段    |
| Type          | 11     | 1 字节       | 数据类型 (0=原始数据)     |
| Padding       | 12     | 0 字节       | (对齐)                    |
| **PixelData** | **12** | **512 字节** | 128个 uint32 (4字节×128)  |
|               |        |              | 数据范围: 0 ~ 10000 ADC值 |

**总计：12 + 512 = 524 字节**

#### **格式示例（C语言伪代码）**：

```c
#pragma pack(1)
typedef struct {
    uint32_t magic;      // 0xAA55AA55
    uint32_t frame_num;  // 帧号
    uint16_t row;        // 行号 0-511
    uint8_t  seg;        // 段号 0-3
    uint8_t  type;       // 数据类型
    uint32_t pixels[128]; // 128个像素数据 (每个uint32 = 1像素)
} ImagePacket;  // 总共 12 + 512 = 524字节
#pragma pack()
```

#### **发送顺序**：

```
Frame 0:
  for row = 0 to 511:
    for seg = 0 to 3:  // 4个段 = 512像素
      发送1个524字节的图像包

总包数 = 512行 × 4段 = 2048包/帧
```

---

## 🔧 FPGA 实现建议

### **伪代码框架**：

```python
# 初始化 UDP 服务器
udp_socket = socket(AF_INET, SOCK_DGRAM)
udp_socket.setsockopt(SOL_SOCKET, SO_REUSEADDR, 1)
udp_socket.bind(('0.0.0.0', 8080))  # 监听8080端口

pc_ip = None  # PC 的IP地址（第一次收到指令时确定）
is_scanning = False
frame_counter = 0

while True:
    # 1. 接收指令
    command_data, pc_addr = udp_socket.recvfrom(1024)
    pc_ip = pc_addr[0]  # 记录发送方IP

    if len(command_data) != 28:
        continue

    # 2. 解析指令头
    header = unpack('<I', command_data[0:4])[0]
    if header != 0x55AAAA55:
        continue

    cmd_id = unpack('<I', command_data[4:8])[0]
    mode = unpack('<I', command_data[8:12])[0]
    param1 = unpack('<I', command_data[12:16])[0]

    # 3. 立即回复ACK（关键！）
    udp_socket.sendto(command_data, (pc_ip, 8080))

    # 4. 执行命令
    if cmd_id == 0:  # PING
        print(f"收到PING，已回复ACK")

    elif cmd_id == 1:  # RESET
        is_scanning = False
        frame_counter = 0
        print("系统已复位")

    elif cmd_id == 2:  # CONFIG
        print(f"配置参数: Mode={mode}, Param1={param1}")

    elif cmd_id == 3:  # START
        is_scanning = True
        print("开始扫描")
        # 启动图像采集线程/任务
        start_image_transmission()

    elif cmd_id == 4:  # STOP
        is_scanning = False
        print("停止扫描")

# 并发执行：图像数据发送线程
def send_image_data():
    image_socket = socket(AF_INET, SOCK_DGRAM)

    while True:
        if not is_scanning or pc_ip is None:
            sleep(0.01)
            continue

        # 发送一帧数据
        for row in range(512):
            for seg in range(4):
                # 获取图像数据（来自ADC或内存）
                pixels = get_pixel_data(row, seg)  # 128个uint32

                # 构造图像包（524字节）
                packet = pack('<IIHBB',
                    0xAA55AA55,    # Magic
                    frame_counter, # FrameNum
                    row,           # RowNum
                    seg,           # SegNum
                    0              # Type
                )
                packet += pack('<128I', *pixels)  # 512字节的像素数据

                # 发送到 PC 的8081端口
                image_socket.sendto(packet, (pc_ip, 8081))

                # 帧率控制：30 FPS = 33.3ms/帧
                # 2048包/帧，需要在33.3ms内发送完
                # 理论速率: 2048 * 524 bytes / 0.0333s ≈ 32 Mbps

        frame_counter += 1
```

---

## ⚡ 关键实现要点

### **1. Echo ACK 机制（最重要！）**

```
收到任何指令 → 立即回复相同的28字节 → 否则C#超时
```

C#代码会在这里等待：

```csharp
// C# 等待3秒超时
for elapsedMs = 0 to 3000:
    if RxCount增加:
        break  // 连接成功
timeout:
    print("连接失败")
```

**FPGA必须**：

- 在1000ms内收到Ping后立即回复
- 回复的内容必须是接收到的完整28字节

### **2. 端口绑定（FPGA侧）**

```
监听：0.0.0.0:8080  (接收所有网卡上的指令)
发送：192.168.2.100:8081 (发送图像数据到PC的8081端口)
```

### **3. IP地址解析**

```
第一次收到指令包时，自动提取发送方IP
from socket: recvfrom() 返回 (data, (sender_ip, sender_port))
pc_ip = sender_ip  // 保存下来，用于后续发送
```

### **4. 像素数据范围**

- 每个像素是 **uint32** (4字节)
- 数据范围：**0 ~ 10000** (10bit ADC值扩展到uint32)
- 数据顺序：Little Endian (Intel格式)
- 字节序：`0x00 0x27 0x00 0x00` = 10000 (0x2710)

### **5. 帧率控制**

```
目标帧率: 30 FPS
单帧数据量: 512行 × 512像素 = 262,144像素
单帧包数: 512 × 4 = 2,048个524字节包
单帧数据: 2048 × 524 = 1,073,152字节 ≈ 1MB/帧
网络速率: 1MB × 30FPS = 30MB/s (千兆网完全支持)
```

---

## 🧪 测试步骤

### **第1步：FPGA端实现最小化版本**

实现以下功能即可测试连接：

```python
# test_fpga_minimal.py
import socket
import struct
import threading

sock = socket.socket(AF_INET, SOCK_DGRAM)
sock.setsockopt(SOL_SOCKET, SO_REUSEADDR, 1)
sock.bind(('0.0.0.0', 8080))

print("FPGA监听 0.0.0.0:8080")
print("等待C#连接...")

pc_ip = None
is_running = False

def recv_commands():
    global pc_ip, is_running
    while True:
        data, addr = sock.recvfrom(1024)
        pc_ip = addr[0]

        if len(data) != 28:
            continue

        header = struct.unpack('<I', data[0:4])[0]
        if header != 0x55AAAA55:
            continue

        cmd_id = struct.unpack('<I', data[4:8])[0]

        # 立即回复ACK
        sock.sendto(data, (pc_ip, 8080))

        if cmd_id == 0:
            print(f"✓ 收到PING，已回复ACK到{pc_ip}:8080")
        elif cmd_id == 3:
            is_running = True
            print("✓ 启动扫描")
        elif cmd_id == 4:
            is_running = False
            print("✓ 停止扫描")

def send_images():
    global is_running, pc_ip
    tx_sock = socket.socket(AF_INET, SOCK_DGRAM)
    frame = 0

    while True:
        if not is_running or pc_ip is None:
            time.sleep(0.1)
            continue

        for row in range(512):
            for seg in range(4):
                # 简单的测试波形
                pixels = []
                for i in range(128):
                    val = int(abs((row + i*4 + frame) % 512 - 256) * 39.0625)
                    pixels.append(val)

                pkt = struct.pack('<IIHBB', 0xAA55AA55, frame, row, seg, 0)
                pkt += struct.pack('<128I', *pixels)

                tx_sock.sendto(pkt, (pc_ip, 8081))

        frame += 1
        print(f"发送第{frame}帧", end='\r')

threading.Thread(target=recv_commands, daemon=True).start()
threading.Thread(target=send_images, daemon=True).start()

while True:
    time.sleep(1)
```

### **第2步：测试连接**

1. FPGA板卡或PC运行上面的Python脚本
2. 启动C#程序
3. 点击"连接/绑定"
4. 预期看到：`✓ 握手成功，连接已建立`

### **第3步：完整实现**

在FPGA上实现真实的：

- ADC数据采集
- TIA增益控制
- 行扫描信号生成
- UDP包构造和发送

---

## 🐛 常见问题排查

| 问题                       | 原因                 | 解决                                       |
| -------------------------- | -------------------- | ------------------------------------------ |
| C#显示"握手超时"           | FPGA没有回复ACK      | 检查Echo ACK逻辑是否在28字节检查后立即发送 |
| 只连接一次成功，第二次失败 | CTS重复创建          | C#已修复，重新编译                         |
| 接收到部分数据但不完整     | 网络丢包或缓冲区过小 | 增加Socket接收缓冲区到10MB                 |
| 软件退出报错               | 资源清理不完整       | C#已修复Wait()异常处理                     |
| PC获取不到FPGA的IP         | 网络不通             | 检查ping是否正常，检查防火墙               |

---

## 📝 最小化验证清单

- [ ] FPGA能接收192.168.2.100发来的28字节包
- [ ] FPGA解析Header=0x55AAAA55
- [ ] FPGA立即回复相同的28字节到发送方:8080
- [ ] C#收到ACK，RxCount增加
- [ ] C#日志显示"握手成功"
- [ ] FPGA能发送524字节的图像数据包
- [ ] C#能接收并解析图像数据
- [ ] 显示实时波形图

---

## 📞 参考链接

- C# 代码位置: [NetworkDriver.cs](ArrayCamera/NetworkDriver.cs#L1)
- Python模拟器参考: [udp_device_sim.py](udp_device_sim.py)
- 编译方法: `dotnet build ArrayCamera.csproj -c Release`
- 运行方法: `.\ArrayCamera\bin\Release\net6.0-windows\ArrayCamera.exe`
