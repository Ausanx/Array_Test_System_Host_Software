#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
UDP 网络诊断脚本
用途：验证与FPGA板卡 (192.168.2.88) 的网络连通性
作者：交叉阵列测试系统开发组
日期：2026-01-29
"""

import socket
import time
import sys
import struct
from datetime import datetime

# ==================== 配置参数 ====================
LOCAL_IP = "192.168.2.100"      # 本机IP（必须与板卡同网段）
LOCAL_PORT_CMD = 8080           # 本地监听端口（接收指令反馈）
LOCAL_PORT_DATA = 8081          # 本地监听端口（接收图像数据）

TARGET_IP = "192.168.2.88"      # 板卡IP
TARGET_PORT = 8080              # 板卡端口

PING_INTERVAL = 1.0             # Ping发送间隔（秒）
SEND_BINARY_PING = True         # 发送二进制Ping包（模拟C#协议）

# ==================== 颜色打印 ====================
class Colors:
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'

def log_info(msg):
    print(f"{Colors.OKBLUE}[INFO]{Colors.ENDC} {msg}")

def log_success(msg):
    print(f"{Colors.OKGREEN}[SUCCESS]{Colors.ENDC} {msg}")

def log_warning(msg):
    print(f"{Colors.WARNING}[WARNING]{Colors.ENDC} {msg}")

def log_error(msg):
    print(f"{Colors.FAIL}[ERROR]{Colors.ENDC} {msg}")

# ==================== 协议包生成 ====================
def create_command_packet(cmd_id=0):
    """
    生成C#协议格式的指令包
    CommandPacket结构 (28字节):
      uint Header (4)      = 0x55AAAA55
      uint CmdID (4)       = cmd_id
      uint Mode (4)        = 0
      uint Param1 (4)      = 0
      uint Param2 (4)      = 0
      uint Reserved1 (4)   = 0
      uint Reserved2 (4)   = 0
    """
    packet = struct.pack(
        '<7I',  # 7个无符号整数，小端序
        0x55AAAA55,  # Header
        cmd_id,      # CmdID (0=Ping)
        0,           # Mode
        0,           # Param1
        0,           # Param2
        0,           # Reserved1
        0            # Reserved2
    )
    return packet

# ==================== 主程序 ====================
def main():
    print(f"{Colors.HEADER}{'='*60}")
    print(f"   UDP 网络诊断工具 - FPGA 板卡连通性测试")
    print(f"{'='*60}{Colors.ENDC}\n")

    log_info(f"本机配置: {LOCAL_IP}:{LOCAL_PORT_CMD} (指令) & {LOCAL_PORT_DATA} (数据)")
    log_info(f"目标板卡: {TARGET_IP}:{TARGET_PORT}")
    log_info(f"发送模式: {'二进制协议包 (28字节)' if SEND_BINARY_PING else 'ASCII文本'}\n")

    # 测试ICMP连通性
    log_info("步骤1: 测试ICMP连通性...")
    import subprocess
    try:
        result = subprocess.run(
            ['ping', '-n', '2', TARGET_IP],
            capture_output=True,
            text=True,
            timeout=5
        )
        if 'TTL=' in result.stdout:
            log_success(f"✓ ICMP Ping 成功！板卡物理连接正常")
        else:
            log_error(f"✖ ICMP Ping 失败！板卡可能未上电")
            log_warning("请检查: 1.网线连接 2.板卡电源 3.板卡IP配置")
            return
    except Exception as e:
        log_warning(f"无法执行Ping测试: {e}")

    print()  # 空行

    # 创建发送Socket
    log_info("步骤2: 创建发送Socket...")
    try:
        sock_tx = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock_tx.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        log_success("✓ 发送Socket已创建")
    except Exception as e:
        log_error(f"创建发送Socket失败: {e}")
        return

    # 创建接收Socket（绑定8080端口）
    log_info("步骤3: 绑定接收Socket...")
    try:
        sock_rx_cmd = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock_rx_cmd.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        sock_rx_cmd.bind((LOCAL_IP, LOCAL_PORT_CMD))
        sock_rx_cmd.settimeout(0.5)  # 500ms超时
        
        # 验证绑定
        actual_addr = sock_rx_cmd.getsockname()
        log_success(f"✓ 监听Socket已绑定: {actual_addr[0]}:{actual_addr[1]}")
    except OSError as e:
        log_error(f"绑定端口 {LOCAL_PORT_CMD} 失败: {e}")
        log_warning("可能原因:")
        log_warning("  1. 本机IP不是 192.168.2.100（请用 ipconfig 检查）")
        log_warning("  2. 端口已被占用（请关闭C#程序或其他工具）")
        log_warning("  3. 防火墙阻止了端口绑定")
        return

    log_info("\n━━━━━━━━━━━━━━━ 开始测试 ━━━━━━━━━━━━━━━")
    log_info("按 Ctrl+C 停止测试\n")

    tx_count = 0
    rx_count = 0
    last_ping_time = 0

    try:
        while True:
            current_time = time.time()

            # 每秒发送一次Ping
            if current_time - last_ping_time >= PING_INTERVAL:
                timestamp = datetime.now().strftime("%H:%M:%S.%f")[:-3]
                
                if SEND_BINARY_PING:
                    # 发送二进制协议包（模拟C#程序）
                    message = create_command_packet(cmd_id=0)
                    msg_desc = f"Binary Ping (28 bytes, Header=0x55AAAA55)"
                else:
                    # 发送ASCII文本
                    message = f"Ping from Python [{timestamp}]".encode('ascii')
                    msg_desc = f"Text: Ping from Python [{timestamp}]"
                
                try:
                    sock_tx.sendto(message, (TARGET_IP, TARGET_PORT))
                    tx_count += 1
                    print(f"{Colors.OKCYAN}[TX #{tx_count:03d}]{Colors.ENDC} → {TARGET_IP}:{TARGET_PORT} | {msg_desc}")
                except Exception as e:
                    log_error(f"发送失败: {e}")
                
                last_ping_time = current_time

            # 非阻塞接收数据
            try:
                data, addr = sock_rx_cmd.recvfrom(4096)
                rx_count += 1
                timestamp = datetime.now().strftime("%H:%M:%S.%f")[:-3]
                
                # 尝试解析为文本
                try:
                    text = data.decode('ascii').strip()
                    print(f"{Colors.OKGREEN}[RX #{rx_count:03d}]{Colors.ENDC} ← {addr} | {text} ({len(data)} bytes)")
                except:
                    # 二进制数据
                    hex_preview = ' '.join(f'{b:02X}' for b in data[:16])
                    if len(data) > 16:
                        hex_preview += "..."
                    print(f"{Colors.OKGREEN}[RX #{rx_count:03d}]{Colors.ENDC} ← {addr} | Binary: {hex_preview} ({len(data)} bytes)")
                
            except socket.timeout:
                pass  # 超时是正常的
            except Exception as e:
                log_error(f"接收失败: {e}")

            time.sleep(0.01)  # 10ms循环间隔

    except KeyboardInterrupt:
        print(f"\n\n{Colors.HEADER}━━━━━━━━━━━━━━━ 测试终止 ━━━━━━━━━━━━━━━{Colors.ENDC}")
        log_info(f"发送统计: {tx_count} 个Ping")
        log_info(f"接收统计: {rx_count} 个响应")
        
        if rx_count > 0:
            log_success("\n✓ 网络连通正常！板卡有UDP响应。")
            log_success("  → 如果C#程序仍然超时，说明是代码逻辑问题，而非网络问题。")
        else:
            log_warning("\n✖ 未收到任何UDP响应。")
            log_warning("  诊断结果:")
            log_warning("  ✓ ICMP Ping 正常 → 物理连接OK")
            log_warning("  ✖ UDP 无响应 → 板卡UDP程序可能未运行")
            log_warning("")
            log_warning("  可能原因:")
            log_warning("  1. 板卡FPGA程序未加载或未启动")
            log_warning("  2. 板卡UDP端口不是8080（查阅硬件文档）")
            log_warning("  3. 板卡只响应特定格式的协议包")
            log_warning("  4. 板卡处于错误状态（需要复位）")
            log_warning("")
            log_info("  建议操作:")
            log_info("  1. 检查板卡显示屏/LED指示灯状态")
            log_info("  2. 尝试给板卡发送复位指令")
            log_info("  3. 重新上电板卡")
            log_info("  4. 查阅板卡技术文档确认通信协议")

    finally:
        sock_tx.close()
        sock_rx_cmd.close()
        log_info("Socket已关闭，程序退出。")

if __name__ == "__main__":
    main()
