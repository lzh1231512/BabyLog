<template>
  <div class="chunk-upload-demo">
    <h1>分片上传演示</h1>
    
    <!-- 使用ChunkUploader组件 -->
    <ChunkUploader 
      :maxParallelFiles="3"
      :maxParallelChunks="3"
      :chunkSize="2 * 1024 * 1024"
      @upload-complete="handleUploadComplete"
      @all-completed="handleAllCompleted"
    />
    
    <!-- 上传完成的文件列表 -->
    <div class="completed-files" v-if="completedFiles.length > 0">
      <h2>上传完成的文件</h2>
      <ul>
        <li v-for="(file, index) in completedFiles" :key="index">
          {{ file.fileName }} ({{ formatFileSize(file.size) }}) - MD5: {{ file.md5 }}
        </li>
      </ul>
    </div>
  </div>
</template>

<script>
import ChunkUploader from '../components/ChunkUploader.vue'

export default {
  name: 'ChunkUploadDemo',
  components: {
    ChunkUploader
  },
  data() {
    return {
      completedFiles: []
    }
  },
  methods: {
    handleUploadComplete(fileInfo) {
      this.completedFiles.push(fileInfo)
      console.log('文件上传完成:', fileInfo)
    },
    
    handleAllCompleted() {
      console.log('所有文件上传完成')
    },
    
    formatFileSize(bytes) {
      if (bytes < 1024) return bytes + ' B'
      else if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB'
      else if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + ' MB'
      else return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB'
    }
  }
}
</script>

<style scoped>
.chunk-upload-demo {
  padding: 20px;
  max-width: 800px;
  margin: 0 auto;
}

h1 {
  margin-bottom: 20px;
}

.completed-files {
  margin-top: 30px;
  padding: 15px;
  background-color: #f9f9f9;
  border-radius: 4px;
}

.completed-files h2 {
  margin-bottom: 10px;
  font-size: 18px;
}

ul {
  padding-left: 20px;
}

li {
  margin-bottom: 5px;
}
</style>
