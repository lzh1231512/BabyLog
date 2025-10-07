<template>
  <div class="event-detail">
    <!-- 返回按钮 -->
    <header class="detail-header">
      <button class="back-btn" @click="goBack">
        ← 返回
      </button>
      <h1 class="page-title">事件详情</h1>
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
              :style="{ backgroundImage: `url(${getMediaUrl(event.id, image.fileName)})` }"
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
              <video 
                :src="getMediaUrl(event.id, video.fileName)"
                class="video-preview"
                preload="metadata"
                @loadedmetadata="onVideoLoaded"
              >
                您的浏览器不支持视频播放
              </video>
              <div class="video-overlay">
                <span class="play-overlay">▶️</span>
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
                <span v-else>▶️</span>
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
    <div class="photo-modal" v-if="showPhotoViewer" @click="closePhotoViewer">
      <div class="modal-content" @click.stop>
        <button class="modal-close" @click="closePhotoViewer">✕</button>
        <div class="photo-viewer">
          <button class="nav-btn prev" @click="prevPhoto" v-if="currentPhotoIndex > 0">‹</button>
          <div class="current-photo">
            <div 
              class="large-photo-placeholder"
              :style="{ backgroundImage: `url(${getMediaUrl(event.id, event.media.images[currentPhotoIndex].fileName)})` }"
            >
              <span v-if="!event.media.images[currentPhotoIndex].fileName" class="photo-icon">📷</span>
            </div>
            <p class="photo-counter">{{ currentPhotoIndex + 1 }} / {{ event.media.images.length }}</p>
          </div>
          <button class="nav-btn next" @click="nextPhoto" v-if="currentPhotoIndex < event.media.images.length - 1">›</button>
        </div>
      </div>
    </div>

    <!-- 视频播放器模态框 -->
    <div class="video-modal" v-if="showVideoPlayer" @click="closeVideoPlayer">
      <div class="modal-content" @click.stop>
        <button class="modal-close" @click="closeVideoPlayer">✕</button>
        <div class="video-player">
          <video 
            ref="videoPlayerRef"
            :src="getMediaUrl(event.id, currentVideo?.fileName)"
            class="modal-video"
            controls
            autoplay
            @ended="onVideoEnded"
            @error="onVideoError"
          >
            您的浏览器不支持视频播放
          </video>
          <div class="video-info">
            <h3 class="video-title">{{ currentVideo?.desc || '视频' }}</h3>
            <div class="video-controls">
              <button class="nav-btn prev" @click="prevVideo" v-if="currentVideoIndex > 0">‹ 上一个</button>
              <span class="video-counter">{{ currentVideoIndex + 1 }} / {{ event.media.videos.length }}</span>
              <button class="nav-btn next" @click="nextVideo" v-if="currentVideoIndex < event.media.videos.length - 1">下一个 ›</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import dayjs from 'dayjs'
import { getEventById, deleteEvent, getMediaUrl } from '@/api/events'

export default {
  name: 'EventDetail',
  setup() {
    const route = useRoute()
    const router = useRouter()
    
    const event = ref(null)
    const isPlaying = ref(false)
    const showPhotoViewer = ref(false)
    const currentPhotoIndex = ref(0)
    const showVideoPlayer = ref(false)
    const currentVideoIndex = ref(0)
    const currentVideo = ref(null)
    const videoPlayerRef = ref(null)
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

    // 上一张照片
    const prevPhoto = () => {
      if (currentPhotoIndex.value > 0) {
        currentPhotoIndex.value--
      }
    }

    // 下一张照片
    const nextPhoto = () => {
      if (currentPhotoIndex.value < event.value.media.images.length - 1) {
        currentPhotoIndex.value++
      }
    }

    // 打开视频播放器
    const openVideoPlayer = (video, index) => {
      currentVideo.value = video
      currentVideoIndex.value = index
      showVideoPlayer.value = true
    }

    // 关闭视频播放器
    const closeVideoPlayer = () => {
      showVideoPlayer.value = false
      if (videoPlayerRef.value) {
        videoPlayerRef.value.pause()
      }
    }

    // 上一个视频
    const prevVideo = () => {
      if (currentVideoIndex.value > 0) {
        currentVideoIndex.value--
        currentVideo.value = event.value.media.videos[currentVideoIndex.value]
      }
    }

    // 下一个视频
    const nextVideo = () => {
      if (currentVideoIndex.value < event.value.media.videos.length - 1) {
        currentVideoIndex.value++
        currentVideo.value = event.value.media.videos[currentVideoIndex.value]
      }
    }

    // 视频加载完成
    const onVideoLoaded = (e) => {
      // 获取视频时长等信息
      const video = e.target
      if (video.duration) {
        console.log('视频时长:', video.duration)
      }
    }

    // 视频播放结束
    const onVideoEnded = () => {
      console.log('视频播放结束')
      // 可以自动播放下一个视频
      if (currentVideoIndex.value < event.value.media.videos.length - 1) {
        nextVideo()
      }
    }

    // 视频播放错误
    const onVideoError = (e) => {
      console.error('视频播放错误:', e)
      alert('视频播放失败，请检查网络连接或联系管理员')
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
    })

    return {
      event,
      isPlaying,
      showPhotoViewer,
      currentPhotoIndex,
      showVideoPlayer,
      currentVideoIndex,
      currentVideo,
      videoPlayerRef,
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
      prevPhoto,
      nextPhoto,
      openVideoPlayer,
      closeVideoPlayer,
      prevVideo,
      nextVideo,
      onVideoLoaded,
      onVideoEnded,
      onVideoError,
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
      getMediaUrl
    }
  }
}
</script>

<style scoped>
.event-detail {
  min-height: 100vh;
  background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
  padding: 20px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
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

/* 事件容器 */
.event-container {
  max-width: 800px;
  margin: 0 auto;
  background: white;
  border-radius: 20px;
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
  color: #2c3e50;
  margin: 0 0 15px 0;
}

.event-meta-info {
  display: flex;
  justify-content: center;
  gap: 20px;
  font-size: 16px;
}

.event-date {
  color: #e74c3c;
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
  object-fit: cover;
  background: linear-gradient(135deg, #8e44ad 0%, #9b59b6 100%);
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

/* 模态框通用样式 */
.photo-modal, .video-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.9);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  position: relative;
  max-width: 90vw;
  max-height: 90vh;
}

.modal-close {
  position: absolute;
  top: -40px;
  right: 0;
  background: none;
  border: none;
  color: white;
  font-size: 24px;
  cursor: pointer;
  padding: 10px;
}

.photo-viewer {
  display: flex;
  align-items: center;
  gap: 20px;
}

.current-photo {
  text-align: center;
}

.large-photo-placeholder {
  width: 500px;
  height: 400px;
  background: linear-gradient(135deg, #84fab0 0%, #8fd3f4 100%);
  background-size: cover;
  background-position: center;
  background-repeat: no-repeat;
  border-radius: 15px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 80px;
  color: white;
  margin-bottom: 15px;
}

.photo-counter {
  color: white;
  font-size: 16px;
  margin: 0;
}

.nav-btn {
  background: rgba(255, 255, 255, 0.2);
  border: none;
  color: white;
  font-size: 40px;
  padding: 20px 15px;
  cursor: pointer;
  border-radius: 10px;
  transition: background 0.2s ease;
}

.nav-btn:hover {
  background: rgba(255, 255, 255, 0.3);
}

/* 视频播放器模态框 */
.video-player {
  display: flex;
  flex-direction: column;
  align-items: center;
  max-width: 90vw;
  max-height: 90vh;
}

.modal-video {
  max-width: 100%;
  max-height: 70vh;
  border-radius: 10px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
}

.video-info {
  margin-top: 20px;
  text-align: center;
  color: white;
}

.video-title {
  font-size: 18px;
  font-weight: 600;
  margin: 0 0 15px 0;
  color: white;
}

.video-controls {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 20px;
}

.video-counter {
  color: white;
  font-size: 14px;
  padding: 8px 16px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 20px;
}

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
  
  .large-photo-placeholder {
    width: 90vw;
    height: 300px;
    font-size: 60px;
  }
  
  .nav-btn {
    font-size: 30px;
    padding: 15px 10px;
  }
  
  .modal-video {
    max-height: 60vh;
  }
  
  .video-controls {
    flex-direction: column;
    gap: 10px;
  }
  
  .video-title {
    font-size: 16px;
  }
  
  .action-buttons {
    flex-direction: column;
  }
  
  .event-meta-info {
    flex-direction: column;
    gap: 10px;
  }
}
</style>
