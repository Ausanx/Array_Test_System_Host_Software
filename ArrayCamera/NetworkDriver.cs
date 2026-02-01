using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ArrayCamera
{
    /// <summary>
    /// 控制指令包结构 (28字节)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandPacket
    {
        public uint Header;      // 0x55AAAA55
        public uint CmdID;       // 0=Ping, 1=Reset, 2=Config, 3=Start, 4=Stop
        public uint Mode;        // 0=Preview, 1=Precision
        public uint Param1;      // TIA Range
        public uint Param2;      // Wait Cycles
        public uint Reserved1;
        public uint Reserved2;
    }

    /// <summary>
    /// UDP数据包头结构 (12字节)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UdpPacketHeader
    {
        public uint Header;        // 0xAA55AA55
        public uint FrameID;       // 帧号
        public ushort RowIndex;    // 行号 (0-511)
        public byte SegIndex;      // 分段号 (0-3)
        public byte DataType;      // 数据类型
        // 后接 512字节 Raw Data
    }

    /// <summary>
    /// 网络通信驱动类 - 重构版（智能网卡绑定 + 显式端口）
    /// </summary>
    public class NetworkDriver : IDisposable
    {
        // 常量定义
        private const int IMAGE_SIZE = 512;
        private const int PACKET_DATA_SIZE = 512;
        private const int HEADER_SIZE = 12;
        private const int FULL_PACKET_SIZE = HEADER_SIZE + PACKET_DATA_SIZE;

        // 目标设备配置
        private readonly string _targetIP;
        private readonly int _targetPort;

        // 本地网络配置（智能搜索结果）
        private IPAddress? _localIP;
        private const int LOCAL_CMD_PORT = 8080;   // 指令端口（接收ACK）
        private const int LOCAL_DATA_PORT = 8081;  // 数据端口（接收图像）

        // Socket 实例（双通道）
        private UdpClient? _cmdSocket;      // Socket A: 8080端口（指令+ACK）
        private UdpClient? _dataSocket;     // Socket B: 8081端口（图像数据）
        private IPEndPoint? _targetEndPoint;

        // 接收线程控制
        private CancellationTokenSource? _cts;
        private Task? _cmdReceiveTask;
        private Task? _dataReceiveTask;
        private bool _isConnected = false;

        // 数据队列（生产者-消费者）
        private readonly ConcurrentQueue<byte[]> _rawPacketQueue = new();
        private readonly FrameReassembler _reassembler;

        // 事件回调
        public event Action<string, LogType>? OnLog;
        public event Action<int[]>? OnFrameReady;
        public event Action<string>? OnTextMessageReceived;

        // 统计信息
        public int TxCount { get; private set; }
        public int RxCount { get; private set; }

        public NetworkDriver(string targetIP, int targetPort = 8080)
        {
            _targetIP = targetIP;
            _targetPort = targetPort;
            _reassembler = new FrameReassembler();
            _reassembler.OnFrameComplete += frame => OnFrameReady?.Invoke(frame);
        }

        /// <summary>
        /// 智能搜索本地IP地址（192.168.2.x网段）
        /// </summary>
        public IPAddress? GetLocalIpAddress()
        {
            OnLog?.Invoke("正在搜索192.168.2.x网段的网卡...", LogType.Info);

            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // 只考虑已启用的物理网卡
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        continue;

                    IPInterfaceProperties ipProps = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        // 检查是否为192.168.2.x网段
                        byte[] bytes = addr.Address.GetAddressBytes();
                        if (bytes[0] == 192 && bytes[1] == 168 && bytes[2] == 2)
                        {
                            OnLog?.Invoke($"✓ 找到目标网卡: {ni.Name}", LogType.Success);
                            OnLog?.Invoke($"  本地IP: {addr.Address}", LogType.Success);
                            OnLog?.Invoke($"  子网掩码: {addr.IPv4Mask}", LogType.Info);
                            OnLog?.Invoke($"  网卡类型: {ni.NetworkInterfaceType}", LogType.Info);
                            return addr.Address;
                        }
                    }
                }

                OnLog?.Invoke("✖ 未找到192.168.2.x网段的网卡", LogType.Error);
                OnLog?.Invoke("  请检查网线是否连接或本机IP配置", LogType.Warning);
                return null;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"搜索网卡失败: {ex.Message}", LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 建立连接（智能绑定 + 真实握手）
        /// </summary>
        public async Task<bool> Connect(int handshakeTimeoutMs = 3000)
        {
            try
            {
                // 步骤1: 搜索本地IP
                _localIP = GetLocalIpAddress();
                if (_localIP == null)
                {
                    OnLog?.Invoke("无法找到192.168.2.x网段的网卡，连接失败", LogType.Error);
                    return false;
                }

                // 步骤2: 创建并绑定指令Socket（8080端口）
                OnLog?.Invoke($"正在绑定指令Socket: {_localIP}:{LOCAL_CMD_PORT}...", LogType.Info);

                _cmdSocket?.Close();
                try
                {
                    var localCmdEP = new IPEndPoint(_localIP, LOCAL_CMD_PORT);
                    _cmdSocket = new UdpClient();
                    _cmdSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _cmdSocket.Client.Bind(localCmdEP);

                    OnLog?.Invoke($"✓ 指令Socket已绑定: {_localIP}:{LOCAL_CMD_PORT}", LogType.Success);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AccessDenied)
                {
                    OnLog?.Invoke("✖ 端口8080访问被拒绝（防火墙拦截）", LogType.Error);
                    OnLog?.Invoke("  解决方法：关闭防火墙或添加程序例外", LogType.Warning);
                    return false;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    OnLog?.Invoke("✖ 端口8080已被占用", LogType.Error);
                    OnLog?.Invoke("  请关闭其他占用8080端口的程序", LogType.Warning);
                    return false;
                }

                // 步骤3: 创建并绑定数据Socket（8081端口）
                OnLog?.Invoke($"正在绑定数据Socket: {_localIP}:{LOCAL_DATA_PORT}...", LogType.Info);

                _dataSocket?.Close();
                try
                {
                    var localDataEP = new IPEndPoint(_localIP, LOCAL_DATA_PORT);
                    _dataSocket = new UdpClient();
                    _dataSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _dataSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 10 * 1024 * 1024); // 10MB缓冲
                    _dataSocket.Client.Bind(localDataEP);

                    OnLog?.Invoke($"✓ 数据Socket已绑定: {_localIP}:{LOCAL_DATA_PORT} (10MB缓冲)", LogType.Success);
                }
                catch (SocketException ex)
                {
                    OnLog?.Invoke($"绑定数据端口失败: {ex.Message}", LogType.Error);
                    _cmdSocket?.Close();
                    return false;
                }

                // 步骤4: 重置取消令牌
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                // 步骤5: 启动接收线程（在发送Ping之前）
                OnLog?.Invoke("启动数据接收线程...", LogType.Info);
                StartReceiveLoops();
                await Task.Delay(300); // 给接收线程300ms启动时间

                // 步骤5: 发送Ping指令
                _targetEndPoint = new IPEndPoint(IPAddress.Parse(_targetIP), _targetPort);

                var pingPacket = new CommandPacket
                {
                    Header = 0x55AAAA55,
                    CmdID = 0,  // Ping
                    Mode = 0,
                    Param1 = 0,
                    Param2 = 0,
                    Reserved1 = 0,
                    Reserved2 = 0
                };

                byte[] data = StructToBytes(pingPacket);
                _cmdSocket.Send(data, data.Length, _targetEndPoint);
                TxCount++;

                OnLog?.Invoke($"→ 发送Ping指令到 {_targetIP}:{_targetPort} (28字节)", LogType.Info);

                // 步骤6: 等待握手响应（检查RxCount变化）
                int initialRxCount = RxCount;
                int elapsedMs = 0;
                int checkInterval = 100;

                while (elapsedMs < handshakeTimeoutMs)
                {
                    await Task.Delay(checkInterval);
                    elapsedMs += checkInterval;

                    if (RxCount > initialRxCount)
                    {
                        _isConnected = true;
                        OnLog?.Invoke($"← 收到板卡响应！(RxCount: {initialRxCount} → {RxCount})", LogType.Success);
                        OnLog?.Invoke("━━━━━ 握手成功，连接已建立 ━━━━━", LogType.Success);
                        return true;
                    }
                }

                // 超时
                OnLog?.Invoke($"✖ 握手超时 ({handshakeTimeoutMs}ms)", LogType.Error);
                OnLog?.Invoke("  已发送Ping，但未收到板卡响应", LogType.Warning);

                // 清理资源
                Disconnect();
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"连接异常: {ex.Message}", LogType.Error);
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            if (!_isConnected && _cmdSocket == null && _dataSocket == null)
                return; // 已经断开

            _isConnected = false;

            // 先取消Token
            try { _cts?.Cancel(); } catch { }

            // 关闭Socket（会触发ReceiveAsync抛出异常）
            try { _cmdSocket?.Close(); } catch { }
            try { _dataSocket?.Close(); } catch { }

            // 等待任务结束（用try-catch避免OperationCanceledException）
            try { _cmdReceiveTask?.Wait(1000); } catch { }
            try { _dataReceiveTask?.Wait(1000); } catch { }

            _cmdSocket = null;
            _dataSocket = null;

            OnLog?.Invoke("连接已断开", LogType.Info);
        }

        /// <summary>
        /// 启动双通道接收循环
        /// </summary>
        private void StartReceiveLoops()
        {
            _cts = new CancellationTokenSource();

            // 启动指令通道接收（8080端口）
            _cmdReceiveTask = Task.Run(() => CmdReceiveLoop(_cts.Token), _cts.Token);

            // 启动数据通道接收（8081端口）
            _dataReceiveTask = Task.Run(() => DataReceiveLoop(_cts.Token), _cts.Token);

            OnLog?.Invoke("✓ 双通道接收线程已启动", LogType.Success);
        }

        /// <summary>
        /// 指令通道接收循环（8080端口 - ACK和文本消息）
        /// </summary>
        private async Task CmdReceiveLoop(CancellationToken ct)
        {
            OnLog?.Invoke("[线程] 指令接收线程已启动 (8080)", LogType.Info);

            while (!ct.IsCancellationRequested && _cmdSocket != null)
            {
                try
                {
                    var result = await _cmdSocket.ReceiveAsync();
                    byte[] data = result.Buffer;
                    RxCount++;

                    // 优先检查是否为CommandPacket ACK (28字节)
                    if (data.Length == 28)
                    {
                        try
                        {
                            var ack = BytesToStruct<CommandPacket>(data);
                            if (ack.Header == 0x55AAAA55)
                            {
                                OnLog?.Invoke($"[8080 收到ACK] CmdID={ack.CmdID}, Mode={ack.Mode}", LogType.Success);
                                continue;
                            }
                        }
                        catch { }
                    }

                    // 尝试解析为文本
                    if (data.Length < 100)
                    {
                        try
                        {
                            string msg = System.Text.Encoding.ASCII.GetString(data).TrimEnd('\0');
                            OnTextMessageReceived?.Invoke(msg);
                            OnLog?.Invoke($"[8080 收到文本] {msg} ({data.Length}字节)", LogType.Success);
                        }
                        catch
                        {
                            OnLog?.Invoke($"[8080 收到数据] {data.Length}字节 (二进制)", LogType.Info);
                        }
                    }
                    else
                    {
                        OnLog?.Invoke($"[8080 收到数据] {data.Length}字节", LogType.Info);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    OnLog?.Invoke($"[8080接收异常] {ex.Message}", LogType.Error);
                }
            }

            OnLog?.Invoke("[线程] 指令接收线程已退出", LogType.Info);
        }

        /// <summary>
        /// 数据通道接收循环（8081端口 - 图像数据包）
        /// </summary>
        private async Task DataReceiveLoop(CancellationToken ct)
        {
            OnLog?.Invoke("[线程] 数据接收线程已启动 (8081)", LogType.Info);

            while (!ct.IsCancellationRequested && _dataSocket != null)
            {
                try
                {
                    var result = await _dataSocket.ReceiveAsync();
                    byte[] data = result.Buffer;
                    RxCount++;

                    // 判断是否为图像数据包
                    if (data.Length == FULL_PACKET_SIZE && BitConverter.ToUInt32(data, 0) == 0xAA55AA55)
                    {
                        _rawPacketQueue.Enqueue(data);
                        ProcessPacketQueue();
                    }
                    else
                    {
                        OnLog?.Invoke($"[8081 收到非标准包] {data.Length}字节", LogType.Warning);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    OnLog?.Invoke($"[8081接收异常] {ex.Message}", LogType.Error);
                }
            }

            OnLog?.Invoke("[线程] 数据接收线程已退出", LogType.Info);
        }

        /// <summary>
        /// 处理数据包队列
        /// </summary>
        private void ProcessPacketQueue()
        {
            while (_rawPacketQueue.TryDequeue(out byte[]? packet))
            {
                if (packet == null || packet.Length != FULL_PACKET_SIZE) continue;

                // 解析包头
                UdpPacketHeader header = BytesToStruct<UdpPacketHeader>(packet);

                // 提取数据段
                byte[] rowData = new byte[PACKET_DATA_SIZE];
                Array.Copy(packet, HEADER_SIZE, rowData, 0, PACKET_DATA_SIZE);

                // 送入拼包器
                _reassembler.AddPacket(header.FrameID, header.RowIndex, header.SegIndex, rowData);
            }
        }

        /// <summary>
        /// 发送控制指令
        /// </summary>
        public void SendCommand(uint cmdId, uint mode = 0, uint param1 = 0, uint param2 = 0)
        {
            try
            {
                if (_cmdSocket == null || _targetEndPoint == null || !_isConnected)
                {
                    OnLog?.Invoke("未连接到设备，无法发送指令", LogType.Warning);
                    return;
                }

                var packet = new CommandPacket
                {
                    Header = 0x55AAAA55,
                    CmdID = cmdId,
                    Mode = mode,
                    Param1 = param1,
                    Param2 = param2,
                    Reserved1 = 0,
                    Reserved2 = 0
                };

                byte[] data = StructToBytes(packet);
                _cmdSocket.Send(data, data.Length, _targetEndPoint);
                TxCount++;

                string cmdName = cmdId switch
                {
                    0 => "Ping",
                    1 => "Reset",
                    2 => "Config",
                    3 => "Start",
                    4 => "Stop",
                    _ => $"Custom({cmdId})"
                };

                OnLog?.Invoke($"→ 发送指令: {cmdName} (Mode={mode}, P1={param1}, P2={param2})", LogType.Success);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"发送指令失败: {ex.Message}", LogType.Error);
            }
        }

        /// <summary>
        /// 停止接收
        /// </summary>
        public void StopListening()
        {
            Disconnect();
        }

        /// <summary>
        /// 结构体转字节数组
        /// </summary>
        private static byte[] StructToBytes<T>(T structure) where T : struct
        {
            int size = Marshal.SizeOf(structure);
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(structure, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return buffer;
        }

        /// <summary>
        /// 字节数组转结构体
        /// </summary>
        private static T BytesToStruct<T>(byte[] buffer) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(buffer, 0, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void Dispose()
        {
            Disconnect();
            _cts?.Dispose();
        }
    }
}
