using BabyLog.Commons;
using BabyLog.Models;
using FFMpegCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BabyLog.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public class FilesController : ControllerBase
    {
        private readonly ILogger<FilesController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly long _fileSizeLimit;
        private readonly string[] _permittedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mov", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".mp3", ".wav" };

        public FilesController(ILogger<FilesController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
            _fileSizeLimit = long.MaxValue; // No limit, or set to specific value
        }

        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "未收到有效文件"
                    });
                }

                // Clean up old files first
                CleanUpOldFiles();

                // Ensure TempFile directory exists
                var tempFileDirectory = Path.Combine(_env.ContentRootPath, "TempFile");
                if (!Directory.Exists(tempFileDirectory))
                {
                    Directory.CreateDirectory(tempFileDirectory);
                }

                // Generate unique filename
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                string randomStr = Path.GetRandomFileName().Replace(".", "").Substring(0, 6);
                string extension = Path.GetExtension(file.FileName);
                string serverFileName = $"{timestamp}_{randomStr}{extension}";
                string fullPath = Path.Combine(tempFileDirectory, serverFileName);

                // Use a file stream to save directly to disk
                using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    await file.CopyToAsync(stream);
                }

                // Return success response with file info
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        OriginalName = file.FileName,
                        ServerFileName = serverFileName,
                        Size = file.Length,
                        Type = file.ContentType,
                        UploadTime = DateTime.Now.ToString("o")
                    },
                    Message = "文件上传成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = $"文件上传失败: {ex.Message}"
                });
            }
        }

        [HttpPost("upload-multiple")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> UploadFiles(List<IFormFile> files)
        {
            try
            {
                if (files == null || files.Count == 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "未收到有效文件"
                    });
                }

                // Clean up old files first
                CleanUpOldFiles();

                // Ensure TempFile directory exists
                var tempFileDirectory = Path.Combine(_env.ContentRootPath, "TempFile");
                if (!Directory.Exists(tempFileDirectory))
                {
                    Directory.CreateDirectory(tempFileDirectory);
                }

                var successful = new List<object>();
                var failed = 0;

                foreach (var file in files)
                {
                    try
                    {
                        if (file.Length > 0)
                        {
                            // Generate unique filename
                            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                            string randomStr = Path.GetRandomFileName().Replace(".", "").Substring(0, 6);
                            string extension = Path.GetExtension(file.FileName);
                            string serverFileName = $"{timestamp}_{randomStr}{extension}";
                            string fullPath = Path.Combine(tempFileDirectory, serverFileName);

                            // Use a file stream to save directly to disk
                            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                            {
                                await file.CopyToAsync(stream);
                            }

                            // Add to successful list
                            successful.Add(new
                            {
                                OriginalName = file.FileName,
                                ServerFileName = serverFileName,
                                Size = file.Length,
                                Type = file.ContentType,
                                UploadTime = DateTime.Now.ToString("o")
                            });
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to upload file: {file.FileName}");
                        failed++;
                    }
                }

                string message = failed == 0
                    ? $"成功上传 {successful.Count} 个文件"
                    : $"上传完成：成功 {successful.Count} 个，失败 {failed} 个";

                return Ok(new ApiResponse<object>
                {
                    Success = failed == 0,
                    Data = new
                    {
                        Successful = successful,
                        Failed = failed
                    },
                    Message = message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple files");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = $"批量上传失败: {ex.Message}"
                });
            }
        }

        [HttpGet("download")]
        public IActionResult DownloadFile([FromQuery] int? id, [FromQuery] string fileName, [FromQuery] bool thumbnail = false)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "文件名不能为空"
                    });
                }

                string filePath = null;
                string contentType = null;
                string fileNameToUse = fileName;
                bool isVideoFile = IsVideoFile(fileName);

                // If thumbnail is requested for a video, we need to look for a PNG file
                if (thumbnail && isVideoFile)
                {
                    fileNameToUse = Path.GetFileNameWithoutExtension(fileName) + ".png";
                }

                // First check if thumbnail is requested and exists
                if (thumbnail && id.HasValue)
                {
                    var thumbnailDirectory = Path.Combine(_env.ContentRootPath, "Thumbnail", id.Value.ToString());
                    var thumbnailPath = Path.Combine(thumbnailDirectory, fileNameToUse);
                    if (System.IO.File.Exists(thumbnailPath))
                    {
                        filePath = thumbnailPath;
                        contentType = GetContentType(fileNameToUse);
                    }
                }

                // If no thumbnail found, check for original file
                if (filePath == null)
                {
                    // Reset to original filename for finding the source file
                    fileNameToUse = fileName;

                    // Check TempFile directory
                    var tempFilePath = Path.Combine(_env.ContentRootPath, "TempFile", fileNameToUse);
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        filePath = tempFilePath;
                        contentType = GetContentType(fileNameToUse);
                    }
                    // If not found and id is provided, check the Events directory
                    else if (id.HasValue)
                    {
                        var eventsFilePath = Path.Combine(_env.ContentRootPath, "Events", id.Value.ToString(), fileNameToUse);
                        if (System.IO.File.Exists(eventsFilePath))
                        {
                            filePath = eventsFilePath;
                            contentType = GetContentType(fileNameToUse);
                        }
                    }

                    // Generate thumbnail if requested for image/video files
                    if (filePath != null && thumbnail && id.HasValue && IsMediaFile(fileNameToUse))
                    {
                        try
                        {
                            var thumbnailDirectory = Path.Combine(_env.ContentRootPath, "Thumbnail", id.Value.ToString());
                            Directory.CreateDirectory(thumbnailDirectory);

                            // For videos, use .png extension for the thumbnail
                            string thumbnailFileName = isVideoFile
                                ? Path.GetFileNameWithoutExtension(fileNameToUse) + ".png"
                                : fileNameToUse;

                            var thumbnailPath = Path.Combine(thumbnailDirectory, thumbnailFileName);

                            // Generate thumbnail if it doesn't exist
                            if (!System.IO.File.Exists(thumbnailPath))
                            {
                                GenerateThumbnail(filePath, thumbnailPath);
                            }

                            // Use thumbnail if successfully created
                            if (System.IO.File.Exists(thumbnailPath))
                            {
                                filePath = thumbnailPath;
                                contentType = GetContentType(thumbnailFileName);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error generating thumbnail for {fileNameToUse}");
                            // Continue with original file if thumbnail generation fails
                        }
                    }
                }

                if (filePath != null)
                {
                    // Update last access time for cleanup tracking
                    new FileInfo(filePath).LastAccessTime = DateTime.Now;

                    // Return the file
                    var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    return File(fileStream, contentType, Path.GetFileName(filePath));
                }

                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "文件不存在"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading file: {fileName}");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "文件下载失败"
                });
            }
        }


        private bool IsVideoFile(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".mp4" || ext == ".mov";
        }

        private bool IsMediaFile(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => true,
                ".mp4" or ".mov" => true,
                _ => false
            };
        }

        private void GenerateThumbnail(string sourceFilePath, string targetFilePath)
        {
            var sourceExt = Path.GetExtension(sourceFilePath).ToLowerInvariant();

            if (sourceExt == ".jpg" || sourceExt == ".jpeg" || sourceExt == ".png" || sourceExt == ".gif" || sourceExt == ".webp")
            {
                // Generate thumbnail for images
                using var image = Image.Load(sourceFilePath);

                // Resize to 300px max width/height preserving aspect ratio
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(300, 300)
                }));

                image.Save(targetFilePath);
            }
            else if (sourceExt == ".mp4" || sourceExt == ".mov")
            {
                // For video files - extract a frame and save as PNG thumbnail
                try
                {
                    // Generate a thumbnail image at 1 second into the video
                    FFMpeg.Snapshot(sourceFilePath, targetFilePath, new System.Drawing.Size(300, 300),
                        TimeSpan.FromSeconds(1));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to generate thumbnail for video: {ex.Message}", ex);
                }
            }
        }

        private string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        private void CleanUpOldFiles()
        {
            try
            {
                var tempFileDirectory = Path.Combine(_env.ContentRootPath, "TempFile");
                if (!Directory.Exists(tempFileDirectory))
                {
                    return;
                }

                var currentDate = DateTime.Now;
                var files = Directory.GetFiles(tempFileDirectory);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    // Check if the file's last access time is older than 3 months
                    if ((currentDate - fileInfo.LastAccessTime).TotalDays > 90)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                            _logger.LogInformation($"Deleted old file: {fileInfo.Name}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Failed to delete old file: {fileInfo.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old files");
            }
        }

        [HttpGet("list")]
        public IActionResult ListFiles()
        {
            try
            {
                var tempFileDirectory = Path.Combine(_env.ContentRootPath, "TempFile");
                if (!Directory.Exists(tempFileDirectory))
                {
                    return Ok(new ApiResponse<List<object>>
                    {
                        Success = true,
                        Data = new List<object>(),
                        Message = "暂无文件"
                    });
                }

                var fileList = new List<object>();
                var files = Directory.GetFiles(tempFileDirectory);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    fileList.Add(new
                    {
                        FileName = fileInfo.Name,
                        Size = fileInfo.Length,
                        CreationTime = fileInfo.CreationTime,
                        LastAccessTime = fileInfo.LastAccessTime
                    });
                }

                return Ok(new ApiResponse<List<object>>
                {
                    Success = true,
                    Data = fileList,
                    Message = $"获取到 {fileList.Count} 个文件"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "获取文件列表失败"
                });
            }
        }

        [HttpDelete("{fileName}")]
        public IActionResult DeleteFile(string fileName)
        {
            try
            {
                var tempFileDirectory = Path.Combine(_env.ContentRootPath, "TempFile");
                var filePath = Path.Combine(tempFileDirectory, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "文件不存在"
                    });
                }

                System.IO.File.Delete(filePath);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = null,
                    Message = "文件删除成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting file: {fileName}");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "文件删除失败"
                });
            }
        }

        [HttpPost("clean")]
        public IActionResult CleanOldFiles()
        {
            try
            {
                var tempFileDirectory = Path.Combine(_env.ContentRootPath, "TempFile");
                if (!Directory.Exists(tempFileDirectory))
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Data = null,
                        Message = "无需清理，目录不存在"
                    });
                }

                var currentDate = DateTime.Now;
                var files = Directory.GetFiles(tempFileDirectory);
                int deletedCount = 0;

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    // Check if the file's last access time is older than 3 months
                    if ((currentDate - fileInfo.LastAccessTime).TotalDays > 90)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Failed to delete old file: {fileInfo.Name}");
                        }
                    }
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { DeletedCount = deletedCount },
                    Message = $"清理完成，删除了 {deletedCount} 个过期文件"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old files");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "清理过期文件失败"
                });
            }
        }
    
        [HttpGet("downloadVideo")]
        public IActionResult DownloadVideoFile([FromQuery] int? id, [FromQuery] string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "文件名不能为空"
                    });
                }

                if (!id.HasValue)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "需要指定事件ID"
                    });
                }

                // 查找视频文件路径
                string filePath = null;

                // 先检查临时文件目录
                var tempFilePath = Path.Combine(_env.ContentRootPath, "TempFile", fileName);
                if (System.IO.File.Exists(tempFilePath))
                {
                    filePath = tempFilePath;
                }
                // 再检查事件目录
                else
                {
                    var eventsFilePath = Path.Combine(_env.ContentRootPath, "Events", id.Value.ToString(), fileName);
                    if (System.IO.File.Exists(eventsFilePath))
                    {
                        filePath = eventsFilePath;
                    }
                }

                if (filePath == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "文件不存在"
                    });
                }

                // 获取文件信息和内容类型
                var fileInfo = new FileInfo(filePath);
                var fileLength = fileInfo.Length;
                var contentType = GetContentType(fileName);

                // 更新文件最后访问时间（用于清理跟踪）
                fileInfo.LastAccessTime = DateTime.Now;

                // 启用范围请求处理
                Response.Headers.Add("Accept-Ranges", "bytes");

                // 检查是否有Range请求头
                if (!Request.Headers.ContainsKey("Range"))
                {
                    // 没有Range头，返回完整文件但启用范围处理
                    return File(System.IO.File.OpenRead(filePath), contentType, enableRangeProcessing: true);
                }

                // 解析Range请求头
                var rangeHeader = Request.Headers["Range"].ToString();
                var rangeValue = rangeHeader.Replace("bytes=", "");
                var rangeParts = rangeValue.Split('-');

                // 计算开始和结束位置
                long startByte = 0;
                long endByte = fileLength - 1;

                if (rangeParts.Length > 0 && long.TryParse(rangeParts[0], out var parsedStart))
                {
                    startByte = parsedStart;
                }

                if (rangeParts.Length > 1 && !string.IsNullOrEmpty(rangeParts[1]) &&
                    long.TryParse(rangeParts[1], out var parsedEnd))
                {
                    endByte = Math.Min(parsedEnd, fileLength - 1);
                }

                // 计算返回的内容长度
                var contentLength = endByte - startByte + 1;

                // 设置响应头
                Response.ContentLength = contentLength;
                Response.StatusCode = StatusCodes.Status206PartialContent;
                Response.Headers.Add("Content-Range", $"bytes {startByte}-{endByte}/{fileLength}");

                // 打开文件并定位到请求的起始位置
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                fileStream.Seek(startByte, SeekOrigin.Begin);

                // 创建一个有限制长度的流来确保只返回所请求范围的内容
                return File(new LimitedStream(fileStream, contentLength), contentType, enableRangeProcessing: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"视频文件下载失败: {fileName}");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = $"视频文件下载失败: {ex.Message}"
                });
            }
        }
    }
        
}