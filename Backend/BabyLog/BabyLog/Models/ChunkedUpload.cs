using System;
using System.Collections.Generic;

namespace BabyLog.Models
{
    // �ϴ�����״̬
    public enum ChunkUploadStatus
    {
        Initialized,  // ��ʼ��
        InProgress,   // �ϴ���
        Completed,    // �����
        Failed        // ʧ��
    }
    
    // ��Ƭ�ϴ�������Ϣ
    public class ChunkUploadTask
    {
        public string TaskId { get; set; }                   // �ϴ�����ID
        public string OriginalFileName { get; set; }         // ԭʼ�ļ���
        public string FileType { get; set; }                 // �ļ�����
        public long TotalSize { get; set; }                  // �ļ��ܴ�С
        public int TotalChunks { get; set; }                 // �ܷ�Ƭ��
        public HashSet<int> CompletedChunks { get; set; }    // ����ɵķ�Ƭ���
        public ChunkUploadStatus Status { get; set; }        // ����״̬
        public DateTime CreatedAt { get; set; }              // ����ʱ��
        public DateTime LastUpdatedAt { get; set; }          // ������ʱ��
        public string ResultFileName { get; set; }           // �ϲ�����ļ���
        public string FileMD5 { get; set; }                  // �ͻ����ṩ���ļ�MD5
        public string ActualMD5 { get; set; }                // ʵ�ʼ�����ļ�MD5
        public bool MD5Verified { get; set; }                // MD5У���Ƿ�ͨ��
    }
    
    // ��ʼ���ϴ�����ģ��
    public class InitChunkUploadRequest
    {
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public int ChunkCount { get; set; }
        public string FileMD5 { get; set; }                  // �ͻ����ṩ���ļ�MD5
    }
    
    // ��ʼ���ϴ���Ӧģ��
    public class InitChunkUploadResponse
    {
        public string TaskId { get; set; }
    }
    
    // �ϴ�״̬��ѯ��Ӧ
    public class ChunkUploadStatusResponse
    {
        public string TaskId { get; set; }
        public string OriginalFileName { get; set; }
        public ChunkUploadStatus Status { get; set; }
        public int TotalChunks { get; set; }
        public int CompletedChunks { get; set; }
        public string ResultFileName { get; set; }
        public string MD5 { get; set; }                      // �ļ�MD5
        public bool MD5Verified { get; set; }                // MD5У���Ƿ�ͨ��
    }
}