using System.Runtime.InteropServices;

namespace ArrayCamera.Driver
{
    /// <summary>
    /// UDP接收数据包头结构
    /// 一行512像素被拆分为4个UDP包（每包128像素）
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UdpPacketHeader
    {
        /// <summary>包头标识 0xAA55AA55</summary>
        public uint Header;
        
        /// <summary>帧号</summary>
        public uint FrameID;
        
        /// <summary>行号 (0-511)</summary>
        public ushort RowIndex;
        
        /// <summary>分段号 (0-3)</summary>
        public byte SegIndex;
        
        /// <summary>数据类型</summary>
        public byte DataType;

        public const uint VALID_HEADER = 0xAA55AA55;
        public const int PIXELS_PER_SEGMENT = 128;
        public const int SEGMENTS_PER_ROW = 4;
        public const int DATA_SIZE = PIXELS_PER_SEGMENT * sizeof(int); // 128 * 4 = 512 bytes
    }

    /// <summary>
    /// 控制指令包结构 (固定32字节)
    /// 发送到 FPGA 的 Port 8080
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandPacket
    {
        /// <summary>包头标识 0x55AAAA55</summary>
        public uint Header;
        
        /// <summary>命令ID: 1=Reset, 2=Config, 3=Start, 4=Stop</summary>
        public uint CmdID;
        
        /// <summary>工作模式: 0=Preview(快速), 1=Precision(高精)</summary>
        public uint Mode;
        
        /// <summary>参数1: 例如TIA量程</summary>
        public uint Param1;
        
        /// <summary>参数2: 例如Wait_Cycles</summary>
        public uint Param2;
        
        /// <summary>保留字段1</summary>
        public uint Reserved1;
        
        /// <summary>保留字段2</summary>
        public uint Reserved2;
        
        /// <summary>保留字段3</summary>
        public uint Reserved3;

        public const uint VALID_HEADER = 0x55AAAA55;
        public const int PACKET_SIZE = 32;

        public static CommandPacket CreateReset()
        {
            return new CommandPacket
            {
                Header = VALID_HEADER,
                CmdID = 1,
                Mode = 0,
                Param1 = 0,
                Param2 = 0,
                Reserved1 = 0,
                Reserved2 = 0,
                Reserved3 = 0
            };
        }

        public static CommandPacket CreateConfig(uint mode, uint tiaRange, uint waitCycles)
        {
            return new CommandPacket
            {
                Header = VALID_HEADER,
                CmdID = 2,
                Mode = mode,
                Param1 = tiaRange,
                Param2 = waitCycles,
                Reserved1 = 0,
                Reserved2 = 0,
                Reserved3 = 0
            };
        }

        public static CommandPacket CreateStart()
        {
            return new CommandPacket
            {
                Header = VALID_HEADER,
                CmdID = 3,
                Mode = 0,
                Param1 = 0,
                Param2 = 0,
                Reserved1 = 0,
                Reserved2 = 0,
                Reserved3 = 0
            };
        }

        public static CommandPacket CreateStop()
        {
            return new CommandPacket
            {
                Header = VALID_HEADER,
                CmdID = 4,
                Mode = 0,
                Param1 = 0,
                Param2 = 0,
                Reserved1 = 0,
                Reserved2 = 0,
                Reserved3 = 0
            };
        }
    }

    /// <summary>
    /// 工作模式枚举
    /// </summary>
    public enum AcquisitionMode
    {
        /// <summary>预览模式 - 低Wait_Cycles，快速刷新</summary>
        Preview = 0,
        
        /// <summary>高精模式 - 高Wait_Cycles，支持多帧平均</summary>
        Precision = 1
    }

    /// <summary>
    /// 命令ID枚举
    /// </summary>
    public enum CommandID
    {
        Reset = 1,
        Config = 2,
        Start = 3,
        Stop = 4
    }
}
