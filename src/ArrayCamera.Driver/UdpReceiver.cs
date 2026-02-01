using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace ArrayCamera.Driver
{
    /// <summary>
    /// UDP接收器 - 实现生产者模式
    /// 独立线程接收UDP数据，重组后放入队列
    /// </summary>
    public class UdpReceiver : IDisposable
    {
        private const int RX_PORT = 8081;
        private const int TX_PORT = 8080;
        private const int BUFFER_SIZE = 65536;

        private UdpClient _rxClient;
        private UdpClient _txClient;
        private Thread _receiveThread;
        private volatile bool _isRunning;
        private IPEndPoint _fpgaEndPoint;

        private readonly FrameReassembler _reassembler;
        private readonly ConcurrentQueue<int[,]> _frameQueue;
        private readonly int _maxQueueSize;

        // 事件
        public event EventHandler<int[,]> FrameReceived;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler ConnectionLost;

        private DateTime _lastPacketTime;
        private readonly Timer _heartbeatTimer;

        public bool IsConnected { get; private set; }
        public string FpgaAddress { get; private set; }

        public UdpReceiver(int maxQueueSize = 10)
        {
            _maxQueueSize = maxQueueSize;
            _frameQueue = new ConcurrentQueue<int[,]>();
            _reassembler = new FrameReassembler();
            _heartbeatTimer = new Timer(CheckHeartbeat, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// 连接到FPGA
        /// </summary>
        public void Connect(string fpgaIpAddress)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Already connected. Disconnect first.");
            }

            try
            {
                FpgaAddress = fpgaIpAddress;
                _fpgaEndPoint = new IPEndPoint(IPAddress.Parse(fpgaIpAddress), TX_PORT);

                // 创建接收客户端 (绑定到本地端口)
                _rxClient = new UdpClient(RX_PORT);
                _rxClient.Client.ReceiveBufferSize = BUFFER_SIZE * 10; // 增大接收缓冲区

                // 创建发送客户端
                _txClient = new UdpClient();

                // 启动接收线程
                _isRunning = true;
                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal,
                    Name = "UDP Receiver Thread"
                };
                _receiveThread.Start();

                // 启动心跳检测
                _lastPacketTime = DateTime.Now;
                _heartbeatTimer.Change(1000, 1000);

                IsConnected = true;
            }
            catch (Exception ex)
            {
                Dispose();
                throw new InvalidOperationException($"Failed to connect: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _isRunning = false;
            _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join(1000);
            }

            _rxClient?.Close();
            _txClient?.Close();
            
            IsConnected = false;
            
            // 清空队列
            while (_frameQueue.TryDequeue(out _)) { }
            _reassembler.Reset();
        }

        /// <summary>
        /// 发送控制命令到FPGA
        /// </summary>
        public void SendCommand(CommandPacket command)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to FPGA.");
            }

            try
            {
                byte[] data = StructToBytes(command);
                _txClient.Send(data, data.Length, _fpgaEndPoint);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to send command: {ex.Message}");
            }
        }

        /// <summary>
        /// 尝试从队列中获取一帧数据
        /// </summary>
        public bool TryGetFrame(out int[,] frame)
        {
            return _frameQueue.TryDequeue(out frame);
        }

        /// <summary>
        /// 获取队列中的帧数量
        /// </summary>
        public int GetQueueCount()
        {
            return _frameQueue.Count;
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStatistics()
        {
            return _reassembler.GetStatistics();
        }

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void ResetStatistics()
        {
            _reassembler.Reset();
        }

        // ==================== 私有方法 ====================

        private void ReceiveLoop()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            
            while (_isRunning)
            {
                try
                {
                    // 接收UDP数据
                    byte[] data = _rxClient.Receive(ref remoteEP);
                    _lastPacketTime = DateTime.Now;

                    // 解析包头
                    if (data.Length < Marshal.SizeOf<UdpPacketHeader>())
                    {
                        continue;
                    }

                    UdpPacketHeader header = BytesToStruct<UdpPacketHeader>(data, 0);

                    // 提取像素数据
                    int headerSize = Marshal.SizeOf<UdpPacketHeader>();
                    int pixelDataSize = UdpPacketHeader.PIXELS_PER_SEGMENT;
                    int[] pixelData = new int[pixelDataSize];
                    
                    Buffer.BlockCopy(data, headerSize, pixelData, 0, pixelDataSize * sizeof(int));

                    // 重组帧
                    int[,] completeFrame = _reassembler.ProcessPacket(header, pixelData);

                    if (completeFrame != null)
                    {
                        // 限制队列大小，避免内存溢出
                        if (_frameQueue.Count >= _maxQueueSize)
                        {
                            _frameQueue.TryDequeue(out _); // 丢弃最旧的帧
                        }

                        _frameQueue.Enqueue(completeFrame);
                        OnFrameReceived(completeFrame);
                    }
                }
                catch (SocketException)
                {
                    if (_isRunning)
                    {
                        Thread.Sleep(10); // 避免CPU占用过高
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"Receive error: {ex.Message}");
                }
            }
        }

        private void CheckHeartbeat(object state)
        {
            if (!_isRunning) return;

            var timeSinceLastPacket = DateTime.Now - _lastPacketTime;
            if (timeSinceLastPacket.TotalSeconds > 3)
            {
                OnConnectionLost();
            }
        }

        // ==================== 辅助方法 ====================

        private static byte[] StructToBytes<T>(T structure) where T : struct
        {
            int size = Marshal.SizeOf(structure);
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(structure, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return bytes;
        }

        private static T BytesToStruct<T>(byte[] bytes, int offset) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(bytes, offset, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        // ==================== 事件触发 ====================

        protected virtual void OnFrameReceived(int[,] frame)
        {
            FrameReceived?.Invoke(this, frame);
        }

        protected virtual void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }

        protected virtual void OnConnectionLost()
        {
            ConnectionLost?.Invoke(this, EventArgs.Empty);
        }

        // ==================== IDisposable ====================

        public void Dispose()
        {
            Disconnect();
            _heartbeatTimer?.Dispose();
            _rxClient?.Dispose();
            _txClient?.Dispose();
        }
    }
}
