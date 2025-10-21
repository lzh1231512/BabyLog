<template>
  <div class="video-modal" v-if="show">
    <div class="modal-backdrop" @click="close"></div>
    <div class="modal-content">
      <button class="modal-close" @click="close">✕</button>
      <button class="nav-btn prev" @click.stop="prevVideo" v-if="currentIndex > 0">‹</button>
      <button class="nav-btn next" @click.stop="nextVideo" v-if="currentIndex < totalVideos - 1">›</button>
      <p class="video-counter">{{ currentIndex + 1 }} / {{ totalVideos }}</p>
      <div class="swipe-hint">
        <span class="hint-text">← 左右切换视频 →</span>
      </div>
      
      <div class="large-video-container">
        <!-- Video player -->
        <div 
          ref="videoContainer" 
          class="video-js-container"
          v-show="currentVideo"
        >
          <div class="video-player-wrapper" ref="playerWrapper">
            <video
              id="video-player"
              ref="videoPlayer"
              class="video-js vjs-big-play-centered"
            ></video>
          </div>
        </div>
        
        <!-- Video info -->
        <div class="video-info">
          <h2 class="video-title">{{ currentVideo?.desc || '视频' }}</h2>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { ref, watch, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import videojs from 'video.js'
import 'video.js/dist/video-js.css'
import { initializeVideoPlayer, formatDuration } from '@/utils/videoPlayerUtils'
import VideoService from '@/services/videoService'
import logger from '@/utils/videoLogger'

export default {
  name: 'VideoViewer',
  props: {
    show: {
      type: Boolean,
      default: false
    },
    videos: {
      type: Array,
      required: true
    },
    initialIndex: {
      type: Number,
      default: 0
    },
    eventId: {
      type: String,
      required: true
    },
    getVideoUrl: {
      type: Function,
      required: true
    }
  },
  emits: ['close', 'indexChange'],
  setup(props, { emit }) {
    const currentIndex = ref(props.initialIndex)
    const loading = ref(false)
    const error = ref(null)
    const checkInterval = ref(null)
    
    // Refs
    const playerWrapper = ref(null)
    const videoContainer = ref(null)
    let player = null
    
    // 同步 initialIndex 变化
    watch(() => props.initialIndex, (newVal) => {
      currentIndex.value = newVal
    })
    
    // 监听 show 属性变化，当打开时初始化视频播放器
    watch(() => props.show, async (newVal) => {
      if (newVal) {
        await nextTick()
        initPlayer()
      } else {
        disposePlayer()
      }
    })
    
    // 关闭查看器
    const close = () => {
      // 清理视频播放器
      disposePlayer()
      
      // 通知父组件关闭
      emit('close')
    }
    
    // 总视频数量
    const totalVideos = computed(() => props.videos ? props.videos.length : 0)
    
    // 获取当前视频对象
    const currentVideo = computed(() => {
      if (props.videos && props.videos[currentIndex.value]) {
        return props.videos[currentIndex.value]
      }
      return null
    })
    
    // 获取视频缩略图URL
    const getThumbnailUrl = (fileName) => {
      return VideoService.getThumbnailUrl(props.eventId, fileName)
    }
    
    // 初始化视频播放器
    const initPlayer = async () => {
      // 清理之前的播放器实例
      disposePlayer()
      
      // 检查视频数据是否存在
      if (!currentVideo.value || !props.eventId || !playerWrapper.value) {
        logger.error('Missing video data or player element')
        setError('视频数据或播放器元素不可用')
        return
      }
      
      logger.info(`Initializing player for video: ${currentVideo.value.fileName}`)
      loading.value = true
      
      try {
        // 获取视频URL
        const urlResult = await VideoService.getVideoUrl(
          props.eventId, 
          currentVideo.value.fileName
        )
        
        if (!urlResult.success) {
          if (urlResult.isProcessing) {
            setError('视频正在转码中，请稍后再试', true)
          } else {
            setError(urlResult.error)
          }
          return
        }
        
        // 配置播放器
        const playerConfig = {
          sources: [{
            src: urlResult.videoUrl,
            type: 'application/x-mpegURL' // HLS format
          }]
        }

        // 创建 video 元素
        const videoElement = document.createElement('video')
        videoElement.id = 'video-player'
        videoElement.className = 'video-js vjs-big-play-centered'
        
        // 清空容器并添加新创建的元素
        playerWrapper.value.innerHTML = ''
        playerWrapper.value.appendChild(videoElement)
        
        // 初始化播放器
        player = initializeVideoPlayer(
          videojs, 
          'video-player', 
          playerConfig,
          onPlayerReady,
          onPlayerError
        )
        
        // 添加额外的事件监听器
        setupPlayerEventListeners()
      } catch (err) {
        logger.error(`Error initializing player: ${err.message}`)
        setError('初始化播放器失败')
      } finally {
        loading.value = false
      }
    }
    
    // 清理播放器
    const disposePlayer = () => {
      if (player) {
        try {
          player.dispose()
          player = null
        } catch (e) {
          logger.warning(`Error disposing player: ${e.message}`)
        }
      }
      
      // 清除检查转码状态的定时器
      if (checkInterval.value) {
        clearInterval(checkInterval.value)
        checkInterval.value = null
      }
    }
    
    // Player event callbacks
    const onPlayerReady = () => {
      logger.success('Player ready')
    }
    
    const onPlayerError = (err) => {
      const errorMessage = typeof err === 'string' ? err : (err?.message || JSON.stringify(err) || '未知错误')
      logger.error(`Player error: ${errorMessage}`)
      setError(`播放器错误: ${errorMessage}`)
    }
    
    // Setup additional player events
    const setupPlayerEventListeners = () => {
      if (!player) return
      
      player.on('loadstart', () => logger.info('开始加载视频'))
      player.on('loadeddata', () => logger.success('视频数据已加载'))
      player.on('loadedmetadata', () => {
        logger.success('视频元数据已加载')
        const duration = player.duration()
        logger.info(`视频时长: ${duration}秒`)
      })
      
      player.on('play', () => logger.info('视频开始播放'))
      player.on('pause', () => logger.info('视频暂停'))
      player.on('ended', () => {
        logger.info('视频播放结束')
        // Auto-play next video if available
        if (currentIndex.value < totalVideos.value - 1) {
          nextVideo()
        }
      })
    }
    
    // 切换到上一个视频
    const prevVideo = async () => {
      if (currentIndex.value > 0) {
        currentIndex.value--
        await nextTick()
        initPlayer()
        // 通知父组件索引变化
        emit('indexChange', currentIndex.value)
      }
    }

    // 切换到下一个视频
    const nextVideo = async () => {
      if (currentIndex.value < props.videos.length - 1) {
        currentIndex.value++
        await nextTick()
        initPlayer()
        // 通知父组件索引变化
        emit('indexChange', currentIndex.value)
      }
    }

    // 错误处理
    const setError = (message, isProcessing = false) => {
      error.value = {
        message,
        isProcessing
      }
      
      if (isProcessing) {
        logger.warning(`Processing error: ${message}`)
        // 开始自动检查转码状态
        startTranscodeCheck()
      } else {
        logger.error(`Error: ${message}`)
      }
    }
    
    // 清除错误信息
    const clearError = () => {
      error.value = null
      if (checkInterval.value) {
        clearInterval(checkInterval.value)
        checkInterval.value = null
      }
    }
    
    // 检查转码状态
    const startTranscodeCheck = () => {
      if (checkInterval.value) {
        clearInterval(checkInterval.value)
      }
      
      logger.info('Starting automatic transcode check')
      
      let checkCount = 0
      const maxChecks = 20 // Maximum 20 checks (about 1 minute)
      
      checkInterval.value = setInterval(async () => {
        checkCount++
        logger.info(`Transcode check #${checkCount}`)
        
        if (!props.eventId || !currentVideo.value) {
          clearInterval(checkInterval.value)
          return
        }
        
        try {
          const urlResult = await VideoService.getVideoUrl(
            props.eventId, 
            currentVideo.value.fileName
          )
          
          if (urlResult.success) {
            logger.success('Transcoding complete, initializing player')
            clearInterval(checkInterval.value)
            clearError()
            initPlayer()
          } else if (checkCount >= maxChecks) {
            logger.warning('Maximum transcode checks reached')
            clearInterval(checkInterval.value)
            setError('视频转码时间过长，请稍后重试')
          } else if (!urlResult.isProcessing) {
            logger.error(`Error checking transcode status: ${urlResult.error}`)
            clearInterval(checkInterval.value)
            setError(urlResult.error)
          }
        } catch (err) {
          logger.error(`Error checking transcode status: ${err.message}`)
        }
      }, 3000)
    }

    // 键盘事件处理
    const handleKeyDown = (e) => {
      if (!props.show) return
      
      switch (e.key) {
        case 'ArrowLeft':
          e.preventDefault()
          prevVideo()
          break
        case 'ArrowRight':
          e.preventDefault()
          nextVideo()
          break
        case 'Escape':
          e.preventDefault()
          close()
          break
      }
    }

    onMounted(() => {
      // 添加键盘事件监听
      document.addEventListener('keydown', handleKeyDown)
    })

    // 组件卸载时移除事件监听
    onBeforeUnmount(() => {
      document.removeEventListener('keydown', handleKeyDown)
      // 清理播放器
      disposePlayer()
    })

    return {
      currentIndex,
      currentVideo,
      totalVideos,
      loading,
      error,
      playerWrapper,
      videoContainer,
      close,
      prevVideo,
      nextVideo,
      getThumbnailUrl,
      formatDuration
    }
  }
}
</script>

<style scoped>
/* 模态框通用样式 */
.video-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-backdrop {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.9);
}

.modal-content {
  position: relative;
  z-index: 1001;
  max-width: 100vw;
  max-height: 100vh;
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 0;
}

/* 视频容器样式 */
.large-video-container {
  width: 80vw;
  max-width: 1200px;
  height: 80vh;
  max-height: 90vh;
  overflow: hidden;
  border-radius: 15px;
  position: relative;
  margin-bottom: 15px;
  background: rgba(0, 0, 0, 0.2);
  display: flex;
  flex-direction: column;
}

.video-js-container {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.video-player-wrapper {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* VideoJS 样式覆盖 */
:deep(.video-js) {
  width: 100%;
  height: 100%;
  border-radius: 15px;
  overflow: hidden;
}

:deep(.vjs-big-play-centered .vjs-big-play-button) {
  margin-top: -1.5em;
  margin-left: -1.5em;
}

.modal-close {
  position: absolute;
  top: 10px;
  right: 10px;
  background: rgba(0, 0, 0, 0.5);
  border: none;
  color: white;
  font-size: 24px;
  cursor: pointer;
  padding: 10px;
  z-index: 1010;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.video-counter {
  color: white;
  font-size: 16px;
  margin: 0;
  position: absolute;
  bottom: 40px;
  left: 0;
  right: 0;
  text-align: center;
}

.swipe-hint {
  display: block;
  position: absolute;
  bottom: 10px;
  left: 0;
  right: 0;
  text-align: center;
}

.hint-text {
  color: white;
  font-size: 12px;
  background: rgba(0, 0, 0, 0.5);
  padding: 4px 12px;
  border-radius: 15px;
  display: inline-block;
  font-size: 11px;
  opacity: 0.8;
  animation: fadeInOut 3s ease-in-out infinite;
}

/* 视频信息 */
.video-info {
  position: absolute;
  top: 10px;
  left: 0;
  right: 0;
  text-align: center;
  padding: 10px;
  z-index: 1002;
}

.video-title {
  color: white;
  font-size: 18px;
  margin: 0;
  text-shadow: 0 1px 3px rgba(0, 0, 0, 0.5);
  background-color: rgba(0, 0, 0, 0.5);
  display: inline-block;
  padding: 5px 15px;
  border-radius: 20px;
}

/* 导航按钮 */
.nav-btn {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  z-index: 1001;
  background: rgba(0, 0, 0, 0.7);
  border: 2px solid rgba(255, 255, 255, 0.3);
  color: white;
  font-size: 24px;
  padding: 12px 8px;
  min-width: 40px;
  min-height: 40px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.nav-btn.prev {
  left: 10px;
}

.nav-btn.next {
  right: 10px;
}

.nav-btn:hover,
.nav-btn:active {
  background: rgba(0, 0, 0, 0.8);
  border-color: rgba(255, 255, 255, 0.5);
}

.nav-btn:active {
  transform: translateY(-50%) scale(0.95);
}

/* 加载和错误状态 */
.loading-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1005;
}

.loading-content {
  text-align: center;
  color: white;
}

.loading-spinner {
  font-size: 40px;
  margin-bottom: 15px;
  animation: spin 1.5s linear infinite;
}

.error-notification {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  background: rgba(255, 0, 0, 0.8);
  color: white;
  padding: 20px;
  border-radius: 10px;
  text-align: center;
  max-width: 300px;
  z-index: 1006;
}

.error-notification.transcoding {
  background: rgba(255, 165, 0, 0.8);
}

.error-icon {
  font-size: 30px;
  margin-bottom: 10px;
}

.retry-btn, .close-error-btn {
  margin-top: 15px;
  padding: 8px 16px;
  border: none;
  border-radius: 5px;
  background: white;
  color: black;
  cursor: pointer;
  margin-right: 10px;
}

/* 动画定义 */
@keyframes fadeInOut {
  0%, 100% { opacity: 0.4; }
  50% { opacity: 1; }
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

/* 响应式设计 */
@media (max-width: 768px) {
  .large-video-container {
    width: 100vw;
    height: 85vh;
    margin-bottom: 0;
    border-radius: 0;
  }
  
  :deep(.video-js) {
    border-radius: 0;
  }
  
  .nav-btn {
    font-size: 30px;
    padding: 15px 10px;
  }
}

/* 更小屏幕设备的优化 */
@media (max-width: 480px) {
  .modal-content .nav-btn {
    font-size: 20px;
    padding: 10px 6px;
    min-width: 36px;
    min-height: 36px;
  }
  
  .modal-content .nav-btn.prev {
    left: 5px;
  }

  .modal-content .nav-btn.next {
    right: 5px;
  }
  
  .large-video-container {
    width: 95vw;
    height: 85vh;
  }
  
  .modal-close {
    top: 10px;
    right: 10px;
    font-size: 20px;
    padding: 8px;
    background: rgba(0, 0, 0, 0.5);
    border-radius: 50%;
    width: 36px;
    height: 36px;
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1010;
  }
  
  .large-video-container {
    width: 100%;
    height: 100vh;
    display: flex;
    flex-direction: column;
    justify-content: center;
  }
}
</style>
