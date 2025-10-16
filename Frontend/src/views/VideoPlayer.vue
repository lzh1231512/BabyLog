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
        <!-- 调试信息面板 -->
        <div v-if="showDebugLog" class="debug-panel">
          <div class="debug-header">
            <h4>调试日志</h4>
            <button @click="clearLog" class="clear-log-btn">清空</button>
            <button @click="showDebugLog = false" class="close-debug-btn">×</button>
          </div>
          <div class="debug-content" ref="debugContent">
            <div 
              v-for="(log, index) in debugLogs" 
              :key="index"
              :class="['log-entry', `log-${log.type}`]"
            >
              <span class="log-time">{{ log.time }}</span>
              <span class="log-message">{{ log.message }}</span>
            </div>
          </div>
        </div>

        <!-- 调试控制按钮 -->
        <div class="debug-controls">
          <button 
            @click="showDebugLog = !showDebugLog" 
            class="debug-toggle-btn"
            :class="{ active: showDebugLog }"
          >
            🐛 调试日志 ({{ debugLogs.length }})
          </button>
          <button @click="testVideoUrl" class="test-url-btn">测试视频URL</button>
          <button @click="checkDOMState" class="test-url-btn">检查DOM状态</button>
        </div>

        <div 
          ref="videoContainer" 
          class="video-js-container"
          :style="{ transform: `rotate(${currentRotation}deg)` }"
          v-if="currentVideo"
        >
          <video
            ref="videoPlayer"
            class="video-js vjs-default-skin"
            controls
            preload="auto"
            :data-setup="JSON.stringify(videoJsOptions)"
            :key="`video-${event.id}-${currentVideoIndex}`"
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
import { ref, onMounted, onBeforeUnmount, nextTick, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import dayjs from 'dayjs'
import videojs from 'video.js'
import 'video.js/dist/video-js.css'
import { getEventById, getVideoRotation, getVideoUrl, getMediaUrl } from '@/api/events'
import { loadConfig } from '@/config'

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
    const debugContent = ref(null)
    let player = null

    // 调试相关状态
    const showDebugLog = ref(false)
    const debugLogs = ref([])

    // 添加调试日志
    const addLog = (message, type = 'info') => {
      const log = {
        time: dayjs().format('HH:mm:ss.SSS'),
        message,
        type
      }
      debugLogs.value.push(log)
      console.log(`[VideoPlayer ${type}]:`, message)
      
      // 滚动到底部
      nextTick(() => {
        if (debugContent.value) {
          debugContent.value.scrollTop = debugContent.value.scrollHeight
        }
      })
    }

    // 清空日志
    const clearLog = () => {
      debugLogs.value = []
    }

    // 等待DOM元素可用
    const waitForElement = (refElement, maxWait = 3000) => {
      return new Promise((resolve, reject) => {
        const startTime = Date.now()
        
        const check = () => {
          if (refElement.value) {
            addLog('DOM元素已就绪', 'success')
            resolve(refElement.value)
            return
          }
          
          if (Date.now() - startTime > maxWait) {
            addLog(`等待DOM元素超时 (${maxWait}ms)`, 'error')
            reject(new Error('等待DOM元素超时'))
            return
          }
          
          setTimeout(check, 50)
        }
        
        check()
      })
    }

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
        addLog('开始加载视频数据', 'info')
        
        const eventId = route.params.id
        const videoIndex = parseInt(route.params.videoIndex) || 0
        addLog(`路由参数 - eventId: ${eventId}, videoIndex: ${videoIndex}`, 'info')

        // 检查配置加载
        try {
          await loadConfig()
          addLog('配置文件加载成功', 'success')
        } catch (configErr) {
          addLog(`配置文件加载失败: ${configErr.message}`, 'error')
          error.value = '配置文件加载失败'
          return
        }
        
        // 获取事件详情
        addLog('正在获取事件详情...', 'info')
        const response = await getEventById(eventId)
        if (!response.success) {
          addLog(`获取事件详情失败: ${response.message}`, 'error')
          error.value = response.message || '获取事件详情失败'
          return
        }
        
        event.value = response.data
        videoList.value = event.value.media?.videos || []
        addLog(`事件详情获取成功, 找到 ${videoList.value.length} 个视频`, 'success')
        
        if (videoList.value.length === 0) {
          addLog('该事件没有视频文件', 'warning')
          error.value = '该事件没有视频'
          return
        }
        
        // 设置当前视频
        currentVideoIndex.value = Math.min(videoIndex, videoList.value.length - 1)
        currentVideo.value = videoList.value[currentVideoIndex.value]
        addLog(`当前视频: ${currentVideo.value.fileName} (索引: ${currentVideoIndex.value})`, 'info')
        
        // 获取视频旋转角度
        await loadVideoRotation()
        
        // DOM 更新后，watch 会自动调用 initializePlayer
        addLog('等待DOM更新和播放器初始化...', 'info')
        
      } catch (err) {
        addLog(`加载视频数据失败: ${err.message}`, 'error')
        error.value = '网络错误，请稍后重试'
        console.error('加载视频数据失败:', err)
      } finally {
        loading.value = false
      }
    }

    // 获取视频旋转角度
    const loadVideoRotation = async () => {
      try {
        addLog('正在获取视频旋转角度...', 'info')
        const response = await getVideoRotation(event.value.id, currentVideo.value.fileName)
        if (response.success && response.data !== null) {
          // 检查 response.data 是否是数字还是对象
          if (typeof response.data === 'number') {
            currentRotation.value = response.data
          } else if (response.data.rotation !== undefined) {
            currentRotation.value = response.data.rotation
          } else {
            currentRotation.value = 0
          }
          addLog(`视频旋转角度: ${currentRotation.value}度`, 'success')
        } else {
          currentRotation.value = 0
          addLog('使用默认旋转角度: 0度', 'info')
        }
      } catch (err) {
        addLog(`获取视频旋转角度失败: ${err.message}`, 'warning')
        console.warn('获取视频旋转角度失败:', err)
        currentRotation.value = 0
      }
    }

    // 初始化Video.js播放器
    const initializePlayer = async () => {
      addLog('开始初始化Video.js播放器...', 'info')
      
      if (player) {
        addLog('清理旧的播放器实例', 'info')
        player.dispose()
        player = null
      }

      try {
        // 等待视频元素可用
        addLog('等待视频元素可用...', 'info')
        await waitForElement(videoPlayer, 3000)
        
        addLog(`视频元素已找到: ${videoPlayer.value.tagName}`, 'success')
        addLog(`视频元素类名: ${videoPlayer.value.className}`, 'info')
      } catch (err) {
        addLog(`等待视频元素失败: ${err.message}`, 'error')
        addLog(`DOM中video元素数量: ${document.querySelectorAll('video').length}`, 'info')
        addLog(`DOM中.video-js元素数量: ${document.querySelectorAll('.video-js').length}`, 'info')
        error.value = '视频播放器初始化失败：视频元素未找到'
        return
      }

      try {
        const videoUrl = getVideoUrl(event.value.id, currentVideo.value.fileName)
        addLog(`生成的视频URL: ${videoUrl}`, 'info')

        player = videojs(videoPlayer.value, videoJsOptions, () => {
          addLog('Video.js播放器初始化成功', 'success')
          console.log('Video.js player initialized')
          
          // 设置视频源
          addLog('设置视频源...', 'info')
          player.src({
            src: videoUrl,
            type: 'video/mp4'
          })
          
          // 添加事件监听
          player.on('loadstart', () => {
            addLog('开始加载视频', 'info')
          })

          player.on('loadeddata', () => {
            addLog('视频数据已加载', 'success')
          })
          
          player.on('loadedmetadata', () => {
            addLog('视频元数据已加载', 'success')
            const duration = player.duration()
            addLog(`视频时长: ${duration}秒`, 'info')
          })

          player.on('canplay', () => {
            addLog('视频可以开始播放', 'success')
          })

          player.on('canplaythrough', () => {
            addLog('视频已缓冲足够，可以流畅播放', 'success')
          })

          player.on('play', () => {
            addLog('视频开始播放', 'success')
          })

          player.on('pause', () => {
            addLog('视频暂停', 'info')
          })

          player.on('waiting', () => {
            addLog('视频缓冲中...', 'warning')
          })

          player.on('stalled', () => {
            addLog('视频加载停滞', 'warning')
          })
          
          player.on('error', (e) => {
            const playerError = player.error()
            let errorMsg = '未知错误'
            if (playerError) {
              switch (playerError.code) {
                case 1:
                  errorMsg = '视频加载被用户中止'
                  break
                case 2:
                  errorMsg = '网络错误导致视频下载失败'
                  break
                case 3:
                  errorMsg = '视频解码失败'
                  break
                case 4:
                  errorMsg = '视频格式不支持'
                  break
                default:
                  errorMsg = `播放器错误 (代码: ${playerError.code})`
              }
            }
            addLog(`Video.js播放器错误: ${errorMsg}`, 'error')
            console.error('Video.js player error:', e, playerError)
            error.value = `视频播放失败: ${errorMsg}`
          })
          
          player.on('ended', () => {
            addLog('视频播放结束', 'info')
            console.log('Video ended')
            // 自动播放下一个视频
            if (currentVideoIndex.value < videoList.value.length - 1) {
              switchVideo(currentVideoIndex.value + 1)
            }
          })
        })
      } catch (err) {
        addLog(`Video.js初始化失败: ${err.message}`, 'error')
        console.error('Failed to initialize Video.js:', err)
        error.value = '视频播放器初始化失败'
      }
    }

    // 检查DOM状态
    const checkDOMState = () => {
      addLog('=== DOM状态检查 ===', 'info')
      addLog(`videoPlayer.value: ${!!videoPlayer.value}`, 'info')
      addLog(`videoContainer.value: ${!!videoContainer.value}`, 'info')
      addLog(`currentVideo: ${!!currentVideo.value}`, 'info')
      addLog(`event: ${!!event.value}`, 'info')
      
      if (videoPlayer.value) {
        addLog(`视频元素ID: ${videoPlayer.value.id || '无'}`, 'info')
        addLog(`视频元素类名: ${videoPlayer.value.className}`, 'info')
        addLog(`视频元素是否在DOM中: ${document.contains(videoPlayer.value)}`, 'info')
        addLog(`视频元素宽度: ${videoPlayer.value.offsetWidth}`, 'info')
        addLog(`视频元素高度: ${videoPlayer.value.offsetHeight}`, 'info')
      }
      
      const allVideos = document.querySelectorAll('video')
      addLog(`页面中video元素总数: ${allVideos.length}`, 'info')
      
      const vjsElements = document.querySelectorAll('.video-js')
      addLog(`页面中.video-js元素总数: ${vjsElements.length}`, 'info')
      
      if (player) {
        addLog(`Video.js播放器状态: 已初始化`, 'success')
        try {
          addLog(`播放器就绪状态: ${player.readyState()}`, 'info')
        } catch (e) {
          addLog(`获取播放器状态失败: ${e.message}`, 'warning')
        }
      } else {
        addLog(`Video.js播放器状态: 未初始化`, 'warning')
      }
    }

    // 测试视频URL
    const testVideoUrl = async () => {
      if (!event.value || !currentVideo.value) {
        addLog('没有可测试的视频', 'warning')
        return
      }

      const videoUrl = getVideoUrl(event.value.id, currentVideo.value.fileName)
      addLog(`测试视频URL: ${videoUrl}`, 'info')
      
      try {
        const response = await fetch(videoUrl, { method: 'HEAD' })
        if (response.ok) {
          addLog(`URL测试成功 - 状态码: ${response.status}`, 'success')
          addLog(`内容类型: ${response.headers.get('content-type')}`, 'info')
          addLog(`内容长度: ${response.headers.get('content-length')} bytes`, 'info')
        } else {
          addLog(`URL测试失败 - 状态码: ${response.status}`, 'error')
        }
      } catch (err) {
        addLog(`URL测试错误: ${err.message}`, 'error')
      }
    }

    // 切换视频
    const switchVideo = async (index) => {
      if (index < 0 || index >= videoList.value.length || index === currentVideoIndex.value) {
        return
      }
      
      addLog(`切换到视频 ${index + 1}/${videoList.value.length}`, 'info')
      currentVideoIndex.value = index
      currentVideo.value = videoList.value[index]
      addLog(`新视频文件: ${currentVideo.value.fileName}`, 'info')
      
      // 获取新视频的旋转角度
      await loadVideoRotation()
      
      // 更新播放器源
      if (player) {
        const videoUrl = getVideoUrl(event.value.id, currentVideo.value.fileName)
        addLog(`更新播放器源: ${videoUrl}`, 'info')
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

    // 监听 currentVideo 变化，确保 DOM 元素可用后再初始化播放器
    watch([currentVideo, () => videoPlayer.value], async ([newVideo, newVideoElement], [oldVideo]) => {
      if (newVideo && newVideoElement && newVideo !== oldVideo) {
        addLog('检测到currentVideo变化，重新初始化播放器', 'info')
        await nextTick()
        await initializePlayer()
      }
    }, { flush: 'post' })

    // 组件挂载
    onMounted(async () => {
      addLog('VideoPlayer组件已挂载', 'info')
      addLog(`当前环境: ${process.env.NODE_ENV}`, 'info')
      addLog(`用户代理: ${navigator.userAgent}`, 'info')
      
      // 检查Video.js是否可用
      if (typeof videojs === 'undefined') {
        addLog('Video.js未正确加载', 'error')
      } else {
        addLog(`Video.js版本: ${videojs.VERSION}`, 'success')
      }
      
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
      debugContent,
      showDebugLog,
      debugLogs,
      loadVideoData,
      switchVideo,
      testVideoUrl,
      checkDOMState,
      clearLog,
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

  .debug-controls {
    flex-direction: column;
    align-items: center;
  }

  .debug-content {
    max-height: 200px;
    font-size: 11px;
  }

  .debug-toggle-btn, .test-url-btn {
    font-size: 12px;
    padding: 6px 12px;
  }
}

/* 调试面板样式 */
.debug-controls {
  display: flex;
  gap: 10px;
  margin-bottom: 20px;
  justify-content: center;
}

.debug-toggle-btn, .test-url-btn {
  background: rgba(52, 152, 219, 0.1);
  border: 1px solid rgba(52, 152, 219, 0.3);
  color: #3498db;
  padding: 8px 15px;
  border-radius: 20px;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s ease;
  backdrop-filter: blur(10px);
}

.debug-toggle-btn:hover, .test-url-btn:hover {
  background: rgba(52, 152, 219, 0.2);
  transform: translateY(-1px);
}

.debug-toggle-btn.active {
  background: rgba(52, 152, 219, 0.2);
  border-color: #3498db;
  font-weight: 500;
}

.debug-panel {
  background: rgba(0, 0, 0, 0.9);
  border-radius: 10px;
  margin-bottom: 20px;
  overflow: hidden;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
  backdrop-filter: blur(20px);
}

.debug-header {
  background: rgba(52, 152, 219, 0.1);
  padding: 12px 15px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.debug-header h4 {
  margin: 0;
  color: #3498db;
  font-size: 14px;
  font-weight: 600;
}

.clear-log-btn, .close-debug-btn {
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  color: rgba(255, 255, 255, 0.8);
  padding: 4px 8px;
  border-radius: 4px;
  cursor: pointer;
  font-size: 12px;
  transition: all 0.2s ease;
}

.clear-log-btn:hover, .close-debug-btn:hover {
  background: rgba(255, 255, 255, 0.2);
  color: white;
}

.close-debug-btn {
  font-weight: bold;
  font-size: 16px;
  padding: 2px 8px;
}

.debug-content {
  max-height: 300px;
  overflow-y: auto;
  padding: 10px;
  font-family: 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.4;
}

.log-entry {
  display: flex;
  margin-bottom: 4px;
  padding: 2px 0;
  border-left: 3px solid transparent;
  padding-left: 8px;
}

.log-time {
  color: rgba(255, 255, 255, 0.6);
  margin-right: 10px;
  min-width: 70px;
  font-size: 11px;
}

.log-message {
  color: rgba(255, 255, 255, 0.9);
  word-break: break-all;
}

.log-info {
  border-left-color: #3498db;
}

.log-info .log-message {
  color: #3498db;
}

.log-success {
  border-left-color: #2ecc71;
}

.log-success .log-message {
  color: #2ecc71;
}

.log-warning {
  border-left-color: #f39c12;
}

.log-warning .log-message {
  color: #f39c12;
}

.log-error {
  border-left-color: #e74c3c;
}

.log-error .log-message {
  color: #e74c3c;
  font-weight: 500;
}

/* 调试内容滚动条样式 */
.debug-content::-webkit-scrollbar {
  width: 6px;
}

.debug-content::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 3px;
}

.debug-content::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.3);
  border-radius: 3px;
}

.debug-content::-webkit-scrollbar-thumb:hover {
  background: rgba(255, 255, 255, 0.5);
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
