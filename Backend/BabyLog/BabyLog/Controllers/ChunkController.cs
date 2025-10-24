using BabyLog.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using FFMpegCore;
using IOFile = System.IO.File;

namespace BabyLog.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChunkController : ControllerBase
    {
        private readonly ILogger<FilesController> _logger;
        private readonly IWebHostEnvironment _env;
        public ChunkController(ILogger<FilesController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        // 存储上传任务信息的字典（使用内存缓存 + JSON文件持久化）
        private static readonly ConcurrentDictionary<string, ChunkUploadTask> _chunkUploadTasks = new ConcurrentDictionary<string, ChunkUploadTask>();

        // 分片上传临时目录名
        private const string CHUNK_UPLOAD_DIR = "TempFile_Chunk";
        // 任务信息文件后缀
        private const string TASK_FILE_SUFFIX = ".task.json";

        // 清理过期的分片上传任务（超过24小时未更新）
        private void CleanupExpiredChunkUploadTasks()
        {
            var expireTime = DateTime.Now.AddHours(-24);

            // 先从内存中获取过期任务
            var expiredTaskIds = _chunkUploadTasks.Where(kv => kv.Value.LastUpdatedAt < expireTime)
                                                .Select(kv => kv.Key).ToList();

            foreach (var taskId in expiredTaskIds)
            {
                if (_chunkUploadTasks.TryRemove(taskId, out var task))
                {
                    try
                    {
                        // 清理该任务的临时文件目录和任务文件
                        var taskDir = Path.Combine(_env.ContentRootPath, CHUNK_UPLOAD_DIR, taskId);
                        if (Directory.Exists(taskDir))
                        {
                            // 删除任务文件
                            var taskFilePath = Path.Combine(taskDir, $"{taskId}{TASK_FILE_SUFFIX}");
                            if (System.IO.File.Exists(taskFilePath))
                            {
                                System.IO.File.Delete(taskFilePath);
                            }

                            // 删除整个目录
                            Directory.Delete(taskDir, true);
                        }
                        _logger.LogInformation($"已清理过期上传任务: {taskId}, 文件: {task.OriginalFileName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"清理过期上传任务失败: {taskId}");
                    }
                }
            }

            // 检查文件系统中的过期任务（可能不在内存中）
            try
            {
                var chunkDir = Path.Combine(_env.ContentRootPath, CHUNK_UPLOAD_DIR);
                if (Directory.Exists(chunkDir))
                {
                    var taskDirs = Directory.GetDirectories(chunkDir);
                    foreach (var taskDir in taskDirs)
                    {
                        var dirInfo = new DirectoryInfo(taskDir);
                        var taskId = dirInfo.Name;

                        // 如果内存中没有这个任务，尝试从文件加载
                        if (!_chunkUploadTasks.ContainsKey(taskId))
                        {
                            var taskFilePath = Path.Combine(taskDir, $"{taskId}{TASK_FILE_SUFFIX}");
                            if (IOFile.Exists(taskFilePath))
                            {
                                try
                                {
                                    var task = LoadTaskFromFile(taskFilePath);
                                    if (task != null && task.LastUpdatedAt < expireTime)
                                    {
                                        // 过期了，删除文件和目录
                                        IOFile.Delete(taskFilePath);
                                        Directory.Delete(taskDir, true);
                                        _logger.LogInformation($"已清理文件系统中的过期任务: {taskId}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"处理文件系统中的过期任务失败: {taskId}");
                                    // 如果文件损坏，也直接删除
                                    try
                                    {
                                        Directory.Delete(taskDir, true);
                                    }
                                    catch { /* 忽略删除失败 */ }
                                }
                            }
                            else
                            {
                                // 没有任务文件的目录直接删除
                                try
                                {
                                    Directory.Delete(taskDir, true);
                                    _logger.LogInformation($"已清理无任务文件的目录: {taskId}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"清理无任务文件的目录失败: {taskId}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理文件系统中的过期任务失败");
            }
        }

        /// <summary>
        /// 将任务信息保存到JSON文件中
        /// </summary>
        private void SaveTaskToFile(ChunkUploadTask task)
        {
            try
            {
                var taskDir = Path.Combine(_env.ContentRootPath, CHUNK_UPLOAD_DIR, task.TaskId);
                if (!Directory.Exists(taskDir))
                {
                    Directory.CreateDirectory(taskDir);
                }

                var taskFilePath = Path.Combine(taskDir, $"{task.TaskId}{TASK_FILE_SUFFIX}");
                var jsonString = JsonSerializer.Serialize(task, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                IOFile.WriteAllText(taskFilePath, jsonString);
                _logger.LogDebug($"已保存任务到文件: {task.TaskId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"保存任务到文件失败: {task.TaskId}");
                // 保存失败不影响内存中的操作，继续执行
            }
        }

        /// <summary>
        /// 从JSON文件加载任务信息
        /// </summary>
        private ChunkUploadTask LoadTaskFromFile(string taskFilePath)
        {
            try
            {
                if (IOFile.Exists(taskFilePath))
                {
                    var jsonString = IOFile.ReadAllText(taskFilePath);
                    var task = JsonSerializer.Deserialize<ChunkUploadTask>(jsonString);
                    return task;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"从文件加载任务失败: {taskFilePath}");
            }

            return null;
        }

        /// <summary>
        /// 尝试获取任务（先从内存，不存在则从文件加载）
        /// </summary>
        private bool TryGetTask(string taskId, out ChunkUploadTask task)
        {
            // 先从内存中获取
            if (_chunkUploadTasks.TryGetValue(taskId, out task))
            {
                return true;
            }

            // 再尝试从文件加载
            try
            {
                var taskFilePath = Path.Combine(_env.ContentRootPath, CHUNK_UPLOAD_DIR, taskId, $"{taskId}{TASK_FILE_SUFFIX}");
                task = LoadTaskFromFile(taskFilePath);

                if (task != null)
                {
                    // 加入内存缓存
                    _chunkUploadTasks[taskId] = task;
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"尝试获取任务失败: {taskId}");
            }

            task = null;
            return false;
        }

        /// <summary>
        /// 更新任务状态（同时更新文件）
        /// </summary>
        private void UpdateTaskStatus(ChunkUploadTask task)
        {
            task.LastUpdatedAt = DateTime.Now;
            _chunkUploadTasks[task.TaskId] = task;
            SaveTaskToFile(task);
        }

        /// <summary>
        /// 计算文件的MD5值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>MD5值的十六进制字符串</returns>
        private string CalculateFileMD5(string filePath)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算文件MD5值失败: {filePath}");
                throw;
            }
        }

        /// <summary>
        /// 初始化分片上传任务
        /// </summary>
        [HttpPost("init")]
        public IActionResult InitChunkUpload([FromBody] InitChunkUploadRequest request)
        {
            try
            {
                // 验证请求参数
                if (string.IsNullOrEmpty(request.FileName) || request.FileSize <= 0 || request.ChunkCount <= 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "无效的请求参数"
                    });
                }

                // 自动清理过期任务
                CleanupExpiredChunkUploadTasks();

                // 生成唯一的任务ID
                string taskId = Guid.NewGuid().ToString("N");

                // 创建上传任务对象
                var uploadTask = new ChunkUploadTask
                {
                    TaskId = taskId,
                    OriginalFileName = request.FileName,
                    FileType = request.FileType ?? FilesController.GetContentType(request.FileName),
                    TotalSize = request.FileSize,
                    TotalChunks = request.ChunkCount,
                    CompletedChunks = new HashSet<int>(),
                    Status = ChunkUploadStatus.Initialized,
                    CreatedAt = DateTime.Now,
                    LastUpdatedAt = DateTime.Now,
                    FileMD5 = request.FileMD5 // 保存客户端提供的MD5值
                };

                // 存储任务信息到内存
                _chunkUploadTasks[taskId] = uploadTask;

                // 创建任务的临时目录
                var chunkDirectory = Path.Combine(_env.ContentRootPath, CHUNK_UPLOAD_DIR, taskId);
                Directory.CreateDirectory(chunkDirectory);

                // 保存任务信息到文件
                SaveTaskToFile(uploadTask);

                _logger.LogInformation($"初始化分片上传任务: {taskId}, 文件: {request.FileName}, 大小: {request.FileSize}, 分片数: {request.ChunkCount}, MD5: {request.FileMD5}");

                return Ok(new ApiResponse<InitChunkUploadResponse>
                {
                    Success = true,
                    Data = new InitChunkUploadResponse { TaskId = taskId },
                    Message = "分片上传任务初始化成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化分片上传任务失败");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"初始化分片上传任务失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 上传单个分片
        /// </summary>
        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        [RequestSizeLimit(long.MaxValue)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> UploadChunk([FromQuery] string taskId, [FromQuery] int chunkIndex)
        {
            try
            {
                // 验证任务ID
                if (string.IsNullOrEmpty(taskId) || !TryGetTask(taskId, out var uploadTask))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "无效的上传任务ID"
                    });
                }

                // 验证分片索引
                if (chunkIndex < 0 || chunkIndex >= uploadTask.TotalChunks)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"无效的分片索引: {chunkIndex}, 应在 0 至 {uploadTask.TotalChunks - 1} 之间"
                    });
                }

                // 检查任务状态
                if (uploadTask.Status == ChunkUploadStatus.Completed || uploadTask.Status == ChunkUploadStatus.Failed)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"上传任务已{(uploadTask.Status == ChunkUploadStatus.Completed ? "完成" : "失败")}"
                    });
                }

                // 检查是否已上传该分片
                if (uploadTask.CompletedChunks.Contains(chunkIndex))
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = $"分片 {chunkIndex} 已上传"
                    });
                }

                // 获取上传的文件
                var file = Request.Form.Files.FirstOrDefault();
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "未接收到有效的分片文件"
                    });
                }

                // 确保分片上传目录存在
                var chunkDirectory = Path.Combine(_env.ContentRootPath, CHUNK_UPLOAD_DIR, taskId);
                if (!Directory.Exists(chunkDirectory))
                {
                    Directory.CreateDirectory(chunkDirectory);
                }

                // 保存分片文件
                var chunkFilePath = Path.Combine(chunkDirectory, $"chunk_{chunkIndex}");
                using (var fileStream = new FileStream(chunkFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    await file.CopyToAsync(fileStream);
                }

                // 更新任务状态
                uploadTask.CompletedChunks.Add(chunkIndex);
                uploadTask.Status = ChunkUploadStatus.InProgress;
                uploadTask.LastUpdatedAt = DateTime.Now;

                // 保存更新后的任务状态
                UpdateTaskStatus(uploadTask);

                _logger.LogInformation($"上传任务 {taskId} 的分片 {chunkIndex} 上传成功");

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        ChunkIndex = chunkIndex,
                        CompletedChunks = uploadTask.CompletedChunks.Count,
                        TotalChunks = uploadTask.TotalChunks
                    },
                    Message = $"分片 {chunkIndex} 上传成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"上传分片失败: 任务ID={taskId}, 分片索引={chunkIndex}");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"上传分片失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 完成上传（合并分片）
        /// </summary>
        [HttpPost("complete")]
        public async Task<IActionResult> CompleteChunkUpload([FromQuery] string taskId)
        {
            try
            {
                // 验证任务ID
                if (string.IsNullOrEmpty(taskId) || !TryGetTask(taskId, out var uploadTask))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "无效的上传任务ID"
                    });
                }

                // 检查任务状态
                if (uploadTask.Status == ChunkUploadStatus.Completed)
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Data = new
                        {
                            OriginalName = uploadTask.OriginalFileName,
                            ServerFileName = uploadTask.ResultFileName,
                            Size = uploadTask.TotalSize,
                            Type = uploadTask.FileType,
                            UploadTime = DateTime.Now.ToString("o"),
                            MD5 = uploadTask.ActualMD5
                        },
                        Message = "文件已完成上传"
                    });
                }

                if (uploadTask.Status == ChunkUploadStatus.Failed)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "上传任务已失败"
                    });
                }

                // 检查是否所有分片都已上传
                if (uploadTask.CompletedChunks.Count != uploadTask.TotalChunks)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"尚未上传所有分片: 已完成 {uploadTask.CompletedChunks.Count}/{uploadTask.TotalChunks}"
                    });
                }

                // 确保TempFile目录存在
                var tempFileDirectory = Path.Combine(_env.ContentRootPath, "TempFile");
                if (!Directory.Exists(tempFileDirectory))
                {
                    Directory.CreateDirectory(tempFileDirectory);
                }

                // 生成合并后的文件名
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                string randomStr = Path.GetRandomFileName().Replace(".", "").Substring(0, 6);
                string extension = Path.GetExtension(uploadTask.OriginalFileName);
                string serverFileName = $"{timestamp}_{randomStr}{extension}";
                string fullPath = Path.Combine(tempFileDirectory, serverFileName);

                // 合并分片
                using (var outputStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    var chunkDirectory = Path.Combine(_env.ContentRootPath, CHUNK_UPLOAD_DIR, taskId);

                    // 按顺序合并分片
                    for (int i = 0; i < uploadTask.TotalChunks; i++)
                    {
                        var chunkFilePath = Path.Combine(chunkDirectory, $"chunk_{i}");
                        if (!IOFile.Exists(chunkFilePath))
                        {
                            throw new FileNotFoundException($"分片文件不存在: {chunkFilePath}");
                        }

                        using (var inputStream = new FileStream(chunkFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            await inputStream.CopyToAsync(outputStream);
                        }
                    }
                }

                // 计算合并后文件的MD5并验证
                string actualMD5 = CalculateFileMD5(fullPath);

                // 更新任务状态
                uploadTask.ActualMD5 = actualMD5;

                // 验证MD5
                bool md5Verified = false;
                if (!string.IsNullOrEmpty(uploadTask.FileMD5))
                {
                    md5Verified = string.Equals(uploadTask.FileMD5, actualMD5, StringComparison.OrdinalIgnoreCase);
                    uploadTask.MD5Verified = md5Verified;

                    if (!md5Verified)
                    {
                        _logger.LogWarning($"文件MD5校验失败: 任务ID={taskId}, 期望值={uploadTask.FileMD5}, 实际值={actualMD5}");
                    }
                    else
                    {
                        _logger.LogInformation($"文件MD5校验成功: 任务ID={taskId}, MD5={actualMD5}");
                    }
                }

                uploadTask.Status = ChunkUploadStatus.Completed;
                uploadTask.LastUpdatedAt = DateTime.Now;
                uploadTask.ResultFileName = serverFileName;

                // 保存更新后的任务状态
                UpdateTaskStatus(uploadTask);

                _logger.LogInformation($"上传任务 {taskId} 完成, 合并文件: {serverFileName}, MD5: {actualMD5}");

                // 清理分片文件（异步进行，不影响响应）
                Task.Run(() => {
                    try
                    {
                        var chunkDirectory = Path.Combine(_env.ContentRootPath, CHUNK_UPLOAD_DIR, taskId);
                        if (Directory.Exists(chunkDirectory))
                        {
                            // 保留任务状态文件，删除分片文件
                            var taskFilePath = Path.Combine(chunkDirectory, $"{taskId}{TASK_FILE_SUFFIX}");
                            var files = Directory.GetFiles(chunkDirectory);

                            foreach (var file in files)
                            {
                                if (Path.GetFileName(file) != $"{taskId}{TASK_FILE_SUFFIX}")
                                {
                                    IOFile.Delete(file);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"清理分片文件失败: 任务ID={taskId}");
                    }
                });

                // 获取媒体文件的拍摄时间（如果是图片或视频）
                DateTime? captureTime = null;
                string fileExtension = Path.GetExtension(uploadTask.OriginalFileName).ToLowerInvariant();
                bool isImageOrVideo = fileExtension == ".jpg" || fileExtension == ".jpeg" || 
                                     fileExtension == ".png" || fileExtension == ".gif" || 
                                     fileExtension == ".webp" || fileExtension == ".mp4" || 
                                     fileExtension == ".mov";

                if (isImageOrVideo)
                {
                    captureTime = GetMediaCaptureTime(fullPath, uploadTask.OriginalFileName, _logger);
                    if (captureTime.HasValue)
                    {
                        _logger.LogInformation($"提取到文件拍摄时间: {captureTime.Value:o}, 文件: {serverFileName}");
                    }
                    else
                    {
                        _logger.LogInformation($"未能提取到拍摄时间, 文件: {serverFileName}");
                    }
                }

                // 返回成功信息、MD5校验结果和拍摄时间
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        OriginalName = uploadTask.OriginalFileName,
                        ServerFileName = serverFileName,
                        Size = uploadTask.TotalSize,
                        Type = uploadTask.FileType,
                        UploadTime = DateTime.Now.ToString("o"),
                        MD5 = actualMD5,
                        MD5Verified = md5Verified,
                        ExpectedMD5 = uploadTask.FileMD5,
                        CaptureTime = captureTime?.ToString("yyyy/MM/dd")
                    },
                    Message = md5Verified ?
                        "文件上传完成并通过MD5校验" :
                        string.IsNullOrEmpty(uploadTask.FileMD5) ?
                            "文件上传完成但未提供MD5进行校验" :
                            "文件上传完成但MD5校验失败"
                });
            }
            catch (Exception ex)
            {
                // 更新任务状态为失败
                if (TryGetTask(taskId, out var uploadTask))
                {
                    uploadTask.Status = ChunkUploadStatus.Failed;
                    uploadTask.LastUpdatedAt = DateTime.Now;

                    // 保存失败状态
                    UpdateTaskStatus(uploadTask);
                }

                _logger.LogError(ex, $"合并分片失败: 任务ID={taskId}");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"合并分片失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 查询上传状态
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetChunkUploadStatus([FromQuery] string taskId)
        {
            try
            {
                // 验证任务ID
                if (string.IsNullOrEmpty(taskId) || !TryGetTask(taskId, out var uploadTask))
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "未找到指定的上传任务"
                    });
                }

                // 返回任务状态
                return Ok(new ApiResponse<ChunkUploadStatusResponse>
                {
                    Success = true,
                    Data = new ChunkUploadStatusResponse
                    {
                        TaskId = uploadTask.TaskId,
                        OriginalFileName = uploadTask.OriginalFileName,
                        Status = uploadTask.Status,
                        TotalChunks = uploadTask.TotalChunks,
                        CompletedChunks = uploadTask.CompletedChunks.Count,
                        ResultFileName = uploadTask.ResultFileName,
                        MD5 = uploadTask.ActualMD5,
                        MD5Verified = uploadTask.MD5Verified
                    },
                    Message = "获取上传状态成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取上传状态失败: 任务ID={taskId}");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"获取上传状态失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 从图片中提取EXIF数据中的拍摄时间
        /// </summary>
        private static DateTime? ExtractImageCaptureTime(string filePath, ILogger _logger)
        {
            try
            {
                using var image = Image.Load(filePath);
                if (image.Metadata.ExifProfile != null)
                {
                    // 尝试获取DateTimeOriginal属性（拍摄时间）
                    if (image.Metadata.ExifProfile.TryGetValue(ExifTag.DateTimeOriginal, out var dateTimeOriginal) && 
                        dateTimeOriginal.Value != null)
                    {
                        string dateTimeStr = dateTimeOriginal.Value.ToString();
                        if (DateTime.TryParseExact(dateTimeStr, "yyyy:MM:dd HH:mm:ss", 
                            System.Globalization.CultureInfo.InvariantCulture, 
                            System.Globalization.DateTimeStyles.None, out var dateTime2))
                        {
                            return dateTime2;
                        }
                    }
                    
                    // 如果DateTimeOriginal不存在，尝试获取DateTime属性
                    if (image.Metadata.ExifProfile.TryGetValue(ExifTag.DateTime, out var dateTime) && 
                        dateTime.Value != null)
                    {
                        string dateTimeStr = dateTime.Value.ToString();
                        if (DateTime.TryParseExact(dateTimeStr, "yyyy:MM:dd HH:mm:ss", 
                            System.Globalization.CultureInfo.InvariantCulture, 
                            System.Globalization.DateTimeStyles.None, out var dt))
                        {
                            return dt;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"从图片提取EXIF拍摄时间失败: {filePath}");
            }
            
            return null;
        }

        /// <summary>
        /// 从视频中提取创建时间
        /// </summary>
        private static DateTime? ExtractVideoCaptureTime(string filePath, ILogger _logger)
        {
            try
            {
                var mediaInfo = FFProbe.Analyse(filePath);
                
                // 尝试获取创建时间
                if (mediaInfo.Format?.Tags != null)
                {
                    string creationTime = null;
                    if (mediaInfo.Format.Tags.TryGetValue("creation_time", out creationTime) && !string.IsNullOrEmpty(creationTime))
                    {
                        if (DateTime.TryParse(creationTime, out var dateTime))
                        {
                            return dateTime;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"从视频提取拍摄时间失败: {filePath}");
            }
            
            return null;
        }
        
        /// <summary>
        /// 尝试从文件名中提取日期时间
        /// </summary>
        private static DateTime? ExtractDateTimeFromFileName(string fileName, ILogger _logger)
        {
            try
            {
                // 匹配常见的日期时间格式：YYYYMMDD_HHMMSS、YYYY-MM-DD HH.MM.SS等
                var patterns = new[]
                {
                    @"(\d{4})[\-_]?(\d{2})[\-_]?(\d{2})[\-_\s]?(\d{2})[\-_:]?(\d{2})[\-_:]?(\d{2})", // 20210101_123045
                    @"(\d{4})[\-_]?(\d{2})[\-_]?(\d{2})",  // 20210101
                    @"IMG_(\d{8})_(\d{6})" // IMG_20210101_123045
                };
                
                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(fileName, pattern);
                    if (match.Success)
                    {
                        if (match.Groups.Count >= 7) // 完整日期时间
                        {
                            int year = int.Parse(match.Groups[1].Value);
                            int month = int.Parse(match.Groups[2].Value);
                            int day = int.Parse(match.Groups[3].Value);
                            int hour = int.Parse(match.Groups[4].Value);
                            int minute = int.Parse(match.Groups[5].Value);
                            int second = int.Parse(match.Groups[6].Value);
                            
                            if (IsValidDateTime(year, month, day, hour, minute, second))
                            {
                                return new DateTime(year, month, day, hour, minute, second);
                            }
                        }
                        else if (match.Groups.Count >= 4) // 只有日期
                        {
                            int year = int.Parse(match.Groups[1].Value);
                            int month = int.Parse(match.Groups[2].Value);
                            int day = int.Parse(match.Groups[3].Value);
                            
                            if (IsValidDateTime(year, month, day, 0, 0, 0))
                            {
                                return new DateTime(year, month, day);
                            }
                        }
                        else if (match.Groups.Count == 3 && match.Groups[1].Value.Length == 8 && match.Groups[2].Value.Length == 6)
                        {
                            // 处理类似IMG_20210101_123045格式
                            string dateStr = match.Groups[1].Value;
                            string timeStr = match.Groups[2].Value;
                            
                            int year = int.Parse(dateStr.Substring(0, 4));
                            int month = int.Parse(dateStr.Substring(4, 2));
                            int day = int.Parse(dateStr.Substring(6, 2));
                            int hour = int.Parse(timeStr.Substring(0, 2));
                            int minute = int.Parse(timeStr.Substring(2, 2));
                            int second = int.Parse(timeStr.Substring(4, 2));
                            
                            if (IsValidDateTime(year, month, day, hour, minute, second))
                            {
                                return new DateTime(year, month, day, hour, minute, second);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"从文件名提取日期时间失败: {fileName}");
            }
            
            return null;
        }
        
        /// <summary>
        /// 验证日期时间是否有效
        /// </summary>
        private static bool IsValidDateTime(int year, int month, int day, int hour, int minute, int second)
        {
            if (year < 1970 || year > DateTime.Now.Year + 1) return false;
            if (month < 1 || month > 12) return false;
            if (day < 1 || day > 31) return false;
            if (hour < 0 || hour > 23) return false;
            if (minute < 0 || minute > 59) return false;
            if (second < 0 || second > 59) return false;
            
            try
            {
                new DateTime(year, month, day, hour, minute, second);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 获取媒体文件的拍摄时间
        /// </summary>
        public static DateTime? GetMediaCaptureTime(string filePath, string fileName, ILogger _logger)
        {
            DateTime? captureTime = null;
            string fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            
            // 判断是图片还是视频文件
            if (fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png" || 
                fileExtension == ".gif" || fileExtension == ".webp")
            {
                // 尝试从图片EXIF中获取拍摄时间
                captureTime = ExtractImageCaptureTime(filePath, _logger);
            }
            else if (fileExtension == ".mp4" || fileExtension == ".mov")
            {
                // 尝试从视频文件中获取拍摄时间
                captureTime = ExtractVideoCaptureTime(filePath, _logger);
            }
            
            // 如果无法从媒体元数据中获取拍摄时间，尝试从文件名中提取
            if (captureTime == null)
            {
                captureTime = ExtractDateTimeFromFileName(Path.GetFileNameWithoutExtension(fileName), _logger);
            }
            
            return captureTime;
        }

        public static DateTime? GetMediaCaptureTime(string filePath,  ILogger _logger)
        {
            return GetMediaCaptureTime(filePath, filePath, _logger);
        }
    }
}
