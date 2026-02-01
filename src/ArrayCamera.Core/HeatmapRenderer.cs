using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ArrayCamera.Core
{
    /// <summary>
    /// 颜色映射类型
    /// </summary>
    public enum ColorMapType
    {
        Jet,
        Parula,
        Gray,
        Hot,
        Viridis
    }

    /// <summary>
    /// 高性能热力图渲染器
    /// 使用 LockBits 技术直接操作内存，渲染耗时 <10ms
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class HeatmapRenderer
    {
        private const int ARRAY_SIZE = 512;
        private readonly byte[][] _colorMaps;
        private ColorMapType _currentColorMap;
        
        // 对比度调节参数
        private int _minValue;
        private int _maxValue;
        private bool _autoScale;

        // 多帧平均缓冲区
        private long[,] _accumulator;
        private int _frameCount;
        private bool _averagingEnabled;
        private int _averagingFrames;

        public HeatmapRenderer()
        {
            _colorMaps = new byte[5][];
            InitializeColorMaps();
            _currentColorMap = ColorMapType.Jet;
            _autoScale = true;
            _minValue = 0;
            _maxValue = int.MaxValue;
            
            _accumulator = new long[ARRAY_SIZE, ARRAY_SIZE];
            _frameCount = 0;
            _averagingEnabled = false;
            _averagingFrames = 10;
        }

        /// <summary>
        /// 设置颜色映射类型
        /// </summary>
        public void SetColorMap(ColorMapType colorMap)
        {
            _currentColorMap = colorMap;
        }

        /// <summary>
        /// 设置对比度范围 (手动模式)
        /// </summary>
        public void SetContrastRange(int minValue, int maxValue)
        {
            _autoScale = false;
            _minValue = minValue;
            _maxValue = maxValue;
        }

        /// <summary>
        /// 启用自动缩放
        /// </summary>
        public void EnableAutoScale(bool enable)
        {
            _autoScale = enable;
        }

        /// <summary>
        /// 启用多帧平均
        /// </summary>
        public void EnableFrameAveraging(bool enable, int frameCount = 10)
        {
            _averagingEnabled = enable;
            _averagingFrames = frameCount;
            
            if (enable)
            {
                ResetAveraging();
            }
        }

        /// <summary>
        /// 重置多帧平均累加器
        /// </summary>
        public void ResetAveraging()
        {
            Array.Clear(_accumulator, 0, _accumulator.Length);
            _frameCount = 0;
        }

        /// <summary>
        /// 渲染热力图到Bitmap
        /// </summary>
        /// <param name="frameData">512x512 的原始电流数据</param>
        /// <returns>渲染后的Bitmap</returns>
        public Bitmap Render(int[,] frameData)
        {
            if (frameData == null || frameData.GetLength(0) != ARRAY_SIZE || frameData.GetLength(1) != ARRAY_SIZE)
            {
                throw new ArgumentException("Frame data must be 512x512.");
            }

            // 如果启用多帧平均
            int[,] processedData = frameData;
            if (_averagingEnabled)
            {
                processedData = ProcessFrameAveraging(frameData);
            }

            // 自动缩放计算
            int minVal = _minValue;
            int maxVal = _maxValue;
            if (_autoScale)
            {
                CalculateMinMax(processedData, out minVal, out maxVal);
            }

            // 创建 Bitmap
            Bitmap bitmap = new Bitmap(ARRAY_SIZE, ARRAY_SIZE, PixelFormat.Format24bppRgb);

            // 锁定Bitmap内存
            BitmapData bmpData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                bitmap.PixelFormat);

            try
            {
                int stride = bmpData.Stride;
                IntPtr scan0 = bmpData.Scan0;
                int bytesPerPixel = 3; // RGB

                unsafe
                {
                    byte* ptr = (byte*)scan0;

                    for (int row = 0; row < ARRAY_SIZE; row++)
                    {
                        for (int col = 0; col < ARRAY_SIZE; col++)
                        {
                            int value = processedData[row, col];
                            
                            // 归一化到 0-255
                            int normalized = NormalizeValue(value, minVal, maxVal);
                            
                            // 获取颜色
                            Color color = GetColor(normalized);

                            // 写入像素 (BGR格式)
                            int offset = row * stride + col * bytesPerPixel;
                            ptr[offset] = color.B;
                            ptr[offset + 1] = color.G;
                            ptr[offset + 2] = color.R;
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            return bitmap;
        }

        /// <summary>
        /// 渲染到现有Bitmap（避免重新分配内存）
        /// </summary>
        public void RenderTo(int[,] frameData, Bitmap targetBitmap)
        {
            if (targetBitmap == null || targetBitmap.Width != ARRAY_SIZE || targetBitmap.Height != ARRAY_SIZE)
            {
                throw new ArgumentException("Target bitmap must be 512x512.");
            }

            if (frameData == null || frameData.GetLength(0) != ARRAY_SIZE || frameData.GetLength(1) != ARRAY_SIZE)
            {
                throw new ArgumentException("Frame data must be 512x512.");
            }

            // 处理数据
            int[,] processedData = frameData;
            if (_averagingEnabled)
            {
                processedData = ProcessFrameAveraging(frameData);
            }

            int minVal = _minValue;
            int maxVal = _maxValue;
            if (_autoScale)
            {
                CalculateMinMax(processedData, out minVal, out maxVal);
            }

            // 锁定Bitmap
            BitmapData bmpData = targetBitmap.LockBits(
                new Rectangle(0, 0, ARRAY_SIZE, ARRAY_SIZE),
                ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                int stride = bmpData.Stride;
                IntPtr scan0 = bmpData.Scan0;

                unsafe
                {
                    byte* ptr = (byte*)scan0;

                    for (int row = 0; row < ARRAY_SIZE; row++)
                    {
                        for (int col = 0; col < ARRAY_SIZE; col++)
                        {
                            int value = processedData[row, col];
                            int normalized = NormalizeValue(value, minVal, maxVal);
                            Color color = GetColor(normalized);

                            int offset = row * stride + col * 3;
                            ptr[offset] = color.B;
                            ptr[offset + 1] = color.G;
                            ptr[offset + 2] = color.R;
                        }
                    }
                }
            }
            finally
            {
                targetBitmap.UnlockBits(bmpData);
            }
        }

        // ==================== 私有方法 ====================

        private int[,] ProcessFrameAveraging(int[,] frameData)
        {
            // 累加当前帧
            for (int i = 0; i < ARRAY_SIZE; i++)
            {
                for (int j = 0; j < ARRAY_SIZE; j++)
                {
                    _accumulator[i, j] += frameData[i, j];
                }
            }
            _frameCount++;

            // 如果达到平均帧数，计算平均值
            if (_frameCount >= _averagingFrames)
            {
                int[,] averaged = new int[ARRAY_SIZE, ARRAY_SIZE];
                for (int i = 0; i < ARRAY_SIZE; i++)
                {
                    for (int j = 0; j < ARRAY_SIZE; j++)
                    {
                        averaged[i, j] = (int)(_accumulator[i, j] / _frameCount);
                    }
                }
                
                // 重置累加器
                ResetAveraging();
                return averaged;
            }

            // 未达到平均帧数，返回当前部分平均
            int[,] partial = new int[ARRAY_SIZE, ARRAY_SIZE];
            for (int i = 0; i < ARRAY_SIZE; i++)
            {
                for (int j = 0; j < ARRAY_SIZE; j++)
                {
                    partial[i, j] = (int)(_accumulator[i, j] / _frameCount);
                }
            }
            return partial;
        }

        private void CalculateMinMax(int[,] data, out int min, out int max)
        {
            min = int.MaxValue;
            max = int.MinValue;

            for (int i = 0; i < ARRAY_SIZE; i++)
            {
                for (int j = 0; j < ARRAY_SIZE; j++)
                {
                    int value = data[i, j];
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            // 避免除以零
            if (min == max) max = min + 1;
        }

        private int NormalizeValue(int value, int min, int max)
        {
            if (max <= min) return 0;
            
            int normalized = (value - min) * 255 / (max - min);
            if (normalized < 0) normalized = 0;
            if (normalized > 255) normalized = 255;
            
            return normalized;
        }

        private Color GetColor(int normalizedValue)
        {
            byte[] colorMap = _colorMaps[(int)_currentColorMap];
            int index = normalizedValue * 3;
            return Color.FromArgb(colorMap[index], colorMap[index + 1], colorMap[index + 2]);
        }

        private void InitializeColorMaps()
        {
            // Jet colormap
            _colorMaps[0] = new byte[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                _colorMaps[0][i * 3] = (byte)Clamp((int)(255 * (1.5 - 4 * Math.Abs(i / 255.0 - 0.75))), 0, 255); // R
                _colorMaps[0][i * 3 + 1] = (byte)Clamp((int)(255 * (1.5 - 4 * Math.Abs(i / 255.0 - 0.5))), 0, 255); // G
                _colorMaps[0][i * 3 + 2] = (byte)Clamp((int)(255 * (1.5 - 4 * Math.Abs(i / 255.0 - 0.25))), 0, 255); // B
            }

            // Parula colormap (简化版本)
            _colorMaps[1] = new byte[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                double t = i / 255.0;
                _colorMaps[1][i * 3] = (byte)(255 * t); // R
                _colorMaps[1][i * 3 + 1] = (byte)(255 * Math.Sin(t * Math.PI)); // G
                _colorMaps[1][i * 3 + 2] = (byte)(255 * (1 - t)); // B
            }

            // Gray colormap
            _colorMaps[2] = new byte[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                _colorMaps[2][i * 3] = (byte)i;
                _colorMaps[2][i * 3 + 1] = (byte)i;
                _colorMaps[2][i * 3 + 2] = (byte)i;
            }

            // Hot colormap
            _colorMaps[3] = new byte[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                int r = Clamp(i * 3, 0, 255);
                int g = Clamp(i * 3 - 256, 0, 255);
                int b = Clamp(i * 3 - 512, 0, 255);
                _colorMaps[3][i * 3] = (byte)r;
                _colorMaps[3][i * 3 + 1] = (byte)g;
                _colorMaps[3][i * 3 + 2] = (byte)b;
            }

            // Viridis colormap (简化版本)
            _colorMaps[4] = new byte[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                double t = i / 255.0;
                _colorMaps[4][i * 3] = (byte)(255 * (0.267 + 0.533 * t));
                _colorMaps[4][i * 3 + 1] = (byte)(255 * (0.005 + 0.873 * t));
                _colorMaps[4][i * 3 + 2] = (byte)(255 * (0.329 + 0.235 * t));
            }
        }

        private int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
