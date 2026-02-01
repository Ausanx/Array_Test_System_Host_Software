# FPGA UDP 通信 Verilog 参考实现

本文件提供FPGA侧UDP通信的Verilog参考代码框架。

## 前置条件

- FPGA开发板带以太网PHY和MAC控制器
- 已实现TCP/IP协议栈（如xilinx的LWIP或第三方库）
- 或直接使用UDP IP核（Xilinx DMA/Checksums）

## 整体框架

```
┌─────────────────────────┐
│   C# PC Application     │
│  192.168.2.100:8080/81  │
└────────────┬────────────┘
             │ UDP Frame
             ▼
┌─────────────────────────┐
│  Ethernet PHY (RGMII)   │
└────────────┬────────────┘
             │ MII
             ▼
┌──────────────────────────┐
│  Ethernet MAC (FIFO)     │
│  (Xilinx或第三方IP核)    │
└────────────┬─────────────┘
             │
      ┌──────┴──────┐
      ▼             ▼
┌──────────┐  ┌──────────┐
│UDP RX    │  │UDP TX    │
│Rx Cmd    │  │Tx Data   │
│Port 8080 │  │Port 8081 │
└─────┬────┘  └────┬─────┘
      │            │
      ▼            ▼
┌──────────────────────────┐
│  控制逻辑                 │
│  - Ping ACK              │
│  - 命令解析              │
│  - ADC数据采集           │
│  - 图像数据打包          │
└──────────────────────────┘
```

## 1. UDP 接收模块（RX - 命令处理）

### Verilog 伪代码

```verilog
module udp_rx_processor (
    input clk,
    input rst_n,
    
    // UDP接收接口（来自MAC）
    input [7:0] rx_data,
    input rx_valid,
    input rx_sof,      // Start of Frame
    input rx_eof,      // End of Frame
    
    // 命令输出
    output reg [31:0] cmd_id,
    output reg [31:0] mode,
    output reg [31:0] param1,
    output reg cmd_valid,
    
    // PC地址（用于回复）
    output reg [31:0] pc_ip_addr,
    output reg [15:0] pc_port,
    
    // 日志
    output reg [255:0] log_msg
);

reg [7:0] buffer [27:0];    // 28字节缓冲
reg [4:0] byte_count;

always @(posedge clk or negedge rst_n) begin
    if (~rst_n) begin
        byte_count <= 0;
        cmd_valid <= 0;
    end
    else if (rx_sof) begin
        byte_count <= 0;
    end
    else if (rx_valid) begin
        buffer[byte_count] <= rx_data;
        byte_count <= byte_count + 1;
        
        if (byte_count == 27) begin  // 收满28字节
            // 提取字段（Little Endian）
            if (buffer[3:0] == 32'h55AAAA55) begin  // Header检查
                cmd_id   <= {buffer[7], buffer[6], buffer[5], buffer[4]};
                mode     <= {buffer[11], buffer[10], buffer[9], buffer[8]};
                param1   <= {buffer[15], buffer[14], buffer[13], buffer[12]};
                cmd_valid <= 1;
                
                // 提取发送方IP地址（从以太网帧中获取）
                // 注意：这里需要从完整的以太网帧中解析
                // 假设已通过其他方式获取
            end
        end
    end
end

endmodule
```

## 2. UDP 发送模块（TX - ACK回复）

### Verilog 伪代码

```verilog
module udp_tx_ack (
    input clk,
    input rst_n,
    
    // 接收的命令包（28字节）
    input [7:0] rx_buffer [27:0],
    input rx_complete,
    
    // 目标地址
    input [31:0] pc_ip,
    input [15:0] pc_port,  // 8080
    
    // UDP TX接口（送到MAC）
    output reg [7:0] tx_data,
    output reg tx_valid,
    output reg tx_sof,
    output reg tx_eof,
    
    // 状态
    output reg ack_sent
);

reg [4:0] tx_byte_count;
reg tx_busy;

always @(posedge clk or negedge rst_n) begin
    if (~rst_n) begin
        tx_busy <= 0;
        tx_byte_count <= 0;
        ack_sent <= 0;
    end
    else if (rx_complete && ~tx_busy) begin
        // 开始发送ACK（回复相同的28字节）
        tx_busy <= 1;
        tx_byte_count <= 0;
        tx_sof <= 1;
    end
    else if (tx_busy) begin
        tx_data <= rx_buffer[tx_byte_count];
        tx_valid <= 1;
        
        if (tx_byte_count == 27) begin
            tx_eof <= 1;
            tx_busy <= 0;
            ack_sent <= 1;
        end
        tx_byte_count <= tx_byte_count + 1;
    end
end

endmodule
```

## 3. 命令执行逻辑

### Verilog 伪代码

```verilog
module cmd_executor (
    input clk,
    input rst_n,
    
    // 命令输入
    input [31:0] cmd_id,
    input [31:0] mode,
    input [31:0] param1,
    input cmd_valid,
    
    // 控制信号输出
    output reg start_scan,      // 启动扫描
    output reg stop_scan,       // 停止扫描
    output reg [2:0] tia_gain,  // TIA增益
    output reg reset_sys,       // 系统复位
    
    // 状态输出
    output reg [7:0] status
);

always @(posedge clk or negedge rst_n) begin
    if (~rst_n) begin
        start_scan <= 0;
        stop_scan <= 0;
        reset_sys <= 0;
        status <= 8'h00;
    end
    else if (cmd_valid) begin
        case (cmd_id)
            32'h0: begin  // PING
                status <= 8'h01;
                // ACK已由TX模块处理
            end
            
            32'h1: begin  // RESET
                reset_sys <= 1;
                status <= 8'h02;
            end
            
            32'h2: begin  // CONFIG
                tia_gain <= param1[2:0];
                status <= 8'h03;
            end
            
            32'h3: begin  // START
                start_scan <= 1;
                status <= 8'h04;
            end
            
            32'h4: begin  // STOP
                stop_scan <= 1;
                status <= 8'h05;
            end
            
            default: status <= 8'hFF;
        endcase
    end
end

endmodule
```

## 4. 图像数据打包模块

### Verilog 伪代码

```verilog
module image_packer (
    input clk,
    input rst_n,
    
    // ADC数据输入（来自阵列采集模块）
    input [31:0] adc_data,      // ADC值（0-10000）
    input adc_valid,
    input [8:0] row_addr,       // 行地址 0-511
    input [1:0] seg_addr,       // 段地址 0-3
    
    // 扫描控制
    input scan_enable,
    
    // UDP TX接口
    output reg [7:0] tx_data,
    output reg tx_valid,
    output reg tx_sof,
    output reg tx_eof,
    
    // 统计
    output reg [31:0] frame_count,
    output reg [31:0] packet_count
);

reg [31:0] pixel_buffer [127:0];  // 128个像素缓冲
reg [7:0] pixel_count;
reg [31:0] current_frame;
reg [8:0] current_row;
reg [1:0] current_seg;
reg [9:0] tx_byte_count;

always @(posedge clk or negedge rst_n) begin
    if (~rst_n) begin
        pixel_count <= 0;
        current_frame <= 0;
        current_row <= 0;
        current_seg <= 0;
        tx_byte_count <= 0;
        packet_count <= 0;
    end
    else if (scan_enable && adc_valid) begin
        // 收集128个像素（每个段）
        pixel_buffer[pixel_count] <= adc_data;
        pixel_count <= pixel_count + 1;
        
        if (pixel_count == 127) begin
            // 一个完整的524字节包已准备好，开始发送
            tx_sof <= 1;
            tx_valid <= 1;
            tx_byte_count <= 0;
        end
    end
    
    // 发送包
    if (tx_valid && tx_byte_count < 524) begin
        case (tx_byte_count)
            // Header (12字节)
            0: tx_data <= 32'hAA55AA55[7:0];    // Magic低字节
            1: tx_data <= 32'hAA55AA55[15:8];
            2: tx_data <= 32'hAA55AA55[23:16];
            3: tx_data <= 32'hAA55AA55[31:24];  // Magic高字节
            
            4: tx_data <= current_frame[7:0];   // FrameNum
            5: tx_data <= current_frame[15:8];
            6: tx_data <= current_frame[23:16];
            7: tx_data <= current_frame[31:24];
            
            8: tx_data <= current_row[7:0];     // RowNum
            9: tx_data <= current_row[8];
            
            10: tx_data <= current_seg;         // SegNum
            11: tx_data <= 8'h00;               // Type
            
            // 像素数据 (512字节)
            default: begin
                if (tx_byte_count < 12 + 512) begin
                    // 发送像素数据
                    // pixel_buffer[0]的第一个字节 -> pixel_buffer[127]的最后一个字节
                    tx_data <= pixel_buffer[(tx_byte_count-12)/4][(tx_byte_count%4)*8+:8];
                end
            end
        endcase
        
        tx_byte_count <= tx_byte_count + 1;
        
        if (tx_byte_count == 523) begin
            tx_eof <= 1;
            tx_valid <= 0;
            packet_count <= packet_count + 1;
        end
    end
end

endmodule
```

## 5. 系统顶级模块

```verilog
module fpga_udp_system (
    input clk,
    input rst_n,
    
    // Ethernet RGMII接口
    input eth_clk,
    inout [3:0] eth_data_tx,
    inout [3:0] eth_data_rx,
    output eth_tx_en,
    input eth_rx_dv,
    
    // ADC数据
    input [31:0] adc_data,
    input adc_valid,
    input [8:0] row,
    input [1:0] seg,
    
    // 控制信号
    output scan_start,
    output scan_stop,
    output [2:0] tia_gain,
    output sys_reset,
    
    // 调试/监测
    output [7:0] led_status,
    output [31:0] frame_counter
);

// 内部信号
wire [7:0] rx_data;
wire rx_valid;
wire [31:0] cmd_id, mode, param1;
wire cmd_valid;
wire [31:0] pc_ip;
wire [15:0] pc_port;

// 例化子模块
udp_rx_processor rx_inst (
    .clk(clk),
    .rst_n(rst_n),
    .rx_data(rx_data),
    .rx_valid(rx_valid),
    .cmd_id(cmd_id),
    .mode(mode),
    .param1(param1),
    .cmd_valid(cmd_valid),
    .pc_ip_addr(pc_ip),
    .pc_port(pc_port)
);

cmd_executor cmd_inst (
    .clk(clk),
    .rst_n(rst_n),
    .cmd_id(cmd_id),
    .mode(mode),
    .param1(param1),
    .cmd_valid(cmd_valid),
    .start_scan(scan_start),
    .stop_scan(scan_stop),
    .tia_gain(tia_gain),
    .reset_sys(sys_reset)
);

image_packer packer_inst (
    .clk(clk),
    .rst_n(rst_n),
    .adc_data(adc_data),
    .adc_valid(adc_valid),
    .row_addr(row),
    .seg_addr(seg),
    .scan_enable(scan_start),
    .frame_count(frame_counter)
);

endmodule
```

## 6. 实现步骤

### 第一步：验证网络连接

```bash
# PC端测试
ping 192.168.2.88
# 应该能收到ICMP响应
```

### 第二步：验证UDP接收

使用现成的UDP IP核（如Xilinx的AXI Ethernet）：
- 配置IP: 192.168.2.88/24
- 配置UDP端口：8080 (RX), 8081 (TX)
- 启用FIFO缓冲

### 第三步：实现Echo ACK

当接收到28字节的有效命令包时，立即将相同数据发送回发送方:8080

### 第四步：实现命令处理

根据CmdID执行相应的扫描逻辑

### 第五步：实现图像打包

将ADC数据组织成524字节的图像包并发送到PC:8081

## 7. 调试技巧

### Wireshark抓包验证

```bash
# 过滤规则
udp.port == 8080 or udp.port == 8081

# 导出数据
File > Export Specific Packets > 选择范围 > 保存为PCAP
```

### FPGA内部调试

```verilog
// 添加Integrated Logic Analyzer (ILA) IP核
// 监测信号：
// - rx_data, rx_valid
// - tx_data, tx_valid
// - cmd_valid, cmd_id
// - frame_count, packet_count
```

### LED指示状态

```verilog
// LED0: 网络连接状态
// LED1: 命令接收
// LED2: 扫描运行
// LED3: 数据发送
// LED4-7: 帧计数器低4位
assign led_status = {frame_counter[3:0], scan_running, cmd_received, net_connected, 1'b0};
```

## 8. 性能目标

| 指标 | 目标值 |
|------|-------|
| 握手延迟 | < 100ms |
| ACK响应时间 | < 1ms |
| 帧率 | 30 FPS |
| 分辨率 | 512×512 |
| 单帧数据量 | ~1MB |
| 网络吞吐量 | ~30MB/s |
| 时钟频率 | 100MHz+ |

---

**下一步**：根据你的FPGA开发平台（Xilinx/Altera/其他）选择合适的UDP IP核或第三方库进行实现。

