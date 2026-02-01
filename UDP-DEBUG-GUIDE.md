# UDP 网络诊断指南

## 问题背景

当前上位机软件显示"握手超时"，但物理网络连通（CMD可以Ping通192.168.2.88）。

## 根本原因

**端口绑定策略错误**：

- ❌ 旧代码：使用随机端口（如54339）发送Ping，然后等待该端口收回复
- ✅ 新代码：使用固定端口8080发送Ping，并在8080上监听回复

**FPGA行为分析**：
板卡收到来自任意端口的UDP包后，通常只会向**固定目标端口8080**回复，而不是回复给源端口。

## 诊断步骤

### 步骤1：运行Python诊断脚本

```powershell
# 确保已安装Python 3.x
python --version

# 运行诊断脚本
python debug_udp.py
```

**预期输出**（网络正常）：

```
[TX #001] → 192.168.2.88:8080 | Ping from Python [14:23:45.123]
[RX #001] ← 192.168.2.88:8080 | Board Alive (12 bytes)
[TX #002] → 192.168.2.88:8080 | Ping from Python [14:23:46.125]
[RX #002] ← 192.168.2.88:8080 | ACK (4 bytes)
```

**异常输出**（网络问题）：

```
[TX #001] → 192.168.2.88:8080 | Ping from Python [14:23:45.123]
[TX #002] → 192.168.2.88:8080 | Ping from Python [14:23:46.125]
# 无任何[RX]消息 → 板卡未响应
```

### 步骤2：分析结果

#### 情况A：Python能收到响应

✅ **结论**：网络通信正常，问题出在C#代码逻辑

- **已修复**：新版NetworkDriver.cs已绑定固定端口8080
- **操作**：重启C#程序，点击"连接/绑定"按钮

#### 情况B：Python也收不到响应

❌ **结论**：网络层问题，与代码无关

- **检查清单**：
  1. 板卡是否上电（电源指示灯）
  2. 网线是否插紧（网口指示灯闪烁）
  3. 本机IP是否为192.168.2.x（运行`ipconfig`检查）
  4. 板卡IP是否真的是192.168.2.88（查阅硬件文档）
  5. 防火墙是否拦截（临时关闭Windows防火墙测试）

### 步骤3：使用C#程序连接

修复后的连接流程：

1. 点击"连接/绑定"按钮
2. 日志显示：

   ```
   [INFO] 正在扫描本地网卡...
   [SUCCESS] ✓ 已绑定网卡: 以太网 (192.168.2.100)
   [INFO] 子网掩码: 255.255.255.0
   [INFO] 强制绑定端口: 8080 (指令反馈) & 8081 (图像数据)
   [INFO] 启动监听线程（等待板卡回复）...
   [SUCCESS] 启动数据接收 Socket: 监听端口=8081
   [SUCCESS] 已创建指令 Socket: 本地端口=8080 (固定绑定)
   [INFO] → 发送 Ping 指令 (28字节) 到 192.168.2.88:8080
   [SUCCESS] ← 收到板卡响应！(RxCount: 0 → 1)
   [SUCCESS] ✓ 握手成功，硬件在线
   ```

3. 如果失败，日志会显示详细错误原因

## 关键修复点对比

### 修复前（错误代码）

```csharp
// 随机端口绑定
_cmdClient = new UdpClient(0);  // 系统分配随机端口如54339
var receiveTask = _cmdClient.ReceiveAsync();  // 在54339等待回复（收不到）
```

### 修复后（正确代码）

```csharp
// 固定端口绑定
var localEP = new IPEndPoint(localIP, 8080);  // 强制绑定8080
_cmdClient = new UdpClient(localEP);
// 同时启动8081监听线程
StartListening();  // 提前开启接收，防止错过快速回复
```

## 防火墙配置

如果遇到 `SocketException: Access Denied`：

### Windows防火墙规则

```powershell
# 方法1：临时关闭防火墙（测试用）
netsh advfirewall set allprofiles state off

# 方法2：添加程序例外（推荐）
netsh advfirewall firewall add rule name="ArrayCamera UDP" ^
  dir=in action=allow protocol=UDP localport=8080-8081

# 恢复防火墙
netsh advfirewall set allprofiles state on
```

### 图形界面方法

1. 控制面板 → Windows Defender 防火墙
2. 高级设置 → 入站规则 → 新建规则
3. 端口 → UDP → 特定本地端口：8080,8081
4. 允许连接 → 全部配置文件 → 命名"ArrayCamera UDP"

## 故障排查工具

### Wireshark抓包

```bash
# 过滤UDP 8080/8081端口
udp.port == 8080 or udp.port == 8081
```

### 查看端口占用

```powershell
netstat -ano | findstr :8080
netstat -ano | findstr :8081
```

## 联系支持

如果以上步骤均无法解决问题，请提供：

1. `debug_udp.py` 的完整输出日志
2. C#程序的日志输出（LogBox内容）
3. `ipconfig /all` 的输出
4. 板卡型号和固件版本
