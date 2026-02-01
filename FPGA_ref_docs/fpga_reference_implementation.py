#!/usr/bin/env python3
"""
FPGA UDP 通信协议 - 完整Python参考实现
用于测试或作为FPGA侧实现的参考

本脚本演示FPGA应如何：
1. 监听PC的指令
2. 立即回复ACK
3. 发送图像数据
"""

import socket
import struct
import threading
import time
import sys
from datetime import datetime

# ============================================================================
# 配置
# ============================================================================

FPGA_IP = "0.0.0.0"              # 监听所有网卡
FPGA_RX_PORT = 8080              # 接收指令端口
FPGA_TX_PORT = 8081              # 发送数据端口

# 协议常量
CMD_HEADER = 0x55AAAA55
IMG_HEADER = 0xAA55AA55

# 命令定义
CMD_PING = 0
CMD_RESET = 1
CMD_CONFIG = 2
CMD_START = 3
CMD_STOP = 4

# ============================================================================
# 数据结构
# ============================================================================

class CommandPacket:
    """指令包 - 28字节"""
    def __init__(self, data=None):
        if data and len(data) >= 28:
            values = struct.unpack('<7I', data[:28])
            self.header = values[0]
            self.cmd_id = values[1]
            self.mode = values[2]
            self.param1 = values[3]
            self.param2 = values[4]
            self.res1 = values[5]
            self.res2 = values[6]
        else:
            self.header = CMD_HEADER
            self.cmd_id = 0
            self.mode = 0
            self.param1 = 0
            self.param2 = 0
            self.res1 = 0
            self.res2 = 0
    
    def to_bytes(self):
        """转换为字节"""
        return struct.pack('<7I', 
            self.header, self.cmd_id, self.mode, 
            self.param1, self.param2, self.res1, self.res2)
    
    def is_valid(self):
        """验证包有效性"""
        return self.header == CMD_HEADER and len(self.to_bytes()) == 28
    
    def __str__(self):
        cmd_names = {0: "PING", 1: "RESET", 2: "CONFIG", 3: "START", 4: "STOP"}
        return f"Cmd({cmd_names.get(self.cmd_id, 'UNKNOWN')}), Mode={self.mode}, P1={self.param1}"

class ImagePacket:
    """图像数据包 - 524字节"""
    def __init__(self, frame_num, row, seg, pixels):
        self.magic = IMG_HEADER
        self.frame_num = frame_num
        self.row = row
        self.seg = seg
        self.type = 0
        self.pixels = pixels  # 128个uint32
    
    def to_bytes(self):
        """转换为字节"""
        header = struct.pack('<IIHBB', 
            self.magic, self.frame_num, self.row, self.seg, self.type)
        payload = struct.pack('<128I', *self.pixels)
        return header + payload
    
    def get_size(self):
        """返回包大小"""
        return 12 + 512  # 524字节

# ============================================================================
# FPGA模拟器核心逻辑
# ============================================================================

class FPGASimulator:
    def __init__(self):
        self.rx_socket = None
        self.tx_socket = None
        self.pc_ip = None
        self.is_running = False
        self.is_scanning = False
        self.frame_counter = 0
        self.packet_counter = 0
        self.cmd_counter = 0
        
        # 统计信息
        self.stats = {
            'ping_received': 0,
            'reset_received': 0,
            'config_received': 0,
            'start_received': 0,
            'stop_received': 0,
            'packets_sent': 0,
            'bytes_sent': 0,
            'errors': 0
        }
    
    def log(self, level, msg):
        """日志输出"""
        timestamp = datetime.now().strftime("%H:%M:%S.%f")[:-3]
        level_str = {
            'INFO': '[INFO]',
            'SUCCESS': '[✓]',
            'WARNING': '[!]',
            'ERROR': '[✗]'
        }.get(level, '[?]')
        print(f"{timestamp} {level_str} {msg}")
    
    def init_sockets(self):
        """初始化Socket"""
        try:
            # RX Socket - 接收指令
            self.rx_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            self.rx_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.rx_socket.bind((FPGA_IP, FPGA_RX_PORT))
            self.rx_socket.settimeout(None)  # 阻塞模式
            
            # TX Socket - 发送数据
            self.tx_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            
            self.log('SUCCESS', f"Socket初始化成功")
            self.log('INFO', f"监听 {FPGA_IP}:{FPGA_RX_PORT}")
            
        except Exception as e:
            self.log('ERROR', f"Socket初始化失败: {e}")
            sys.exit(1)
    
    def process_command(self, data, addr):
        """处理接收到的指令"""
        try:
            if len(data) != 28:
                self.log('WARNING', f"无效包长度: {len(data)}字节 (期望28)")
                self.stats['errors'] += 1
                return
            
            # 解析指令包
            cmd = CommandPacket(data)
            
            if not cmd.is_valid():
                self.log('WARNING', f"无效的Header: 0x{cmd.header:08X}")
                self.stats['errors'] += 1
                return
            
            # 记录发送方IP
            self.pc_ip = addr[0]
            self.cmd_counter += 1
            
            # ⭐ 关键：立即回复ACK（Echo）
            try:
                self.rx_socket.sendto(data, addr)
            except Exception as e:
                self.log('ERROR', f"ACK发送失败: {e}")
                self.stats['errors'] += 1
                return
            
            # 处理具体命令
            if cmd.cmd_id == CMD_PING:
                self.stats['ping_received'] += 1
                self.log('SUCCESS', f"收到PING → ACK已回复到 {self.pc_ip}:8080")
            
            elif cmd.cmd_id == CMD_RESET:
                self.stats['reset_received'] += 1
                self.is_scanning = False
                self.frame_counter = 0
                self.packet_counter = 0
                self.log('SUCCESS', f"系统复位 (CmdID={cmd.cmd_id})")
            
            elif cmd.cmd_id == CMD_CONFIG:
                self.stats['config_received'] += 1
                self.log('INFO', f"配置参数 (Mode={cmd.mode}, P1={cmd.param1})")
            
            elif cmd.cmd_id == CMD_START:
                self.stats['start_received'] += 1
                self.is_scanning = True
                self.log('SUCCESS', f"启动扫描 (Mode={cmd.mode})")
            
            elif cmd.cmd_id == CMD_STOP:
                self.stats['stop_received'] += 1
                self.is_scanning = False
                self.log('SUCCESS', f"停止扫描")
            
            else:
                self.log('WARNING', f"未知命令: {cmd.cmd_id}")
                self.stats['errors'] += 1
        
        except Exception as e:
            self.log('ERROR', f"命令处理异常: {e}")
            self.stats['errors'] += 1
    
    def generate_test_image(self, row, seg):
        """生成测试图像数据"""
        pixels = []
        for i in range(128):
            # 生成移动的波形图案
            x = row * 128 + seg * 128 + i
            y = (self.frame_counter * 10 + x) % 1024
            val = int(abs(y - 512) * 9.765625)  # 0-10000
            val = min(val, 10000)
            pixels.append(val)
        return pixels
    
    def transmit_image(self):
        """发送一帧图像数据"""
        if not self.is_scanning or self.pc_ip is None:
            return
        
        # 一帧数据: 512行 × 4段 = 2048个包
        for row in range(512):
            for seg in range(4):
                try:
                    pixels = self.generate_test_image(row, seg)
                    img = ImagePacket(self.frame_counter, row, seg, pixels)
                    data = img.to_bytes()
                    
                    # 发送到PC的8081端口
                    self.tx_socket.sendto(data, (self.pc_ip, FPGA_TX_PORT))
                    
                    self.packet_counter += 1
                    self.stats['packets_sent'] += 1
                    self.stats['bytes_sent'] += len(data)
                    
                except Exception as e:
                    self.log('ERROR', f"发送包失败: {e}")
                    self.stats['errors'] += 1
        
        self.frame_counter += 1
        self.log('INFO', f"发送第 {self.frame_counter} 帧 (包数: {self.packet_counter})", end='\r')
    
    def rx_thread_func(self):
        """接收线程"""
        self.log('INFO', "RX线程启动")
        
        while self.is_running:
            try:
                data, addr = self.rx_socket.recvfrom(1024)
                self.process_command(data, addr)
            except socket.timeout:
                pass
            except Exception as e:
                self.log('ERROR', f"RX异常: {e}")
        
        self.log('INFO', "RX线程退出")
    
    def tx_thread_func(self):
        """发送线程"""
        self.log('INFO', "TX线程启动")
        
        frame_count = 0
        while self.is_running:
            if self.is_scanning and self.pc_ip:
                start_time = time.time()
                self.transmit_image()
                frame_count += 1
                
                # 控制帧率: 30FPS = 33.3ms/帧
                elapsed = time.time() - start_time
                sleep_time = max(0.033 - elapsed, 0)
                time.sleep(sleep_time)
            else:
                time.sleep(0.1)
        
        self.log('INFO', "TX线程退出")
    
    def print_stats(self):
        """打印统计信息"""
        print("\n" + "="*60)
        print("FPGA 统计信息")
        print("="*60)
        print(f"命令接收:")
        print(f"  PING:   {self.stats['ping_received']}")
        print(f"  RESET:  {self.stats['reset_received']}")
        print(f"  CONFIG: {self.stats['config_received']}")
        print(f"  START:  {self.stats['start_received']}")
        print(f"  STOP:   {self.stats['stop_received']}")
        print(f"\n数据发送:")
        print(f"  包数:   {self.stats['packets_sent']}")
        print(f"  字节:   {self.stats['bytes_sent']:,} ({self.stats['bytes_sent']/1024/1024:.1f} MB)")
        print(f"  帧数:   {self.frame_counter}")
        print(f"  错误:   {self.stats['errors']}")
        print("="*60 + "\n")
    
    def run(self):
        """主运行循环"""
        self.init_sockets()
        self.is_running = True
        
        print("\n" + "="*60)
        print("FPGA UDP 服务器模拟器")
        print("="*60)
        print(f"配置:")
        print(f"  监听地址: {FPGA_IP}:{FPGA_RX_PORT}")
        print(f"  发送端口: {FPGA_TX_PORT}")
        print(f"\n等待PC连接...")
        print("="*60 + "\n")
        
        # 启动接收线程
        rx_thread = threading.Thread(target=self.rx_thread_func, daemon=True)
        rx_thread.start()
        
        # 启动发送线程
        tx_thread = threading.Thread(target=self.tx_thread_func, daemon=True)
        tx_thread.start()
        
        # 主线程监控
        try:
            while True:
                time.sleep(1)
        except KeyboardInterrupt:
            print("\n\n收到中断信号...")
            self.is_running = False
            time.sleep(1)
            self.print_stats()
            
            # 关闭Socket
            if self.rx_socket:
                self.rx_socket.close()
            if self.tx_socket:
                self.tx_socket.close()
            
            print("已退出\n")
            sys.exit(0)

# ============================================================================
# 主程序
# ============================================================================

if __name__ == "__main__":
    simulator = FPGASimulator()
    simulator.run()
