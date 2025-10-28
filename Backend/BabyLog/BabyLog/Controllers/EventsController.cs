using BabyLog.Commons;
using BabyLog.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace BabyLog.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly ILogger<EventsController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly JsonSerializerOptions _jsonOptions;

        public EventsController(ILogger<EventsController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
            _jsonOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        [HttpGet]
        public async Task<IActionResult> GetEvents()
        {
            try
            {
                var events = await GetEventsFromFiles();
                var timelineData = OrganizeDataForTimeline(events);

                return Ok(new BabyLog.Models.ApiResponse<List<TimelineGroup>>
                {
                    Success = true,
                    Data = timelineData,
                    Message = "获取事件列表成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving events");
                return StatusCode(500, new BabyLog.Models.ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "获取事件列表失败"
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

        //[HttpGet("test")]
        //public async Task<IActionResult> Test()
        //{
        //    var eventFilePath = Path.Combine(_env.ContentRootPath, "Events", $"1.json");
        //    var jsonContent = await System.IO.File.ReadAllTextAsync(eventFilePath);
        //    var eventData = JsonSerializer.Deserialize<Event>(jsonContent, _jsonOptions);
        //    await UpdateDateFromOtherEvent(eventData);
        //    jsonContent = JsonSerializer.Serialize(eventData, _jsonOptions);
        //    await System.IO.File.WriteAllTextAsync(eventFilePath, jsonContent, Encoding.UTF8);
        //    return Ok(eventData);
        //}

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEventById(int id)
        {
            try
            {
                var eventFilePath = Path.Combine(_env.ContentRootPath, "Events", $"{id}.json");
                if (!System.IO.File.Exists(eventFilePath))
                {
                    return NotFound(new BabyLog.Models.ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "事件不存在"
                    });
                }

                var jsonContent = await System.IO.File.ReadAllTextAsync(eventFilePath);
                var eventData = JsonSerializer.Deserialize<Event>(jsonContent, _jsonOptions);

                return Ok(new BabyLog.Models.ApiResponse<Event>
                {
                    Success = true,
                    Data = eventData,
                    Message = "获取事件详情成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving event with ID {id}");
                return StatusCode(500, new BabyLog.Models.ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "获取事件详情失败"
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] Event eventData)
        {
            try
            {
                // Ensure Events directory exists
                var eventsDirectory = Path.Combine(_env.ContentRootPath, "Events");
                if (!Directory.Exists(eventsDirectory))
                {
                    Directory.CreateDirectory(eventsDirectory);
                }

                // Get the next available ID
                int newId = await GetNextEventId();
                
                // Set the new ID to the event data
                eventData.Id = newId;

                // Process media files - move from TempFile to Events/[id] folder
                eventData = await ProcessMediaFiles(eventData);

                // Create transcoding tasks for videos
                await CreateTranscodingTasksForVideos(eventData);

                // Save the event as JSON
                var eventFilePath = Path.Combine(eventsDirectory, $"{newId}.json");
                var jsonContent = JsonSerializer.Serialize(eventData, _jsonOptions);
                await System.IO.File.WriteAllTextAsync(eventFilePath, jsonContent, Encoding.UTF8);

                return Ok(new BabyLog.Models.ApiResponse<Event>
                {
                    Success = true,
                    Data = eventData,
                    Message = "创建事件成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating event");
                return StatusCode(500, new BabyLog.Models.ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "创建事件失败"
                });
            }
        }

        [HttpPost("update/{id}")]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] Event eventData)
        {
            try
            {
                var eventsDirectory = Path.Combine(_env.ContentRootPath, "Events");
                var eventFilePath = Path.Combine(eventsDirectory, $"{id}.json");

                if (!System.IO.File.Exists(eventFilePath))
                {
                    return NotFound(new BabyLog.Models.ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "事件不存在"
                    });
                }

                // Ensure the ID in the path matches the ID in the event data
                eventData.Id = id;

                // Process media files - move from TempFile to Events/[id] folder
                eventData = await ProcessMediaFiles(eventData);

                // Create transcoding tasks for videos
                await CreateTranscodingTasksForVideos(eventData);

                // Save the updated event as JSON
                var jsonContent = JsonSerializer.Serialize(eventData, _jsonOptions);
                await System.IO.File.WriteAllTextAsync(eventFilePath, jsonContent, Encoding.UTF8);

                return Ok(new BabyLog.Models.ApiResponse<Event>
                {
                    Success = true,
                    Data = eventData,
                    Message = "更新事件成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating event with ID {id}");
                return StatusCode(500, new BabyLog.Models.ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "更新事件失败"
                });
            }
        }

        [HttpPost("delete/{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            try
            {
                var eventsDirectory = Path.Combine(_env.ContentRootPath, "Events");
                var eventFilePath = Path.Combine(eventsDirectory, $"{id}.json");

                if (!System.IO.File.Exists(eventFilePath))
                {
                    return NotFound(new BabyLog.Models.ApiResponse<object>
                    {
                        Success = false,
                        Data = null,
                        Message = "事件不存在"
                    });
                }

                // Delete the JSON file
                System.IO.File.Delete(eventFilePath);

                // Check if the event's media directory exists and delete it
                var eventMediaDir = Path.Combine(eventsDirectory, id.ToString());
                if (Directory.Exists(eventMediaDir))
                {
                    Directory.Delete(eventMediaDir, true);
                }

                // Delete any HLS transcoded files
                var hlsDir = Path.Combine(_env.WebRootPath, "m3u8", id.ToString());
                if (Directory.Exists(hlsDir))
                {
                    Directory.Delete(hlsDir, true);
                }

                // Delete any pending transcoding tasks
                var tasksDir = Path.Combine(_env.ContentRootPath, "Tasks");
                if (Directory.Exists(tasksDir))
                {
                    var taskFiles = Directory.GetFiles(tasksDir, $"{id}_*");
                    foreach (var taskFile in taskFiles)
                    {
                        System.IO.File.Delete(taskFile);
                    }
                }

                return Ok(new BabyLog.Models.ApiResponse<object>
                {
                    Success = true,
                    Data = null,
                    Message = "删除事件成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting event with ID {id}");
                return StatusCode(500, new BabyLog.Models.ApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Message = "删除事件失败"
                });
            }
        }

        private async Task CreateTranscodingTasksForVideos(Event eventData)
        {
            if (eventData?.Media?.Videos == null || !eventData.Media.Videos.Any())
                return;
                
            // Ensure Tasks directory exists
            var tasksDirectory = Path.Combine(_env.ContentRootPath, "Tasks");
            if (!Directory.Exists(tasksDirectory))
            {
                Directory.CreateDirectory(tasksDirectory);
            }
            
            foreach (var video in eventData.Media.Videos)
            {
                if (string.IsNullOrEmpty(video.FileName))
                    continue;
                    
                // Check if video file exists in the event directory
                var videoPath = Path.Combine(_env.ContentRootPath, "Events", eventData.Id.ToString(), video.FileName);
                if (!System.IO.File.Exists(videoPath))
                    continue;
                    
                // Check if this video is already processed
                var processedMarkerPath = Path.Combine(_env.ContentRootPath, "Events", "VideoFlag", eventData.Id.ToString(), $"{video.FileName}.processed");
                if (System.IO.File.Exists(processedMarkerPath))
                    continue;
                    
                // Check if there's already a task for this video
                var taskFilePath = Path.Combine(tasksDirectory, $"{eventData.Id}_{video.FileName}");
                if (System.IO.File.Exists(taskFilePath))
                    continue;
                    
                // Create a task file for this video
                await System.IO.File.WriteAllTextAsync(taskFilePath, DateTime.UtcNow.ToString("o"));
                _logger.LogInformation($"Created transcoding task for Event ID: {eventData.Id}, Video: {video.FileName}");
            }
        }

        private async Task<Event> ProcessMediaFiles(Event eventData)
        {
            if (eventData?.Media == null)
                return eventData;

            // Create the event-specific media directory
            var eventMediaDir = Path.Combine(_env.ContentRootPath, "Events", eventData.Id.ToString());
            if (!Directory.Exists(eventMediaDir))
            {
                Directory.CreateDirectory(eventMediaDir);
            }

            // Process all media types (images, videos, audios)
            if (eventData.Media.Images != null)
            {
                await ProcessMediaItems(eventData.Media.Images, eventMediaDir,true,false);
            }
            
            if (eventData.Media.Videos != null)
            {
                await ProcessMediaItems(eventData.Media.Videos, eventMediaDir, false, true);
            }
            
            if (eventData.Media.Audios != null)
            {
                await ProcessMediaItems(eventData.Media.Audios, eventMediaDir, false, false);
            }
            eventData.IsDateValid =
                eventData.Media.Images.Any(f => f.CaptureTime.HasValue) ||
                eventData.Media.Videos.Any(f => f.CaptureTime.HasValue);
            if(!eventData.IsDateValid)
            {
                bool updated = await UpdateDateFromOtherEvent(eventData);
                eventData.IsDateValid = updated;
            }
            return eventData;
        }

        private async Task ProcessMediaItems(List<MediaItem> mediaItems, string targetDirectory,
            bool isImg, bool isVideo)
        {
            if (mediaItems == null || mediaItems.Count == 0)
                return;

            var tempFileDir = Path.Combine(_env.ContentRootPath, "TempFile");
            
            foreach (var mediaItem in mediaItems)
            {
                if (string.IsNullOrEmpty(mediaItem.FileName))
                    continue;

                var tempFilePath = Path.Combine(tempFileDir, mediaItem.FileName);
                var targetFilePath = Path.Combine(targetDirectory, mediaItem.FileName);

                // Check if the file exists in the TempFile folder
                if (System.IO.File.Exists(tempFilePath))
                {
                    // Copy file from temp to event folder
                    await CopyFileAsync(tempFilePath, targetFilePath);
                    
                    // Delete the file from temp folder after copying
                    try
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to delete temp file after transfer: {tempFilePath}");
                    }
                }
                if (isImg || isVideo)
                {
                    if (string.IsNullOrEmpty(mediaItem.Hash))
                    {
                        mediaItem.Hash = isImg? PHashHelper.CalculateImageHashString(targetFilePath):
                            PHashHelper.CalculateVideoHashString(targetFilePath);
                    }
                    if (mediaItem.CaptureTime == null)
                    {
                        mediaItem.CaptureTime= ChunkController.GetMediaCaptureTime(targetFilePath, _logger);
                    }
                }
            }
        }

        private async Task CopyFileAsync(string sourceFilePath, string destinationFilePath)
        {
            using (FileStream sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (FileStream destinationStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }
        }

        private async Task<int> GetNextEventId()
        {
            var events = await GetEventsFromFiles();
            
            if (events.Count == 0)
                return 1;
                
            return events.Max(e => e.Id) + 1;
        }

        private async Task<List<Event>> GetEventsFromFiles()
        {
            var events = new List<Event>();
            var eventsDirectory = Path.Combine(_env.ContentRootPath, "Events");
            
            if (!Directory.Exists(eventsDirectory))
            {
                _logger.LogWarning("Events directory not found");
                return events;
            }

            var jsonFiles = Directory.GetFiles(eventsDirectory, "*.json");

            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    // Skip index.json if it exists
                    if (Path.GetFileName(jsonFile).Equals("index.json", StringComparison.OrdinalIgnoreCase))
                        continue;
                        
                    var jsonContent = await System.IO.File.ReadAllTextAsync(jsonFile);
                    var eventData = JsonSerializer.Deserialize<Event>(jsonContent, _jsonOptions);
                    
                    if (eventData != null)
                    {
                        events.Add(eventData);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error parsing JSON file: {jsonFile}");
                    // Continue with other files even if one fails
                }
            }

            return events.OrderBy(e => e.Id).ToList();
        }

        private List<TimelineGroup> OrganizeDataForTimeline(List<Event> events)
        {
            var grouped = new Dictionary<string, TimelineGroup>();
            var birthDate = new DateTime(2025, 5, 9); // Assuming this is the birth date based on example data

            foreach (var eventItem in events)
            {
                if (!DateTime.TryParse(eventItem.Date, out DateTime eventDate))
                {
                    _logger.LogWarning($"Invalid date format in event ID {eventItem.Id}: {eventItem.Date}");
                    continue;
                }

                // Calculate age
                var totalMonths = ((eventDate.Year - birthDate.Year) * 12) + 
                                  (eventDate.Month - birthDate.Month);

                string ageKey, ageLabel, dateLabel;

                if (totalMonths == 0)
                {
                    ageKey = "0-birth";
                    ageLabel = "出生时";
                    dateLabel = "2025年5月9日";
                }
                else if (totalMonths < 12)
                {
                    ageKey = $"{totalMonths}-months";
                    ageLabel = $"{totalMonths}月龄";
                    dateLabel = eventDate.ToString("yyyy年M月", CultureInfo.GetCultureInfo("zh-CN"));
                }
                else
                {
                    int years = totalMonths / 12;
                    int remainingMonths = totalMonths % 12;
                    if (remainingMonths == 0)
                    {
                        ageKey = $"{years}-years";
                        ageLabel = $"{years}周岁";
                    }
                    else
                    {
                        ageKey = $"{years}-{remainingMonths}-years-months";
                        ageLabel = $"{years}岁{remainingMonths}月";
                    }
                    dateLabel = eventDate.ToString("yyyy年M月", CultureInfo.GetCultureInfo("zh-CN"));
                }

                if (!grouped.ContainsKey(ageKey))
                {
                    grouped[ageKey] = new TimelineGroup
                    {
                        Age = ageLabel,
                        Date = dateLabel,
                        Events = new List<Event>()
                    };
                }

                grouped[ageKey].Events.Add(eventItem);
            }

            // Sort by date
            return grouped.Values
                .OrderBy(g => g.Events.FirstOrDefault()?.Date ?? string.Empty)
                .ToList();
        }
        async Task<bool> UpdateDateFromOtherEvent(Event eve)
        {
            bool updated = false;
            var allEvents = await GetEventsFromFiles();
            if (allEvents == null || allEvents.Count == 0) return false;
            // 排除自身
            allEvents = allEvents.Where(e => e.Id != eve.Id).ToList();
            var mediaTypes = new[] { (eve.Media?.Images, true), (eve.Media?.Videos, false) };
            foreach (var (mediaList, isImg) in mediaTypes)
            {
                if (mediaList == null) continue;
                for (int i = 0; i < mediaList.Count; i++)
                {
                    var item = mediaList[i];
                    if (string.IsNullOrEmpty(item.Hash)) continue;
                    MediaItem bestMatch = null;
                    long bestDist = 100;
                    int beatID = -1;
                    DateTime? bestCaptureTime = null;
                    foreach (var otherEvent in allEvents)
                    {
                        var otherMediaList = isImg ? otherEvent.Media?.Images : otherEvent.Media?.Videos;
                        if (otherMediaList == null) continue;
                        foreach (var otherItem in otherMediaList)
                        {
                            if (string.IsNullOrEmpty(otherItem.Hash) || otherItem.CaptureTime == null) continue;
                            // hash相近判断，可用Levenshtein距离或直接相等
                            var hammingDist = PHashHelper.HammingDistance(item.Hash, otherItem.Hash,isImg);
                            if (hammingDist <= 5)
                            {
                                var otherPath = Path.Combine(_env.ContentRootPath, "Events", otherEvent.Id.ToString(), otherItem.FileName);
                                if (System.IO.File.Exists(otherPath))
                                {
                                    if (hammingDist < bestDist)
                                    {
                                        bestMatch = otherItem;
                                        bestDist = hammingDist;
                                        bestCaptureTime = otherItem.CaptureTime;
                                        beatID = otherEvent.Id;
                                    }
                                }
                            }
                        }
                    }
                    // 替换为更大的文件
                    if (bestMatch != null)
                    {
                        var curPath = Path.Combine(_env.ContentRootPath, "Events", eve.Id.ToString(), item.FileName);
                        var bestPath = Path.Combine(_env.ContentRootPath, "Events", beatID.ToString(), bestMatch.FileName);
                        if (System.IO.File.Exists(bestPath))
                        {
                            var curSize = System.IO.File.Exists(curPath) ? new FileInfo(curPath).Length : 0;
                            if (new FileInfo(bestPath).Length > curSize)
                            {
                                try
                                {
                                    System.IO.File.Copy(bestPath, curPath, true);
                                    _logger?.LogInformation($"Replaced media file {item.FileName} with larger file from event {bestMatch.FileName}");
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogWarning(ex, $"Failed to replace media file {item.FileName}");
                                }
                            }
                        }
                    }
                    // 更新事件日期
                    if (bestCaptureTime.HasValue && (mediaList[i].CaptureTime == null || mediaList[i].CaptureTime < bestCaptureTime))
                    {
                        mediaList[i].CaptureTime = bestCaptureTime;
                        if (!string.IsNullOrEmpty(bestCaptureTime?.ToString()))
                        {
                            eve.Date = bestCaptureTime.Value.ToString("yyyy-MM-dd");
                            eve.IsDateValid = true;
                            updated = true;
                        }
                    }
                }
            }
            return updated;
        }

    }
}