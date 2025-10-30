<template>
  <div class="add-edit-event">
    <!-- Snackbar 提示组件 -->
    <Snackbar 
      v-model="snackbar.show"
      :message="snackbar.message" 
      :subtext="snackbar.subtext" 
      :type="snackbar.type"
      :duration="snackbar.duration"
    />

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
      
      <!-- 分片上传组件 -->
      <div v-if="showChunkUploader" class="chunk-uploader-overlay">
        <div class="chunk-uploader-container">
          <h3>文件分片上传</h3>
          <ChunkUploader
            :maxParallelFiles="3"
            :maxParallelChunks="3"
            :chunkSize="2 * 1024 * 1024"
            @upload-complete="handleUploadComplete"
            @all-completed="handleAllUploadsCompleted"
            ref="chunkUploader"
          />
          <!-- 上传状态指示器 -->
          <div class="chunk-uploader-status">
            <div v-if="uploading" class="upload-status-indicator">
              上传中...请等待
            </div>
            <div v-else class="upload-status-completed">
              上传已完成
            </div>
          </div>
          <div v-if="uploading && uploaderStatus.taskCount === 0" class="upload-error">
            <p>上传组件未正确初始化或未添加文件</p>
            <button type="button" class="retry-btn" @click="retryUpload">重试上传</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import dayjs from 'dayjs'
import { getEventById, createEvent, updateEvent, getMediaUrl } from '@/api/events'
import AudioRecorder from '@/utils/AudioRecorder'
import ChunkUploader from '@/components/ChunkUploader.vue'
import Snackbar from '@/components/Snackbar.vue'

export default {
  name: 'AddEditEvent',
  components: {
    ChunkUploader,
    Snackbar
  },
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
    
    // 分片上传相关状态
    const showChunkUploader = ref(false)
    const uploadFiles = ref([])
    const currentMediaType = ref('')
    const chunkUploader = ref(null) // 分片上传组件的引用
    
    // 录音相关状态
    const isRecording = ref(false)
    const recordingTime = ref(0)
    const audioRecorder = ref(null)
    
    // Snackbar提示相关状态
    const snackbar = ref({
      show: false,
      message: '',
      subtext: '',
      type: 'info',
      duration: 2000
    })
    
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
    
    // 上传组件状态
    const uploaderStatus = computed(() => {
      if (!chunkUploader.value) {
        return { exists: false, taskCount: 0, isUploading: false };
      }
      
      return {
        exists: true,
        taskCount: chunkUploader.value.uploadTasks?.length || 0,
        isUploading: chunkUploader.value.isUploading || false
      };
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

    // 处理文件上传 - 使用分片上传
    // 公共上传处理函数
    const handleChunkUpload = (files, mediaType) => {
      if (!files || files.length === 0) {
        console.log('没有选择文件');
        return;
      }
      
      console.log(`处理文件上传: 类型=${mediaType}, 文件数=${files.length}`);
      files.forEach((file, index) => {
        console.log(`文件[${index}]: 名称=${file.name}, 大小=${file.size}字节, 类型=${file.type}`);
      });
      
      // 保存文件和媒体类型，用于稍后上传
      uploadFiles.value = [...files]; 
      currentMediaType.value = mediaType;
      
      // 显示上传对话框
      showChunkUploader.value = true;
      uploading.value = true;
      
      // 使用两层nextTick确保组件完全渲染后再添加文件
      // 第一个nextTick等待showChunkUploader的变化生效
      nextTick(() => {
        console.log('第一个nextTick: 检查上传组件是否存在:', !!chunkUploader.value);
        
        // 第二个nextTick确保组件已完全初始化
        nextTick(() => {
          console.log('第二个nextTick: 再次检查组件:', !!chunkUploader.value);
          
          if (chunkUploader.value) {
            console.log('添加文件到上传组件');
            // 添加保存的文件到上传组件
            uploadFiles.value.forEach((file, index) => {
              console.log(`添加文件[${index}]: ${file.name} 到上传组件`);
              try {
                chunkUploader.value.addFile(file);
                console.log(`文件[${index}]添加成功`);
              } catch (error) {
                console.error(`添加文件[${index}]失败:`, error);
              }
            });
          } else {
            console.error('两次nextTick后组件引用仍不存在!');
            // 尝试直接获取组件
            const uploader = document.querySelector('.chunk-uploader');
            console.log('DOM中是否存在上传组件:', !!uploader);
          }
        });
      });
    }
    
    // 处理图片上传
    const handleImageUpload = (event) => {
      const files = Array.from(event.target.files);
      handleChunkUpload(files, 'images');
      event.target.value = ''; // 清空input
    }

    // 处理视频上传
    const handleVideoUpload = (event) => {
      const files = Array.from(event.target.files);
      handleChunkUpload(files, 'videos');
      event.target.value = '';
    }

    // 处理音频上传
    const handleAudioUpload = (event) => {
      const files = Array.from(event.target.files);
      handleChunkUpload(files, 'audios');
      event.target.value = '';
    }
    
    // 处理分片上传完成
    const handleUploadComplete = (fileInfo) => {
      console.log('收到单个文件上传完成事件:', fileInfo);
      
      // 根据当前媒体类型，添加到对应的媒体列表中
      if (currentMediaType.value) {
        console.log(`将文件添加到媒体类型: ${currentMediaType.value}`);
        
        // 检查是否是录音文件，如果是则添加特殊描述
        let desc = fileInfo.fileName || '';
        if (currentMediaType.value === 'audios' && uploadFiles.value.recordingDuration) {
          desc = `录音 (${formatTime(uploadFiles.value.recordingDuration)})`;
          console.log(`设置录音描述: ${desc}`);
        }
        
        // 如果文件有捕获时间，记录在控制台
        if (fileInfo.captureTime) {
          console.log(`文件 ${fileInfo.fileName} 有捕获时间: ${fileInfo.captureTime}`);
        }
        
        const mediaItem = {
          fileName: fileInfo.serverFileName, // 使用服务器端文件名
          desc: desc,
          size: fileInfo.size,
          uploadTime: new Date().toISOString(),
          captureTime: fileInfo.captureTime || null // 保存捕获时间信息
        };
        
        console.log('添加媒体项:', mediaItem);
        formData.value.media[currentMediaType.value].push(mediaItem);
        console.log(`当前${currentMediaType.value}数量: ${formData.value.media[currentMediaType.value].length}`);
        
        // 显示上传成功提示
        let mediaTypeText = '';
        switch (currentMediaType.value) {
          case 'images': mediaTypeText = '图片'; break;
          case 'videos': mediaTypeText = '视频'; break;
          case 'audios': mediaTypeText = '音频'; break;
        }
        
        showSnackbar(`${mediaTypeText}上传成功`, {
          subtext: fileInfo.fileName,
          type: 'info',
          duration: 1000
        });
      } else {
        console.warn('文件上传完成但媒体类型未设置!');
        showSnackbar('文件上传完成', {
          type: 'warning',
          subtext: '媒体类型未设置，请重试',
          duration: 3000
        });
      }
    }
    
    // 所有文件上传完成
    const handleAllUploadsCompleted = (data) => {
      console.log('所有文件上传完成', data);
      uploading.value = false;
      
      // 检查并处理捕获时间
      if (data && data.captureTime) {
        console.log(`获取到捕获时间: ${data.captureTime}`);
        // 将捕获时间转换为日期格式 YYYY-MM-DD 并更新表单日期
        try {
          const captureDate = dayjs(data.captureTime).format('YYYY-MM-DD');
          const oldDate = formData.value.date;
          console.log(`将事件日期从 ${oldDate} 更新为 ${captureDate}`);
          formData.value.date = captureDate;
          
          // 使用辅助函数显示Snackbar提示
          showSnackbar('已从媒体文件中检测到日期', {
            subtext: `事件日期已自动更新为: ${captureDate}`,
            type: 'success',
            duration: 1000
          });
        } catch (error) {
          console.error('无法解析捕获时间:', error);
          
          // 使用辅助函数显示错误提示
          showSnackbar('日期格式解析失败', {
            subtext: '无法从媒体文件中提取日期信息',
            type: 'error',
            duration: 4000
          });
        }
      }
      
      // 清除录音持续时间
      if (uploadFiles.value.recordingDuration) {
        uploadFiles.value.recordingDuration = null;
        console.log('清除录音持续时间');
      }
      
      recordingTime.value = 0;
      
      // 延迟隐藏上传区域，给用户一点时间看到完成状态
      setTimeout(() => {
        console.log('隐藏上传区域');
        showChunkUploader.value = false;
      }, 1000);
    }
    
    // 重试上传
    const retryUpload = () => {
      console.log('重试上传, 文件数:', uploadFiles.value.length);
      
      if (!uploadFiles.value || uploadFiles.value.length === 0) {
        console.warn('没有待上传文件，无法重试');
        return;
      }
      
      // 添加小延迟以确保组件已完全初始化
      nextTick(() => {
        if (chunkUploader.value) {
          console.log('尝试重新添加文件到上传组件');
          uploadFiles.value.forEach((file, index) => {
            console.log(`重新添加文件[${index}]: ${file.name}`);
            try {
              chunkUploader.value.addFile(file);
            } catch (error) {
              console.error(`重新添加文件失败:`, error);
            }
          });
        } else {
          console.error('上传组件仍不可用');
        }
      });
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
        // 创建文件名
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-')
        const extension = audioRecorder.value.getFileExtension()
        const fileName = `recording_${timestamp}.${extension}`
        
        // 创建File对象
        const audioFile = new File([audioBlob], fileName, { 
          type: audioBlob.type 
        })

        // 设置当前媒体类型为音频
        currentMediaType.value = 'audios'
        
        // 记录录音持续时间，用于后续描述
        uploadFiles.value = [audioFile]
        uploadFiles.value.recordingDuration = duration
        
        // 显示上传对话框并设置上传中状态
        showChunkUploader.value = true
        uploading.value = true
        
        // 添加文件到上传组件 (会自动开始上传)
        nextTick(() => {
          if (chunkUploader.value) {
            chunkUploader.value.addFile(audioFile);
          }
        });
      } catch (err) {
        alert('网络错误，录音上传失败')
        console.error('录音上传失败:', err)
        recordingTime.value = 0
      }
    }

    // 格式化时间显示
    const formatTime = (seconds) => {
      const mins = Math.floor(seconds / 60)
      const secs = seconds % 60
      return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`
    }
    
    // 显示Snackbar消息的辅助函数
    const showSnackbar = (message, options = {}) => {
      // 先重置show为false，强制触发变更
      snackbar.value.show = false;
      
      // 使用nextTick确保DOM更新后再显示
      nextTick(() => {
        snackbar.value = {
          show: true,
          message,
          subtext: options.subtext || '',
          type: options.type || 'info',
          duration: options.duration || 4000
        };
        
        console.log('显示Snackbar:', snackbar.value);
      });
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
              desc: img.desc,
              hash:'',
              captureTime:null
            })),
            videos: formData.value.media.videos.map(vid => ({
              fileName: vid.fileName, // 这里保存的是服务器端文件名
              desc: vid.desc,
              hash:'',
              captureTime:null
            })),
            audios: formData.value.media.audios.map(aud => ({
              fileName: aud.fileName, // 这里保存的是服务器端文件名
              desc: aud.desc,
              hash:'',
              captureTime:null
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
      showChunkUploader,
      uploadFiles,
      currentMediaType,
      uploaderStatus,
      snackbar,
      triggerImageUpload,
      triggerVideoUpload,
      triggerAudioUpload,
      handleImageUpload,
      handleVideoUpload,
      handleAudioUpload,
      handleUploadComplete,
      handleAllUploadsCompleted,
      toggleRecording,
      formatTime,
      removeMedia,
      saveEvent,
      goBack,
      loadEditData,
      getMediaUrl,
      route,
      retryUpload,
      chunkUploader,
      showSnackbar
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
  background: rgba(65, 105, 225, 0.7);
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

.cancel-btn, .save-btn, .debug-btn {
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

.debug-btn {
  background: #7f8c8d;
  color: white;
  font-size: 12px;
  padding: 8px 12px;
  position: absolute;
  right: 10px;
  bottom: 10px;
  min-width: auto;
  opacity: 0.5;
}

.debug-btn:hover {
  opacity: 1;
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

/* 分片上传弹窗 */
.chunk-uploader-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
}

.chunk-uploader-container {
  background: white;
  border-radius: 15px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.3);
  width: 90%;
  max-width: 600px;
  max-height: 90vh;
  overflow-y: auto;
  padding: 20px;
}

.chunk-uploader-container h3 {
  margin-top: 0;
  margin-bottom: 20px;
  font-size: 18px;
  font-weight: 600;
  color: #2c3e50;
  text-align: center;
}

.chunk-uploader-status {
  margin-top: 20px;
  display: flex;
  justify-content: center;
  padding: 10px;
  text-align: center;
}

.upload-status-indicator {
  color: #3498db;
  font-weight: bold;
  padding: 10px;
  background-color: rgba(52, 152, 219, 0.1);
  border-radius: 6px;
  animation: pulse 1.5s infinite;
}

.upload-status-completed {
  color: #27ae60;
  font-weight: bold;
  padding: 10px;
  background-color: rgba(39, 174, 96, 0.1);
  border-radius: 6px;
}

.upload-error {
  color: #e74c3c;
  font-weight: bold;
  padding: 10px;
  background-color: rgba(231, 76, 60, 0.1);
  border-radius: 6px;
  margin-top: 10px;
  text-align: center;
}

.upload-error .retry-btn {
  margin-top: 10px;
  padding: 8px 16px;
  background-color: #e74c3c;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: normal;
}

.upload-error .retry-btn:hover {
  background-color: #c0392b;
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
