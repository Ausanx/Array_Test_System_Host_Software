using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ArrayCamera.Driver
{
    /// <summary>
    /// 帧重组器 - 负责将UDP分包数据重组为完整帧
    /// 一帧 = 512行 × 512像素
    /// 一行 = 4个分段 × 128像素
    /// </summary>
    public class FrameReassembler
    {
        private const int ROWS_PER_FRAME = 512;
        private const int PIXELS_PER_ROW = 512;
        private const int SEGMENTS_PER_ROW = 4;
        private const int TIMEOUT_MS = 100; // 帧超时阈值

        // 当前正在重组的帧
        private class FrameBuffer
        {
            public uint FrameID;
            public int[,] Data; // [row, pixel]
            public bool[,] SegmentReceived; // [row, segment]
            public int ReceivedRows;
            public Stopwatch Timer;

            public FrameBuffer(uint frameId)
            {
                FrameID = frameId;
                Data = new int[ROWS_PER_FRAME, PIXELS_PER_ROW];
                SegmentReceived = new bool[ROWS_PER_FRAME, SEGMENTS_PER_ROW];
                ReceivedRows = 0;
                Timer = Stopwatch.StartNew();
            }

            public bool IsComplete()
            {
                return ReceivedRows == ROWS_PER_FRAME;
            }

            public bool IsTimeout()
            {
                return Timer.ElapsedMilliseconds > TIMEOUT_MS;
            }
        }

        private readonly object _lock = new object();
        private FrameBuffer _currentFrame;
        private uint _expectedFrameID = 0;
        
        // 统计信息
        public long TotalPacketsReceived { get; private set; }
        public long TotalPacketsDropped { get; private set; }
        public long TotalFramesCompleted { get; private set; }
        public long TotalFramesTimedOut { get; private set; }

        public FrameReassembler()
        {
            _currentFrame = new FrameBuffer(0);
        }

        /// <summary>
        /// 处理接收到的UDP数据包
        /// </summary>
        /// <param name="header">包头</param>
        /// <param name="pixelData">像素数据 (128个Int32)</param>
        /// <returns>如果完成一帧，返回完整的帧数据；否则返回null</returns>
        public int[,] ProcessPacket(UdpPacketHeader header, int[] pixelData)
        {
            lock (_lock)
            {
                TotalPacketsReceived++;

                // 验证包头
                if (header.Header != UdpPacketHeader.VALID_HEADER)
                {
                    TotalPacketsDropped++;
                    return null;
                }

                // 验证索引范围
                if (header.RowIndex >= ROWS_PER_FRAME || header.SegIndex >= SEGMENTS_PER_ROW)
                {
                    TotalPacketsDropped++;
                    return null;
                }

                // 验证数据长度
                if (pixelData == null || pixelData.Length != UdpPacketHeader.PIXELS_PER_SEGMENT)
                {
                    TotalPacketsDropped++;
                    return null;
                }

                // 检查是否是新帧
                if (header.FrameID != _currentFrame.FrameID)
                {
                    // 如果当前帧未完成但超时，先输出当前帧（带坏线）
                    int[,] timedOutFrame = null;
                    if (!_currentFrame.IsComplete() && _currentFrame.IsTimeout() && _currentFrame.ReceivedRows > 0)
                    {
                        timedOutFrame = _currentFrame.Data;
                        TotalFramesTimedOut++;
                    }

                    // 开始新帧
                    _currentFrame = new FrameBuffer(header.FrameID);
                    _expectedFrameID = header.FrameID;

                    if (timedOutFrame != null)
                    {
                        // 先返回超时的旧帧
                        ProcessCurrentPacket(header, pixelData);
                        return timedOutFrame;
                    }
                }

                ProcessCurrentPacket(header, pixelData);

                // 检查当前帧是否完成
                if (_currentFrame.IsComplete())
                {
                    TotalFramesCompleted++;
                    var completedFrame = _currentFrame.Data;
                    
                    // 准备下一帧
                    _currentFrame = new FrameBuffer(header.FrameID + 1);
                    _expectedFrameID++;
                    
                    return completedFrame;
                }

                return null;
            }
        }

        private void ProcessCurrentPacket(UdpPacketHeader header, int[] pixelData)
        {
            int row = header.RowIndex;
            int seg = header.SegIndex;

            // 检查是否重复接收
            if (_currentFrame.SegmentReceived[row, seg])
            {
                return; // 忽略重复包
            }

            // 标记段已接收
            _currentFrame.SegmentReceived[row, seg] = true;

            // 复制数据到帧缓冲区
            int pixelOffset = seg * UdpPacketHeader.PIXELS_PER_SEGMENT;
            for (int i = 0; i < UdpPacketHeader.PIXELS_PER_SEGMENT; i++)
            {
                _currentFrame.Data[row, pixelOffset + i] = pixelData[i];
            }

            // 检查该行是否完整
            bool rowComplete = true;
            for (int s = 0; s < SEGMENTS_PER_ROW; s++)
            {
                if (!_currentFrame.SegmentReceived[row, s])
                {
                    rowComplete = false;
                    break;
                }
            }

            if (rowComplete)
            {
                _currentFrame.ReceivedRows++;
            }
        }

        /// <summary>
        /// 重置重组器状态
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _currentFrame = new FrameBuffer(0);
                _expectedFrameID = 0;
                TotalPacketsReceived = 0;
                TotalPacketsDropped = 0;
                TotalFramesCompleted = 0;
                TotalFramesTimedOut = 0;
            }
        }

        /// <summary>
        /// 获取当前丢包率
        /// </summary>
        public double GetDropRate()
        {
            if (TotalPacketsReceived == 0) return 0;
            return (double)TotalPacketsDropped / TotalPacketsReceived;
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStatistics()
        {
            return $"Frames: {TotalFramesCompleted} | Packets: {TotalPacketsReceived} | Drop Rate: {GetDropRate():P2} | Timeouts: {TotalFramesTimedOut}";
        }
    }
}
