import socket
import struct
import time
import threading

# CONFIG
# 模拟FPGA板卡 (默认IP: 192.168.2.88)
# 监听8080端口接收C#命令，发送图像数据到PC的8081端口
TARGET_IP = "192.168.2.100"  # PC IP (修改为你的PC实际IP)
TARGET_PORT = 8081           # PC Data Port
LOCAL_PORT = 8080            # FPGA Command Port (监听C#的命令)
is_running = False

def rx_thread():
    global is_running
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    # Allow port reuse to avoid "Address already in use" error
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    try:
        s.bind(('0.0.0.0', LOCAL_PORT))
        print(f"RX Listener Started on Port {LOCAL_PORT}")
        
        while True:
            # Receive data AND sender address
            d, addr = s.recvfrom(1024)
            
            if len(d) == 28:
                # Unpack: Header, Cmd, Mode, P1, P2, Res1, Res2 (7 uint32 = 28 bytes)
                v = struct.unpack("<7I", d)
                
                # Check Header 0x55AAAA55
                if v[0] == 0x55AAAA55:
                    cmd_id = v[1]
                    
                    if cmd_id == 3: 
                        is_running = True
                        print(f"CMD: START (Mode {v[2]})")
                    elif cmd_id == 4: 
                        is_running = False
                        print("CMD: STOP")
                    elif cmd_id == 1: 
                        print("CMD: RESET/PING")
                    
                    # === CRITICAL: ECHO/ACK BACK TO SENDER ===
                    # Send the exact same packet back to the sender's IP:Port
                    s.sendto(d, addr)
                    # print(f"ACK Sent to {addr}") # Uncomment for debug
                    
    except Exception as e:
        print("RX Error: " + str(e))
    finally:
        s.close()

def main():
    s_tx = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    frame = 0
    
    # Start Listener
    threading.Thread(target=rx_thread, daemon=True).start()
    
    print(f"FPGA Sim Ready -> Target: {TARGET_IP}:{TARGET_PORT}")
    print("Waiting for C# 'Connect'...")

    while True:
        if not is_running:
            time.sleep(0.1)
            continue
            
        # Send one frame (512x512)
        for r in range(512):
            for seg in range(4):
                # Header: Magic, Frame, Row, Seg, Type
                hdr = struct.pack("<IIHBB", 0xAA55AA55, frame, r, seg, 0)
                
                # Data: 128 pixels (int32) - Moving Wave Pattern
                # Val range: 0 ~ 10000
                px = []
                for i in range(128):
                    val = int(abs((r + i*4 + frame)%512 - 256) * 40)
                    px.append(val)
                
                pay = struct.pack("<128i", *px)
                s_tx.sendto(hdr + pay, (TARGET_IP, TARGET_PORT))
        
        # Log status
        if frame % 30 == 0:
            print(f"Streaming Frame {frame}...", end='\r')
            
        frame += 1
        time.sleep(0.033) # 30 FPS

if __name__ == "__main__":
    main()
