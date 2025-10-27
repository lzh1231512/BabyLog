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
    public partial class FilesController : ControllerBase
    {
        private readonly ILogger<FilesController> _logger;
        private readonly IWebHostEnvironment _env;
        
        public FilesController(ILogger<FilesController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        [HttpGet("download")]
        public IActionResult DownloadFile([FromQuery] int? id, [FromQuery] string fileName, [FromQuery] bool thumbnail = false)
        {
            _logger.Log(LogLevel.Information, $"Download request received: id={id}, fileName={fileName}, thumbnail={thumbnail}");

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
                    // 更新last access time for cleanup tracking
                    new FileInfo(filePath).LastAccessTime = DateTime.Now;

                    var fileInfo = new FileInfo(filePath);
                    var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    
                    // 显式添加内容类型和内容长度头
                    Response.Headers.Add("Content-Type", contentType ?? "application/octet-stream");
                    Response.Headers.Add("Content-Length", fileInfo.Length.ToString());
                    Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{Path.GetFileName(filePath)}\"");
                    Response.Headers.Add("Accept-Ranges", "bytes");
                    
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
                    Message = "文件下载失败:"+ ex.Message
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

        public static string GetContentType(string fileName)
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
    }
        
}