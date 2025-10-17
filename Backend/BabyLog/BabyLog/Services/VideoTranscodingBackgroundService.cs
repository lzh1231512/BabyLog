using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace BabyLog.Services
{
    public class VideoTranscodingBackgroundService
    {
        private readonly ILogger<VideoTranscodingBackgroundService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly VideoTranscodingService _transcodingService;
        private readonly IRecurringJobManager _recurringJobManager;  // 添加此行

        public VideoTranscodingBackgroundService(
            ILogger<VideoTranscodingBackgroundService> logger, 
            IWebHostEnvironment env,
            VideoTranscodingService transcodingService,
            IRecurringJobManager recurringJobManager)  // 添加此参数
        {
            _logger = logger;
            _env = env;
            _transcodingService = transcodingService;
            _recurringJobManager = recurringJobManager;  // 添加此行
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task ProcessVideoTranscodingTasks()
        {
            try
            {
                _logger.LogInformation("Starting video transcoding task processing");
                
                var tasksDir = Path.Combine(_env.ContentRootPath, "Tasks");
                
                // Ensure tasks directory exists
                if (!Directory.Exists(tasksDir))
                {
                    Directory.CreateDirectory(tasksDir);
                    return; // No tasks to process
                }

                // Get all task files
                var taskFiles = Directory.GetFiles(tasksDir);
                if (taskFiles.Length == 0)
                {
                    _logger.LogInformation("No transcoding tasks found");
                    return;
                }

                _logger.LogInformation($"Found {taskFiles.Length} pending transcoding tasks");

                foreach (var taskFile in taskFiles)
                {
                    var fileName = Path.GetFileName(taskFile);
                    
                    // Parse task filename to get event ID and video filename
                    // Format: [id]_[FileName]
                    var match = Regex.Match(fileName, @"^(\d+)_(.+)$");
                    if (!match.Success)
                    {
                        _logger.LogWarning($"Invalid task filename format: {fileName}");
                        continue;
                    }

                    int eventId = int.Parse(match.Groups[1].Value);
                    string videoFileName = match.Groups[2].Value;

                    _logger.LogInformation($"Processing transcoding task for Event ID: {eventId}, Video: {videoFileName}");

                    // Check if source video exists
                    var sourceVideoPath = Path.Combine(_env.ContentRootPath, "Events", eventId.ToString(), videoFileName);
                    if (!File.Exists(sourceVideoPath))
                    {
                        _logger.LogWarning($"Source video file not found: {sourceVideoPath}");
                        File.Delete(taskFile); // Delete invalid task
                        continue;
                    }

                    // Process the transcoding
                    bool success = await _transcodingService.TranscodeVideoToM3U8(eventId, videoFileName);

                    if (success)
                    {
                        // Delete the task file as it's completed
                        File.Delete(taskFile);
                        _logger.LogInformation($"Transcoding task completed and removed: {fileName}");
                    }
                    else
                    {
                        _logger.LogError($"Transcoding failed for: {fileName}");
                        // The task file remains for retry
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing video transcoding tasks");
                throw; // Let Hangfire retry
            }
        }
        
        [AutomaticRetry(Attempts = 0)] // Don't retry this job
        public void ScheduleRecurringTranscodingTasks()
        {
            // 使用注入的 IRecurringJobManager 而不是静态 RecurringJob 类
            _recurringJobManager.AddOrUpdate(
                "process-video-transcoding-tasks",
                () => ProcessVideoTranscodingTasks(),
                Cron.Minutely);

            _logger.LogInformation("Scheduled recurring video transcoding tasks");
        }
    }
}