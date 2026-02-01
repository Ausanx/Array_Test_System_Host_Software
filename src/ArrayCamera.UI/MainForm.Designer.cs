namespace ArrayCamera.UI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabImaging;
        private System.Windows.Forms.TabPage tabNeural;
        private System.Windows.Forms.TabPage tabDeviceTest;
        private System.Windows.Forms.TabPage tabKeithley;

        private System.Windows.Forms.Panel panelLeft;
        private System.Windows.Forms.Panel panelRight;
        
        private System.Windows.Forms.PictureBox _pictureBoxHeatmap;
        private System.Windows.Forms.GroupBox groupConnection;
        private System.Windows.Forms.GroupBox groupControl;
        private System.Windows.Forms.GroupBox groupParameters;
        private System.Windows.Forms.GroupBox groupRendering;

        private System.Windows.Forms.Label labelFpgaIp;
        private System.Windows.Forms.TextBox txtFpgaIpAddress;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnDisconnect;
        private System.Windows.Forms.Button btnReset;

        private System.Windows.Forms.RadioButton rbPreviewMode;
        private System.Windows.Forms.RadioButton rbPrecisionMode;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;

        private System.Windows.Forms.Label labelTiaRange;
        private System.Windows.Forms.ComboBox comboTiaRange;
        private System.Windows.Forms.Label labelWaitCycles;
        private System.Windows.Forms.NumericUpDown numWaitCycles;
        private System.Windows.Forms.CheckBox chkFrameAveraging;
        private System.Windows.Forms.Label labelAvgFrames;
        private System.Windows.Forms.NumericUpDown numAveragingFrames;

        private System.Windows.Forms.Label labelColorMap;
        private System.Windows.Forms.ComboBox comboColorMap;

        private System.Windows.Forms.StatusStrip _statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel _statusLabelFps;
        private System.Windows.Forms.ToolStripStatusLabel _statusLabelDropRate;
        private System.Windows.Forms.ToolStripStatusLabel _statusLabelConnection;
        private System.Windows.Forms.ToolStripStatusLabel _statusLabelMousePos;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.SuspendLayout();

            // ==================== Form ====================
            this.Text = "Array Test System - 交叉阵列测试系统 v1.0";
            this.Size = new System.Drawing.Size(1600, 950);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(240, 242, 245);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular);

            // ==================== TabControl ====================
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            
            this.tabImaging = new System.Windows.Forms.TabPage { Text = "阵列成像" };
            this.tabNeural = new System.Windows.Forms.TabPage { Text = "神经形态训练", Enabled = false };
            this.tabDeviceTest = new System.Windows.Forms.TabPage { Text = "独立器件测试", Enabled = false };
            this.tabKeithley = new System.Windows.Forms.TabPage { Text = "外部源表控制", Enabled = false };

            this.tabControl.TabPages.Add(this.tabImaging);
            this.tabControl.TabPages.Add(this.tabNeural);
            this.tabControl.TabPages.Add(this.tabDeviceTest);
            this.tabControl.TabPages.Add(this.tabKeithley);

            // 占位符点击事件
            this.tabNeural.Click += TabNeural_Click;
            this.tabDeviceTest.Click += TabDeviceTest_Click;
            this.tabKeithley.Click += TabKeithley_Click;

            // ==================== Tab: 阵列成像 ====================
            this.panelLeft = new System.Windows.Forms.Panel
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                Padding = new System.Windows.Forms.Padding(16),
                BackColor = System.Drawing.Color.FromArgb(240, 242, 245)
            };

            this.panelRight = new System.Windows.Forms.Panel
            {
                Dock = System.Windows.Forms.DockStyle.Right,
                Width = 380,
                Padding = new System.Windows.Forms.Padding(16),
                BackColor = System.Drawing.Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
            };

            // 热力图显示
            this._pictureBoxHeatmap = new System.Windows.Forms.PictureBox
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom,
                BackColor = System.Drawing.Color.FromArgb(15, 23, 42),
                Margin = new System.Windows.Forms.Padding(0, 0, 8, 0)
            };
            this._pictureBoxHeatmap.MouseMove += PictureBoxHeatmap_MouseMove;
            this.panelLeft.Controls.Add(this._pictureBoxHeatmap);

            // ==================== 右侧控制面板 ====================
            int yPos = 10;

            // 连接组
            this.groupConnection = new System.Windows.Forms.GroupBox
            {
                Text = "连接设置",
                Location = new System.Drawing.Point(12, yPos),
                Size = new System.Drawing.Size(350, 130),
                Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F),
                ForeColor = System.Drawing.Color.FromArgb(30, 41, 59),
                FlatStyle = System.Windows.Forms.FlatStyle.Flat
            };

            this.labelFpgaIp = new System.Windows.Forms.Label
            {
                Text = "FPGA IP地址:",
                Location = new System.Drawing.Point(12, 26),
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9F),
                ForeColor = System.Drawing.Color.FromArgb(71, 85, 105)
            };

            this.txtFpgaIpAddress = new System.Windows.Forms.TextBox
            {
                Text = "192.168.1.100",
                Location = new System.Drawing.Point(12, 52),
                Size = new System.Drawing.Size(320, 26),
                Font = new System.Drawing.Font("Consolas", 10F),
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.FromArgb(248, 250, 252),
                ForeColor = System.Drawing.Color.FromArgb(15, 23, 42)
            };

            this.btnConnect = new System.Windows.Forms.Button
            {
                Text = "连接",
                Location = new System.Drawing.Point(12, 90),
                Size = new System.Drawing.Size(100, 32),
                FlatStyle = System.Windows.Forms.FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(34, 197, 94),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F),
                Cursor = System.Windows.Forms.Cursors.Hand
            };
            this.btnConnect.FlatAppearance.BorderSize = 0;
            this.btnConnect.Click += BtnConnect_Click;

            this.btnDisconnect = new System.Windows.Forms.Button
            {
                Text = "断开",
                Location = new System.Drawing.Point(122, 90),
                Size = new System.Drawing.Size(100, 32),
                Enabled = false,
                FlatStyle = System.Windows.Forms.FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(239, 68, 68),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F),
                Cursor = System.Windows.Forms.Cursors.Hand
            };
            this.btnDisconnect.FlatAppearance.BorderSize = 0;
            this.btnDisconnect.Click += BtnDisconnect_Click;

            this.btnReset = new System.Windows.Forms.Button
            {
                Text = "复位",
                Location = new System.Drawing.Point(232, 90),
                Size = new System.Drawing.Size(100, 32),
                FlatStyle = System.Windows.Forms.FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(100, 116, 139),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F),
                Cursor = System.Windows.Forms.Cursors.Hand
            };
            this.btnReset.FlatAppearance.BorderSize = 0;
            this.btnReset.Click += BtnReset_Click;

            this.groupConnection.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                this.labelFpgaIp, this.txtFpgaIpAddress,
                this.btnConnect, this.btnDisconnect, this.btnReset
            });

            yPos += 130;

            // 采集控制组
            this.groupControl = new System.Windows.Forms.GroupBox
            {
                Text = "采集控制",
                Location = new System.Drawing.Point(12, yPos),
                Size = new System.Drawing.Size(350, 150),
                Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F),
                ForeColor = System.Drawing.Color.FromArgb(30, 41, 59),
                FlatStyle = System.Windows.Forms.FlatStyle.Flat
            };

            this.rbPreviewMode = new System.Windows.Forms.RadioButton
            {
                Text = "预览模式 (快速)",
                Location = new System.Drawing.Point(10, 25),
                AutoSize = true,
                Checked = true
            };

            this.rbPrecisionMode = new System.Windows.Forms.RadioButton
            {
                Text = "高精模式",
                Location = new System.Drawing.Point(10, 55),
                AutoSize = true
            };

            this.btnStart = new System.Windows.Forms.Button
            {
                Text = "▶ 开始采集",
                Location = new System.Drawing.Point(12, 100),
                Size = new System.Drawing.Size(160, 40),
                Enabled = false,
                FlatStyle = System.Windows.Forms.FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(59, 130, 246),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold),
                Cursor = System.Windows.Forms.Cursors.Hand
            };
            this.btnStart.FlatAppearance.BorderSize = 0;
            this.btnStart.Click += BtnStart_Click;

            this.btnStop = new System.Windows.Forms.Button
            {
                Text = "⏹ 停止采集",
                Location = new System.Drawing.Point(182, 100),
                Size = new System.Drawing.Size(160, 40),
                Enabled = false,
                FlatStyle = System.Windows.Forms.FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(239, 68, 68),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold),
                Cursor = System.Windows.Forms.Cursors.Hand
            };
            this.btnStop.FlatAppearance.BorderSize = 0;
            this.btnStop.Click += BtnStop_Click;

            this.groupControl.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                this.rbPreviewMode, this.rbPrecisionMode,
                this.btnStart, this.btnStop
            });

            yPos += 150;

            // 参数设置组
            this.groupParameters = new System.Windows.Forms.GroupBox
            {
                Text = "参数设置",
                Location = new System.Drawing.Point(10, yPos),
                Size = new System.Drawing.Size(320, 190)
            };

            this.labelTiaRange = new System.Windows.Forms.Label
            {
                Text = "TIA量程:",
                Location = new System.Drawing.Point(10, 25),
                AutoSize = true
            };

            this.comboTiaRange = new System.Windows.Forms.ComboBox
            {
                Location = new System.Drawing.Point(120, 22),
                Size = new System.Drawing.Size(185, 25),
                DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
            };
            this.comboTiaRange.Items.AddRange(new object[] { "1 MΩ", "10 MΩ", "100 MΩ", "1 GΩ" });
            this.comboTiaRange.SelectedIndex = 1;

            this.labelWaitCycles = new System.Windows.Forms.Label
            {
                Text = "等待周期:",
                Location = new System.Drawing.Point(10, 60),
                AutoSize = true
            };

            this.numWaitCycles = new System.Windows.Forms.NumericUpDown
            {
                Location = new System.Drawing.Point(120, 57),
                Size = new System.Drawing.Size(185, 25),
                Minimum = 1,
                Maximum = 10000,
                Value = 100
            };

            this.chkFrameAveraging = new System.Windows.Forms.CheckBox
            {
                Text = "启用多帧平均",
                Location = new System.Drawing.Point(10, 100),
                AutoSize = true
            };

            this.labelAvgFrames = new System.Windows.Forms.Label
            {
                Text = "平均帧数:",
                Location = new System.Drawing.Point(10, 135),
                AutoSize = true
            };

            this.numAveragingFrames = new System.Windows.Forms.NumericUpDown
            {
                Location = new System.Drawing.Point(120, 132),
                Size = new System.Drawing.Size(185, 25),
                Minimum = 2,
                Maximum = 100,
                Value = 10
            };

            this.groupParameters.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                this.labelTiaRange, this.comboTiaRange,
                this.labelWaitCycles, this.numWaitCycles,
                this.chkFrameAveraging, this.labelAvgFrames, this.numAveragingFrames
            });

            yPos += 200;

            // 渲染设置组
            this.groupRendering = new System.Windows.Forms.GroupBox
            {
                Text = "渲染设置",
                Location = new System.Drawing.Point(10, yPos),
                Size = new System.Drawing.Size(320, 120)
            };

            this.labelColorMap = new System.Windows.Forms.Label
            {
                Text = "颜色映射:",
                Location = new System.Drawing.Point(10, 25),
                AutoSize = true
            };

            this.comboColorMap = new System.Windows.Forms.ComboBox
            {
                Location = new System.Drawing.Point(120, 22),
                Size = new System.Drawing.Size(185, 25),
                DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
            };
            this.comboColorMap.Items.AddRange(new object[] { "Jet", "Parula", "Gray", "Hot", "Viridis" });
            this.comboColorMap.SelectedIndex = 0;
            this.comboColorMap.SelectedIndexChanged += ComboColorMap_SelectedIndexChanged;

            this.groupRendering.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                this.labelColorMap, this.comboColorMap
            });

            this.panelRight.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                this.groupConnection, this.groupControl,
                this.groupParameters, this.groupRendering
            });

            this.tabImaging.Controls.Add(this.panelLeft);
            this.tabImaging.Controls.Add(this.panelRight);

            // ==================== StatusStrip ====================
            this._statusStrip = new System.Windows.Forms.StatusStrip();
            this._statusStrip.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this._statusStrip.Font = new System.Drawing.Font("Consolas", 9F);
            
            this._statusLabelFps = new System.Windows.Forms.ToolStripStatusLabel
            {
                Text = "FPS: 0.0",
                BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right,
                Width = 120,
                Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(34, 197, 94)
            };

            this._statusLabelDropRate = new System.Windows.Forms.ToolStripStatusLabel
            {
                Text = "Frames: 0 | Packets: 0",
                BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right,
                Width = 380,
                ForeColor = System.Drawing.Color.FromArgb(71, 85, 105)
            };

            this._statusLabelConnection = new System.Windows.Forms.ToolStripStatusLabel
            {
                Text = "未连接",
                BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right,
                Width = 140,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(239, 68, 68)
            };

            this._statusLabelMousePos = new System.Windows.Forms.ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Text = "位置: (-, -) | 电流: -",
                ForeColor = System.Drawing.Color.FromArgb(71, 85, 105)
            };

            this._statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
            {
                this._statusLabelFps,
                this._statusLabelDropRate,
                this._statusLabelConnection,
                this._statusLabelMousePos
            });

            // ==================== 添加控件到窗体 ====================
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this._statusStrip);

            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
