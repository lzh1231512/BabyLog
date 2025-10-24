using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using FFMpegCore;

namespace BatchImport
{
    // Event and Media models (same as BabyLog.Models)
    public class Event
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public Media Media { get; set; }
        public string Date { get; set; }
        public string Location { get; set; } = "";
    }

    public class Media
    {
        public List<MediaItem> Images { get; set; } = new List<MediaItem>();
        public List<MediaItem> Videos { get; set; } = new List<MediaItem>();
        public List<MediaItem> Audios { get; set; } = new List<MediaItem>();
    }

    public class MediaItem
    {
        public string FileName { get; set; } = "";
        public string Desc { get; set; } = "";
        public string Hash { get; set; }

        public DateTime? CaptureTime { get; set; }
    }

    internal class Program
    {
        static int Main(string[] args)
        {
            args= new string[] { "C:\\Users\\zack liu\\Pictures\\Screenshots", "D:\\新建文件夹" }; // For testing purpose only
            // Validate command-line arguments - now only need source and target paths
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: BatchImport <sourcePath> <targetPath>");
                return 1;
            }

            try
            {
                string sourcePath = args[0];
                string targetPath = args[1];

                if (!Directory.Exists(sourcePath))
                {
                    Console.WriteLine($"Error: Source directory '{sourcePath}' does not exist.");
                    return 1;
                }

                // Process files
                ProcessFiles(sourcePath, targetPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static void ProcessFiles(string sourcePath, string targetPath)
        {
            Console.WriteLine($"Scanning files in: {sourcePath}");

            // Get all image and video files
            var supportedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            var supportedVideoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv" };

            var allFiles = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories)
                .Where(file => supportedImageExtensions.Contains(Path.GetExtension(file).ToLower()) || 
                               supportedVideoExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();

            Console.WriteLine($"Found {allFiles.Count} media files");

            // Create dictionary to store files grouped by capture date
            var filesByDate = new Dictionary<string, List<MediaFileInfo>>();

            // Process each file to extract capture date
            foreach (var file in allFiles)
            {
                var fileInfo = new MediaFileInfo
                {
                    FilePath = file,
                    FileName = Path.GetFileName(file),
                    Extension = Path.GetExtension(file).ToLower()
                };

                // Determine if it's an image or video
                fileInfo.IsImage = supportedImageExtensions.Contains(fileInfo.Extension);
                fileInfo.IsVideo = supportedVideoExtensions.Contains(fileInfo.Extension);

                // Get capture time
                DateTime? captureTime = GetMediaCaptureTime(file, fileInfo.FileName);

                if (captureTime.HasValue)
                {
                    // Format as YYYY-MM-DD
                    fileInfo.CaptureDate = captureTime.Value.ToString("yyyy-MM-dd");
                    Console.WriteLine($"File: {fileInfo.FileName}, Capture Date: {fileInfo.CaptureDate}");
                }
                else
                {
                    // Use file creation/modification date as fallback
                    var fileCreationTime = File.GetCreationTime(file);
                    var fileModificationTime = File.GetLastWriteTime(file);
                    var earlierTime = fileCreationTime < fileModificationTime ? fileCreationTime : fileModificationTime;
                    
                    fileInfo.CaptureDate = earlierTime.ToString("yyyy-MM-dd");
                    Console.WriteLine($"Warning: Could not extract capture time for {fileInfo.FileName}. Using file date: {fileInfo.CaptureDate}");
                }

                // Add to dictionary grouped by date
                if (!filesByDate.ContainsKey(fileInfo.CaptureDate))
                {
                    filesByDate[fileInfo.CaptureDate] = new List<MediaFileInfo>();
                }
                filesByDate[fileInfo.CaptureDate].Add(fileInfo);
            }

            // Get the next available event ID from existing files
            int nextId = GetNextEventId(targetPath);
            Console.WriteLine($"Starting with event ID: {nextId}");

            // Create events for each date
            int createdEvents = 0;
            foreach (var dateGroup in filesByDate.OrderBy(f => f.Key))
            {
                CreateEvent(dateGroup.Key, dateGroup.Value, nextId, targetPath);
                nextId++;
                createdEvents++;
            }

            Console.WriteLine($"Batch import completed. Created {createdEvents} events.");
        }

        static int GetNextEventId(string targetPath)
        {
            var eventJsonDir = Path.Combine(targetPath, "Events");
            
            // If Events directory doesn't exist, start with ID 1
            if (!Directory.Exists(eventJsonDir))
            {
                return 1;
            }

            // Get all JSON files in the Events directory
            var jsonFiles = Directory.GetFiles(eventJsonDir, "*.json");
            
            if (jsonFiles.Length == 0)
            {
                return 1; // No existing events, start with ID 1
            }

            // Extract IDs from filenames and find the maximum
            var ids = new List<int>();
            foreach (var jsonFile in jsonFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(jsonFile);
                if (int.TryParse(fileName, out int id))
                {
                    ids.Add(id);
                }
            }

            // Return the next available ID (max + 1)
            return ids.Count > 0 ? ids.Max() + 1 : 1;
        }

        static void CreateEvent(string date, List<MediaFileInfo> files, int id, string targetPath)
        {
            Console.WriteLine($"Creating Event ID {id} for date {date}");

            // Create event structure
            var eventData = new Event
            {
                Id = id,
                Title = "",
                Description = "",
                Date = date,
                Location = "",
                Media = new Media
                {
                    Images = new List<MediaItem>(),
                    Videos = new List<MediaItem>(),
                    Audios = new List<MediaItem>()
                }
            };

            // Ensure target directories exist
            var eventJsonDir = Path.Combine(targetPath, "Events");
            var eventMediaDir = Path.Combine(targetPath, "Events", id.ToString());
            var tasksDir = Path.Combine(targetPath, "Tasks");

            Directory.CreateDirectory(eventJsonDir);
            Directory.CreateDirectory(eventMediaDir);
            Directory.CreateDirectory(tasksDir);

            // Process files
            foreach (var file in files)
            {
                // Copy the file to the event directory
                var targetFilePath = Path.Combine(eventMediaDir, file.FileName);
                File.Copy(file.FilePath, targetFilePath, true);
                Console.WriteLine($"Copied: {file.FileName}");

                // Add to the appropriate media list
                var mediaItem = new MediaItem
                {
                    FileName = file.FileName,
                    Desc = "",
                    CaptureTime = GetMediaCaptureTime(targetFilePath, file.FileName)
                };

                if (file.IsImage)
                {
                    mediaItem.Hash = BabyLog.Commons.PHashHelper.CalculateImageHashString(targetFilePath);
                    eventData.Media.Images.Add(mediaItem);
                }
                else if (file.IsVideo)
                {
                    mediaItem.Hash = BabyLog.Commons.PHashHelper.CalculateVideoHashString(targetFilePath);
                    eventData.Media.Videos.Add(mediaItem);
                    
                    // Create task file for video transcoding
                    var taskFilePath = Path.Combine(tasksDir, $"{id}_{file.FileName}");
                    File.WriteAllText(taskFilePath, DateTime.UtcNow.ToString("o"));
                    Console.WriteLine($"Created transcoding task: {id}_{file.FileName}");
                }
            }

            // Save event JSON
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var jsonContent = JsonSerializer.Serialize(eventData, options);
            var jsonFilePath = Path.Combine(eventJsonDir, $"{id}.json");
            File.WriteAllText(jsonFilePath, jsonContent);

            Console.WriteLine($"Saved event JSON: {jsonFilePath}");
        }

        /// <summary>
        /// From image/video, try to extract capture time 
        /// </summary>
        static DateTime? GetMediaCaptureTime(string filePath, string fileName)
        {
            DateTime? captureTime = null;
            string fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            
            // Determine if it's an image or video and extract capture time
            if (fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png" || 
                fileExtension == ".gif" || fileExtension == ".webp")
            {
                // Try to extract from image EXIF data
                captureTime = ExtractImageCaptureTime(filePath);
            }
            else if (fileExtension == ".mp4" || fileExtension == ".mov" || 
                     fileExtension == ".avi" || fileExtension == ".mkv" || fileExtension == ".wmv")
            {
                // Try to extract from video metadata
                captureTime = ExtractVideoCaptureTime(filePath);
            }
            
            // If metadata extraction failed, try from filename
            if (captureTime == null)
            {
                captureTime = ExtractDateTimeFromFileName(Path.GetFileNameWithoutExtension(fileName));
            }
            
            return captureTime;
        }

        /// <summary>
        /// Extract date from EXIF data in image file
        /// </summary>
        static DateTime? ExtractImageCaptureTime(string filePath)
        {
            try
            {
                using var image = Image.Load(filePath);
                if (image.Metadata.ExifProfile != null)
                {
                    // Try to get DateTimeOriginal property (capture time)
                    if (image.Metadata.ExifProfile.TryGetValue(ExifTag.DateTimeOriginal, out var dateTimeOriginal) && 
                        dateTimeOriginal.Value != null)
                    {
                        string dateTimeStr = dateTimeOriginal.Value.ToString();
                        if (DateTime.TryParseExact(dateTimeStr, "yyyy:MM:dd HH:mm:ss", 
                            System.Globalization.CultureInfo.InvariantCulture, 
                            System.Globalization.DateTimeStyles.None, out var dateTimeValue))
                        {
                            return dateTimeValue;
                        }
                    }
                    
                    // If DateTimeOriginal doesn't exist, try DateTime property
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
                Console.WriteLine($"Error extracting EXIF data from image {filePath}: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Extract date from video metadata
        /// </summary>
        static DateTime? ExtractVideoCaptureTime(string filePath)
        {
            try
            {
                var mediaInfo = FFProbe.Analyse(filePath);
                
                // Try to get creation_time
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
                Console.WriteLine($"Error extracting metadata from video {filePath}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Try to extract date from filename
        /// </summary>
        static DateTime? ExtractDateTimeFromFileName(string fileName)
        {
            try
            {
                // Match common date formats: YYYYMMDD_HHMMSS, YYYY-MM-DD, etc.
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
                        if (match.Groups.Count >= 7) // Full date and time
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
                        else if (match.Groups.Count >= 4) // Date only
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
                            // Handle formats like IMG_20210101_123045
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
                Console.WriteLine($"Error extracting date from filename {fileName}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Validate if date/time values are valid
        /// </summary>
        static bool IsValidDateTime(int year, int month, int day, int hour, int minute, int second)
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
    }

    /// <summary>
    /// Helper class to store file information
    /// </summary>
    class MediaFileInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }
        public bool IsImage { get; set; }
        public bool IsVideo { get; set; }
        public string CaptureDate { get; set; }
    }
}
