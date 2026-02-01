@echo off
title FPGA Board Simulator (192.168.2.88:8080)
echo ====================================
echo  FPGA 板卡模拟器
echo ====================================
echo.
echo [监听端口] 8080 (接收C#指令)
echo [发送目标] 192.168.2.100:8081 (图像数据)
echo [协议格式] 28字节CommandPacket
echo.
echo 等待C#程序连接...
echo ====================================
echo.

python udp_device_sim.py

pause
