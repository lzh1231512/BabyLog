<template>
  <div class="chunk-uploader">
    <!-- 上传按钮 -->
    <div class="upload-button-container">
      <input
        type="file"
        ref="fileInput"
        @change="handleFileChange"
        multiple
        class="file-input"
      />
      <button @click="triggerFileInput" class="upload-button">
        选择文件上传
      </button>
    </div>

    <!-- 上传进度对话框 -->
    <div class="upload-dialog" v-if="showUploadDialog">
      <div class="upload-dialog-header">
        <h3>文件上传进度</h3>
        <button @click="closeDialog" class="close-button" :disabled="isUploading">×</button>
      </div>
      <div class="upload-dialog-body">
        <div v-for="(file, index) in uploadTasks" :key="index" class="file-item">
          <div class="file-info">
            <div class="file-name">{{ file.name }}</div>
            <div class="file-size">{{ formatFileSize(file.size) }}</div>
            <div class="file-status">
              {{ getFileStatus(file) }}
              <span v-if="file.error" class="error-message">: {{ file.error }}</span>
            </div>
          </div>
          <div class="progress-container">
            <div class="progress-bar" :style="{ width: file.progress + '%' }"></div>
            <div class="progress-text">{{ file.progress.toFixed(1) }}%</div>
          </div>
        </div>
      </div>
      <div class="upload-dialog-footer">
        <button 
          @click="startUpload" 
          :disabled="isUploading || uploadTasks.length === 0 || allTasksCompleted" 
          class="upload-start-button"
        >
          开始上传
        </button>
        <button 
          @click="closeDialog" 
          :disabled="isUploading" 
          class="close-button"
        >
          关闭
        </button>
      </div>
    </div>
  </div>
</template>

<script>
import { initChunk, uploadChunk, completeChunk } from '../api/events';
import SparkMD5 from 'spark-md5';

export default {
  name: 'ChunkUploader',
  props: {
    // 最大并行上传文件数
    maxParallelFiles: {
      type: Number,
      default: 3
    },
    // 每个文件最大并行分片数
    maxParallelChunks: {
      type: Number,
      default: 3
    },
    // 每个分片大小(bytes) - 默认 2MB
    chunkSize: {
      type: Number,
      default: 2 * 1024 * 1024
    }
  },
  data() {
    return {
      showUploadDialog: false,
      uploadTasks: [],
      activeUploads: 0,
      isUploading: false
    };
  },
  computed: {
    allTasksCompleted() {
      return this.uploadTasks.length > 0 && 
             this.uploadTasks.every(task => task.status === 'completed' || task.status === 'error');
    }
  },
  methods: {
    triggerFileInput() {
      this.$refs.fileInput.click();
    },
    
    handleFileChange(event) {
      const files = event.target.files;
      if (!files || files.length === 0) return;
      
      // 创建任务列表
      const newTasks = Array.from(files).map(file => ({
        file,
        name: file.name,
        size: file.size,
        progress: 0,
        status: 'pending', // pending, calculating, uploading, completed, error
        error: null,
        taskId: null,
        totalChunks: Math.ceil(file.size / this.chunkSize),
        uploadedChunks: 0,
        chunkQueue: [],
        md5: null
      }));
      
      this.uploadTasks = [...this.uploadTasks, ...newTasks];
      this.showUploadDialog = true;
      
      // 清空文件输入，允许重新选择相同的文件
      this.$refs.fileInput.value = '';
    },
    
    closeDialog() {
      if (this.isUploading) return;
      
      // 关闭对话框并清空已完成或错误的任务
      this.showUploadDialog = false;
      this.uploadTasks = this.uploadTasks.filter(
        task => task.status !== 'completed' && task.status !== 'error'
      );
    },
    
    formatFileSize(bytes) {
      if (bytes < 1024) return bytes + ' B';
      else if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
      else if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
      else return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
    },
    
    getFileStatus(file) {
      switch (file.status) {
        case 'pending': return '等待上传';
        case 'calculating': return '计算MD5...';
        case 'uploading': return `上传中 (${file.uploadedChunks}/${file.totalChunks})`;
        case 'verifying': return '验证中...';
        case 'completed': return '完成';
        case 'error': return '错误';
        default: return file.status;
      }
    },
    
    async startUpload() {
      if (this.isUploading) return;
      this.isUploading = true;
      
      // 过滤出需要上传的任务
      const pendingTasks = this.uploadTasks.filter(task => 
        task.status === 'pending' || (task.status === 'error' && !task.taskId)
      );
      
      // 开始计算MD5
      for (const task of pendingTasks) {
        this.calculateFileMD5(task);
      }
    },
    
    async calculateFileMD5(task) {
      task.status = 'calculating';
      
      try {
        const md5 = await this.computeFileMD5(task.file);
        task.md5 = md5;
        this.prepareFileUpload(task);
      } catch (error) {
        console.error('MD5 calculation error:', error);
        task.status = 'error';
        task.error = 'MD5计算失败';
        this.checkAllTasksCompleted();
      }
    },
    
    async computeFileMD5(file) {
      return new Promise((resolve, reject) => {
        const blobSlice = File.prototype.slice || File.prototype.mozSlice || File.prototype.webkitSlice;
        const chunkSize = 2097152; // 2MB per chunk for MD5 calculation
        const chunks = Math.ceil(file.size / chunkSize);
        let currentChunk = 0;
        const spark = new SparkMD5.ArrayBuffer();
        const fileReader = new FileReader();
        
        fileReader.onload = (e) => {
          spark.append(e.target.result); // Append array buffer
          currentChunk++;
          
          if (currentChunk < chunks) {
            loadNext();
          } else {
            resolve(spark.end()); // Return the MD5 hash
          }
        };
        
        fileReader.onerror = (error) => {
          reject(error);
        };
        
        const loadNext = () => {
          const start = currentChunk * chunkSize;
          const end = Math.min(file.size, start + chunkSize);
          fileReader.readAsArrayBuffer(blobSlice.call(file, start, end));
        };
        
        loadNext();
      });
    },
    
    async prepareFileUpload(task) {
      try {
        // 准备分块上传请求
        const initRequest = {
          fileName: task.name,
          fileType: task.file.type,
          fileSize: task.file.size,
          chunkCount: task.totalChunks,
          fileMD5: task.md5
        };
        
        // 初始化分块上传
        const response = await initChunk(initRequest);
        
        if (response.success && response.data) {
          task.taskId = response.data.taskId;
          task.status = 'uploading';
          this.createChunkQueue(task);
          this.processUploadQueue();
        } else {
          task.status = 'error';
          task.error = response.message || '初始化上传失败';
          this.checkAllTasksCompleted();
        }
      } catch (error) {
        console.error('Upload preparation error:', error);
        task.status = 'error';
        task.error = '准备上传失败';
        this.checkAllTasksCompleted();
      }
    },
    
    createChunkQueue(task) {
      const { file } = task;
      const { chunkSize } = this;
      task.chunkQueue = [];
      
      for (let i = 0; i < task.totalChunks; i++) {
        const start = i * chunkSize;
        const end = Math.min(file.size, start + chunkSize);
        const chunkFile = file.slice(start, end);
        
        task.chunkQueue.push({
          index: i,
          file: chunkFile,
          uploaded: false,
          uploading: false,
          retries: 0
        });
      }
    },
    
    async processUploadQueue() {
      // 计算当前正在上传的文件数
      this.activeUploads = this.uploadTasks.filter(task => 
        task.status === 'uploading' && task.chunkQueue.some(chunk => chunk.uploading)
      ).length;
      
      // 检查是否可以开始新的文件上传
      for (const task of this.uploadTasks) {
        if (task.status === 'uploading' && !task.chunkQueue.some(chunk => chunk.uploading)) {
          if (this.activeUploads < this.maxParallelFiles) {
            this.uploadFileChunks(task);
            this.activeUploads++;
          }
        }
      }
    },
    
    async uploadFileChunks(task) {
      // 计算当前活动的分片上传数量
      const activeChunks = task.chunkQueue.filter(chunk => chunk.uploading).length;
      
      // 如果所有分片已上传，完成上传
      if (task.chunkQueue.every(chunk => chunk.uploaded)) {
        await this.completeFileUpload(task);
        return;
      }
      
      // 上传更多分片，直到达到并行限制
      for (let i = 0; i < task.chunkQueue.length && activeChunks < this.maxParallelChunks; i++) {
        const chunk = task.chunkQueue[i];
        if (!chunk.uploaded && !chunk.uploading) {
          chunk.uploading = true;
          this.uploadChunk(task, chunk);
        }
      }
    },
    
    async uploadChunk(task, chunk) {
      try {
        // 使用 API 上传分片
        const response = await uploadChunk(task.taskId, chunk.index, chunk.file);
        
        if (response.success) {
          chunk.uploaded = true;
          chunk.uploading = false;
          task.uploadedChunks++;
          
          // 更新进度
          task.progress = (task.uploadedChunks / task.totalChunks) * 100;
          
          // 继续上传更多分片
          this.uploadFileChunks(task);
        } else {
          // 重试逻辑
          if (chunk.retries < 3) {
            chunk.retries++;
            chunk.uploading = false;
            // 稍后重试
            setTimeout(() => {
              this.uploadChunk(task, chunk);
            }, 1000);
          } else {
            chunk.uploading = false;
            task.status = 'error';
            task.error = response.message || `分片 ${chunk.index + 1} 上传失败`;
            this.checkAllTasksCompleted();
          }
        }
      } catch (error) {
        console.error('Chunk upload error:', error);
        // 重试逻辑同上
        if (chunk.retries < 3) {
          chunk.retries++;
          chunk.uploading = false;
          setTimeout(() => {
            this.uploadChunk(task, chunk);
          }, 1000);
        } else {
          chunk.uploading = false;
          task.status = 'error';
          task.error = `分片 ${chunk.index + 1} 上传失败`;
          this.checkAllTasksCompleted();
        }
      }
      
      // 检查其他任务的上传
      this.processUploadQueue();
    },
    
    async completeFileUpload(task) {
      task.status = 'verifying';
      
      try {
        const response = await completeChunk(task.taskId);
        
        if (response.success) {
          // 检查MD5是否匹配
          if (response.data.md5Verified) {
            task.status = 'completed';
            this.$emit('upload-complete', {
              fileName: response.data.originalName,
              serverFileName: response.data.serverFileName,
              size: response.data.size,
              md5: response.data.md5
            });
          } else {
            task.status = 'error';
            task.error = `MD5校验失败，期望: ${response.data.expectedMD5}，实际: ${response.data.md5}`;
          }
        } else {
          task.status = 'error';
          task.error = response.message || '完成上传失败';
        }
      } catch (error) {
        console.error('Complete upload error:', error);
        task.status = 'error';
        task.error = '完成上传失败';
      }
      
      // 检查是否所有任务都已完成
      this.checkAllTasksCompleted();
      
      // 处理下一个文件上传
      this.processUploadQueue();
    },
    
    checkAllTasksCompleted() {
      // 检查所有任务是否完成
      const isAllCompleted = this.uploadTasks.every(
        task => task.status === 'completed' || task.status === 'error'
      );
      
      if (isAllCompleted) {
        this.isUploading = false;
        this.$emit('all-completed');
      }
    }
  }
};
</script>

<style scoped>
.chunk-uploader {
  display: flex;
  flex-direction: column;
}

.file-input {
  display: none;
}

.upload-button-container {
  margin-bottom: 1rem;
}

.upload-button {
  padding: 8px 16px;
  background-color: #4caf50;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 14px;
}

.upload-button:hover {
  background-color: #45a049;
}

.upload-dialog {
  position: fixed;
  z-index: 1000;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  width: 500px;
  max-width: 90vw;
  background-color: white;
  border-radius: 8px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
  display: flex;
  flex-direction: column;
}

.upload-dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid #eee;
}

.upload-dialog-header h3 {
  margin: 0;
  font-size: 18px;
}

.upload-dialog-body {
  padding: 16px;
  max-height: 400px;
  overflow-y: auto;
}

.upload-dialog-footer {
  padding: 12px 16px;
  border-top: 1px solid #eee;
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.file-item {
  margin-bottom: 16px;
  padding-bottom: 16px;
  border-bottom: 1px solid #eee;
}

.file-item:last-child {
  margin-bottom: 0;
  padding-bottom: 0;
  border-bottom: none;
}

.file-info {
  display: flex;
  justify-content: space-between;
  margin-bottom: 8px;
  flex-wrap: wrap;
}

.file-name {
  font-weight: bold;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 50%;
}

.file-size {
  color: #666;
}

.file-status {
  width: 100%;
  margin-top: 4px;
  color: #555;
}

.error-message {
  color: #f44336;
}

.progress-container {
  height: 20px;
  background-color: #f5f5f5;
  border-radius: 4px;
  position: relative;
  overflow: hidden;
}

.progress-bar {
  height: 100%;
  background-color: #4caf50;
  transition: width 0.3s;
}

.progress-text {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #333;
  font-size: 12px;
  font-weight: bold;
}

.close-button {
  padding: 5px 10px;
  background-color: #f1f1f1;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.close-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.upload-start-button {
  padding: 8px 16px;
  background-color: #2196f3;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.upload-start-button:hover:not(:disabled) {
  background-color: #0b7dda;
}

.upload-start-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
