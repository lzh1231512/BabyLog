using BabyLog.Models;
using FFMpegCore;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BabyLog.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly ILogger<FilesController> _logger;
        private readonly IWebHostEnvironment _env;

        public FilesController(ILogger<FilesController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        [HttpPost("upload")]
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

                // Save the file
                using (var stream = new FileStream(fullPath, FileMode.Create))
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
                    Message = "文件上传失败"
                });
            }
        }

        [HttpPost("upload-multiple")]
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

                            // Save the file
                            using (var stream = new FileStream(fullPath, FileMode.Create))
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
                    catch (Exception)
                    {
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
                    Message = "批量上传失败"
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

                // First check if thumbnail is requested and exists
                if (thumbnail && id.HasValue)
                {
                    var thumbnailDirectory = Path.Combine(_env.ContentRootPath, "Thumbnail", id.Value.ToString());
                    var thumbnailPath = Path.Combine(thumbnailDirectory, fileName);
                    if (System.IO.File.Exists(thumbnailPath))
                    {
                        filePath = thumbnailPath;
                        contentType = GetContentType(fileName);
                    }
                }

                // If no thumbnail found, check for original file
                if (filePath == null)
                {
                    // Check TempFile directory
                    var tempFilePath = Path.Combine(_env.ContentRootPath, "TempFile", fileName);
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        filePath = tempFilePath;
                        contentType = GetContentType(fileName);
                    }
                    // If not found and id is provided, check the Events directory
                    else if (id.HasValue)
                    {
                        var eventsFilePath = Path.Combine(_env.ContentRootPath, "Events", id.Value.ToString(), fileName);
                        if (System.IO.File.Exists(eventsFilePath))
                        {
                            filePath = eventsFilePath;
                            contentType = GetContentType(fileName);
                        }
                    }

                    // Generate thumbnail if requested for image/video files
                    if (filePath != null && thumbnail && id.HasValue && IsMediaFile(fileName))
                    {
                        try
                        {
                            var thumbnailDirectory = Path.Combine(_env.ContentRootPath, "Thumbnail", id.Value.ToString());
                            Directory.CreateDirectory(thumbnailDirectory);
                            
                            var thumbnailPath = Path.Combine(thumbnailDirectory, fileName);
                            
                            // Generate thumbnail if it doesn't exist
                            if (!System.IO.File.Exists(thumbnailPath))
                            {
                                GenerateThumbnail(filePath, thumbnailPath);
                            }

                            // Use thumbnail if successfully created
                            if (System.IO.File.Exists(thumbnailPath))
                            {
                                filePath = thumbnailPath;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error generating thumbnail for {fileName}");
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
                    return File(fileStream, contentType, fileName);
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
            var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            
            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".webp")
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
            else if (ext == ".mp4" || ext == ".mov")
            {
                // For video files - extract a frame and save as thumbnail
                try
                {
                    // Generate a thumbnail image at the 10% position of the video
                    FFMpeg.Snapshot(sourceFilePath, targetFilePath, new System.Drawing.Size(300, 300), 
                        TimeSpan.FromSeconds(1)); // Take snapshot from 1 second in
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
    }
}