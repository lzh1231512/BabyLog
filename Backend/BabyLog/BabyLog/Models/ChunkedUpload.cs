using System;
using System.Collections.Generic;

namespace BabyLog.Models
{
    // 上传任务状态
    public enum ChunkUploadStatus
    {
        Initialized,  // 初始化
        InProgress,   // 上传中
        Completed,    // 已完成
        Failed        // 失败
    }
    
    // 分片上传任务信息
    public class ChunkUploadTask
    {
        public string TaskId { get; set; }                   // 上传任务ID
        public string OriginalFileName { get; set; }         // 原始文件名
        public string FileType { get; set; }                 // 文件类型
        public long TotalSize { get; set; }                  // 文件总大小
        public int TotalChunks { get; set; }                 // 总分片数
        public HashSet<int> CompletedChunks { get; set; }    // 已完成的分片序号
        public ChunkUploadStatus Status { get; set; }        // 任务状态
        public DateTime CreatedAt { get; set; }              // 创建时间
        public DateTime LastUpdatedAt { get; set; }          // 最后更新时间
        public string ResultFileName { get; set; }           // 合并后的文件名
        public string FileMD5 { get; set; }                  // 客户端提供的文件MD5
        public string ActualMD5 { get; set; }                // 实际计算的文件MD5
        public bool MD5Verified { get; set; }                // MD5校验是否通过
    }
    
    // 初始化上传请求模型
    public class InitChunkUploadRequest
    {
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public int ChunkCount { get; set; }
        public string FileMD5 { get; set; }                  // 客户端提供的文件MD5
    }
    
    // 初始化上传响应模型
    public class InitChunkUploadResponse
    {
        public string TaskId { get; set; }
    }
    
    // 上传状态查询响应
    public class ChunkUploadStatusResponse
    {
        public string TaskId { get; set; }
        public string OriginalFileName { get; set; }
        public ChunkUploadStatus Status { get; set; }
        public int TotalChunks { get; set; }
        public int CompletedChunks { get; set; }
        public string ResultFileName { get; set; }
        public string MD5 { get; set; }                      // 文件MD5
        public bool MD5Verified { get; set; }                // MD5校验是否通过
    }
}