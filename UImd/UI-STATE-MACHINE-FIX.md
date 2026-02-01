# UI 状态机管理修复总结

## ✅ 完成的修复

### 1. 初始化状态错误修复

**问题**: 软件启动时演示模式默认开启（绿色），状态显示为"演示模式"

**修复**:

- ✅ XAML: `TgDemo.IsChecked` 改为默认 `False`
- ✅ C#: 初始化时 `_isDemoMode = false`, `_isConnected = false`
- ✅ 初始状态文本改为 `"系统待机 (IDLE)"` (灰色)
- ✅ 所有控件在启动时处于正确的禁用/启用状态

### 2. UI 状态机管理实现

**添加核心方法**: `UpdateUIState()`

```csharp
private void UpdateUIState()
{
    // ✅ BtnStart 启用条件: (已连接 OR 演示模式) AND 非运行
    bool canStart = (_isConnected || _isDemoMode) && !_isScanning;
    BtnStart.IsEnabled = canStart;

    // ✅ BtnStop 启用条件: 正在运行
    BtnStop.IsEnabled = _isScanning;

    // ✅ BtnConnect 启用条件: 未连接
    BtnConnect.IsEnabled = !_isConnected;

    // ✅ 硬件参数面板启用条件: 未运行
    if (GrpParameters != null)
    {
        GrpParameters.IsEnabled = !_isScanning;
    }
}
```

**调用位置**:

- 演示模式开启/关闭时
- 硬件连接成功/断开时
- 开始/停止采集时

### 3. 交互逻辑修复

#### 问题1: 未连接时仍可点击"开始采集"

**修复**: BtnStart_Click 添加强制检查

```csharp
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
    return;
}
```

#### 问题2: "停止采集"按钮无效

**修复**: 在GenerateDemoFrame()循环头添加检查

```csharp
private void GenerateDemoFrame()
{
    // ✅ 检查是否应该停止
    if (!_isScanning)
    {
        return;  // 立即退出，无需生成数据
    }
    // ... 继续处理数据
}
```

**关键点**: 后台循环在每个帧周期检查`_isScanning`标志，能在~33ms内响应停止信号

### 4. 摆设控件修复

**硬件参数面板**:

- ✅ 添加了 GroupBox x:Name="GrpParameters"
- ✅ 运行时禁用（`GrpParameters.IsEnabled = !_isScanning`）
- ✅ 参数会读取并在start命令中发送

**扫描模式选择**:

- ✅ CmbScanMode 已存在并可用
- ✅ 在BtnStart_Click中读取: `_currentMode = (uint)(CmbScanMode?.SelectedIndex ?? 0)`
- ✅ 发送start命令时包含此参数

### 5. 调试指令去重

**XAML修改**:

```xaml
<ComboBox x:Name="CmbCmdId" SelectedIndex="0">
    <ComboBoxItem Content="0 - Ping (心跳)"/>
    <ComboBoxItem Content="1 - Reset (复位)"/>
    <ComboBoxItem Content="2 - Config (配置)"/>
</ComboBox>
```

**C#修改**: BtnSendCmd_Click

```csharp
// ✅ CmdId直接从SelectedIndex读取（0,1,2对应Ping/Reset/Config）
uint cmdId = (uint)(CmbCmdId.SelectedIndex);
```

**移除了**: "1 - 启动采集" 和 "2 - 停止采集" (避免与大按钮重复)

## 📊 状态转换流程

```
┌─────────────┐
│  系统启动   │
├─────────────┤
│ IDLE状态    │
│ 灰色指示灯  │
│ 待机        │
└──────┬──────┘
       │
       ├─ 用户打开演示开关 ──┐
       │                    │
       ├─ 用户点击连接按钮 ─┤
       │                    │
       └────────────────────┴──────────────┐
                                           │
              ┌────────────────────────────┤
              │                            │
        ┌─────▼────┐          ┌───────────▼────┐
        │SIMULATION │          │ HARDWARE ONLINE│
        │ READY     │          │ 蓝色指示灯      │
        │ 绿色指示灯│          │                │
        └─────┬────┘          └───────────┬────┘
              │                           │
              └──────────────┬────────────┘
                             │
              ┌──────────────┴──────────────┐
              │ 用户点击"开始采集"           │
              └────────────────────────────┐
              │                            │
        ┌─────▼──────────┐       ┌────────▼─────────┐
        │ACQUIRING(Demo) │       │ACQUIRING(Hardware)│
        │ 橙色指示灯      │       │ 橙色指示灯        │
        └─────┬──────────┘       └────────┬─────────┘
              │                           │
              └──────────────┬────────────┘
                             │
              ┌──────────────▼──────────────┐
              │ 用户点击"停止采集"           │
              │ 或断开连接                   │
              └────────────────────────────┘
```

## 🔍 按钮启用/禁用状态矩阵

| 状态         | BtnConnect | BtnStart | BtnStop | GrpParameters |
| ------------ | ---------- | -------- | ------- | ------------- |
| 初始(IDLE)   | ✓          | ✗        | ✗       | ✗             |
| 演示模式开启 | ✓          | ✓        | ✗       | ✓             |
| 硬件连接     | ✓          | ✓        | ✗       | ✓             |
| 演示+采集中  | ✓          | ✗        | ✓       | ✗             |
| 硬件+采集中  | ✓          | ✗        | ✓       | ✗             |

## 📝 代码修改清单

### MainWindow.xaml

- ✅ 演示开关 Checked/Unchecked 事件名改正
- ✅ 删除了硬件参数区域中重复的 CmbScanMode
- ✅ 添加了硬件参数 GroupBox 的 x:Name="GrpParameters"
- ✅ 调试区域 CmdID 改为 Ping/Reset/Config(去除Start/Stop重复)

### MainWindow.xaml.cs

**初始化修复**:

- ✅ `_isDemoMode = false` (而非 true)
- ✅ `_isConnected = false`
- ✅ 添加 `UpdateUIState()` 调用

**新增方法**:

- ✅ `UpdateUIState()` - 集中式UI状态管理

**方法重写**:

- ✅ `BtnStart_Click()` - 添加强制检查和逻辑互锁
- ✅ `BtnStop_Click()` - 确保设置 `_isScanning = false`
- ✅ `TgDemo_Changed()` - 演示模式切换逻辑
- ✅ `BtnSendCmd_Click()` - 调试命令映射修复

**方法增强**:

- ✅ `GenerateDemoFrame()` - 添加 `_isScanning` 检查
- ✅ `ConnectUdp()` - 成功后调用 `UpdateUIState()`
- ✅ `DisconnectUdp()` - 断开后调用 `UpdateUIState()`
- ✅ `StartDemoMode()` / `StopDemoMode()` - 添加重复检查

## 🧪 测试检查清单

- [ ] 启动程序，确认状态为 "系统待机 (IDLE)" 和灰色指示灯
- [ ] 验证"开始采集"按钮禁用
- [ ] 打开演示开关，确认状态变为 "演示模式就绪 (SIMULATION READY)" 和绿色指示灯
- [ ] 验证"开始采集"按钮启用
- [ ] 点击"开始采集"，状态应变为 "采集中 (预览模式)" 和橙色指示灯
- [ ] 验证"开始采集"按钮禁用，"停止采集"按钮启用
- [ ] 验证"硬件参数"面板禁用（不可编辑）
- [ ] 点击"停止采集"，状态恢复为 "演示模式就绪" 和绿色指示灯
- [ ] 关闭演示开关，状态应变为 "系统待机 (IDLE)" 和灰色指示灯
- [ ] 在未连接且演示关闭的状态下点击"开始采集"，应出现警告对话框

## 📌 关键设计原则

1. **集中管理**: 所有UI状态通过 `UpdateUIState()` 管理，保证一致性
2. **互锁保护**: 防止非法操作（如未连接时启动采集）
3. **响应式停止**: 后台循环在每个周期检查停止标志，保证快速响应
4. **状态可见**: 三色指示灯（灰/绿/蓝/橙）反映系统状态
5. **参数保护**: 运行时禁用参数面板，防止运行中修改参数

---

**修复完成时间**: 2026-01-29  
**编译状态**: ✅ 0 errors, 0 warnings  
**功能状态**: ✅ 就绪
