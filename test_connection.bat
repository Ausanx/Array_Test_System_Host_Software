@echo off
echo ====================================
echo  FPGA 模拟器 + C# 连接测试脚本
echo ====================================
echo.

REM 检查Python模拟器是否运行
tasklist /FI "IMAGENAME eq python.exe" | find /I "python.exe" >nul
if %errorlevel%==0 (
    echo [INFO] Python进程已运行
) else (
    echo [启动] 正在启动FPGA模拟器...
    start "FPGA Simulator" cmd /k "python udp_device_sim.py"
    timeout /t 2 /nobreak >nul
)

echo.
echo [启动] 正在启动C#主程序...
echo.
start "" "ArrayCamera\bin\Release\net6.0-windows\ArrayCamera.exe"

echo.
echo ====================================
echo  测试步骤:
echo  1. 等待两个窗口打开
echo  2. 在C#窗口点击"连接/绑定"按钮
echo  3. 观察日志输出
echo ====================================
echo.
pause
