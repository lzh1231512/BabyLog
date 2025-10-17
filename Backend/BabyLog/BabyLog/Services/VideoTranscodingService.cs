using System;
using System.IO;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;

namespace BabyLog.Services
{
    public class VideoTranscodingService
    {
        private readonly ILogger<VideoTranscodingService> _logger;
        private readonly IWebHostEnvironment _env;

        public VideoTranscodingService(ILogger<VideoTranscodingService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public async Task<bool> TranscodeVideoToM3U8(int eventId, string fileName)
        {
            try
            {
                // Source video path
                var sourceVideoPath = Path.Combine(_env.ContentRootPath, "Events", eventId.ToString(), fileName);
                
                // Target m3u8 folder path
                var targetFolderPath = Path.Combine(_env.WebRootPath, "m3u8", eventId.ToString(), Path.GetFileNameWithoutExtension(fileName));
                
                // Create target directory if it doesn't exist
                if (!Directory.Exists(targetFolderPath))
                {
                    Directory.CreateDirectory(targetFolderPath);
                }

                // Target m3u8 file path
                var targetM3U8Path = Path.Combine(targetFolderPath, "index.m3u8");

                _logger.LogInformation($"Starting transcoding video: {sourceVideoPath} to HLS format: {targetM3U8Path}");

                // FFmpeg arguments for HLS conversion
                var arguments = $"-i \"{sourceVideoPath}\" -c:v libx264 -crf 21 -preset medium -c:a aac -b:a 128k " +
                              $"-hls_time 10 -hls_list_size 0 -hls_segment_filename \"{targetFolderPath}/segment_%03d.ts\" " +
                              $"\"{targetM3U8Path}\"";

                // Run FFmpeg with the arguments
                bool success = await FFMpegArguments
                    .FromFileInput(sourceVideoPath)
                    .OutputToFile(targetM3U8Path, true, options => options
                        .WithVideoCodec("libx264")
                        .WithConstantRateFactor(21)
                        .WithAudioCodec("aac")
                        .WithAudioBitrate(128)
                        .WithCustomArgument("-hls_time 10 -hls_list_size 0 " +
                                          $"-hls_segment_filename \"{targetFolderPath}/segment_%03d.ts\""))
                    .ProcessAsynchronously();

                if (success)
                {
                    _logger.LogInformation($"Successfully transcoded video to HLS: {targetM3U8Path}");
                    
                    // Create processed marker file
                    var processedMarkerPath = Path.Combine(_env.ContentRootPath, "Events", eventId.ToString(), $"{fileName}.processed");
                    File.WriteAllText(processedMarkerPath, DateTime.UtcNow.ToString("o"));
                }
                else
                {
                    _logger.LogError($"Failed to transcode video to HLS: {targetM3U8Path}");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error transcoding video to M3U8: Event ID: {eventId}, File: {fileName}");
                return false;
            }
        }
    }
}