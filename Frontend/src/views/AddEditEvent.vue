<template>
  <div class="add-edit-event">
    <!-- 头部 -->
    <header class="header">
      <button class="back-btn" @click="goBack">
        ← 返回
      </button>
      <h1 class="page-title">{{ isEdit ? '编辑事件' : '新增事件' }}</h1>
    </header>

    <!-- 加载状态 -->
    <div v-if="loading" class="loading-container">
      <div class="loading-spinner">⏳</div>
      <p>正在加载事件数据...</p>
    </div>

    <!-- 错误状态 -->
    <div v-else-if="error && !isEdit" class="error-container">
      <div class="error-icon">⚠️</div>
      <p>加载失败</p>
      <button class="retry-btn" @click="loadEditData">重试</button>
    </div>

    <!-- 表单容器 -->
    <div v-else class="form-container">

      
      <form @submit.prevent="saveEvent" class="event-form">
        <!-- 基本信息 -->
        <div class="form-section">
          <h3 class="section-title">基本信息</h3>
          
          <div class="form-group">
            <label class="form-label" for="title">事件标题 *</label>
            <input 
              type="text" 
              id="title"
              v-model="formData.title"
              class="form-input"
              placeholder="请输入事件标题"
              required
            />
          </div>

          <div class="form-group">
            <label class="form-label" for="date">事件日期 *</label>
            <input 
              type="date" 
              id="date"
              v-model="formData.date"
              class="form-input"
              required
            />
          </div>

          <div class="form-group">
            <label class="form-label" for="location">地点</label>
            <input 
              type="text" 
              id="location"
              v-model="formData.location"
              class="form-input"
              placeholder="请输入地点"
            />
          </div>

          <div class="form-group">
            <label class="form-label" for="description">详细描述</label>
            <textarea 
              id="description"
              v-model="formData.description"
              class="form-textarea"
              rows="4"
              placeholder="请输入详细描述"
            ></textarea>
          </div>
        </div>

        <!-- 照片上传 -->
        <div class="form-section">
          <h3 class="section-title">照片</h3>
          
          <div class="upload-area" @click="!uploading && triggerImageUpload()" :class="{ uploading }">
            <input 
              type="file" 
              ref="imageInput" 
              @change="handleImageUpload"
              accept="image/*"
              multiple
              style="display: none;"
              :disabled="uploading"
            />
            <div class="upload-placeholder">
              <span v-if="uploading">⏳</span>
              <span v-else>📷</span>
              <p>{{ uploading ? '正在上传照片...' : '点击上传照片' }}</p>
              <span class="upload-hint" v-if="!uploading">支持多张图片上传</span>
            </div>
          </div>

          <div class="media-list" v-if="formData.media.images.length > 0">
            <div 
              v-for="(image, index) in formData.media.images" 
              :key="`image-${index}`"
              class="media-item"
            >
              <div 
                class="media-preview"
                :style="image.fileName && isEdit ? { backgroundImage: `url(${getMediaUrl(route.params.id, image.fileName)})` } : {}"
              >
                <span v-if="!image.fileName || !isEdit" class="media-icon">📷</span>
              </div>
              <div class="media-info">
                <input 
                  type="text" 
                  v-model="image.desc"  
                  class="media-desc-input"
                  placeholder="请输入图片描述"
                />
              </div>
              <button 
                type="button" 
                class="remove-btn"
                @click="removeMedia('images', index)"
              >
                ✕
              </button>
            </div>
          </div>
        </div>

        <!-- 视频上传 -->
        <div class="form-section">
          <h3 class="section-title">视频</h3>
          
          <div class="upload-area" @click="!uploading && triggerVideoUpload()" :class="{ uploading }">
            <input 
              type="file" 
              ref="videoInput" 
              @change="handleVideoUpload"
              accept="video/*"
              multiple
              style="display: none;"
              :disabled="uploading"
            />
            <div class="upload-placeholder">
              <span v-if="uploading">⏳</span>
              <span v-else>🎬</span>
              <p>{{ uploading ? '正在上传视频...' : '点击上传视频' }}</p>
              <span class="upload-hint" v-if="!uploading">支持多个视频上传</span>
            </div>
          </div>

          <div class="media-list" v-if="formData.media.videos.length > 0">
            <div 
              v-for="(video, index) in formData.media.videos" 
              :key="`video-${index}`"
              class="media-item"
            >
              <div class="media-preview video">
                <span class="media-icon">🎬</span>
              </div>
              <div class="media-info">
                <input 
                  type="text" 
                  v-model="video.desc"  
                  class="media-desc-input"
                  placeholder="请输入视频描述"
                />
              </div>
              <button 
                type="button" 
                class="remove-btn"
                @click="removeMedia('videos', index)"
              >
                ✕
              </button>
            </div>
          </div>
        </div>

        <!-- 录音功能 -->
        <div class="form-section">
          <h3 class="section-title">录音</h3>
          
          <!-- 录音控制区域 -->
          <div class="recording-controls">
            <button 
              type="button" 
              class="record-btn"
              :class="{ recording: isRecording, disabled: uploading }"
              @click="toggleRecording"
              :disabled="uploading"
            >
              <span v-if="isRecording">⏹️ 停止录音</span>
              <span v-else-if="uploading">⏳ 上传中...</span>
              <span v-else>🎤 开始录音</span>
            </button>
            
            <div v-if="isRecording" class="recording-info">
              <span class="recording-time">{{ formatTime(recordingTime) }}</span>
              <span class="recording-indicator">🔴 录音中</span>
            </div>
          </div>

          <!-- 文件上传备选 -->
          <div class="upload-area secondary" @click="!uploading && !isRecording && triggerAudioUpload()" :class="{ uploading, disabled: isRecording }">
            <input 
              type="file" 
              ref="audioInput" 
              @change="handleAudioUpload"
              accept="audio/*"
              multiple
              style="display: none;"
              :disabled="uploading || isRecording"
            />
            <div class="upload-placeholder">
              <span>📁</span>
              <p>或者点击上传音频文件</p>
              <span class="upload-hint">支持多段音频上传</span>
            </div>
          </div>

          <div class="media-list" v-if="formData.media.audios.length > 0">
            <div 
              v-for="(audio, index) in formData.media.audios" 
              :key="`audio-${index}`"
              class="media-item"
            >
              <div class="media-preview audio">
                <span class="media-icon">🎙</span>
              </div>
              <div class="media-info">
                <input 
                  type="text" 
                  v-model="audio.desc"  
                  class="media-desc-input"
                  placeholder="请输入音频描述"
                />
              </div>
              <button 
                type="button" 
                class="remove-btn"
                @click="removeMedia('audios', index)"
              >
                ✕
              </button>
            </div>
          </div>
        </div>

        <!-- 提交按钮 -->
        <div class="form-actions">
          <button type="button" class="cancel-btn" @click="goBack">
            取消
          </button>
          <button type="submit" class="save-btn" :disabled="!isFormValid || saving">
            {{ saving ? '保存中...' : (isEdit ? '保存修改' : '创建事件') }}
          </button>
        </div>
      </form>
    </div>
  </div>
</template>

<script>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import dayjs from 'dayjs'
import { getEventById, createEvent, updateEvent, uploadFiles, getMediaUrl } from '@/api/events'
import AudioRecorder from '@/utils/AudioRecorder'

export default {
  name: 'AddEditEvent',
  setup() {
    const route = useRoute()
    const router = useRouter()
    
    const imageInput = ref(null)
    const videoInput = ref(null)
    const audioInput = ref(null)
    const loading = ref(false)
    const saving = ref(false)
    const uploading = ref(false)
    const error = ref('')
    
    // 录音相关状态
    const isRecording = ref(false)
    const recordingTime = ref(0)
    const audioRecorder = ref(null)
    
    const isEdit = computed(() => !!route.params.id)
    
    // 表单数据
    const formData = ref({
      title: '',
      date: dayjs().format('YYYY-MM-DD'),
      location: '',
      description: '',
      media: {
        images: [],
        videos: [],
        audios: []
      }
    })

    // 表单验证
    const isFormValid = computed(() => {
      return formData.value.title.trim() !== '' && formData.value.date !== ''
    })

    // 加载编辑数据
    const loadEditData = async () => {
      if (isEdit.value) {
      try {
        loading.value = true
          
          const eventId = route.params.id
          const response = await getEventById(eventId)
          
          if (response.success) {
            const event = response.data
            formData.value = {
              title: event.title,
              date: event.date,
              location: event.location || '',
              description: event.description || '',
              media: {
                images: [...event.media.images],
                videos: [...event.media.videos],
                audios: [...event.media.audios]
              }
            }
          } else {
            alert(response.message || '获取事件数据失败')
            // 如果事件不存在，返回首页
            if (response.message === '事件不存在') {
              setTimeout(() => {
                router.push('/')
              }, 1000)
            }
          }
        } catch (err) {
          alert('网络错误，请稍后重试')
          console.error('加载编辑数据失败:', err)
        } finally {
          loading.value = false
        }
      }
    }

    // 触发文件上传
    const triggerImageUpload = () => {
      imageInput.value.click()
    }

    const triggerVideoUpload = () => {
      videoInput.value.click()
    }

    const triggerAudioUpload = () => {
      audioInput.value.click()
    }

    // 处理文件上传
    const handleImageUpload = async (event) => {
      const files = Array.from(event.target.files)
      if (files.length === 0) return

      try {
        uploading.value = true

        const response = await uploadFiles(files, 'image')
        
        if (response.success) {
          response.data.successful.forEach(uploadedFile => {
            formData.value.media.images.push({
              fileName: uploadedFile.serverFileName, // 使用服务器端文件名
              desc: '',
              size: uploadedFile.size,
              uploadTime: uploadedFile.uploadTime
            })
          })
          
          if (response.data.failed > 0) {
            alert(response.message)
          }
        } else {
          alert(response.message || '图片上传失败')
        }
      } catch (err) {
        alert('网络错误，图片上传失败')
        console.error('图片上传失败:', err)
      } finally {
        uploading.value = false
        event.target.value = '' // 清空input
      }
    }

    const handleVideoUpload = async (event) => {
      const files = Array.from(event.target.files)
      if (files.length === 0) return

      try {
        uploading.value = true

        const response = await uploadFiles(files, 'video')
        
        if (response.success) {
          response.data.successful.forEach(uploadedFile => {
            formData.value.media.videos.push({
              fileName: uploadedFile.serverFileName, // 使用服务器端文件名
              desc: '',
              size: uploadedFile.size,
              uploadTime: uploadedFile.uploadTime
            })
          })
          
          if (response.data.failed > 0) {
            alert(response.message)
          }
        } else {
          alert(response.message || '视频上传失败')
        }
      } catch (err) {
        alert('网络错误，视频上传失败')
        console.error('视频上传失败:', err)
      } finally {
        uploading.value = false
        event.target.value = ''
      }
    }

    const handleAudioUpload = async (event) => {
      const files = Array.from(event.target.files)
      if (files.length === 0) return

      try {
        uploading.value = true

        const response = await uploadFiles(files, 'audio')
        
        if (response.success) {
          response.data.successful.forEach(uploadedFile => {
            formData.value.media.audios.push({
              fileName: uploadedFile.serverFileName, // 使用服务器端文件名
              desc: '',
              size: uploadedFile.size,
              uploadTime: uploadedFile.uploadTime
            })
          })
          
          if (response.data.failed > 0) {
            alert(response.message)
          }
        } else {
          alert(response.message || '音频上传失败')
        }
      } catch (err) {
        alert('网络错误，音频上传失败')
        console.error('音频上传失败:', err)
      } finally {
        uploading.value = false
        event.target.value = ''
      }
    }

    // 录音相关方法
    const initAudioRecorder = () => {
      if (!audioRecorder.value) {
        audioRecorder.value = new AudioRecorder()
        
        // 设置录音完成回调
        audioRecorder.value.onStop = async (audioBlob, duration) => {
          await uploadRecordedAudio(audioBlob, duration)
        }
        
        // 设置录音时间更新回调
        audioRecorder.value.onTimeUpdate = (time) => {
          recordingTime.value = time
        }
        
        // 设置错误回调
        audioRecorder.value.onError = (err) => {
          alert(`录音失败: ${err.message || '未知错误'}`)
          isRecording.value = false
          recordingTime.value = 0
        }
      }
    }

    const startRecording = async () => {
      try {
        if (!AudioRecorder.isSupported()) {
          alert('当前浏览器不支持录音功能，请使用现代浏览器')
          return
        }

        initAudioRecorder()
        await audioRecorder.value.startRecording()
        isRecording.value = true
        recordingTime.value = 0
      } catch (err) {
        alert(err.message || '无法启动录音，请检查麦克风权限')
        isRecording.value = false
        console.error('录音启动失败:', err)
      }
    }

    const stopRecording = () => {
      if (audioRecorder.value && isRecording.value) {
        audioRecorder.value.stopRecording()
        isRecording.value = false
      }
    }

    const toggleRecording = () => {
      if (isRecording.value) {
        stopRecording()
      } else {
        startRecording()
      }
    }

    const uploadRecordedAudio = async (audioBlob, duration) => {
      try {
        uploading.value = true

        // 创建文件名
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-')
        const extension = audioRecorder.value.getFileExtension()
        const fileName = `recording_${timestamp}.${extension}`
        
        // 创建File对象
        const audioFile = new File([audioBlob], fileName, { 
          type: audioBlob.type 
        })

        const response = await uploadFiles([audioFile], 'audio')
        
        if (response.success) {
          response.data.successful.forEach(uploadedFile => {
            formData.value.media.audios.push({
              fileName: uploadedFile.serverFileName,
              desc: `录音 (${formatTime(duration)})`,
              size: uploadedFile.size,
              uploadTime: uploadedFile.uploadTime
            })
          })
          
          if (response.data.failed > 0) {
            alert(response.message)
          }
        } else {
          alert(response.message || '录音上传失败')
        }
      } catch (err) {
        alert('网络错误，录音上传失败')
        console.error('录音上传失败:', err)
      } finally {
        uploading.value = false
        recordingTime.value = 0
      }
    }

    // 格式化时间显示
    const formatTime = (seconds) => {
      const mins = Math.floor(seconds / 60)
      const secs = seconds % 60
      return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`
    }

    // 移除媒体文件
    const removeMedia = (type, index) => {
      formData.value.media[type].splice(index, 1)
    }

    // 保存事件
    const saveEvent = async () => {
      if (!isFormValid.value || saving.value) return

      try {
        saving.value = true

        const eventData = {
          title: formData.value.title,
          date: formData.value.date,
          location: formData.value.location,
          description: formData.value.description,
          media: {
            images: formData.value.media.images.map(img => ({
              fileName: img.fileName, // 这里保存的是服务器端文件名
              desc: img.desc
            })),
            videos: formData.value.media.videos.map(vid => ({
              fileName: vid.fileName, // 这里保存的是服务器端文件名
              desc: vid.desc
            })),
            audios: formData.value.media.audios.map(aud => ({
              fileName: aud.fileName, // 这里保存的是服务器端文件名
              desc: aud.desc
            }))
          }
        }

        let response
        if (isEdit.value) {
          response = await updateEvent(route.params.id, eventData)
        } else {
          response = await createEvent(eventData)
        }

        if (response.success) {
          // 保存成功，返回首页并定位到该事件
          const eventId = isEdit.value ? route.params.id : response.data.id
          router.push({
            path: '/',
            query: { highlight: eventId }
          })
        } else {
          alert(response.message || '保存失败')
        }
      } catch (err) {
        alert('网络错误，保存失败')
        console.error('保存事件失败:', err)
      } finally {
        saving.value = false
      }
    }

    // 返回
    const goBack = () => {
      if (isEdit.value) {
        // 编辑模式下返回到详情页面
        router.push(`/event/${route.params.id}`)
      } else {
        // 新增模式下返回到首页
        router.push('/')
      }
    }

    onMounted(async () => {
      await loadEditData()
    })

    onUnmounted(() => {
      // 清理录音资源
      if (audioRecorder.value) {
        audioRecorder.value.destroy()
      }
    })

    return {
      isEdit,
      formData,
      isFormValid,
      loading,
      saving,
      uploading,
      error,
      imageInput,
      videoInput,
      audioInput,
      isRecording,
      recordingTime,
      triggerImageUpload,
      triggerVideoUpload,
      triggerAudioUpload,
      handleImageUpload,
      handleVideoUpload,
      handleAudioUpload,
      toggleRecording,
      formatTime,
      removeMedia,
      saveEvent,
      goBack,
      loadEditData,
      getMediaUrl,
      route
    }
  }
}
</script>

<style scoped>
.add-edit-event {
  min-height: 100vh;
  background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
  padding: 20px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

/* 头部 */
.header {
  display: flex;
  align-items: center;
  margin-bottom: 30px;
  max-width: 800px;
  margin-left: auto;
  margin-right: auto;
}

.back-btn {
  background: white;
  border: none;
  padding: 10px 15px;
  border-radius: 10px;
  cursor: pointer;
  font-size: 16px;
  margin-right: 20px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  transition: transform 0.2s ease;
}

.back-btn:hover {
  transform: translateY(-2px);
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: #2c3e50;
  margin: 0;
}

/* 表单容器 */
.form-container {
  max-width: 800px;
  margin: 0 auto;
  background: white;
  border-radius: 20px;
  padding: 30px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
}

.event-form {
  display: flex;
  flex-direction: column;
  gap: 30px;
}

/* 表单区块 */
.form-section {
  border-bottom: 1px solid #ecf0f1;
  padding-bottom: 25px;
}

.form-section:last-of-type {
  border-bottom: none;
}

.section-title {
  font-size: 18px;
  font-weight: 600;
  color: #2c3e50;
  margin: 0 0 20px 0;
  padding-bottom: 8px;
  border-bottom: 2px solid #3498db;
  display: inline-block;
}

/* 表单组 */
.form-group {
  margin-bottom: 20px;
}

.form-label {
  display: block;
  font-size: 14px;
  font-weight: 600;
  color: #2c3e50;
  margin-bottom: 8px;
}

.form-input, .form-textarea {
  width: 100%;
  padding: 12px 16px;
  border: 2px solid #ecf0f1;
  border-radius: 10px;
  font-size: 14px;
  transition: border-color 0.3s ease;
  box-sizing: border-box;
}

.form-input:focus, .form-textarea:focus {
  outline: none;
  border-color: #3498db;
}

.form-textarea {
  resize: vertical;
  min-height: 100px;
}

/* 上传区域 */
.upload-area {
  border: 2px dashed #bdc3c7;
  border-radius: 10px;
  padding: 30px;
  text-align: center;
  cursor: pointer;
  transition: all 0.3s ease;
  margin-bottom: 20px;
}

.upload-area:hover:not(.uploading) {
  border-color: #3498db;
  background-color: #f8f9fa;
}

.upload-area.uploading {
  border-color: #f39c12;
  background-color: #fef9e7;
  cursor: not-allowed;
}

.upload-placeholder {
  color: #7f8c8d;
}

.upload-placeholder p {
  font-size: 16px;
  margin: 10px 0 5px 0;
  font-weight: 500;
}

.upload-hint {
  font-size: 12px;
  color: #95a5a6;
}

/* 媒体列表 */
.media-list {
  display: flex;
  flex-direction: column;
  gap: 15px;
}

.media-item {
  display: flex;
  align-items: center;
  gap: 15px;
  padding: 15px;
  background: #f8f9fa;
  border-radius: 10px;
  border: 1px solid #ecf0f1;
}

.media-preview {
  width: 50px;
  height: 50px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
  color: white;
  flex-shrink: 0;
  background: linear-gradient(135deg, #84fab0 0%, #8fd3f4 100%);
  background-size: cover;
  background-position: center;
  background-repeat: no-repeat;
}

.media-icon {
  text-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
}

.media-preview.video {
  background: linear-gradient(135deg, #8e44ad 0%, #9b59b6 100%);
}

.media-preview.audio {
  background: linear-gradient(135deg, #e67e22 0%, #d35400 100%);
}

.media-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.media-desc-input {
  padding: 8px 12px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
}

.media-desc-input:focus {
  outline: none;
  border-color: #3498db;
}

.media-filename {
  font-size: 12px;
  color: #7f8c8d;
  font-style: italic;
}

.remove-btn {
  width: 30px;
  height: 30px;
  border-radius: 50%;
  border: none;
  background: #e74c3c;
  color: white;
  font-size: 14px;
  cursor: pointer;
  transition: transform 0.2s ease;
  flex-shrink: 0;
}

.remove-btn:hover {
  transform: scale(1.1);
}

/* 操作按钮 */
.form-actions {
  display: flex;
  gap: 15px;
  justify-content: center;
  padding-top: 20px;
}

.cancel-btn, .save-btn {
  padding: 12px 30px;
  border: none;
  border-radius: 25px;
  font-size: 16px;
  cursor: pointer;
  transition: transform 0.2s ease;
  min-width: 120px;
}

.cancel-btn {
  background: #95a5a6;
  color: white;
}

.save-btn {
  background: linear-gradient(135deg, #27ae60 0%, #2ecc71 100%);
  color: white;
}

.save-btn:disabled {
  background: #bdc3c7;
  cursor: not-allowed;
  transform: none;
}

/* 加载和错误状态 */
.loading-container, .error-container {
  text-align: center;
  padding: 60px 20px;
  max-width: 800px;
  margin: 0 auto;
  color: #7f8c8d;
}

.loading-spinner, .error-icon {
  font-size: 48px;
  margin-bottom: 20px;
}

.retry-btn {
  background: linear-gradient(135deg, #3498db 0%, #2980b9 100%);
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 20px;
  cursor: pointer;
  font-size: 14px;
  margin-top: 15px;
  transition: transform 0.2s ease;
}

.retry-btn:hover {
  transform: translateY(-2px);
}



.cancel-btn:hover, .save-btn:hover:not(:disabled) {
  transform: translateY(-2px);
}

/* 录音控制样式 */
.recording-controls {
  display: flex;
  align-items: center;
  gap: 20px;
  justify-content: center;
  margin-bottom: 20px;
  padding: 20px;
  background: #f8f9fa;
  border-radius: 15px;
  border: 2px solid #ecf0f1;
}

.record-btn {
  padding: 15px 30px;
  border: none;
  border-radius: 30px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.3s ease;
  background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
  color: white;
  box-shadow: 0 4px 15px rgba(231, 76, 60, 0.3);
  min-width: 150px;
}

.record-btn.recording {
  background: linear-gradient(135deg, #34495e 0%, #2c3e50 100%);
  animation: pulse 1.5s infinite;
  box-shadow: 0 4px 15px rgba(52, 73, 94, 0.3);
}

.record-btn:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(231, 76, 60, 0.4);
}

.record-btn.recording:hover {
  box-shadow: 0 6px 20px rgba(52, 73, 94, 0.4);
}

.record-btn:disabled {
  background: #bdc3c7;
  cursor: not-allowed;
  transform: none;
  box-shadow: none;
}

.recording-info {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 5px;
  color: #e74c3c;
}

.recording-time {
  font-size: 24px;
  font-weight: 700;
  font-family: 'Courier New', monospace;
  color: #e74c3c;
  text-shadow: 0 1px 3px rgba(231, 76, 60, 0.3);
}

.recording-indicator {
  font-size: 14px;
  font-weight: 600;
  animation: blink 1s infinite;
}

.upload-area.secondary {
  border-style: solid;
  border-width: 1px;
  border-color: #ddd;
  background: #fafafa;
  margin-top: 15px;
}

.upload-area.secondary:hover:not(.uploading):not(.disabled) {
  border-color: #95a5a6;
  background-color: #f0f0f0;
}

.upload-area.disabled {
  opacity: 0.5;
  cursor: not-allowed;
  pointer-events: none;
}

@keyframes pulse {
  0% { 
    transform: scale(1); 
    box-shadow: 0 4px 15px rgba(52, 73, 94, 0.3);
  }
  50% { 
    transform: scale(1.05); 
    box-shadow: 0 6px 20px rgba(52, 73, 94, 0.5);
  }
  100% { 
    transform: scale(1); 
    box-shadow: 0 4px 15px rgba(52, 73, 94, 0.3);
  }
}

@keyframes blink {
  0%, 50% { opacity: 1; }
  51%, 100% { opacity: 0.3; }
}

/* 响应式设计 */
@media (max-width: 768px) {
  .add-edit-event {
    padding: 15px;
  }
  
  .recording-controls {
    flex-direction: column;
    gap: 15px;
    padding: 15px;
  }
  
  .record-btn {
    width: 100%;
    padding: 12px 20px;
    font-size: 14px;
    min-width: auto;
  }
  
  .recording-time {
    font-size: 20px;
  }
  
  .form-container {
    padding: 20px;
  }
  
  .upload-area {
    padding: 20px;
  }
  
  .media-item {
    flex-direction: row;
    align-items: center;
    gap: 10px;
    padding: 10px;
  }
  
  .media-preview {
    width: 40px;
    height: 40px;
    font-size: 20px;
    flex-shrink: 0;
  }
  
  .media-desc-input {
    font-size: 13px;
    padding: 6px 10px;
  }
  
  .remove-btn {
    width: 28px;
    height: 28px;
    font-size: 12px;
    flex-shrink: 0;
  }
  
  .form-actions {
    flex-direction: column;
  }
  
  .cancel-btn, .save-btn {
    width: 100%;
  }
}
</style>
