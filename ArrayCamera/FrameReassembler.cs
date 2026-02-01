using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ArrayCamera
{
    /// <summary>
    /// UDP 分包重组器
    /// </summary>
    public class FrameReassembler
    {
        private const int IMAGE_SIZE = 512;
        private const int SEGMENTS_PER_ROW = 4;  // 每行4个分段
        private const int POINTS_PER_SEGMENT = 128;  // 每段128个点

        // 帧缓存结构
        private class FrameBuffer
        {
            public uint FrameID;
            public int[,] Data = new int[IMAGE_SIZE, IMAGE_SIZE];  // 512x512
            public bool[,] ReceivedSegments = new bool[IMAGE_SIZE, SEGMENTS_PER_ROW];  // 行x分段标记
            public int ReceivedRowCount = 0;  // 已接收的完整行数
            public DateTime LastUpdateTime = DateTime.Now;
        }

        private readonly Dictionary<uint, FrameBuffer> _frameBuffers = new();
        private readonly object _lock = new();
        private uint _lastCompletedFrameID = 0;

        // 配置参数
        private readonly int _minRowsForOutput = 480;  // 至少480行才输出（允许少量丢包）
        private readonly int _frameTimeoutMs = 1000;   // 帧超时时间

        // 事件
        public event Action<int[]>? OnFrameComplete;
        public event Action<string>? OnLog;

        /// <summary>
        /// 添加一个数据包
        /// </summary>
        public void AddPacket(uint frameID, ushort rowIndex, byte segIndex, byte[] rowData)
        {
            lock (_lock)
            {
                // 1. 获取或创建帧缓存
                if (!_frameBuffers.TryGetValue(frameID, out FrameBuffer? frame))
                {
                    frame = new FrameBuffer { FrameID = frameID };
                    _frameBuffers[frameID] = frame;
                }

                // 2. 验证数据有效性
                if (rowIndex >= IMAGE_SIZE || segIndex >= SEGMENTS_PER_ROW)
                {
                    OnLog?.Invoke($"⚠ 无效数据包: FrameID={frameID}, Row={rowIndex}, Seg={segIndex}");
                    return;
                }

                if (rowData.Length < POINTS_PER_SEGMENT)
                {
                    OnLog?.Invoke($"⚠ 数据长度不足: {rowData.Length} < {POINTS_PER_SEGMENT}");
                    return;
                }

                // 3. 检查是否重复
                if (frame.ReceivedSegments[rowIndex, segIndex])
                {
                    return;  // 跳过重复包
                }

                // 4. 写入数据到缓存
                int startCol = segIndex * POINTS_PER_SEGMENT;
                for (int i = 0; i < POINTS_PER_SEGMENT; i++)
                {
                    frame.Data[rowIndex, startCol + i] = rowData[i];
                }

                // 5. 标记分段已接收
                frame.ReceivedSegments[rowIndex, segIndex] = true;

                // 6. 检查该行是否完整
                bool rowComplete = true;
                for (int s = 0; s < SEGMENTS_PER_ROW; s++)
                {
                    if (!frame.ReceivedSegments[rowIndex, s])
                    {
                        rowComplete = false;
                        break;
                    }
                }

                // 7. 如果该行首次完整，增加计数
                if (rowComplete)
                {
                    frame.ReceivedRowCount++;
                    frame.LastUpdateTime = DateTime.Now;
                }

                // 8. 检查是否满足输出条件
                if (frame.ReceivedRowCount >= _minRowsForOutput)
                {
                    OutputFrame(frame);
                }
                else
                {
                    // 清理超时的旧帧
                    CleanupOldFrames();
                }
            }
        }

        /// <summary>
        /// 输出完整帧
        /// </summary>
        private void OutputFrame(FrameBuffer frame)
        {
            // 防止重复输出
            if (frame.FrameID <= _lastCompletedFrameID)
            {
                return;
            }

            // 转换为一维数组
            int[] flatData = new int[IMAGE_SIZE * IMAGE_SIZE];
            for (int row = 0; row < IMAGE_SIZE; row++)
            {
                for (int col = 0; col < IMAGE_SIZE; col++)
                {
                    flatData[row * IMAGE_SIZE + col] = frame.Data[row, col];
                }
            }

            // 触发事件
            OnFrameComplete?.Invoke(flatData);
            _lastCompletedFrameID = frame.FrameID;

            // 统计信息
            int totalSegments = IMAGE_SIZE * SEGMENTS_PER_ROW;
            int receivedSegments = 0;
            for (int r = 0; r < IMAGE_SIZE; r++)
            {
                for (int s = 0; s < SEGMENTS_PER_ROW; s++)
                {
                    if (frame.ReceivedSegments[r, s]) receivedSegments++;
                }
            }

            double completeness = (double)receivedSegments / totalSegments * 100;
            OnLog?.Invoke($"✓ 输出帧 #{frame.FrameID}: 完整度={completeness:F1}% ({receivedSegments}/{totalSegments}包)");

            // 清理该帧
            _frameBuffers.Remove(frame.FrameID);
        }

        /// <summary>
        /// 清理超时的旧帧
        /// </summary>
        private void CleanupOldFrames()
        {
            var now = DateTime.Now;
            var toRemove = _frameBuffers
                .Where(kv => (now - kv.Value.LastUpdateTime).TotalMilliseconds > _frameTimeoutMs)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var frameID in toRemove)
            {
                var frame = _frameBuffers[frameID];
                OnLog?.Invoke($"⏱ 帧 #{frameID} 超时 ({frame.ReceivedRowCount}/512行)，已丢弃");
                _frameBuffers.Remove(frameID);
            }
        }

        /// <summary>
        /// 强制输出当前帧（用于停止扫描时）
        /// </summary>
        public void FlushCurrentFrame()
        {
            lock (_lock)
            {
                var latestFrame = _frameBuffers.Values.OrderByDescending(f => f.FrameID).FirstOrDefault();
                if (latestFrame != null && latestFrame.ReceivedRowCount > 0)
                {
                    OnLog?.Invoke($"强制输出帧 #{latestFrame.FrameID} ({latestFrame.ReceivedRowCount}行)");
                    OutputFrame(latestFrame);
                }
            }
        }

        /// <summary>
        /// 重置所有缓存
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _frameBuffers.Clear();
                _lastCompletedFrameID = 0;
                OnLog?.Invoke("帧缓存已重置");
            }
        }
    }
}
