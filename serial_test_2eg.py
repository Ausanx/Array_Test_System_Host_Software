import socket

# 绑定所有网卡，端口 8080
UDP_IP = "0.0.0.0"
UDP_PORT = 8080

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((UDP_IP, UDP_PORT))

print(f"✅ 正在监听端口 {UDP_PORT} (防火墙必须关闭!)...")

while True:
    data, addr = sock.recvfrom(1024)
    print(f"🎉 收到来自 {addr[0]} 的数据: {data.decode('utf-8', errors='ignore')}")