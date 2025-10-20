using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace APITest
{
    internal class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Chunked File Upload API Test");
                Console.WriteLine("============================");

                // Parse command line arguments or use defaults
                string filePath = GetArgumentValue(args, "-f", "--file", "D:\\largeFile\\test.jpg");
                string apiUrl = GetArgumentValue(args, "-u", "--url", "http://lzhsb.cc:5115");
                int chunkSize = ParseChunkSize(GetArgumentValue(args, "-s", "--size", "1mb"));

                // If file path not provided, prompt user
                if (string.IsNullOrEmpty(filePath))
                {
                    Console.Write("Enter file path to upload: ");
                    filePath = Console.ReadLine() ?? "";
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Console.WriteLine("File path is required.");
                        return;
                    }
                }

                // Validate file exists
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"File not found: {filePath}");
                    return;
                }

                // Set API base URL
                if (!apiUrl.EndsWith("/"))
                {
                    apiUrl += "/";
                }
                httpClient.BaseAddress = new Uri(apiUrl);

                // Get file info
                var fileInfo = new FileInfo(filePath);
                Console.WriteLine($"File: {fileInfo.Name}");
                Console.WriteLine($"Size: {FormatFileSize(fileInfo.Length)}");
                Console.WriteLine($"Chunk size: {FormatFileSize(chunkSize)}");

                // Calculate MD5 hash of the file
                string fileMd5 = await CalculateFileMd5Async(filePath);
                Console.WriteLine($"MD5: {fileMd5}");
                Console.WriteLine("============================");

                // Start chunked upload
                await PerformChunkedUpload(filePath, fileInfo, chunkSize, fileMd5);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static async Task PerformChunkedUpload(string filePath, FileInfo fileInfo, int chunkSize, string fileMd5)
        {
            Console.WriteLine("Starting chunked upload...");

            // Calculate number of chunks
            int totalChunks = (int)Math.Ceiling(fileInfo.Length / (double)chunkSize);
            Console.WriteLine($"Total chunks: {totalChunks}");

            // Step 1: Initialize upload task
            var initResponse = await InitializeUploadTask(fileInfo, totalChunks, fileMd5);
            if (initResponse == null || !initResponse.Success)
            {
                Console.WriteLine("Failed to initialize upload task.");
                return;
            }

            string taskId = initResponse.Data.TaskId;
            Console.WriteLine($"Upload task initialized. TaskID: {taskId}");

            // Step 2: Upload chunks
            Console.WriteLine("Starting to upload chunks...");
            DateTime startTime = DateTime.Now;

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[chunkSize];
                int bytesRead;
                int chunkIndex = 0;
                long totalBytesUploaded = 0;

                while ((bytesRead = await fileStream.ReadAsync(buffer)) > 0)
                {
                    byte[] chunkData;
                    if (bytesRead < chunkSize)
                    {
                        // Last chunk might be smaller
                        chunkData = new byte[bytesRead];
                        Array.Copy(buffer, chunkData, bytesRead);
                    }
                    else
                    {
                        chunkData = buffer;
                    }

                    var uploadSuccess = await UploadChunk(taskId, chunkIndex, chunkData);
                    if (!uploadSuccess)
                    {
                        Console.WriteLine($"\nFailed to upload chunk {chunkIndex}. Aborting.");
                        return;
                    }

                    totalBytesUploaded += bytesRead;
                    double progress = (double)totalBytesUploaded / fileInfo.Length * 100;
                    Console.Write($"\rProgress: {progress:F2}% - Chunk {chunkIndex + 1}/{totalChunks} uploaded");

                    chunkIndex++;
                }
            }

            Console.WriteLine("\nAll chunks uploaded successfully!");

            // Step 3: Complete upload
            Console.WriteLine("Finalizing upload...");
            var completeResponse = await CompleteUpload(taskId);

            if (completeResponse != null && completeResponse.Success)
            {
                TimeSpan duration = DateTime.Now - startTime;
                double speedMBps = fileInfo.Length / (1024.0 * 1024.0) / duration.TotalSeconds;

                Console.WriteLine("============================");
                Console.WriteLine("Upload completed successfully!");
                Console.WriteLine($"Server file name: {completeResponse.Data.ServerFileName}");
                Console.WriteLine($"Time taken: {duration.TotalSeconds:F2} seconds");
                Console.WriteLine($"Average speed: {speedMBps:F2} MB/s");
                Console.WriteLine($"MD5 verification: {(completeResponse.Data.MD5Verified ? "Passed" : "Failed")}");
                Console.WriteLine($"Server MD5: {completeResponse.Data.MD5}");
                Console.WriteLine($"CaptureTime: {completeResponse.Data.CaptureTime}");
            }
            else
            {
                Console.WriteLine("Failed to complete upload.");
            }
        }

        private static async Task<ApiResponse<InitChunkUploadResponse>?> InitializeUploadTask(FileInfo fileInfo, int totalChunks, string fileMd5)
        {
            var request = new
            {
                FileName = fileInfo.Name,
                FileType = GetContentType(fileInfo.Name),
                FileSize = fileInfo.Length,
                ChunkCount = totalChunks,
                FileMD5 = fileMd5
            };

            try
            {
                var response = await httpClient.PostAsJsonAsync("api/chunk/init", request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ApiResponse<InitChunkUploadResponse>>(content, jsonOptions);
                }
                else
                {
                    Console.WriteLine($"Failed to initialize: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing upload: {ex.Message}");
                return null;
            }
        }

        private static async Task<bool> UploadChunk(string taskId, int chunkIndex, byte[] chunkData)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(chunkData);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
                content.Add(fileContent, "file", $"chunk_{chunkIndex}");

                var response = await httpClient.PostAsync($"api/chunk/upload?taskId={taskId}&chunkIndex={chunkIndex}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"\nError uploading chunk {chunkIndex}: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nException uploading chunk {chunkIndex}: {ex.Message}");
                return false;
            }
        }

        private static async Task<ApiResponse<CompleteUploadResponse>?> CompleteUpload(string taskId)
        {
            try
            {
                var response = await httpClient.PostAsync($"api/chunk/complete?taskId={taskId}", null);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ApiResponse<CompleteUploadResponse>>(content, jsonOptions);
                }
                else
                {
                    Console.WriteLine($"Failed to complete upload: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error completing upload: {ex.Message}");
                return null;
            }
        }

        private static async Task<string> CalculateFileMd5Async(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = await md5.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static int ParseChunkSize(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr))
                return 2 * 1024 * 1024; // Default 2MB

            sizeStr = sizeStr.ToLowerInvariant();

            if (int.TryParse(sizeStr, out int simpleSize))
                return simpleSize;

            if (sizeStr.EndsWith("kb"))
                return int.Parse(sizeStr[0..^2]) * 1024;
            
            if (sizeStr.EndsWith("mb"))
                return int.Parse(sizeStr[0..^2]) * 1024 * 1024;
            
            if (sizeStr.EndsWith("gb"))
                return int.Parse(sizeStr[0..^2]) * 1024 * 1024 * 1024;

            // Default
            return 2 * 1024 * 1024;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private static string? GetArgumentValue(string[] args, string shortName, string longName, string? defaultValue = null)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(shortName, StringComparison.OrdinalIgnoreCase) ||
                    args[i].Equals(longName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return defaultValue;
        }

        private static string GetContentType(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".txt" => "text/plain",
                ".doc" or ".docx" => "application/msword",
                ".xls" or ".xlsx" => "application/vnd.ms-excel",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                _ => "application/octet-stream"
            };
        }
    }

    // Model classes to match server API
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
    }

    public class InitChunkUploadResponse
    {
        public string TaskId { get; set; }
    }

    public class CompleteUploadResponse
    {
        public string OriginalName { get; set; }
        public string ServerFileName { get; set; }
        public long Size { get; set; }
        public string Type { get; set; }
        public string UploadTime { get; set; }
        public string MD5 { get; set; }
        public bool MD5Verified { get; set; }
        public string ExpectedMD5 { get; set; }
        public string CaptureTime { get; set; }
    }
}
