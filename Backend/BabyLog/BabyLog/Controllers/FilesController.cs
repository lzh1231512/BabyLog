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
                        Message = "δ�յ���Ч�ļ�"
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
                    Message = "�ļ��ϴ��ɹ�"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = $"�ļ��ϴ�ʧ��: {ex.Message}"
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
                        Message = "δ�յ���Ч�ļ�"
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
                    ? $"�ɹ��ϴ� {successful.Count} ���ļ�"
                    : $"�ϴ���ɣ��ɹ� {successful.Count} ����ʧ�� {failed} ��";

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
                    Message = $"�����ϴ�ʧ��: {ex.Message}"
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
                        Message = "�ļ�������Ϊ��"
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
                    Message = "�ļ�������"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading file: {fileName}");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "�ļ�����ʧ��"
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
                        Message = "�����ļ�"
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
                    Message = $"��ȡ�� {fileList.Count} ���ļ�"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "��ȡ�ļ��б�ʧ��"
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
                        Message = "�ļ�������"
                    });
                }

                System.IO.File.Delete(filePath);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = null,
                    Message = "�ļ�ɾ���ɹ�"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting file: {fileName}");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "�ļ�ɾ��ʧ��"
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
                        Message = "��������Ŀ¼������"
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
                    Message = $"������ɣ�ɾ���� {deletedCount} �������ļ�"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old files");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "��������ļ�ʧ��"
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
                        Message = "�ļ�������Ϊ��"
                    });
                }

                if (!id.HasValue)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "��Ҫָ���¼�ID"
                    });
                }

                // ������Ƶ�ļ�·��
                string filePath = null;

                // �ȼ����ʱ�ļ�Ŀ¼
                var tempFilePath = Path.Combine(_env.ContentRootPath, "TempFile", fileName);
                if (System.IO.File.Exists(tempFilePath))
                {
                    filePath = tempFilePath;
                }
                // �ټ���¼�Ŀ¼
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
                        Message = "�ļ�������"
                    });
                }

                // ��ȡ�ļ���Ϣ����������
                var fileInfo = new FileInfo(filePath);
                var fileLength = fileInfo.Length;
                var contentType = GetContentType(fileName);

                // �����ļ�������ʱ�䣨����������٣�
                fileInfo.LastAccessTime = DateTime.Now;

                // ���÷�Χ������
                Response.Headers.Add("Accept-Ranges", "bytes");

                // ����Ƿ���Range����ͷ
                if (!Request.Headers.ContainsKey("Range"))
                {
                    // û��Rangeͷ�����������ļ������÷�Χ����
                    return File(System.IO.File.OpenRead(filePath), contentType, enableRangeProcessing: true);
                }

                // ����Range����ͷ
                var rangeHeader = Request.Headers["Range"].ToString();
                var rangeValue = rangeHeader.Replace("bytes=", "");
                var rangeParts = rangeValue.Split('-');

                // ���㿪ʼ�ͽ���λ��
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

                // ���㷵�ص����ݳ���
                var contentLength = endByte - startByte + 1;

                // ������Ӧͷ
                Response.ContentLength = contentLength;
                Response.StatusCode = StatusCodes.Status206PartialContent;
                Response.Headers.Add("Content-Range", $"bytes {startByte}-{endByte}/{fileLength}");

                // ���ļ�����λ���������ʼλ��
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                fileStream.Seek(startByte, SeekOrigin.Begin);

                // ����һ�������Ƴ��ȵ�����ȷ��ֻ����������Χ������
                return File(new LimitedStream(fileStream, contentLength), contentType, enableRangeProcessing: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"��Ƶ�ļ�����ʧ��: {fileName}");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = $"��Ƶ�ļ�����ʧ��: {ex.Message}"
                });
            }
        }
    }
        
}