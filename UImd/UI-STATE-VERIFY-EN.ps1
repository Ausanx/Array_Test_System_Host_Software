#!/usr/bin/env powershell
# UI State Machine Fix - Quick Verification Script

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     ArrayCamera UI State Machine - Verification Checklist   ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$checks = @(
    @{
        name  = "Initial State Check"
        items = @(
            "Status shows 'System Idle (IDLE)' in gray color",
            "'Start Acquisition' button is DISABLED",
            "'Stop Acquisition' button is DISABLED",
            "'Hardware Parameters' panel is DISABLED",
            "Demo mode toggle is OFF"
        )
    },
    @{
        name  = "Enable Demo Mode"
        items = @(
            "Toggle demo mode ON",
            "Status changes to 'Simulation Ready (SIMULATION READY)' in green",
            "'Start Acquisition' button becomes ENABLED",
            "'Hardware Parameters' panel becomes ENABLED",
            "Demo rendering starts automatically"
        )
    },
    @{
        name  = "Start Acquisition"
        items = @(
            "Click 'Start Acquisition' button",
            "Status changes to 'Acquiring (Preview Mode)' in orange",
            "'Start Acquisition' button becomes DISABLED",
            "'Stop Acquisition' button becomes ENABLED",
            "'Hardware Parameters' panel becomes DISABLED",
            "Image display updates continuously"
        )
    },
    @{
        name  = "Stop Acquisition"
        items = @(
            "Click 'Stop Acquisition' button",
            "Status reverts to 'Simulation Ready (SIMULATION READY)' in green",
            "'Start Acquisition' button becomes ENABLED",
            "'Stop Acquisition' button becomes DISABLED",
            "'Hardware Parameters' panel becomes ENABLED"
        )
    },
    @{
        name  = "Disable Demo Mode"
        items = @(
            "Toggle demo mode OFF",
            "Status changes to 'System Idle (IDLE)' in gray",
            "'Start Acquisition' button becomes DISABLED",
            "'Hardware Parameters' panel becomes DISABLED"
        )
    },
    @{
        name  = "Error Handling"
        items = @(
            "With demo OFF and hardware NOT connected, click 'Start Acquisition'",
            "Warning dialog appears: 'Please complete one of the following...'",
            "Acquisition does NOT start"
        )
    },
    @{
        name  = "Debug Commands Check"
        items = @(
            "Verify Debug Command dropdown only has: Ping/Reset/Config",
            "NO Start/Stop options should appear",
            "Select Ping, enter parameters, click 'Send Command'",
            "Log displays 'Ping command sent'"
        )
    },
    @{
        name  = "Parameter Protection"
        items = @(
            "Enable demo mode and start acquisition",
            "Verify 'Drain Voltage', 'TIA Range', 'Setup Time' fields are DISABLED (grayed out)",
            "Stop acquisition - fields should become ENABLED again"
        )
    }
)

$checkNumber = 1
foreach ($check in $checks) {
    Write-Host "╔" -ForegroundColor Cyan -NoNewline
    Write-Host "═" * 58 -ForegroundColor Cyan -NoNewline
    Write-Host "╗" -ForegroundColor Cyan
    Write-Host "║ CHECK $checkNumber`: $($check.name)" -ForegroundColor Cyan
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
Write-Host "║                     Quick Start Guide                       ║" -ForegroundColor Green
Write-Host "╠════════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║ 1. Build: dotnet build -c Release                          ║" -ForegroundColor Green
Write-Host "║ 2. Run: .\bin\Release\net6.0-windows\ArrayCamera.exe       ║" -ForegroundColor Green
Write-Host "║ 3. Verify each check above                                 ║" -ForegroundColor Green
Write-Host "║ 4. Review logs for detailed diagnostics                    ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

Write-Host "Key Metrics:" -ForegroundColor Yellow
Write-Host "  * Initial _isDemoMode must be FALSE (not TRUE)" -ForegroundColor Yellow
Write-Host "  * All button states managed by UpdateUIState()" -ForegroundColor Yellow
Write-Host "  * Stop response time < 100ms" -ForegroundColor Yellow
Write-Host "  * Runtime parameter panel disabled during acquisition" -ForegroundColor Yellow
Write-Host ""

Write-Host "Log Search Keywords:" -ForegroundColor Magenta
Write-Host "  * '[UI State]' - State change logs" -ForegroundColor Magenta
Write-Host "  * '[Demo Mode]' - Demo related events" -ForegroundColor Magenta
Write-Host "  * '[Hardware]' - Hardware related events" -ForegroundColor Magenta
Write-Host "  * '[Debug Command]' - Manual command sends" -ForegroundColor Magenta
