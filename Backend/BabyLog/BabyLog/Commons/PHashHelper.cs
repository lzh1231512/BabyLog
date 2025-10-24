using FFMpegCore;
using FFMpegCore.Pipes;
using FFMpegCore.Arguments;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Numerics;

namespace BabyLog.Commons
{
    /// <summary>
    /// 用于生成和比较图像和视频感知哈希(pHash)的帮助类
    /// </summary>
    public class PHashHelper
    {
        // pHash计算的默认图像大小
        private const int Size = 32;
        private const int SmallSize = 8;

        /// <summary>
        /// 计算图像文件的pHash
        /// </summary>
        /// <param name="filePath">图像文件路径</param>
        /// <returns>64位pHash值</returns>
        public static ulong CalculateImageHash(string filePath)
        {
            using var image = Image.Load<Rgba32>(filePath);
            return CalculateImageHash(image);
        }

        public static string CalculateImageHashString(string filePath)
        {
            var hash = CalculateImageHash(filePath);
            return HashToString(hash);
        }

        /// <summary>
        /// 计算图像的pHash
        /// </summary>
        /// <param name="image">要计算哈希的图像</param>
        /// <returns>64位pHash值</returns>
        public static ulong CalculateImageHash(Image<Rgba32> image)
        {
            // 将图像调整为32x32
            image.Mutate(x => x.Resize(Size, Size).Grayscale());
            
            // 转换为灰度值
            var vals = new double[Size, Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var pixel = image[x, y];
                    vals[x, y] = pixel.R; // 由于是灰度图，R、G和B的值相同
                }
            }
            
            // 应用DCT（离散余弦变换）
            var dctVals = ApplyDCT(vals);
            
            // 计算DCT值的平均值（不包括第一项）
            double total = 0;
            for (int y = 0; y < SmallSize; y++)
            {
                for (int x = 0; x < SmallSize; x++)
                {
                    // 跳过第一项(0,0)
                    if (x == 0 && y == 0) continue;
                    total += dctVals[x, y];
                }
            }
            
            // 计算平均值
            double avg = total / ((SmallSize * SmallSize) - 1);
            
            // 创建哈希
            ulong hash = 0;
            for (int y = 0; y < SmallSize; y++)
            {
                for (int x = 0; x < SmallSize; x++)
                {
                    // 跳过第一项(0,0)
                    if (x == 0 && y == 0) continue;
                    
                    // 如果值高于平均值，则设置位
                    if (dctVals[x, y] > avg)
                    {
                        hash |= 1UL;
                    }
                    
                    // 只有当不是最后一位时才移位
                    if (!(x == SmallSize - 1 && y == SmallSize - 1))
                    {
                        hash <<= 1;
                    }
                }
            }
            
            return hash;
        }
        
        /// <summary>
        /// 对输入数组应用简单的离散余弦变换
        /// </summary>
        private static double[,] ApplyDCT(double[,] input)
        {
            int size = input.GetLength(0);
            double[,] output = new double[SmallSize, SmallSize];
            
            for (int u = 0; u < SmallSize; u++)
            {
                for (int v = 0; v < SmallSize; v++)
                {
                    double sum = 0;
                    for (int i = 0; i < size; i++)
                    {
                        for (int j = 0; j < size; j++)
                        {
                            sum += input[i, j] * 
                                   Math.Cos((2 * i + 1) * u * Math.PI / (2 * size)) * 
                                   Math.Cos((2 * j + 1) * v * Math.PI / (2 * size));
                        }
                    }
                    
                    // 应用缩放因子
                    double cu = (u == 0) ? 1 / Math.Sqrt(2) : 1;
                    double cv = (v == 0) ? 1 / Math.Sqrt(2) : 1;
                    output[u, v] = 0.25 * cu * cv * sum;
                }
            }
            
            return output;
        }
        
        /// <summary>
        /// 通过提取1秒、3秒和5秒的帧来计算视频文件的pHash
        /// </summary>
        /// <param name="filePath">视频文件路径</param>
        /// <returns>3个提取帧的pHash值数组</returns>
        public static ulong[] CalculateVideoHash(string filePath)
        {
            var frameTimestamps = new[] { 1, 3, 5 }; // 秒
            var hashes = new ulong[frameTimestamps.Length];
            
            for (int i = 0; i < frameTimestamps.Length; i++)
            {
                var timestamp = frameTimestamps[i];
                
                // 为提取的帧创建临时文件
                string tempFramePath = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid()}.jpg");
                
                try
                {
                    // 提取指定时间戳的帧
                    FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(tempFramePath, false, options => options
                            .Seek(TimeSpan.FromSeconds(timestamp))
                            .WithFrameOutputCount(1))
                        .ProcessSynchronously();
                    
                    // 计算提取帧的哈希
                    if (File.Exists(tempFramePath))
                    {
                        hashes[i] = CalculateImageHash(tempFramePath);
                    }
                    else
                    {
                        // 如果此帧提取失败，使用默认值
                        hashes[i] = 0;
                    }
                }
                catch (Exception ex)
                {
                    // 处理帧提取期间的任何异常
                    Console.WriteLine($"提取 {timestamp}秒 的帧时出错: {ex.Message}");
                    hashes[i] = 0;
                }
                finally
                {
                    // 清理临时文件
                    if (File.Exists(tempFramePath))
                    {
                        try
                        {
                            File.Delete(tempFramePath);
                        }
                        catch { /* 忽略清理错误 */ }
                    }
                }
            }
            
            return hashes;
        }

        public static string CalculateVideoHashString(string filePath)
        {
            var hashes = CalculateVideoHash(filePath);
            return VideoHashToString(hashes);
        }
        /// <summary>
        /// 将单个哈希(ulong)转为16进制字符串
        /// </summary>
        public static string HashToString(ulong hash)
        {
            return hash.ToString("X16");
        }

        /// <summary>
        /// 将哈希字符串还原为ulong
        /// </summary>
        public static ulong StringToHash(string hashString)
        {
            return ulong.Parse(hashString, System.Globalization.NumberStyles.HexNumber);
        }

        /// <summary>
        /// 将视频哈希数组转为字符串（逗号分隔）
        /// </summary>
        public static string VideoHashToString(ulong[] hashes)
        {
            return string.Join(",", hashes.Select(HashToString));
        }

        /// <summary>
        /// 将视频哈希字符串还原为哈希数组
        /// </summary>
        public static ulong[] StringToVideoHash(string hashString)
        {
            return hashString.Split(',').Select(StringToHash).ToArray();
        }

        /// <summary>
        /// 计算两个哈希之间的汉明距离（不同位的数量）
        /// </summary>
        /// <param name="hash1">第一个哈希</param>
        /// <param name="hash2">第二个哈希</param>
        /// <returns>不同位的数量 (0-64)</returns>
        public static int HammingDistance(ulong hash1, ulong hash2)
        {
            ulong xor = hash1 ^ hash2;
            // 计算设置位的数量
            return BitOperations.PopCount(xor);
        }
        
        /// <summary>
        /// 将两个哈希之间的相似度计算为百分比 (100% = 完全相同)
        /// </summary>
        /// <param name="hash1">第一个哈希</param>
        /// <param name="hash2">第二个哈希</param>
        /// <returns>相似度百分比 (0-100)</returns>
        public static double CalculateSimilarity(ulong hash1, ulong hash2)
        {
            int distance = HammingDistance(hash1, hash2);
            return 100 - ((distance / 64.0) * 100);
        }

        /// <summary>
        /// 支持字符串输入的图片哈希相似度比较
        /// </summary>
        public static double CalculateSimilarity(string hashStr1, string hashStr2)
        {
            var h1 = StringToHash(hashStr1);
            var h2 = StringToHash(hashStr2);
            return CalculateSimilarity(h1, h2);
        }
        
        /// <summary>
        /// 计算两组视频哈希之间的平均相似度
        /// </summary>
        /// <param name="videoHashes1">第一组视频帧哈希</param>
        /// <param name="videoHashes2">第二组视频帧哈希</param>
        /// <returns>平均相似度百分比 (0-100)</returns>
        public static double CalculateVideoSimilarity(ulong[] videoHashes1, ulong[] videoHashes2)
        {
            if (videoHashes1.Length != videoHashes2.Length)
            {
                throw new ArgumentException("哈希数组长度必须相同");
            }
            
            double totalSimilarity = 0;
            
            for (int i = 0; i < videoHashes1.Length; i++)
            {
                totalSimilarity += CalculateSimilarity(videoHashes1[i], videoHashes2[i]);
            }
            
            return totalSimilarity / videoHashes1.Length;
        }

        /// <summary>
        /// 支持字符串输入的视频哈希相似度比较
        /// </summary>
        public static double CalculateVideoSimilarity(string videoHashStr1, string videoHashStr2)
        {
            var arr1 = StringToVideoHash(videoHashStr1);
            var arr2 = StringToVideoHash(videoHashStr2);
            return CalculateVideoSimilarity(arr1, arr2);
        }
    }
}
