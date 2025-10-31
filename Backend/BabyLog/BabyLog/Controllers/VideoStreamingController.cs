using BabyLog.Attributes;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BabyLog.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VideoStreamingController : ControllerBase
    {
        private readonly ILogger<VideoStreamingController> _logger;
        private readonly IWebHostEnvironment _env;

        public VideoStreamingController(ILogger<VideoStreamingController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        [HttpGet("check/{eventId}/{fileName}")]
        [APIAuthorize]
        public IActionResult CheckVideoTranscoding(int eventId, string fileName)
        {
            try
            {
                // Check if the video has been transcoded already
                var hlsPath = Path.Combine(_env.WebRootPath, "m3u8", eventId.ToString(), 
                    Path.GetFileNameWithoutExtension(fileName), "index.m3u8");
                
                bool isTranscoded = System.IO.File.Exists(hlsPath);
                
                // Check if there's a processing task for this video
                var taskPath = Path.Combine(_env.ContentRootPath, "Tasks", $"{eventId}_{fileName}");
                bool isProcessing = System.IO.File.Exists(taskPath);
                
                return Ok(new BabyLog.Models.ApiResponse<object>
                {
                    Success = true,
                    Data = new 
                    {
                        IsTranscoded = isTranscoded,
                        IsProcessing = isProcessing,
                        HlsUrl = isTranscoded ? $"/m3u8/{eventId}/{Path.GetFileNameWithoutExtension(fileName)}/index.m3u8" : null
                    },
                    Message = isTranscoded ? "视频已转码完成" : 
                              isProcessing ? "视频正在转码中" : 
                              "视频未转码"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking video transcoding status for Event ID: {eventId}, Video: {fileName}");
                return StatusCode(500, new BabyLog.Models.ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "检查视频转码状态失败"
                });
            }
        }

        [HttpGet("/ok")]
        public async Task<IActionResult> OK()
        {
            // 判断url是否是localhost，如果是的话，延迟0.3s
            var host = HttpContext.Request.Host.Host;
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(300);
            }
            return Ok(new { Ok = "ojbk" });
        }

        [APIAuthorize]
        [HttpGet("/login")]
        public async Task<IActionResult> Login()
        {
            return Ok(new { Ok = "ojbk" });
        }

    }
}