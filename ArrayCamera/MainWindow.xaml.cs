using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ArrayCamera
{
    public enum LogType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public partial class MainWindow : Window
    {
        // ==================== 常量定义 ====================
        private const int ImageSize = 512;
        private const int PixelCount = ImageSize * ImageSize;

        // ==================== 核心状态变量 ====================
        private WriteableBitmap? _heatmapBitmap;
        private byte[]? _pixelBuffer;  // 512×512 灰度数据缓冲区
        private bool _isScanning = false;  // 扫描运行状态标志
        private bool _isDemoMode = false; // 演示模式标志（初始: 关闭）
        private bool _isConnected = false; // 硬件连接状态（初始: 未连接）
        private uint _currentMode = 0; // 当前扫描模式 (0=Preview, 1=Precision)

        // ==================== 演示模式变量 ====================
        private DispatcherTimer? _demoTimer;
        private double _rotationAngle = 0;

        // ==================== 网络通信变量 ====================
        private NetworkDriver? _networkDriver;

        // ==================== 硬件参数 ====================
        private double _drainVoltage = 0.5;
        private int _tiaRange = 1;
        private uint _waitCycles = 20;

        public MainWindow()
        {
            InitializeComponent();
            InitializeResources();
            InitializeNetworkDriver();
            Log("系统已启动，待机状态", LogType.Info);
        }

        // ==================== 资源初始化 ====================

        private void InitializeNetworkDriver()
        {
            _networkDriver = new NetworkDriver("192.168.2.88", 8080);

            // 订阅事件
            _networkDriver.OnLog += (msg, type) => Dispatcher.Invoke(() => Log(msg, type));
            _networkDriver.OnFrameReady += data => Dispatcher.Invoke(() => OnFrameReceived(data));
            _networkDriver.OnTextMessageReceived += msg => Dispatcher.Invoke(() => Log($"[板卡消息] {msg}", LogType.Info));
        }

        private void InitializeResources()
        {
            // 初始化热力图位图
            _heatmapBitmap = new WriteableBitmap(ImageSize, ImageSize, 96, 96, PixelFormats.Bgr32, null);
            HeatmapImage.Source = _heatmapBitmap;

            // 初始化像素缓冲区
            _pixelBuffer = new byte[PixelCount];

            // 绘制初始黑屏
            Array.Clear(_pixelBuffer, 0, _pixelBuffer.Length);
            DrawHeatmap(_pixelBuffer);

            // 绑定鼠标移动事件
            HeatmapImage.MouseMove += HeatmapImage_MouseMove;

            // ✅ 初始状态：演示模式 OFF, 硬件未连接
            _isScanning = false;
            _isDemoMode = false;
            _isConnected = false;

            // ✅ 更新UI状态
            UpdateUIState();

            // ✅ 初始状态文本
            StatusText.Text = "系统待机 (IDLE)";
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(158, 158, 158));  // 灰色
        }

        // ==================== 演示模式逻辑 ====================

        /// <summary>
        /// UI状态机管理 - 集中管理所有按钮和控件的启用/禁用状态
        /// 在任何状态变化时调用此方法以保持UI一致性
        /// </summary>
        private void UpdateUIState()
        {
            // ✅ BtnStart 启用条件: (已连接 OR 演示模式开启) AND 非运行状态
            bool canStart = (_isConnected || _isDemoMode) && !_isScanning;
            BtnStart.IsEnabled = canStart;

            // ✅ BtnStop 启用条件: 正在运行
            BtnStop.IsEnabled = _isScanning;

            // ✅ BtnConnect 启用条件: 未连接状态
            BtnConnect.IsEnabled = !_isConnected;

            // ✅ 硬件参数面板启用条件: 未运行状态
            if (GrpParameters != null)
            {
                GrpParameters.IsEnabled = !_isScanning;
            }

            // ✅ 调试指令面板在运行时也可用
            // 这些低级指令不受_isScanning影响

            Log($"[UI状态] 已连接={_isConnected}, 演示模式={_isDemoMode}, 运行中={_isScanning}", LogType.Info);
        }

        // ==================== 演示模式逻辑 ====================

        private void StartDemoMode()
        {
            if (!_isDemoMode) return;
            if (_demoTimer != null) return;  // 已经在运行

            _demoTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)  // ~30 FPS
            };
            _demoTimer.Tick += (s, e) => GenerateDemoFrame();
            _demoTimer.Start();

            Log("演示模式已启动 (30 FPS 高斯光斑)", LogType.Success);
        }

        private void StopDemoMode()
        {
            if (_demoTimer != null)
            {
                _demoTimer.Stop();
                _demoTimer = null;
                Log("演示模式已停止", LogType.Info);
            }
        }

        private void GenerateDemoFrame()
        {
            // ✅ 检查是否应该停止
            if (!_isScanning)
            {
                return;  // 立即退出，无需生成数据
            }

            if (_pixelBuffer == null || _heatmapBitmap == null) return;

            // 更新旋转角度
            _rotationAngle += 0.02;
            double centerX = ImageSize / 2.0;
            double centerY = ImageSize / 2.0;

            // 生成3个旋转的高斯光斑
            double[][] spots = new double[][]
            {
                new double[] { centerX + 120 * Math.Cos(_rotationAngle), centerY + 120 * Math.Sin(_rotationAngle), 50 },
                new double[] { centerX + 120 * Math.Cos(_rotationAngle + 2.0944), centerY + 120 * Math.Sin(_rotationAngle + 2.0944), 45 },
                new double[] { centerX + 120 * Math.Cos(_rotationAngle + 4.1888), centerY + 120 * Math.Sin(_rotationAngle + 4.1888), 55 }
            };

            // 计算每个像素的强度
            for (int y = 0; y < ImageSize; y++)
            {
                for (int x = 0; x < ImageSize; x++)
                {
                    double intensity = 0;

                    // 叠加所有光斑
                    foreach (var spot in spots)
                    {
                        double dx = x - spot[0];
                        double dy = y - spot[1];
                        double sigma = spot[2];
                        intensity += Math.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));
                    }

                    // 归一化到 0-255
                    intensity = Math.Min(1.0, intensity);
                    _pixelBuffer[y * ImageSize + x] = (byte)(intensity * 255);
                }
            }

            // 绘制到屏幕
            DrawHeatmap(_pixelBuffer);
        }

        private unsafe void DrawHeatmap(byte[] grayData)
        {
            if (_heatmapBitmap == null || grayData.Length != PixelCount) return;

            _heatmapBitmap.Lock();

            try
            {
                byte* pBackBuffer = (byte*)_heatmapBitmap.BackBuffer;
                int stride = _heatmapBitmap.BackBufferStride;

                for (int y = 0; y < ImageSize; y++)
                {
                    byte* row = pBackBuffer + y * stride;
                    for (int x = 0; x < ImageSize; x++)
                    {
                        byte intensity = grayData[y * ImageSize + x];

                        // 热力图配色：黑-深红-橙-黄-白
                        byte r, g, b;
                        if (intensity < 64)  // 黑 -> 深红
                        {
                            double t = intensity / 64.0;
                            r = (byte)(t * 180);
                            g = 0;
                            b = 0;
                        }
                        else if (intensity < 128)  // 深红 -> 橙
                        {
                            double t = (intensity - 64) / 64.0;
                            r = (byte)(180 + t * 75);
                            g = (byte)(t * 100);
                            b = 0;
                        }
                        else if (intensity < 192)  // 橙 -> 黄
                        {
                            double t = (intensity - 128) / 64.0;
                            r = 255;
                            g = (byte)(100 + t * 155);
                            b = 0;
                        }
                        else  // 黄 -> 白
                        {
                            double t = (intensity - 192) / 63.0;
                            r = 255;
                            g = 255;
                            b = (byte)(t * 255);
                        }

                        int index = x * 4;
                        row[index] = b;       // Blue
                        row[index + 1] = g;   // Green
                        row[index + 2] = r;   // Red
                        row[index + 3] = 255; // Alpha
                    }
                }

                _heatmapBitmap.AddDirtyRect(new Int32Rect(0, 0, ImageSize, ImageSize));
            }
            finally
            {
                _heatmapBitmap.Unlock();
            }
        }

        // ==================== 日志系统 ====================

        private void Log(string message, LogType type = LogType.Info)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    var paragraph = new Paragraph();

                    // 时间戳（灰色）
                    paragraph.Inlines.Add(new Run($"[{timestamp}] ") { Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)) });

                    // 类型标签
                    string tag = type switch
                    {
                        LogType.Success => "[SUCCESS] ",
                        LogType.Warning => "[WARNING] ",
                        LogType.Error => "[ERROR] ",
                        _ => "[INFO] "
                    };
                    var tagRun = new Run(tag);
                    tagRun.Foreground = type switch
                    {
                        LogType.Success => new SolidColorBrush(Color.FromRgb(0, 128, 0)),   // 深绿
                        LogType.Warning => new SolidColorBrush(Color.FromRgb(204, 102, 0)), // 深橙
                        LogType.Error => new SolidColorBrush(Color.FromRgb(192, 0, 0)),     // 深红
                        _ => new SolidColorBrush(Color.FromRgb(0, 0, 0))  // 黑色
                    };
                    paragraph.Inlines.Add(tagRun);

                    // 消息内容
                    paragraph.Inlines.Add(new Run(message) { Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)) });

                    LogBox.Document.Blocks.Add(paragraph);
                    LogBox.ScrollToEnd();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"日志写入失败: {ex.Message}");
                }
            });
        }

        // ==================== UDP 网络通信 ====================

        private async void ConnectUdp()
        {
            try
            {
                if (_networkDriver == null)
                {
                    Log("网络驱动未初始化", LogType.Error);
                    return;
                }

                // 更新状态
                NetworkStatusText.Text = "连接中...";
                NetworkStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                BtnConnect.IsEnabled = false;

                Log("━━━━━━━━━━━ 开始连接硬件 ━━━━━━━━━━━", LogType.Info);

                // 调用新的Connect方法（智能绑定 + 真实握手）
                bool success = await _networkDriver.Connect(handshakeTimeoutMs: 3000);

                if (success)
                {
                    // 连接成功
                    _isConnected = true;
                    NetworkStatusText.Text = "已连接";
                    NetworkStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    BtnConnect.Content = "断开";
                    BtnConnect.IsEnabled = true;

                    StatusText.Text = "硬件在线 (HARDWARE ONLINE)";
                    StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(33, 150, 243));

                    Log("━━━━━━━━━━━ 连接成功！━━━━━━━━━━━", LogType.Success);

                    // ✅ 更新UI状态
                    UpdateUIState();
                }
                else
                {
                    // 连接失败
                    NetworkStatusText.Text = "连接失败";
                    NetworkStatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    BtnConnect.IsEnabled = true;

                    MessageBox.Show(
                        "连接失败：未收到板卡响应\n\n" +
                        "请检查:\n" +
                        "1. 板卡是否已上电\n" +
                        "2. 网线是否连接\n" +
                        "3. 本机是否有192.168.2.x网段的IP\n" +
                        "4. 防火墙是否拦截UDP端口8080/8081",
                        "连接失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Log($"连接异常: {ex.Message}", LogType.Error);
                NetworkStatusText.Text = "异常";
                NetworkStatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                BtnConnect.IsEnabled = true;

                MessageBox.Show(
                    $"连接过程中发生异常:\n{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DisconnectUdp()
        {
            if (_networkDriver != null)
            {
                _networkDriver.Disconnect();
                _isConnected = false;

                NetworkStatusText.Text = "未连接";
                NetworkStatusText.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));

                BtnConnect.Content = "连接/绑定";
                BtnConnect.IsEnabled = true;

                Log("已断开连接", LogType.Info);
                // ✅ 更新UI状态
                UpdateUIState();
            }
        }

        private void OnFrameReceived(byte[] frameData)
        {
            if (_pixelBuffer != null && frameData != null && frameData.Length == PixelCount)
            {
                Array.Copy(frameData, _pixelBuffer, PixelCount);
                DrawHeatmap(_pixelBuffer);
            }
        }

        /// <summary>
        /// 处理接收到的完整帧
        /// </summary>
        private void OnFrameReceived(int[] frameData)
        {
            if (frameData.Length != PixelCount)
            {
                Log($"帧数据长度错误: {frameData.Length} (期望 {PixelCount})", LogType.Error);
                return;
            }

            // 转换为 byte 数组
            byte[] byteData = new byte[PixelCount];
            for (int i = 0; i < PixelCount; i++)
            {
                byteData[i] = (byte)Math.Clamp(frameData[i], 0, 255);
            }

            // 渲染热图
            DrawHeatmap(byteData);
        }

        private void SendCommand(uint cmdId, uint param1, uint param2)
        {
            if (_networkDriver == null || !_isConnected)
            {
                Log("未连接到硬件，无法发送指令", LogType.Warning);
                return;
            }

            uint mode = 0; // 默认预览模式
            _networkDriver.SendCommand(cmdId, mode, param1, param2);

            // 更新发送计数
            Dispatcher.Invoke(() =>
            {
                TxCountText.Text = _networkDriver.TxCount.ToString();
                RxCountText.Text = _networkDriver.RxCount.ToString();
            });
        }

        // ==================== 交互事件处理 ====================

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                DisconnectUdp();
            }
            else
            {
                ConnectUdp();
            }
        }

        private void TgDemo_Changed(object sender, RoutedEventArgs e)
        {
            _isDemoMode = TgDemo.IsChecked == true;

            if (_isDemoMode)
            {
                StartDemoMode();
                StatusText.Text = "演示模式就绪 (SIMULATION READY)";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));  // 绿色
                Log("✓ 已启用演示仿真模式（不连接硬件）", LogType.Success);
            }
            else
            {
                StopDemoMode();

                // 如果运行中，停止扫描
                if (_isScanning)
                {
                    _isScanning = false;
                    Log("▢ 演示模式停止，扫描已停止", LogType.Warning);
                }

                // 恢复待机状态
                if (!_isConnected)
                {
                    StatusText.Text = "系统待机 (IDLE)";
                    StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(158, 158, 158));  // 灰色
                }

                Log("✗ 已禁用演示模式", LogType.Info);
            }

            // ✅ 更新UI状态
            UpdateUIState();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            // ✅ 强制检查：不能在"未连接"且"非演示模式"下运行
            if (!_isConnected && !_isDemoMode)
            {
                MessageBox.Show(
                    "请先完成以下之一:\n" +
                    "1. 连接硬件（点击\"连接/绑定\"按钮）\n" +
                    "2. 启用演示模式（打开演示仿真开关）",
                    "无法启动采集",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Log("✗ 启动采集被拒绝: 未连接硬件且未开启演示模式", LogType.Error);
                return;
            }

            // ✅ 检查是否已经运行
            if (_isScanning)
            {
                Log("⚠ 采集已在运行中", LogType.Warning);
                return;
            }

            // ✅ 读取硬件参数
            if (!double.TryParse(TxtDrainVoltage.Text, out _drainVoltage))
            {
                MessageBox.Show("漏极电压格式错误", "参数错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Log("✗ 参数错误: 漏极电压格式不正确", LogType.Error);
                return;
            }

            _tiaRange = CmbTiaRange.SelectedIndex;

            if (!uint.TryParse(TxtWaitCycles.Text, out _waitCycles))
            {
                MessageBox.Show("建立时间格式错误", "参数错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Log("✗ 参数错误: 建立时间格式不正确", LogType.Error);
                return;
            }

            // ✅ 读取扫描模式（从下拉框）
            _currentMode = (uint)(CmbScanMode?.SelectedIndex ?? 0);  // 0=Preview, 1=Precision

            // ✅ 设置运行状态
            _isScanning = true;

            // ✅ 更新UI状态
            UpdateUIState();

            // ✅ 更新状态显示
            string modeName = _currentMode == 0 ? "预览模式" : "高精模式";
            StatusText.Text = $"采集中 ({modeName})";
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 152, 0));  // 橙色

            // ✅ 根据模式执行逻辑
            if (_isDemoMode && !_isConnected)
            {
                // 纯演示模式: 演示数据已经在运行
                Log($"[演示模式] ▶ 开始采集 ({modeName})", LogType.Success);
                Log($"  参数: Vd={_drainVoltage}V, TIA=Range{_tiaRange}, Wait={_waitCycles}us", LogType.Info);
            }
            else if (_isConnected)
            {
                // 硬件模式: 发送START指令
                SendCommand(3, _currentMode, (uint)_tiaRange);  // CmdID=3 (START)
                Log($"[硬件模式] ▶ 已发送启动指令", LogType.Success);
                Log($"  扫描模式: {modeName}", LogType.Info);
                Log($"  参数: Vd={_drainVoltage}V, TIA=Range{_tiaRange}, Wait={_waitCycles}us", LogType.Info);
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            // ✅ 检查是否正在运行
            if (!_isScanning)
            {
                Log("⚠ 采集未运行", LogType.Warning);
                return;
            }

            // ✅ 关键：立即设置运行标志为false
            // 这样后台循环（演示或硬件）会在下一个检查点退出
            _isScanning = false;

            // ✅ 更新UI状态
            UpdateUIState();

            // ✅ 更新状态显示
            if (_isDemoMode && !_isConnected)
            {
                StatusText.Text = "演示模式就绪 (SIMULATION READY)";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));  // 绿色
                Log("[演示模式] ■ 采集已停止", LogType.Warning);
            }
            else if (_isConnected)
            {
                StatusText.Text = "硬件在线 (HARDWARE ONLINE)";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(33, 150, 243));  // 蓝色

                // 发送STOP指令
                SendCommand(4, 0, 0);  // CmdID=4 (STOP)
                Log("[硬件模式] ■ 已发送停止指令", LogType.Warning);
            }
            else
            {
                StatusText.Text = "系统待机 (IDLE)";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(158, 158, 158));  // 灰色
                Log("■ 采集已停止", LogType.Info);
            }
        }

        private void CmbTiaRange_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbTiaRange == null) return;
            _tiaRange = CmbTiaRange.SelectedIndex;
            Log($"TIA 量程已更新: Range {_tiaRange}", LogType.Info);
        }

        private void HeatmapImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (HeatmapImage == null || MousePosText == null) return;

            Point pos = e.GetPosition(HeatmapImage);
            double scaleX = ImageSize / HeatmapImage.ActualWidth;
            double scaleY = ImageSize / HeatmapImage.ActualHeight;

            int pixelX = (int)(pos.X * scaleX);
            int pixelY = (int)(pos.Y * scaleY);

            if (pixelX >= 0 && pixelX < ImageSize && pixelY >= 0 && pixelY < ImageSize)
            {
                int value = _pixelBuffer != null ? _pixelBuffer[pixelY * ImageSize + pixelX] : 0;
                MousePosText.Text = $"位置: ({pixelX}, {pixelY}) = {value}";
            }
            else
            {
                MousePosText.Text = "位置: (-, -)";
            }
        }

        private void BtnSendCmd_Click(object sender, RoutedEventArgs e)
        {
            // ✅ CmdId直接从SelectedIndex读取（0,1,2对应Ping/Reset/Config）
            uint cmdId = (uint)(CmbCmdId.SelectedIndex);

            if (!uint.TryParse(TxtParam1.Text, out uint param1))
            {
                Log("Param1 格式错误", LogType.Error);
                return;
            }
            if (!uint.TryParse(TxtParam2.Text, out uint param2))
            {
                Log("Param2 格式错误", LogType.Error);
                return;
            }

            string cmdName = cmdId switch
            {
                0 => "Ping (心跳)",
                1 => "Reset (复位)",
                2 => "Config (配置)",
                _ => "Unknown"
            };

            Log($"[调试指令] 发送 {cmdName} (P1={param1}, P2={param2})", LogType.Info);
            SendCommand(cmdId, param1, param2);
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Document.Blocks.Clear();
            Log("日志已清空", LogType.Info);
        }

        protected override void OnClosed(EventArgs e)
        {
            _demoTimer?.Stop();
            _networkDriver?.Dispose();
            Log("程序退出，资源已释放", LogType.Info);
            base.OnClosed(e);
        }
    }
}
