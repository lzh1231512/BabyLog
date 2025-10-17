<template>
  <div class="chunk-uploader">
    <!-- 上传进度对话框 -->
    <div class="upload-dialog">
      <div class="upload-dialog-header">
        <h3>文件上传进度</h3>
        <button 
          type="button" 
          class="toggle-logs-btn" 
          @click="showLogs = !showLogs"
        >
          {{ showLogs ? '隐藏日志' : '显示日志' }}
        </button>
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

        <!-- 日志区域 -->
        <div v-if="showLogs" class="log-container">
          <div class="log-header">
            <h4>上传日志</h4>
            <div class="log-actions">
              <button 
                type="button" 
                class="clear-logs-btn" 
                @click="clearLogs"
              >
                清空日志
              </button>
              <button
                type="button"
                class="copy-logs-btn"
                @click="copyLogs"
              >
                复制日志
              </button>
            </div>
          </div>
          <div class="log-content" ref="logContent">
            <div v-for="(log, index) in logs" :key="index" class="log-entry" :class="getLogClass(log)">
              <span class="log-time">{{ log.time }}</span>
              <span class="log-level">[{{ log.level }}]</span>
              <span class="log-message">{{ log.message }}</span>
            </div>
          </div>
        </div>
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
    // 每个分片大小(bytes) - 默认 512k
    chunkSize: {
      type: Number,
      default:  512 * 1024
    }
  },
  data() {
    return {
      uploadTasks: [],
      activeUploads: 0,
      isUploading: false,
      showLogs: false,
      logs: [],
      maxLogs: 100  // 最多保存的日志条数
    };
  },
  computed: {
    allTasksCompleted() {
      return this.uploadTasks.length > 0 && 
             this.uploadTasks.every(task => task.status === 'completed' || task.status === 'error');
    }
  },
  methods: {
    // 日志相关方法
    log(level, message) {
      const time = new Date().toLocaleTimeString();
      this.logs.unshift({ time, level, message });
      
      // 限制日志数量
      if (this.logs.length > this.maxLogs) {
        this.logs = this.logs.slice(0, this.maxLogs);
      }
      
      // 同时输出到控制台
      switch (level) {
        case 'ERROR':
          console.error(message);
          break;
        case 'WARN':
          console.warn(message);
          break;
        case 'INFO':
          console.log(message);
          break;
        default:
          console.log(message);
      }
      
      // 如果日志区域可见，滚动到最新日志
      this.$nextTick(() => {
        if (this.showLogs && this.$refs.logContent) {
          this.$refs.logContent.scrollTop = 0;
        }
      });
    },
    
    logInfo(message) {
      this.log('INFO', message);
    },
    
    logWarn(message) {
      this.log('WARN', message);
    },
    
    logError(message) {
      this.log('ERROR', message);
    },
    
    clearLogs() {
      this.logs = [];
    },
    
    copyLogs() {
      // 将日志格式化为文本
      const logText = this.logs
        .map(log => `${log.time} [${log.level}] ${log.message}`)
        .join('\n');
      
      // 复制到剪贴板
      navigator.clipboard.writeText(logText)
        .then(() => {
          this.logInfo('日志已复制到剪贴板');
        })
        .catch(err => {
          this.logError('复制日志失败: ' + err);
        });
    },
    
    getLogClass(log) {
      return {
        'log-info': log.level === 'INFO',
        'log-warn': log.level === 'WARN',
        'log-error': log.level === 'ERROR'
      };
    },
    
    // 添加单个文件到上传队列并自动开始上传
    addFile(file) {
      try {
        if (!file) {
          this.logError('尝试添加文件，但文件对象为空!');
          return null;
        }
        
        this.logInfo(`添加文件到上传队列: ${file.name}, 大小: ${this.formatFileSize(file.size)}`);
        
        // 创建新任务
        const newTask = {
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
        };
        
        // 添加到任务列表
        this.uploadTasks.push(newTask);
        this.logInfo(`添加成功，当前任务数: ${this.uploadTasks.length}`);
        
        // 自动开始计算该文件的MD5并上传
        if (!this.isUploading) {
          this.logInfo('启动上传流程');
          setTimeout(() => {
            try {
              this.startUpload();
            } catch (error) {
              this.logError(`开始上传过程出错: ${error.message || error}`);
            }
          }, 100); // 添加小延迟确保DOM已更新
        } else {
          this.logInfo('已有上传任务进行中，新任务将排队等待');
        }
        
        return newTask;
      } catch (error) {
        this.logError(`添加文件过程中发生错误: ${error.message || error}`);
        return null;
      }
    },
    
    clearCompletedTasks() {
      // 清空已完成或错误的任务
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
      if (this.isUploading) {
        this.logInfo('已有上传任务进行中，跳过本次调用');
        return;
      }
      
      this.logInfo(`开始上传处理, 当前任务数: ${this.uploadTasks.length}`);
      this.isUploading = true;
      
      try {
        // 过滤出需要上传的任务
        const pendingTasks = this.uploadTasks.filter(task => 
          task.status === 'pending' || (task.status === 'error' && !task.taskId)
        );
        
        this.logInfo(`待处理任务数: ${pendingTasks.length}`);
        
        if (pendingTasks.length === 0) {
          this.logInfo('没有待处理的任务，标记上传过程结束');
          this.isUploading = false;
          // 检查是否需要触发完成事件
          this.checkAllTasksCompleted();
          return;
        }
        
        // 开始计算MD5
        this.logInfo(`开始处理 ${pendingTasks.length} 个任务的MD5计算`);
        for (const task of pendingTasks) {
          await this.calculateFileMD5(task);
        }
      } catch (error) {
        this.logError(`上传处理过程中发生错误: ${error.message || error}`);
        this.isUploading = false;
      }
    },
    
    async calculateFileMD5(task) {
      this.logInfo(`开始计算MD5，文件: ${task.name}`);
      task.status = 'calculating';
      
      try {
        const md5 = await this.computeFileMD5(task.file);
        this.logInfo(`MD5计算完成: ${md5}`);
        task.md5 = md5;
        await this.prepareFileUpload(task);
      } catch (error) {
        this.logError(`MD5计算失败: ${error.message || error}`);
        task.status = 'error';
        task.error = 'MD5计算失败';
        this.checkAllTasksCompleted();
      }
    },
    
    async computeFileMD5(file) {
      return new Promise((resolve, reject) => {
        this.logInfo(`开始分片计算MD5: ${file.name}, 大小: ${file.size}字节`);
        
        // 对于非常小的文件，使用简单的算法
        if (file.size < 1024 * 1024) { // 小于1MB的文件
          this.logInfo('小文件使用简单MD5计算');
          const reader = new FileReader();
          reader.onload = (e) => {
            try {
              const arrayBuffer = e.target.result;
              const spark = new SparkMD5.ArrayBuffer();
              spark.append(arrayBuffer);
              const hash = spark.end();
              resolve(hash);
            } catch (err) {
              reject(err);
            }
          };
          reader.onerror = (err) => reject(err);
          reader.readAsArrayBuffer(file);
          return;
        }
        
        // 对于大文件，使用分片计算
        const blobSlice = File.prototype.slice || File.prototype.mozSlice || File.prototype.webkitSlice;
        const chunkSize = 2097152; // 2MB per chunk for MD5 calculation
        const chunks = Math.ceil(file.size / chunkSize);
        let currentChunk = 0;
        const spark = new SparkMD5.ArrayBuffer();
        const fileReader = new FileReader();
        
        this.logInfo(`文件将分为 ${chunks} 个块计算`);
        
        fileReader.onload = (e) => {
          try {
            spark.append(e.target.result); // Append array buffer
            currentChunk++;
            
            const progress = Math.round((currentChunk / chunks) * 100);
            if (currentChunk % 10 === 0 || currentChunk === chunks) {
              this.logInfo(`MD5计算进度: ${progress}%`);
            }
            
            if (currentChunk < chunks) {
              loadNext();
            } else {
              const hash = spark.end();
              this.logInfo(`MD5计算完成: ${hash}`);
              resolve(hash); // Return the MD5 hash
            }
          } catch (error) {
            this.logError(`MD5计算中发生错误: ${error.message || error}`);
            reject(error);
          }
        };
        
        fileReader.onerror = (error) => {
          this.logError(`FileReader错误: ${error.message || error}`);
          reject(error);
        };
        
        const loadNext = () => {
          try {
            const start = currentChunk * chunkSize;
            const end = Math.min(file.size, start + chunkSize);
            const chunk = blobSlice.call(file, start, end);
            fileReader.readAsArrayBuffer(chunk);
          } catch (error) {
            this.logError(`加载下一个块时出错: ${error.message || error}`);
            reject(error);
          }
        };
        
        loadNext();
      });
    },
    
    async prepareFileUpload(task) {
      try {
        this.logInfo(`准备上传文件: ${task.name}`);
        
        // 准备分块上传请求
        const initRequest = {
          fileName: task.name,
          fileType: task.file.type,
          fileSize: task.file.size,
          chunkCount: task.totalChunks,
          fileMD5: task.md5
        };
        
        this.logInfo(`发送初始化请求: ${JSON.stringify(initRequest)}`);
        
        // 初始化分块上传
        const response = await initChunk(initRequest);
        this.logInfo(`初始化响应: ${JSON.stringify(response)}`);
        
        if (response.success && response.data) {
          task.taskId = response.data.taskId;
          task.status = 'uploading';
          this.logInfo(`获得任务ID: ${task.taskId}, 创建分片队列`);
          
          this.createChunkQueue(task);
          this.processUploadQueue();
        } else {
          this.logError(`初始化失败: ${JSON.stringify(response)}`);
          task.status = 'error';
          task.error = response.message || '初始化上传失败';
          this.checkAllTasksCompleted();
        }
      } catch (error) {
        this.logError(`上传准备错误: ${error.message || error}`);
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
      this.logInfo('处理上传队列');
      
      // 计算当前正在上传的文件数
      this.activeUploads = this.uploadTasks.filter(task => 
        task.status === 'uploading' && task.chunkQueue.some(chunk => chunk.uploading)
      ).length;
      
      this.logInfo(`当前活跃上传数: ${this.activeUploads}, 最大并行: ${this.maxParallelFiles}`);
      
      // 检查是否可以开始新的文件上传
      for (const task of this.uploadTasks) {
        if (task.status === 'uploading' && !task.chunkQueue.some(chunk => chunk.uploading)) {
          if (this.activeUploads < this.maxParallelFiles) {
            this.logInfo(`开始上传文件: ${task.name}`);
            this.uploadFileChunks(task);
            this.activeUploads++;
          }
        }
      }
      
      // 如果没有活跃上传但有等待中的任务，尝试再次处理
      if (this.activeUploads === 0) {
        const pendingTasks = this.uploadTasks.filter(task => 
          task.status === 'uploading' && task.chunkQueue.some(chunk => !chunk.uploaded)
        );
        
        if (pendingTasks.length > 0) {
          this.logInfo('重新尝试处理等待中的任务');
          for (const task of pendingTasks.slice(0, this.maxParallelFiles)) {
            this.uploadFileChunks(task);
            this.activeUploads++;
          }
        }
      }
    },
    
    async uploadFileChunks(task) {
      this.logInfo(`处理文件分片上传: ${task.name}`);
      
      // 计算当前活动的分片上传数量
      const activeChunks = task.chunkQueue.filter(chunk => chunk.uploading).length;
      this.logInfo(`活跃分片数: ${activeChunks}, 总分片数: ${task.totalChunks}, 已完成: ${task.uploadedChunks}`);
      
      // 如果所有分片已上传，完成上传
      if (task.chunkQueue.every(chunk => chunk.uploaded)) {
        this.logInfo(`所有分片已上传，完成文件 ${task.name}`);
        await this.completeFileUpload(task);
        return;
      }
      
      // 上传更多分片，直到达到并行限制
      let startedNewUploads = 0;
      for (let i = 0; i < task.chunkQueue.length && activeChunks + startedNewUploads < this.maxParallelChunks; i++) {
        const chunk = task.chunkQueue[i];
        if (!chunk.uploaded && !chunk.uploading) {
          this.logInfo(`开始上传分片 ${i+1}/${task.totalChunks}`);
          chunk.uploading = true;
          this.uploadChunk(task, chunk);
          startedNewUploads++;
        }
      }
      
      this.logInfo(`开始了 ${startedNewUploads} 个新分片上传`);
    },
    
    async uploadChunk(task, chunk) {
      try {
        this.logInfo(`上传分片: 文件=${task.name}, 分片索引=${chunk.index}, 分片大小=${chunk.file.size}字节`);
        
        // 使用 API 上传分片
        const response = await uploadChunk(task.taskId, chunk.index, chunk.file);
        this.logInfo(`分片上传响应: ${JSON.stringify(response)}`);
        
        if (response.success) {
          chunk.uploaded = true;
          chunk.uploading = false;
          task.uploadedChunks++;
          
          // 更新进度
          task.progress = (task.uploadedChunks / task.totalChunks) * 100;
          this.logInfo(`分片上传成功: ${chunk.index}, 进度: ${task.progress.toFixed(1)}%`);
          
          // 继续上传更多分片
          this.uploadFileChunks(task);
        } else {
          this.logWarn(`分片上传失败: ${chunk.index}, 错误: ${response.message}`);
          // 重试逻辑
          if (chunk.retries < 3) {
            chunk.retries++;
            chunk.uploading = false;
            this.logInfo(`将在1秒后重试分片 ${chunk.index} (尝试 ${chunk.retries}/3)`);
            // 稍后重试
            setTimeout(() => {
              this.uploadChunk(task, chunk);
            }, 1000);
          } else {
            chunk.uploading = false;
            task.status = 'error';
            task.error = response.message || `分片 ${chunk.index + 1} 上传失败`;
            this.logError(`分片 ${chunk.index} 达到最大重试次数, 标记任务为错误`);
            this.checkAllTasksCompleted();
          }
        }
      } catch (error) {
        this.logError(`分片上传错误: ${error.message || error}`);
        // 重试逻辑同上
        if (chunk.retries < 3) {
          chunk.retries++;
          chunk.uploading = false;
          this.logInfo(`将在1秒后重试分片 ${chunk.index} (尝试 ${chunk.retries}/3)`);
          setTimeout(() => {
            this.uploadChunk(task, chunk);
          }, 1000);
        } else {
          chunk.uploading = false;
          task.status = 'error';
          task.error = `分片 ${chunk.index + 1} 上传失败`;
          this.logError(`分片 ${chunk.index} 达到最大重试次数, 标记任务为错误`);
          this.checkAllTasksCompleted();
        }
      }
      
      // 检查其他任务的上传
      this.processUploadQueue();
    },
    
    async completeFileUpload(task) {
      this.logInfo(`完成文件上传: ${task.name}, 任务ID: ${task.taskId}`);
      task.status = 'verifying';
      
      try {
        this.logInfo('发送完成请求');
        const response = await completeChunk(task.taskId);
        this.logInfo(`完成响应: ${JSON.stringify(response)}`);
        
        if (response.success) {
          // 检查MD5是否匹配
          if (response.data.md5Verified) {
            task.status = 'completed';
            this.logInfo(`文件 ${task.name} 上传完成并验证成功`);
            
            const fileInfo = {
              fileName: response.data.originalName,
              serverFileName: response.data.serverFileName,
              size: response.data.size,
              md5: response.data.md5
            };
            
            this.logInfo(`发出上传完成事件: ${JSON.stringify(fileInfo)}`);
            this.$emit('upload-complete', fileInfo);
          } else {
            this.logError(`MD5验证失败: ${JSON.stringify(response.data)}`);
            task.status = 'error';
            task.error = `MD5校验失败，期望: ${response.data.expectedMD5}，实际: ${response.data.md5}`;
          }
        } else {
          this.logError(`完成上传失败: ${JSON.stringify(response)}`);
          task.status = 'error';
          task.error = response.message || '完成上传失败';
        }
      } catch (error) {
        this.logError(`上传完成处理错误: ${error.message || error}`);
        task.status = 'error';
        task.error = '完成上传失败';
      }
      
      // 检查是否所有任务都已完成
      this.checkAllTasksCompleted();
      
      // 处理下一个文件上传
      this.processUploadQueue();
    },
    
    checkAllTasksCompleted() {
      this.logInfo('检查是否所有任务都已完成');
      
      // 检查所有任务是否完成
      const isAllCompleted = this.uploadTasks.every(
        task => task.status === 'completed' || task.status === 'error'
      );
      
      this.logInfo(`所有任务是否完成: ${isAllCompleted}`);
      if (isAllCompleted && this.uploadTasks.length > 0) {
        this.logInfo('所有任务都已完成，发出全部完成事件');
        this.isUploading = false;
        this.$emit('all-completed');
        
        // 汇总结果
        const completedTasks = this.uploadTasks.filter(task => task.status === 'completed').length;
        const errorTasks = this.uploadTasks.filter(task => task.status === 'error').length;
        this.logInfo(`上传完成: ${completedTasks}个成功, ${errorTasks}个失败`);
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

.upload-dialog {
  width: 100%;
  background-color: white;
  border-radius: 8px;
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

.toggle-logs-btn {
  padding: 4px 10px;
  font-size: 12px;
  background-color: #f1f1f1;
  border: 1px solid #ddd;
  border-radius: 4px;
  cursor: pointer;
  color: #333;
}

.toggle-logs-btn:hover {
  background-color: #e9e9e9;
}

.log-container {
  margin-top: 20px;
  border: 1px solid #eee;
  border-radius: 4px;
  background-color: #f9f9f9;
}

.log-header {
  padding: 8px 12px;
  border-bottom: 1px solid #eee;
  background-color: #f1f1f1;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.log-header h4 {
  margin: 0;
  font-size: 14px;
  color: #333;
}

.log-actions {
  display: flex;
  gap: 8px;
}

.clear-logs-btn,
.copy-logs-btn {
  padding: 3px 8px;
  font-size: 11px;
  background-color: #fff;
  border: 1px solid #ddd;
  border-radius: 3px;
  cursor: pointer;
  color: #333;
}

.clear-logs-btn:hover,
.copy-logs-btn:hover {
  background-color: #f1f1f1;
}

.log-content {
  padding: 8px;
  max-height: 200px;
  overflow-y: auto;
  font-family: monospace;
  font-size: 12px;
  color: #333;
}

.log-entry {
  padding: 2px 0;
  border-bottom: 1px dotted #eee;
  white-space: pre-wrap;
  word-break: break-all;
}

.log-time {
  color: #666;
  margin-right: 6px;
}

.log-level {
  font-weight: bold;
  margin-right: 6px;
}

.log-info .log-level {
  color: #2196f3;
}

.log-warn .log-level {
  color: #ff9800;
}

.log-error .log-level {
  color: #f44336;
}

.log-message {
  word-wrap: break-word;
}

</style>
