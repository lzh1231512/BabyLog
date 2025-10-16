<template>
  <div class="video-player-page">
    <!-- 返回按钮 -->
    <header class="player-header">
      <button class="back-btn" @click="goBack">
        ← 返回
      </button>
      <h1 class="page-title">视频播放</h1>
    </header>

    <!-- 加载状态 -->
    <div v-if="loading" class="loading">
      <div class="loading-spinner">⏳</div>
      <p>正在加载视频...</p>
    </div>

    <!-- 错误状态 -->
    <div v-else-if="error" class="error-container">
      <div class="error-icon">⚠️</div>
      <p>{{ error }}</p>
      <button class="retry-btn" @click="loadVideoData">重试</button>
    </div>

    <!-- 视频播放器 -->
    <div v-else-if="event && currentVideo" class="video-container">
      <div class="player-wrapper">
        <div 
          ref="videoContainer" 
          class="video-js-container"
          :style="{ transform: `rotate(${currentRotation}deg)` }"
        >
          <video
            ref="videoPlayer"
            class="video-js vjs-default-skin"
            controls
            preload="auto"
            :data-setup="JSON.stringify(videoJsOptions)"
          >
            <p class="vjs-no-js">
              您的浏览器不支持视频播放。
              <a href="https://videojs.com/html5-video-support/" target="_blank">
                请升级您的浏览器
              </a>
            </p>
          </video>
        </div>
        
        <!-- 视频信息 -->
        <div class="video-info">
          <h2 class="video-title">{{ currentVideo.desc || '视频' }}</h2>
          <div class="video-meta">
            <span class="event-title">来自: {{ event.title }}</span>
            <span class="video-date">{{ formatDate(event.date) }}</span>
          </div>
        </div>

        <!-- 视频列表 -->
        <div v-if="videoList.length > 1" class="video-list">
          <h3 class="list-title">视频列表 ({{ videoList.length }}个)</h3>
          <div class="video-thumbnails">
            <div 
              v-for="(video, index) in videoList" 
              :key="index"
              class="thumbnail-item"
              :class="{ active: index === currentVideoIndex }"
              @click="switchVideo(index)"
            >
              <div 
                class="thumbnail-preview"
                :style="{ backgroundImage: `url(${getMediaUrl(event.id, video.fileName, true)})` }"
              >
                <span class="play-overlay">▶️</span>
                <span v-if="video.duration" class="duration">{{ formatDuration(video.duration) }}</span>
              </div>
              <p class="thumbnail-desc">{{ video.desc || `视频${index + 1}` }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { ref, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import dayjs from 'dayjs'
import videojs from 'video.js'
import 'video.js/dist/video-js.css'
import { getEventById, getVideoRotation, getVideoUrl, getMediaUrl } from '@/api/events'

export default {
  name: 'VideoPlayer',
  setup() {
    const route = useRoute()
    const router = useRouter()
    
    const event = ref(null)
    const currentVideo = ref(null)
    const currentVideoIndex = ref(0)
    const videoList = ref([])
    const currentRotation = ref(0)
    const loading = ref(true)
    const error = ref('')
    
    const videoPlayer = ref(null)
    const videoContainer = ref(null)
    let player = null

    const videoJsOptions = {
      fluid: true,
      responsive: true,
      aspectRatio: '16:9',
      playbackRates: [0.5, 1, 1.25, 1.5, 2],
      controls: true,
      preload: 'auto'
    }

    // 加载视频数据
    const loadVideoData = async () => {
      try {
        loading.value = true
        error.value = ''
        
        const eventId = route.params.id
        const videoIndex = parseInt(route.params.videoIndex) || 0
        
        // 获取事件详情
        const response = await getEventById(eventId)
        if (!response.success) {
          error.value = response.message || '获取事件详情失败'
          return
        }
        
        event.value = response.data
        videoList.value = event.value.media.videos || []
        
        if (videoList.value.length === 0) {
          error.value = '该事件没有视频'
          return
        }
        
        // 设置当前视频
        currentVideoIndex.value = Math.min(videoIndex, videoList.value.length - 1)
        currentVideo.value = videoList.value[currentVideoIndex.value]
        
        // 获取视频旋转角度
        await loadVideoRotation()
        
        // 初始化播放器
        await nextTick()
        await initializePlayer()
        
      } catch (err) {
        error.value = '网络错误，请稍后重试'
        console.error('加载视频数据失败:', err)
      } finally {
        loading.value = false
      }
    }

    // 获取视频旋转角度
    const loadVideoRotation = async () => {
      try {
        const response = await getVideoRotation(event.value.id, currentVideo.value.fileName)
        if (response.success && response.data !== null) {
          currentRotation.value = response.data
        } else {
          currentRotation.value = 0
        }
      } catch (err) {
        console.warn('获取视频旋转角度失败:', err)
        currentRotation.value = 0
      }
    }

    // 初始化Video.js播放器
    const initializePlayer = async () => {
      if (player) {
        player.dispose()
        player = null
      }

      await nextTick()
      
      if (!videoPlayer.value) {
        console.error('Video element not found')
        return
      }

      try {
        player = videojs(videoPlayer.value, videoJsOptions, () => {
          console.log('Video.js player initialized')
          
          // 设置视频源
          const videoUrl = getVideoUrl(event.value.id, currentVideo.value.fileName)
          player.src({
            src: videoUrl,
            type: 'video/mp4'
          })
          
          // 添加事件监听
          player.on('loadedmetadata', () => {
            console.log('Video metadata loaded')
          })
          
          player.on('error', (e) => {
            console.error('Video.js player error:', e)
            error.value = '视频播放失败，请检查网络连接'
          })
          
          player.on('ended', () => {
            console.log('Video ended')
            // 自动播放下一个视频
            if (currentVideoIndex.value < videoList.value.length - 1) {
              switchVideo(currentVideoIndex.value + 1)
            }
          })
        })
      } catch (err) {
        console.error('Failed to initialize Video.js:', err)
        error.value = '视频播放器初始化失败'
      }
    }

    // 切换视频
    const switchVideo = async (index) => {
      if (index < 0 || index >= videoList.value.length || index === currentVideoIndex.value) {
        return
      }
      
      currentVideoIndex.value = index
      currentVideo.value = videoList.value[index]
      
      // 获取新视频的旋转角度
      await loadVideoRotation()
      
      // 更新播放器源
      if (player) {
        const videoUrl = getVideoUrl(event.value.id, currentVideo.value.fileName)
        player.src({
          src: videoUrl,
          type: 'video/mp4'
        })
        player.play()
      }
      
      // 更新URL但不重新加载页面
      const newPath = `/video-player/${event.value.id}/${index}`
      if (route.path !== newPath) {
        router.replace(newPath)
      }
    }

    // 格式化日期
    const formatDate = (date) => {
      return dayjs(date).format('YYYY年MM月DD日')
    }

    // 格式化视频时长
    const formatDuration = (seconds) => {
      const mins = Math.floor(seconds / 60)
      const secs = Math.floor(seconds % 60)
      return `${mins}:${secs.toString().padStart(2, '0')}`
    }

    // 返回上一页
    const goBack = () => {
      router.back()
    }

    // 组件挂载
    onMounted(() => {
      loadVideoData()
    })

    // 组件卸载时清理
    onBeforeUnmount(() => {
      if (player) {
        player.dispose()
        player = null
      }
    })

    return {
      event,
      currentVideo,
      currentVideoIndex,
      videoList,
      currentRotation,
      loading,
      error,
      videoPlayer,
      videoContainer,
      loadVideoData,
      switchVideo,
      formatDate,
      formatDuration,
      goBack,
      getMediaUrl,
      getVideoUrl
    }
  }
}
</script>

<style scoped>
.video-player-page {
  min-height: 100vh;
  background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%);
  padding: 20px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

/* 头部 */
.player-header {
  display: flex;
  align-items: center;
  margin-bottom: 30px;
  max-width: 1200px;
  margin-left: auto;
  margin-right: auto;
}

.back-btn {
  background: rgba(255, 255, 255, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.3);
  color: white;
  padding: 10px 15px;
  border-radius: 10px;
  cursor: pointer;
  font-size: 16px;
  margin-right: 20px;
  backdrop-filter: blur(10px);
  transition: all 0.2s ease;
}

.back-btn:hover {
  background: rgba(255, 255, 255, 0.3);
  transform: translateY(-2px);
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: white;
  margin: 0;
}

/* 视频容器 */
.video-container {
  max-width: 1200px;
  margin: 0 auto;
}

.player-wrapper {
  background: rgba(255, 255, 255, 0.95);
  border-radius: 20px;
  padding: 30px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
  backdrop-filter: blur(20px);
}

.video-js-container {
  margin-bottom: 30px;
  border-radius: 15px;
  overflow: hidden;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
  transition: transform 0.3s ease;
}

/* Video.js 样式覆盖 */
.video-js {
  width: 100% !important;
  height: auto !important;
  border-radius: 15px;
  background-color: #000;
}

.video-js .vjs-big-play-button {
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.8);
  border: 3px solid rgba(255, 255, 255, 0.9);
  font-size: 2.5em;
  height: 1.8em;
  width: 1.8em;
  line-height: 1.8em;
  margin-left: -0.9em;
  margin-top: -0.9em;
  transition: all 0.3s ease;
}

.video-js:hover .vjs-big-play-button {
  background: rgba(0, 0, 0, 0.9);
  transform: scale(1.1);
}

.video-js .vjs-control-bar {
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(10px);
  border-radius: 0 0 15px 15px;
}

.video-js .vjs-progress-control {
  height: 6px;
}

.video-js .vjs-play-progress {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

/* 视频信息 */
.video-info {
  margin-bottom: 30px;
  text-align: center;
}

.video-title {
  font-size: 24px;
  font-weight: 600;
  color: #2c3e50;
  margin: 0 0 10px 0;
}

.video-meta {
  display: flex;
  justify-content: center;
  gap: 20px;
  font-size: 14px;
  color: #7f8c8d;
}

.event-title {
  font-weight: 500;
}

/* 视频列表 */
.video-list {
  border-top: 2px solid #ecf0f1;
  padding-top: 30px;
}

.list-title {
  font-size: 18px;
  font-weight: 600;
  color: #2c3e50;
  margin: 0 0 20px 0;
  text-align: center;
}

.video-thumbnails {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 15px;
}

.thumbnail-item {
  cursor: pointer;
  transition: transform 0.3s ease, box-shadow 0.3s ease;
  border-radius: 10px;
  overflow: hidden;
  background: white;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
}

.thumbnail-item:hover {
  transform: translateY(-5px);
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
}

.thumbnail-item.active {
  border: 3px solid #3498db;
  transform: translateY(-5px);
  box-shadow: 0 4px 20px rgba(52, 152, 219, 0.3);
}

.thumbnail-preview {
  height: 120px;
  background: linear-gradient(135deg, #8e44ad 0%, #9b59b6 100%);
  background-size: cover;
  background-position: center;
  background-repeat: no-repeat;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
}

.play-overlay {
  font-size: 24px;
  color: white;
  background: rgba(0, 0, 0, 0.6);
  border-radius: 50%;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.5);
}

.duration {
  position: absolute;
  bottom: 8px;
  right: 8px;
  background: rgba(0, 0, 0, 0.8);
  color: white;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 12px;
}

.thumbnail-desc {
  padding: 12px;
  font-size: 13px;
  color: #2c3e50;
  margin: 0;
  font-weight: 500;
  text-align: center;
  line-height: 1.3;
}

/* 加载和错误状态 */
.loading, .error-container {
  text-align: center;
  padding: 60px 20px;
  max-width: 800px;
  margin: 0 auto;
  color: white;
}

.loading-spinner, .error-icon {
  font-size: 48px;
  margin-bottom: 20px;
}

.retry-btn {
  background: rgba(255, 255, 255, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.3);
  color: white;
  padding: 10px 20px;
  border-radius: 20px;
  cursor: pointer;
  font-size: 14px;
  margin-top: 15px;
  backdrop-filter: blur(10px);
  transition: all 0.2s ease;
}

.retry-btn:hover {
  background: rgba(255, 255, 255, 0.3);
  transform: translateY(-2px);
}

/* 响应式设计 */
@media (max-width: 768px) {
  .video-player-page {
    padding: 15px;
  }

  .player-wrapper {
    padding: 20px;
  }

  .video-title {
    font-size: 20px;
  }

  .video-meta {
    flex-direction: column;
    gap: 10px;
  }

  .video-thumbnails {
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 10px;
  }

  .thumbnail-preview {
    height: 100px;
  }

  .play-overlay {
    font-size: 20px;
    width: 32px;
    height: 32px;
  }

  .thumbnail-desc {
    padding: 8px;
    font-size: 12px;
  }
}

/* 视频旋转样式 */
.video-js-container[style*="rotate(90deg)"],
.video-js-container[style*="rotate(270deg)"] {
  height: 60vw;
  max-height: 500px;
}

.video-js-container[style*="rotate(180deg)"] {
  transform-origin: center;
}
</style>
