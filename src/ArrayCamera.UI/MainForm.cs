using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using ArrayCamera.Core;
using ArrayCamera.Driver;

namespace ArrayCamera.UI
{
    /// <summary>
    /// 主窗体 - 实现生产者-消费者模式
    /// </summary>
    public partial class MainForm : Form
    {
        private UdpReceiver _udpReceiver;
        private HeatmapRenderer _renderer;
        private System.Windows.Forms.Timer _renderTimer;
        private Bitmap _displayBitmap;
        
        // 性能统计
        private Stopwatch _fpsTimer;
        private int _frameCounter;
        private double _currentFps;

        // 鼠标交互
        private Point _lastMousePos;
        private int[,] _currentFrameData;

        public MainForm()
        {
            InitializeComponent();
            InitializeCustomComponents();
        }

        private void InitializeCustomComponents()
        {
            // 初始化渲染器
            _renderer = new HeatmapRenderer();
            _renderer.SetColorMap(ColorMapType.Jet);
            _renderer.EnableAutoScale(true);

            // 初始化UDP接收器
            _udpReceiver = new UdpReceiver(maxQueueSize: 10);
            _udpReceiver.ErrorOccurred += OnUdpError;
            _udpReceiver.ConnectionLost += OnConnectionLost;

            // 初始化显示Bitmap（内存复用）
            _displayBitmap = new Bitmap(512, 512, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            // 添加按钮悬停效果
            AddButtonHoverEffects();

            // 初始化渲染定时器 (30ms ≈ 33 FPS)
            _renderTimer = new System.Windows.Forms.Timer();
            _renderTimer.Interval = 30;
            _renderTimer.Tick += RenderTimer_Tick;

            // FPS计时器
            _fpsTimer = Stopwatch.StartNew();
            _frameCounter = 0;
            _currentFps = 0;
        }

        /// <summary>
        /// 渲染定时器 - 消费者线程
        /// </summary>
        private void RenderTimer_Tick(object sender, EventArgs e)
        {
            // 从队列获取最新帧
            if (_udpReceiver.TryGetFrame(out int[,] frameData))
            {
                _currentFrameData = frameData;

                try
                {
                    // 使用LockBits高性能渲染
                    _renderer.RenderTo(frameData, _displayBitmap);
                    
                    // 更新显示
                    _pictureBoxHeatmap.Image = _displayBitmap;
                    
                    // 更新FPS
                    _frameCounter++;
                    if (_fpsTimer.ElapsedMilliseconds >= 1000)
                    {
                        _currentFps = _frameCounter * 1000.0 / _fpsTimer.ElapsedMilliseconds;
                        _frameCounter = 0;
                        _fpsTimer.Restart();
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Render error: {ex.Message}");
                }
            }

            // 更新状态栏
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            _statusLabelFps.Text = $"FPS: {_currentFps:F1}";
            _statusLabelDropRate.Text = _udpReceiver.GetStatistics();
            
            if (_udpReceiver.IsConnected)
            {
                _statusLabelConnection.Text = "● 已连接";
                _statusLabelConnection.ForeColor = Color.FromArgb(34, 197, 94);
            }
            else
            {
                _statusLabelConnection.Text = "○ 未连接";
                _statusLabelConnection.ForeColor = Color.FromArgb(239, 68, 68);
            }
            
            if (_currentFrameData != null && _lastMousePos.X >= 0 && _lastMousePos.Y >= 0 &&
                _lastMousePos.X < 512 && _lastMousePos.Y < 512)
            {
                int value = _currentFrameData[_lastMousePos.Y, _lastMousePos.X];
                _statusLabelMousePos.Text = $"位置: ({_lastMousePos.X}, {_lastMousePos.Y}) | 电流: {value}";
            }
        }

        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateStatus), message);
                return;
            }
            _statusLabelConnection.Text = message;
        }

        private void OnUdpError(object sender, string message)
        {
            UpdateStatus($"错误: {message}");
        }

        private void OnConnectionLost(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnConnectionLost(sender, e)));
                return;
            }

            MessageBox.Show("连接断开，3秒未接收到数据。", "连接丢失", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            
            BtnDisconnect_Click(null, null);
        }

        // ==================== 按钮事件处理 ====================

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                string fpgaIp = txtFpgaIpAddress.Text.Trim();
                if (string.IsNullOrEmpty(fpgaIp))
                {
                    MessageBox.Show("请输入FPGA IP地址", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _udpReceiver.Connect(fpgaIp);
                _renderTimer.Start();

                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;
                btnStart.Enabled = true;
                btnStop.Enabled = false;

                UpdateStatus("已连接到FPGA");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            _renderTimer.Stop();
            _udpReceiver.Disconnect();

            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
            btnStart.Enabled = false;
            btnStop.Enabled = false;

            UpdateStatus("已断开连接");
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            try
            {
                // 获取参数
                uint mode = rbPreviewMode.Checked ? 0u : 1u;
                uint tiaRange = (uint)comboTiaRange.SelectedIndex;
                uint waitCycles = (uint)numWaitCycles.Value;

                // 发送配置命令
                var configCmd = CommandPacket.CreateConfig(mode, tiaRange, waitCycles);
                _udpReceiver.SendCommand(configCmd);

                System.Threading.Thread.Sleep(50);

                // 发送开始命令
                var startCmd = CommandPacket.CreateStart();
                _udpReceiver.SendCommand(startCmd);

                btnStart.Enabled = false;
                btnStop.Enabled = true;

                // 启用帧平均（仅在高精模式下）
                if (mode == 1 && chkFrameAveraging.Checked)
                {
                    _renderer.EnableFrameAveraging(true, (int)numAveragingFrames.Value);
                }
                else
                {
                    _renderer.EnableFrameAveraging(false);
                }

                UpdateStatus("采集中...");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            try
            {
                var stopCmd = CommandPacket.CreateStop();
                _udpReceiver.SendCommand(stopCmd);

                btnStart.Enabled = true;
                btnStop.Enabled = false;

                UpdateStatus("已停止采集");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            try
            {
                var resetCmd = CommandPacket.CreateReset();
                _udpReceiver.SendCommand(resetCmd);
                _udpReceiver.ResetStatistics();

                UpdateStatus("FPGA已复位");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复位失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==================== ColorMap 选择 ====================

        private void ComboColorMap_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedMap = (ColorMapType)comboColorMap.SelectedIndex;
            _renderer.SetColorMap(selectedMap);
        }

        // ==================== 鼠标事件 ====================

        private void PictureBoxHeatmap_MouseMove(object sender, MouseEventArgs e)
        {
            // 转换鼠标坐标到像素坐标
            float scaleX = 512f / _pictureBoxHeatmap.Width;
            float scaleY = 512f / _pictureBoxHeatmap.Height;

            int pixelX = (int)(e.X * scaleX);
            int pixelY = (int)(e.Y * scaleY);

            if (pixelX >= 0 && pixelX < 512 && pixelY >= 0 && pixelY < 512)
            {
                _lastMousePos = new Point(pixelX, pixelY);
            }
        }

        // ==================== Tab 占位符事件 ====================

        private void TabNeural_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Module requires hardware license.", "功能锁定", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TabDeviceTest_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Module requires hardware license.", "功能锁定", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TabKeithley_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Module requires hardware license.", "功能锁定", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ==================== UI增强 ====================

        private void AddButtonHoverEffects()
        {
            // 为所有按钮添加悬停效果
            AddHoverEffect(btnConnect, Color.FromArgb(34, 197, 94), Color.FromArgb(22, 163, 74));
            AddHoverEffect(btnDisconnect, Color.FromArgb(239, 68, 68), Color.FromArgb(220, 38, 38));
            AddHoverEffect(btnReset, Color.FromArgb(100, 116, 139), Color.FromArgb(71, 85, 105));
            AddHoverEffect(btnStart, Color.FromArgb(59, 130, 246), Color.FromArgb(37, 99, 235));
            AddHoverEffect(btnStop, Color.FromArgb(239, 68, 68), Color.FromArgb(220, 38, 38));
        }

        private void AddHoverEffect(Button button, Color normalColor, Color hoverColor)
        {
            button.MouseEnter += (s, e) =>
            {
                if (button.Enabled)
                {
                    button.BackColor = hoverColor;
                }
            };

            button.MouseLeave += (s, e) =>
            {
                if (button.Enabled)
                {
                    button.BackColor = normalColor;
                }
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _renderTimer?.Stop();
            _udpReceiver?.Dispose();
            _displayBitmap?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
