#!/usr/bin/env powershell
# UI 状态机修复 - 快速验证脚本

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     ArrayCamera UI 状态机修复 - 验证检查清单                 ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$checks = @(
    @{
        name  = "初始状态检查"
        items = @(
            "启动程序后，状态显示为 '系统待机 (IDLE)' (灰色)",
            "'开始采集'按钮应禁用",
            "'停止采集'按钮应禁用",
            "'硬件参数'面板应禁用",
            "演示模式开关应关闭"
        )
    },
    @{
        name  = "演示模式启用"
        items = @(
            "打开演示模式开关",
            "状态应变为 '演示模式就绪 (SIMULATION READY)' (绿色)",
            "'开始采集'按钮应启用",
            "'硬件参数'面板应启用",
            "演示渲染应自动开始"
        )
    },
    @{
        name  = "开始采集"
        items = @(
            "点击'开始采集'按钮",
            "状态应变为 '采集中 (预览模式)' (橙色)",
            "'开始采集'按钮应禁用",
            "'停止采集'按钮应启用",
            "'硬件参数'面板应禁用",
            "图像显示应更新"
        )
    },
    @{
        name  = "停止采集"
        items = @(
            "点击'停止采集'按钮",
            "状态应恢复为 '演示模式就绪 (SIMULATION READY)' (绿色)",
            "'开始采集'按钮应启用",
            "'停止采集'按钮应禁用",
            "'硬件参数'面板应启用"
        )
    },
    @{
        name  = "关闭演示模式"
        items = @(
            "关闭演示模式开关",
            "状态应变为 '系统待机 (IDLE)' (灰色)",
            "'开始采集'按钮应禁用",
            "'硬件参数'面板应禁用"
        )
    },
    @{
        name  = "错误处理检查"
        items = @(
            "在演示关闭且硬件未连接状态下，尝试点击'开始采集'",
            "应出现警告对话框：'请先完成以下之一...'",
            "采集不会启动"
        )
    },
    @{
        name  = "调试指令检查"
        items = @(
            "验证调试指令 CmdID 下拉框只有 Ping/Reset/Config",
            "不应出现 Start/Stop 选项",
            "选择 Ping，输入参数，点击'发送指令'",
            "日志应显示发送了 Ping 命令"
        )
    },
    @{
        name  = "参数保护检查"
        items = @(
            "开启演示模式，开始采集",
            "验证'漏极电压'、'TIA量程'、'建立时间'字段禁用（变灰）",
            "停止采集后，字段应恢复可编辑"
        )
    }
)

$checkNumber = 1
foreach ($check in $checks) {
    Write-Host "╔" -ForegroundColor Cyan -NoNewline
    Write-Host "═" * 58 -ForegroundColor Cyan -NoNewline
    Write-Host "╗" -ForegroundColor Cyan
    Write-Host "║ 检查 $checkNumber`: $($check.name)" -ForegroundColor Cyan
    Write-Host "╚" -ForegroundColor Cyan -NoNewline
    Write-Host "═" * 58 -ForegroundColor Cyan -NoNewline
    Write-Host "╝" -ForegroundColor Cyan
    
    $itemNumber = 1
    foreach ($item in $check.items) {
        Write-Host "  ☐ $itemNumber. $item" -ForegroundColor White
        $itemNumber++
    }
    Write-Host ""
    $checkNumber++
}

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                     快速验证指南                            ║" -ForegroundColor Green
Write-Host "╠════════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║ 1. 编译程序: dotnet build -c Release                       ║" -ForegroundColor Green
Write-Host "║ 2. 运行程序: .\bin\Release\net6.0-windows\ArrayCamera.exe  ║" -ForegroundColor Green
Write-Host "║ 3. 按上面的检查清单逐项验证                                ║" -ForegroundColor Green
Write-Host "║ 4. 如发现问题，查看日志窗口获取详细信息                    ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

Write-Host "关键指标:" -ForegroundColor Yellow
Write-Host "  • 初始化时 _isDemoMode 应为 false (不是 true)" -ForegroundColor Yellow
Write-Host "  • 所有按钮状态通过 UpdateUIState() 集中管理" -ForegroundColor Yellow
Write-Host "  • 停止采集应在 100ms 内响应" -ForegroundColor Yellow
Write-Host "  • 运行时参数面板应禁用（变灰）" -ForegroundColor Yellow
Write-Host ""

Write-Host "日志关键字查找:" -ForegroundColor Magenta
Write-Host "  • '[UI状态]' - 状态变化日志" -ForegroundColor Magenta
Write-Host "  • '[演示模式]' - 演示相关事件" -ForegroundColor Magenta
Write-Host "  • '[硬件模式]' - 硬件相关事件" -ForegroundColor Magenta
Write-Host "  • '[调试指令]' - 手动发送的指令" -ForegroundColor Magenta
