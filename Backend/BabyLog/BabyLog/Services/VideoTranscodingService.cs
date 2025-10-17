using System;
using System.IO;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace BabyLog.Services
{
    public class VideoTranscodingService
    {
        private readonly ILogger<VideoTranscodingService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly bool _enableDebugMode;
        private readonly int _debugModeVideoDuration;
        private readonly int _standardCrf;
        private readonly int _debugCrf;
        private readonly string _standardPreset;
        private readonly string _debugPreset;
        private readonly int _standardAudioBitrate;
        private readonly int _debugAudioBitrate;
        private readonly string _standardResolution;
        private readonly string _debugResolution;

        public VideoTranscodingService(ILogger<VideoTranscodingService> logger, IWebHostEnvironment env, IConfiguration configuration)
        {
            _logger = logger;
            _env = env;
            _configuration = configuration; // ����ʵ��δ��ֵ

            // ����ת������
            _enableDebugMode = _configuration.GetValue<bool>("VideoTranscoding:EnableDebugMode", false);
            _debugModeVideoDuration = _configuration.GetValue<int>("VideoTranscoding:DebugModeVideoDuration", 3);
            _standardCrf = _configuration.GetValue<int>("VideoTranscoding:StandardCrf", 21);
            _debugCrf = _configuration.GetValue<int>("VideoTranscoding:DebugCrf", 30);
            _standardPreset = _configuration.GetValue<string>("VideoTranscoding:StandardPreset", "medium");
            _debugPreset = _configuration.GetValue<string>("VideoTranscoding:DebugPreset", "ultrafast");
            _standardAudioBitrate = _configuration.GetValue<int>("VideoTranscoding:StandardAudioBitrate", 128);
            _debugAudioBitrate = _configuration.GetValue<int>("VideoTranscoding:DebugAudioBitrate", 64);
            _standardResolution = _configuration.GetValue<string>("VideoTranscoding:StandardResolution", "");
            _debugResolution = _configuration.GetValue<string>("VideoTranscoding:DebugResolution", "480:-2");

            _logger.LogInformation($"��Ƶת�����ģʽ: {(_enableDebugMode ? "����" : "����")}");
            if (_enableDebugMode)
            {
                _logger.LogInformation($"����ģʽ����: ʱ��={_debugModeVideoDuration}��, CRF={_debugCrf}, Ԥ��={_debugPreset}, ��Ƶ������={_debugAudioBitrate}, �ֱ���={_debugResolution}");
            }
        }

        // �޸� CS0029 ����ffmpegArgs = ffmpegArgs.OutputToFile(...) ���ص��� FFMpegArgumentProcessor�������� FFMpegArguments��
        // Ӧ�� ffmpegArgs ����������Ϊ FFMpegArgumentProcessor�����Һ������� ProcessAsynchronously() ҲӦʹ�ø����͡�

        public async Task<bool> TranscodeVideoToM3U8(int eventId, string fileName)
        {
            try
            {
                var sourceVideoPath = Path.Combine(_env.ContentRootPath, "Events", eventId.ToString(), fileName);
                var targetFolderPath = Path.Combine(_env.WebRootPath, "m3u8", eventId.ToString(), Path.GetFileNameWithoutExtension(fileName));
                if (!Directory.Exists(targetFolderPath))
                {
                    Directory.CreateDirectory(targetFolderPath);
                }
                var targetM3U8Path = Path.Combine(targetFolderPath, "index.m3u8");

                _logger.LogInformation($"Starting transcoding video: {sourceVideoPath} to HLS format: {targetM3U8Path}");
                _logger.LogInformation($"ת��ģʽ: {(_enableDebugMode ? "����" : "��׼")}");

                // �������� ffmpegProcessor ������ ffmpegArgs
                FFMpegArgumentProcessor ffmpegProcessor;

                if (_enableDebugMode)
                {
                    ffmpegProcessor = FFMpegArguments
                        .FromFileInput(sourceVideoPath)
                        .OutputToFile(targetM3U8Path, true, options => options
                            .WithDuration(TimeSpan.FromSeconds(_debugModeVideoDuration))
                            .WithVideoCodec("libx264")
                            .WithConstantRateFactor(_debugCrf)
                            .WithCustomArgument($"-preset {_debugPreset}")
                            .WithAudioCodec("aac")
                            .WithAudioBitrate(_debugAudioBitrate)
                            .WithCustomArgument(_debugResolution.Length > 0 ? $"-vf scale={_debugResolution}" : "")
                            .WithCustomArgument("-hls_time 3 -hls_list_size 0 " +
                                                $"-hls_segment_filename \"{targetFolderPath}/segment_%03d.ts\""));
                    _logger.LogInformation($"ʹ�õ���ģʽת�����: ʱ��={_debugModeVideoDuration}��, CRF={_debugCrf}, Ԥ��={_debugPreset}, ��Ƶ������={_debugAudioBitrate}kbps");
                }
                else
                {
                    ffmpegProcessor = FFMpegArguments
                        .FromFileInput(sourceVideoPath)
                        .OutputToFile(targetM3U8Path, true, options => options
                            .WithVideoCodec("libx264")
                            .WithConstantRateFactor(_standardCrf)
                            .WithCustomArgument($"-preset {_standardPreset}")
                            .WithAudioCodec("aac")
                            .WithAudioBitrate(_standardAudioBitrate)
                            .WithCustomArgument(_standardResolution.Length > 0 ? $"-vf scale={_standardResolution}" : "")
                            .WithCustomArgument("-hls_time 10 -hls_list_size 0 " +
                                                $"-hls_segment_filename \"{targetFolderPath}/segment_%03d.ts\""));
                    _logger.LogInformation($"ʹ�ñ�׼ģʽת�����: CRF={_standardCrf}, Ԥ��={_standardPreset}, ��Ƶ������={_standardAudioBitrate}kbps");
                }

                // ִ��ת��
                bool success = await ffmpegProcessor.ProcessAsynchronously();

                if (success)
                {
                    _logger.LogInformation($"Successfully transcoded video to HLS: {targetM3U8Path}");
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