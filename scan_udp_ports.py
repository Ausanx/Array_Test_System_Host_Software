#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
端口扫描脚本 - 扫描板卡开放的UDP端口
"""

import socket
import struct
import time

TARGET_IP = "192.168.2.88"
PORTS_TO_SCAN = [8000, 8080, 8081, 9000, 10000, 5000, 5001, 6000, 7000]
TIMEOUT = 1.0

def create_ping_packet():
    """创建C#协议格式的Ping包"""
    return struct.pack('<7I', 0x55AAAA55, 0, 0, 0, 0, 0, 0)

def scan_port(ip, port):
    """扫描单个UDP端口"""
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.settimeout(TIMEOUT)
        
        # 发送Ping包
        data = create_ping_packet()
        sock.sendto(data, (ip, port))
        
        # 尝试接收响应
        try:
            response, addr = sock.recvfrom(4096)
            sock.close()
            return True, len(response)
        except socket.timeout:
            sock.close()
            return False, 0
    except Exception as e:
        return False, 0

print("=" * 60)
print(f"UDP端口扫描 - 目标: {TARGET_IP}")
print("=" * 60)
print()

open_ports = []

for port in PORTS_TO_SCAN:
    print(f"[扫描] 端口 {port}...", end=" ", flush=True)
    has_response, response_len = scan_port(TARGET_IP, port)
    
    if has_response:
        print(f"✓ 有响应 ({response_len} 字节)")
        open_ports.append(port)
    else:
        print("✖ 无响应")
    
    time.sleep(0.2)

print()
print("=" * 60)
print("扫描结果:")
print("=" * 60)

if open_ports:
    print(f"✓ 发现 {len(open_ports)} 个开放端口:")
    for port in open_ports:
        print(f"  - {port}")
    print()
    print("建议：修改C#代码中的TARGET_PORT为上述端口号")
else:
    print("✖ 未发现任何开放的UDP端口")
    print()
    print("可能原因:")
    print("  1. 板卡UDP服务未运行")
    print("  2. 板卡使用非标准端口（需查阅文档）")
    print("  3. 板卡需要特殊握手协议")
