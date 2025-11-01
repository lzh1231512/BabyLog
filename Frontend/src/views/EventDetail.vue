<template>
  <div class="event-detail">
    <!-- 返回按钮 -->
    <header class="detail-header">
      <button class="back-btn" @click="goBack">
        ← 返回
      </button>
      <h1 class="page-title"></h1>
    </header>

    <!-- 加载状态 -->
    <div v-if="loading" class="loading">
      <div class="loading-spinner">⏳</div>
      <p>正在加载事件详情...</p>
    </div>

    <!-- 错误状态 -->
    <div v-else-if="error" class="error-container">
      <div class="error-icon">⚠️</div>
      <p>{{ error }}</p>
      <button class="retry-btn" @click="loadEventDetail">重试</button>
    </div>

    <!-- 事件信息 -->
    <div class="event-container" v-else-if="event">
      <div class="event-header">
        <h2 class="event-title">{{ event.title }}</h2>
        <div class="event-meta-info">
          <span class="event-date">{{ formatDate(event.date) }}</span>
          <span class="event-age">{{ getAgeAtEvent(event.date) }}</span>
        </div>
      </div>

      <!-- 照片展示 -->
      <div class="photos-section" v-if="event.media.images && event.media.images.length > 0">
        <h3 class="section-title">照片 ({{ event.media.images.length }}张)</h3>
        <div class="photos-grid">
          <div 
            v-for="(image, index) in event.media.images" 
            :key="index"
            class="photo-item"
            @click="openPhotoViewer(index)"
          >
            <div 
              class="photo-placeholder"
              :style="{ backgroundImage: `url(${getMediaUrl(event.id, image.fileName,true)})` }"
            >
              <span v-if="!image.fileName" class="photo-icon">📷</span>
              <span class="photo-index">{{ index + 1 }}</span>
            </div>
            <p class="media-desc">{{ image.desc }}</p>
          </div>
        </div>
      </div>

      <!-- 视频展示 -->
      <div class="videos-section" v-if="event.media.videos && event.media.videos.length > 0">
        <h3 class="section-title">视频 ({{ event.media.videos.length }}个)</h3>
        <div class="videos-grid">
          <div 
            v-for="(video, index) in event.media.videos" 
            :key="index"
            class="video-item"
          >
            <div class="video-container" @click="openVideoPlayer(video, index)">
              <div 
                class="video-preview"
                :style="{ backgroundImage: `url(${getMediaUrl(event.id, video.fileName, true)})` }"
              >
                <span v-if="!video.fileName" class="video-icon">🎬</span>
              </div>
              <div class="video-overlay">
                <span class="play-overlay">
                  <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 48 48">
                    <circle cx="24" cy="24" r="22" fill="#FFB6C1"/>
                    <polygon points="18,14 34,24 18,34" fill="#FFFFFF"/>
                  </svg>
                </span>
                <span class="video-duration" v-if="video.duration">{{ formatDuration(video.duration) }}</span>
              </div>
            </div>
            <p class="media-desc">{{ video.desc }}</p>
          </div>
        </div>
      </div>

      <!-- 音频播放 -->
      <div class="audio-section" v-if="event.media.audios && event.media.audios.length > 0">
        <h3 class="section-title">录音 ({{ event.media.audios.length }}段)</h3>
        <div class="audio-list">
          <div 
            v-for="(audio, index) in event.media.audios" 
            :key="index"
            class="audio-item"
          >
            <div class="audio-player">
              <button class="play-btn" @click="toggleAudio(audio, index)">
                <span v-if="currentPlayingAudio === index && isAudioPlaying">⏸️</span>
                <span v-else-if="currentPlayingAudio === index && isAudioLoading">⏳</span>
                <span class="play-overlay">
                  <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 48 48">
                    <circle cx="24" cy="24" r="22" fill="#FFB6C1"/>
                    <polygon points="18,14 34,24 18,34" fill="#FFFFFF"/>
                  </svg>
                </span>
              </button>
              <div class="audio-info">
                <span class="audio-desc">{{ audio.desc }}</span>
                <div class="audio-progress" v-if="currentPlayingAudio === index">
                  <div class="progress-bar">
                    <div 
                      class="progress-fill" 
                      :style="{ width: audioProgress + '%' }"
                    ></div>
                  </div>
                  <span class="time-display">
                    {{ formatTime(audioCurrentTime) }} / {{ formatTime(audioDuration) }}
                  </span>
                </div>
              </div>
              <div class="audio-controls" v-if="currentPlayingAudio === index">
                <button class="volume-btn" @click="toggleMute">
                  {{ isAudioMuted ? '🔇' : '🔊' }}
                </button>
              </div>
            </div>
            <!-- 隐藏的音频元素 -->
            <audio 
              :ref="`audioPlayer${index}`"
              :src="getMediaUrl(event.id, audio.fileName)"
              @loadedmetadata="onAudioLoadedMetadata"
              @timeupdate="onAudioTimeUpdate"
              @ended="onAudioEnded"
              @error="onAudioError"
              @loadstart="onAudioLoadStart"
              @canplay="onAudioCanPlay"
              preload="metadata"
            ></audio>
          </div>
        </div>
      </div>

      <!-- 事件描述 -->
      <div class="description-section">
        <h3 class="section-title">详细描述</h3>
        <p class="event-description">{{ event.description }}</p>
      </div>

      <!-- 其他信息 -->
      <div class="additional-info">
        <div class="info-item" v-if="event.location">
          <span class="info-label">地点:</span>
          <span class="info-value">{{ event.location }}</span>
        </div>
        <div class="info-item">
          <span class="info-label">记录时间:</span>
          <span class="info-value">{{ formatDateTime(event.date) }}</span>
        </div>
      </div>

      <!-- 操作按钮 -->
      <div class="action-buttons">
        <button class="edit-btn" @click="editEvent">
          ✏️ 编辑
        </button>
        <button class="delete-btn" @click="deleteEvent">
          🗑️ 删除
        </button>
      </div>
    </div>



    <!-- 照片查看器模态框 -->
    <photo-viewer
      v-if="event && event.media && event.media.images"
      :show="showPhotoViewer"
      :images="event.media.images"
      :initialIndex="currentPhotoIndex"
      :eventId="event ? event.id : ''"
      :getMediaUrl="getMediaUrl"
      @close="closePhotoViewer"
      @indexChange="handlePhotoIndexChange"
    />
    
    <!-- 视频查看器模态框 -->
    <video-viewer
      v-if="event && event.media && event.media.videos"
      :show="showVideoViewer"
      :videos="event.media.videos"
      :initialIndex="currentVideoIndex"
      :eventId="event ? event.id : ''"
      :getVideoUrl="getVideoUrl"
      @close="closeVideoViewer"
      @indexChange="handleVideoIndexChange"
    />

  </div>
</template>

<script>
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter, onBeforeRouteLeave } from 'vue-router'
import dayjs from 'dayjs'
import { getEventById, deleteEvent, getMediaUrl } from '@/api/events'
import PhotoViewer from '@/components/PhotoViewer.vue'
import VideoViewer from '@/components/VideoViewer.vue'

export default {
  name: 'EventDetail',
  components: {
    PhotoViewer,
    VideoViewer
  },
  setup() {
    const route = useRoute()
    const router = useRouter()
    
    const event = ref(null)
    const isPlaying = ref(false)
    const showPhotoViewer = ref(false)
    const currentPhotoIndex = ref(0)
    const showVideoViewer = ref(false)
    const currentVideoIndex = ref(0)

    const currentPlayingAudio = ref(-1)
    const isAudioPlaying = ref(false)
    const isAudioLoading = ref(false)
    const isAudioMuted = ref(false)
    const audioProgress = ref(0)
    const audioCurrentTime = ref(0)
    const audioDuration = ref(0)
    const currentAudioElement = ref(null)
    const loading = ref(true)
    const error = ref('')
    
    // 获取事件详情
    const loadEventDetail = async () => {
      try {
        loading.value = true
        error.value = ''
        
        const eventId = route.params.id
        const response = await getEventById(eventId)
        
        if (response.success) {
          event.value = response.data
        } else {
          error.value = response.message || '获取事件详情失败'
          // 如果事件不存在，3秒后返回首页
          if (response.message === '事件不存在') {
            setTimeout(() => {
              router.push('/')
            }, 3000)
          }
        }
      } catch (err) {
        error.value = '网络错误，请稍后重试'
        console.error('加载事件详情失败:', err)
      } finally {
        loading.value = false
      }
    }

    // 格式化日期
    const formatDate = (date) => {
      return dayjs(date).format('YYYY年MM月DD日')
    }

    // 格式化完整日期时间
    const formatDateTime = (date) => {
      return dayjs(date).format('YYYY年MM月DD日 HH:mm')
    }

    // 计算事件发生时宝宝的年龄
    const getAgeAtEvent = (eventDate) => {
      const birth = dayjs('2025-05-09')
      const eventDay = dayjs(eventDate)
      const totalMonths = eventDay.diff(birth, 'month')
      
      if (totalMonths < 12) {
        const monthStart = birth.add(totalMonths, 'month')
        const days = eventDay.diff(monthStart, 'day')
        
        if (totalMonths === 0) {
          const totalDays = eventDay.diff(birth, 'day')
          return `出生第${totalDays}天`
        } else {
          return `${totalMonths}个月${days > 0 ? `${days}天` : ''}`
        }
      } else {
        const years = Math.floor(totalMonths / 12)
        const remainingMonths = totalMonths % 12
        return `${years}岁${remainingMonths > 0 ? `${remainingMonths}个月` : ''}`
      }
    }

    // 返回首页
    const goBack = () => {
      // 返回首页时传递当前事件ID，用于定位
      router.push({
        path: '/',
        query: { highlight: event.value?.id }
      })
    }

    // 打开照片查看器
    const openPhotoViewer = (index) => {
      currentPhotoIndex.value = index
      showPhotoViewer.value = true
    }

    // 关闭照片查看器
    const closePhotoViewer = () => {
      showPhotoViewer.value = false
    }
    
    // 处理照片索引变化
    const handlePhotoIndexChange = (index) => {
      currentPhotoIndex.value = index
    }

    // 打开视频播放器
    const openVideoPlayer = (video, index) => {
      currentVideoIndex.value = index
      showVideoViewer.value = true
    }
    
    // 关闭视频播放器
    const closeVideoViewer = () => {
      showVideoViewer.value = false
    }
    
    // 处理视频索引变化
    const handleVideoIndexChange = (index) => {
      currentVideoIndex.value = index
    }
    
    // 获取视频URL的方法，供VideoViewer组件使用
    const getVideoUrl = async (eventId, fileName) => {
      try {
        const { getVideoURL } = await import('@/api/events')
        const response = await getVideoURL(eventId, fileName)
        if (response && response.success && response.data) {
          return {
            success: true,
            videoUrl: response.data.hlsUrl,
            isTranscoded: response.data.isTranscoded
          }
        } else {
          return {
            success: false,
            error: response.message || 'Failed to get video URL',
            isProcessing: response.data?.isProcessing || false
          }
        }
      } catch (error) {
        console.error('Error getting video URL:', error)
        return {
          success: false,
          error: 'Failed to get video URL'
        }
      }
    }



    // 格式化视频时长
    const formatDuration = (seconds) => {
      const mins = Math.floor(seconds / 60)
      const secs = Math.floor(seconds % 60)
      return `${mins}:${secs.toString().padStart(2, '0')}`
    }

    // 播放/暂停音频
    const toggleAudio = async (audio, index) => {
      const audioElement = document.querySelector(`audio[src*="${audio.fileName}"]`)
      
      if (!audioElement) {
        console.error('音频元素不存在')
        return
      }

      // 如果正在播放同一个音频，则暂停
      if (currentPlayingAudio.value === index && isAudioPlaying.value) {
        audioElement.pause()
        isAudioPlaying.value = false
        return
      }

      // 停止之前播放的音频
      if (currentAudioElement.value && currentAudioElement.value !== audioElement) {
        currentAudioElement.value.pause()
        currentAudioElement.value.currentTime = 0
      }

      try {
        currentPlayingAudio.value = index
        currentAudioElement.value = audioElement
        isAudioLoading.value = true
        
        await audioElement.play()
        isAudioPlaying.value = true
        isAudioLoading.value = false
      } catch (error) {
        console.error('音频播放失败:', error)
        isAudioLoading.value = false
        isAudioPlaying.value = false
        alert('音频播放失败，请检查网络连接')
      }
    }

    // 音频加载开始
    const onAudioLoadStart = () => {
      isAudioLoading.value = true
    }

    // 音频可以播放
    const onAudioCanPlay = () => {
      isAudioLoading.value = false
    }

    // 音频元数据加载完成
    const onAudioLoadedMetadata = (e) => {
      const audio = e.target
      audioDuration.value = audio.duration || 0
    }

    // 音频时间更新
    const onAudioTimeUpdate = (e) => {
      const audio = e.target
      audioCurrentTime.value = audio.currentTime
      if (audioDuration.value > 0) {
        audioProgress.value = (audio.currentTime / audioDuration.value) * 100
      }
    }

    // 音频播放结束
    const onAudioEnded = () => {
      isAudioPlaying.value = false
      currentPlayingAudio.value = -1
      audioProgress.value = 0
      audioCurrentTime.value = 0
      currentAudioElement.value = null
    }

    // 音频播放错误
    const onAudioError = (e) => {
      console.error('音频播放错误:', e)
      isAudioPlaying.value = false
      isAudioLoading.value = false
      currentPlayingAudio.value = -1
      alert('音频加载失败，请检查网络连接')
    }

    // 切换静音
    const toggleMute = () => {
      if (currentAudioElement.value) {
        isAudioMuted.value = !isAudioMuted.value
        currentAudioElement.value.muted = isAudioMuted.value
      }
    }

    // 格式化时间显示
    const formatTime = (seconds) => {
      if (!seconds || isNaN(seconds)) return '0:00'
      const mins = Math.floor(seconds / 60)
      const secs = Math.floor(seconds % 60)
      return `${mins}:${secs.toString().padStart(2, '0')}`
    }

    // 键盘事件处理
    const handleKeyDown = (e) => {
      if (!showPhotoViewer.value) return
      
      switch (e.key) {
        case 'Escape':
          e.preventDefault()
          closePhotoViewer()
          break
      }
    }

    // 编辑事件
    const editEvent = () => {
      console.log('编辑事件:', event.value.id)
      // 跳转到编辑页面
      router.push(`/edit/${event.value.id}`)
    }

    // 删除事件
    const handleDeleteEvent = async () => {
      if (confirm('确定要删除这个事件吗？此操作不可恢复。')) {
        try {
          const response = await deleteEvent(event.value.id)
          if (response.success) {
            // 删除成功，返回首页并传递已删除的ID，让首页定位到最接近的事件
            router.push({
              path: '/',
              query: { highlight: event.value.id }
            })
          } else {
            alert(response.message || '删除失败')
          }
        } catch (err) {
          alert('网络错误，删除失败')
          console.error('删除事件失败:', err)
        }
      }
    }

    onMounted(async () => {
      await loadEventDetail()
      // 确保页面滚动到顶部
      window.scrollTo(0, 0)
      // 添加键盘事件监听
      document.addEventListener('keydown', handleKeyDown)
    })

    // 组件卸载时移除事件监听
    onBeforeUnmount(() => {
      document.removeEventListener('keydown', handleKeyDown)
    })

    onBeforeRouteLeave((to, from, next) => {
      if (showPhotoViewer.value) {
        closePhotoViewer()
        // 阻止路由跳转
        next(false)
        return
      }
      if (showVideoViewer.value) {
        closeVideoViewer()
        next(false)
        return
      }
      if (to.path == '/' && event.value?.id && to.query.highlight != event.value?.id) {
        next({
          path: '/',
          query: { highlight: event.value.id }
        })
        return
      }
      next()
    })

    return {
      event,
      isPlaying,
      showPhotoViewer,
      currentPhotoIndex,
      showVideoViewer,
      currentVideoIndex,
      currentPlayingAudio,
      isAudioPlaying,
      isAudioLoading,
      isAudioMuted,
      audioProgress,
      audioCurrentTime,
      audioDuration,
      currentAudioElement,
      loading,
      error,
      formatDate,
      formatDateTime,
      getAgeAtEvent,
      goBack,
      openPhotoViewer,
      closePhotoViewer,
      handlePhotoIndexChange,
      openVideoPlayer,
      closeVideoViewer,
      handleVideoIndexChange,
      formatDuration,
      toggleAudio,
      onAudioLoadStart,
      onAudioCanPlay,
      onAudioLoadedMetadata,
      onAudioTimeUpdate,
      onAudioEnded,
      onAudioError,
      toggleMute,
      formatTime,
      editEvent,
      deleteEvent: handleDeleteEvent,
      loadEventDetail,
      getMediaUrl,
      getVideoUrl
    }
  }
}
</script>

<style scoped>
@import '../assets/styles/theme.css';
.event-detail {
  min-height: 100vh;
  background: var(--color-background);
  padding: var(--spacing);
  font-family: var(--font-family-base);
}

/* 头部 */
.detail-header {
  display: flex;
  align-items: center;
  margin-bottom: 30px;
  max-width: 800px;
  margin-left: auto;
  margin-right: auto;
}

.back-btn {
  background: var(--color-primary);
  border: none;
  padding: 10px 15px;
  border-radius: var(--border-radius);
  cursor: pointer;
  font-size: 16px;
  margin-right: 20px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  color: #fff;
  transition: transform 0.2s ease;
}

.back-btn:hover {
  transform: translateY(-2px);
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--color-primary);
  margin: 0;
}

/* 事件容器 */
.event-container {
  max-width: 800px;
  margin: 0 auto;
  background: var(--color-surface);
  border-radius: var(--border-radius);
  padding: 30px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
}

.event-header {
  margin-bottom: 30px;
  text-align: center;
}

.event-title {
  font-size: 28px;
  font-weight: 700;
  color: var(--color-primary);
  margin: 0 0 15px 0;
}

.event-meta-info {
  display: flex;
  justify-content: center;
  gap: 20px;
  font-size: 16px;
}

.event-date {
  color: var(--color-text-highlight);
  font-weight: 600;
}

.event-age {
  color: #7f8c8d;
}

/* 区块标题 */
.section-title {
  font-size: 18px;
  font-weight: 600;
  color: #2c3e50;
  margin: 30px 0 15px 0;
  padding-bottom: 10px;
  border-bottom: 2px solid #ecf0f1;
}

/* 媒体区域通用样式 */
.photos-section, .videos-section, .audio-section {
  margin-bottom: 30px;
}

/* 照片区域 */
.photos-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 15px;
}

.photo-item {
  cursor: pointer;
  transition: transform 0.3s ease;
}

.photo-item:hover {
  transform: scale(1.05);
}

.photo-placeholder {
  height: 150px;
  background: linear-gradient(135deg, #84fab0 0%, #8fd3f4 100%);
  background-size: cover;
  background-position: center;
  background-repeat: no-repeat;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 30px;
  color: white;
  position: relative;
  margin-bottom: 8px;
}

.photo-icon {
  text-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
}

.video-icon {
  text-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
}

.photo-index {
  position: absolute;
  bottom: 10px;
  right: 10px;
  background: rgba(0, 0, 0, 0.7);
  color: white;
  padding: 4px 8px;
  border-radius: 12px;
  font-size: 12px;
}

/* 视频区域 */
.videos-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 15px;
}

.video-item {
  transition: transform 0.3s ease;
}

.video-item:hover {
  transform: scale(1.05);
}

.video-container {
  position: relative;
  height: 150px;
  border-radius: 10px;
  overflow: hidden;
  cursor: pointer;
  margin-bottom: 8px;
}

.video-preview {
  width: 100%;
  height: 100%;
  background: linear-gradient(135deg, #8e44ad 0%, #9b59b6 100%);
  background-size: cover;
  background-position: center;
  background-repeat: no-repeat;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 30px;
  color: white;
}

.video-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.3);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.3s ease;
}
circle{
  fill:var(--color-secondary);
}
.video-container:hover .video-overlay {
  background: rgba(0, 0, 0, 0.5);
}

.play-overlay {
  font-size: 40px;
  color: white;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.5);
  transition: transform 0.3s ease;
}

.video-container:hover .play-overlay {
  transform: scale(1.2);
}

.video-duration {
  position: absolute;
  bottom: 8px;
  right: 8px;
  background: rgba(0, 0, 0, 0.8);
  color: white;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 12px;
}

/* 媒体描述文字 */
.media-desc {
  font-size: 12px;
  color: #7f8c8d;
  margin: 5px 0 2px 0;
  text-align: center;
  line-height: 1.3;
}

.media-filename {
  font-size: 11px;
  color: #95a5a6;
  text-align: center;
  display: block;
  font-style: italic;
}

/* 音频播放器 */
.audio-list {
  display: flex;
  flex-direction: column;
  gap: 15px;
}

.audio-item {
  background: #f8f9fa;
  border-radius: 10px;
  overflow: hidden;
  position: relative;
}

.audio-item audio {
  display: none;
}

.audio-player {
  display: flex;
  align-items: center;
  gap: 15px;
  padding: 15px;
}

.play-btn {
  width: 45px;
  height: 45px;
  border-radius: 50%;
  border: none;
  background: linear-gradient(135deg, #e67e22 0%, #d35400 100%);
  color: white;
  font-size: 18px;
  cursor: pointer;
  transition: transform 0.2s ease;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
}

.play-btn:hover {
  transform: scale(1.1);
}

.play-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}

.audio-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.audio-desc {
  font-size: 14px;
  color: #2c3e50;
  font-weight: 500;
}

.audio-progress {
  display: flex;
  align-items: center;
  gap: 10px;
}

.progress-bar {
  flex: 1;
  height: 4px;
  background: #ecf0f1;
  border-radius: 2px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(135deg, #e67e22 0%, #d35400 100%);
  border-radius: 2px;
  transition: width 0.1s ease;
}

.time-display {
  font-size: 12px;
  color: #7f8c8d;
  min-width: 70px;
  text-align: right;
}

.audio-controls {
  display: flex;
  align-items: center;
  gap: 10px;
}

.volume-btn {
  width: 35px;
  height: 35px;
  border-radius: 50%;
  border: none;
  background: rgba(231, 76, 60, 0.1);
  color: #e74c3c;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}

.volume-btn:hover {
  background: rgba(231, 76, 60, 0.2);
  transform: scale(1.1);
}

.audio-filename {
  font-size: 12px;
  color: #7f8c8d;
  font-style: italic;
}

/* 描述区域 */
.description-section {
  margin-bottom: 30px;
}

.event-description {
  font-size: 16px;
  line-height: 1.6;
  color: #34495e;
  margin: 0;
}

/* 附加信息 */
.additional-info {
  margin-bottom: 30px;
  padding: 20px;
  background: #f8f9fa;
  border-radius: 10px;
}

.info-item {
  display: flex;
  margin-bottom: 10px;
}

.info-item:last-child {
  margin-bottom: 0;
}

.info-label {
  font-weight: 600;
  color: #7f8c8d;
  width: 100px;
}

.info-value {
  color: #2c3e50;
}

/* 操作按钮 */
.action-buttons {
  display: flex;
  gap: 15px;
  justify-content: center;
}

.edit-btn, .delete-btn {
  padding: 12px 24px;
  border: none;
  border-radius: 25px;
  font-size: 16px;
  cursor: pointer;
  transition: transform 0.2s ease;
}

.edit-btn {
  background: linear-gradient(135deg, #74b9ff 0%, #0984e3 100%);
  color: white;
}

.delete-btn {
  background: linear-gradient(135deg, #ff7675 0%, #d63031 100%);
  color: white;
}

.edit-btn:hover, .delete-btn:hover {
  transform: translateY(-2px);
}

/* 加载状态 */
.loading, .error-container {
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

/* 照片查看器样式已移至 PhotoViewer 组件 */



/* 响应式设计 */
@media (max-width: 768px) {
  .event-detail {
    padding: 15px;
  }
  
  .event-container {
    padding: 20px;
  }
  
  .photos-grid {
    grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  }
  
  .videos-grid {
    grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  }
  
  .photo-placeholder, .video-placeholder {
    height: 120px;
    font-size: 24px;
  }
  
  .play-overlay {
    font-size: 30px;
  }
  /* 照片查看器相关样式已移至 PhotoViewer 组件 */
  

  
  .action-buttons {
    flex-direction: column;
  }
  
  .event-meta-info {
    flex-direction: column;
    gap: 10px;
  }
}

/* 动画定义 */
/* 照片查看器动画已移至 PhotoViewer 组件 */

/* 更小屏幕设备的优化 */
@media (max-width: 480px) {
  /* 照片查看器小屏幕样式已移至 PhotoViewer 组件 */
}
</style>
