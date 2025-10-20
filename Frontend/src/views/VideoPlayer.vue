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
    <div v-if="loading" class="loading-overlay">
      <div class="loading-content">
        <div class="loading-spinner">⏳</div>
        <p>正在加载视频...</p>
      </div>
    </div>

    <!-- 错误状态 -->
    <div v-if="error" class="error-notification" :class="{ 'transcoding': error.includes('转码中') }">
      <div class="error-icon">⚠️</div>
      <p v-html="error"></p>
      <button v-if="!error.includes('转码中')" class="retry-btn" @click="loadVideoData">重试</button>
      <button class="close-error-btn" @click="error = ''">关闭</button>
    </div>

    <!-- 视频播放器 -->
    <div v-if="event && currentVideo" class="video-container">
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
          v-show="event"
        >
          <div class="video-player-wrapper">
            <video
              id="video-player"
              ref="videoPlayer"
              class="video-js vjs-big-play-centered"
            ></video>
          </div>
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
import { getEventById, getVideoURL, getMediaUrl } from '@/api/events'
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

    const videoPlayerOptions = ref({
      fluid: true,
      responsive: true,
      width: '100%',
      height: 'auto',
      playbackRates: [0.5, 1, 1.25, 1.5, 2],
      controls: true,
      preload: 'auto',
      playsinline: true,
      autoplay: false,
      language: 'zh-cn'
    })

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
        
        // 等待DOM更新并初始化播放器
        addLog('等待DOM更新和播放器初始化...', 'info')
        await nextTick()
        await initializePlayer()
        
      } catch (err) {
        addLog(`加载视频数据失败: ${err.message}`, 'error')
        error.value = '网络错误，请稍后重试'
        console.error('加载视频数据失败:', err)
      } finally {
        loading.value = false
      }
    }



    // 初始化Video.js播放器
    const initializePlayer = async () => {
      addLog('开始初始化Video.js播放器...', 'info')
      
      // 清理旧实例
      if (player) {
        addLog('清理旧的播放器实例', 'info')
        try {
          player.dispose()
        } catch (e) {
          addLog(`清理播放器实例出错: ${e.message}，继续初始化`, 'warning')
        }
        player = null
      }

      // 确保视频容器和当前视频已正确设置
      if (!currentVideo.value || !event.value) {
        addLog('当前视频数据不存在，无法初始化播放器', 'error')
        error.value = '视频数据加载失败'
        return
      }

      // 确认DOM元素存在
      if (!videoPlayer.value || !videoContainer.value) {
        addLog('找不到播放器DOM元素，等待300ms后重试', 'warning')
        await new Promise(resolve => setTimeout(resolve, 300))
        
        if (!videoPlayer.value || !videoContainer.value) {
          addLog('最终检查失败，播放器DOM元素不存在', 'error')
          error.value = '播放器加载失败: DOM元素丢失'
          return
        }
      }

      addLog(`视频元素ID: ${videoPlayer.value.id}`, 'info')
      addLog(`视频容器存在: ${!!videoContainer.value}`, 'info')
      
      try {
        // 等待容器元素可用
        addLog('等待容器元素可用...', 'info')
        await waitForElement(videoPlayer, 3000)
        
        addLog(`容器元素已找到: ${videoPlayer.value.tagName}`, 'success')
        addLog(`容器元素ID: ${videoPlayer.value.id}`, 'info')
      } catch (err) {
        addLog(`等待容器元素失败: ${err.message}`, 'error')
        error.value = '视频播放器初始化失败：容器元素未找到'
        return
      }

      try {
        addLog('获取视频URL...', 'info')
        const videoUrlResponse = await getVideoURL(event.value.id, currentVideo.value.fileName)
        
        if (!videoUrlResponse.success) {
          addLog(`获取视频URL失败: ${videoUrlResponse.message}`, 'error')
          error.value = videoUrlResponse.message || '获取视频URL失败'
          return
        }

        const videoData = videoUrlResponse.data
        addLog(`视频状态: ${JSON.stringify(videoData)}`, 'info')

        // 检查视频是否正在处理中
        if (videoData.isProcessing) {
          addLog('视频正在转码中，请稍后再试', 'warning')
          error.value = '视频正在转码中，请稍后再试 <span class="retry-transcode-btn" id="retry-transcode">自动重试</span>'
          
          // 添加重试按钮的事件监听器
          setTimeout(() => {
            const retryBtn = document.getElementById('retry-transcode')
            if (retryBtn) {
              retryBtn.addEventListener('click', () => {
                addLog('自动重试转码检查...', 'info')
                // 设置定时器每3秒检查一次转码状态
                let checkCount = 0
                const maxChecks = 20 // 最多检查20次，约1分钟
                
                const checkInterval = setInterval(async () => {
                  checkCount++
                  addLog(`第${checkCount}次检查转码状态...`, 'info')
                  
                  try {
                    const response = await getVideoURL(event.value.id, currentVideo.value.fileName)
                    if (response.success && response.data) {
                      if (!response.data.isProcessing && response.data.isTranscoded) {
                        addLog('视频转码已完成', 'success')
                        clearInterval(checkInterval)
                        initializePlayer()
                      } else if (checkCount >= maxChecks) {
                        addLog('达到最大检查次数，停止检查', 'warning')
                        error.value = '视频转码时间过长，请稍后刷新页面重试'
                        clearInterval(checkInterval)
                      } else {
                        addLog('视频仍在转码中...', 'info')
                      }
                    } else {
                      addLog('检查转码状态失败', 'error')
                      clearInterval(checkInterval)
                    }
                  } catch (err) {
                    addLog(`检查转码状态出错: ${err.message}`, 'error')
                    clearInterval(checkInterval)
                  }
                }, 3000) // 每3秒检查一次
              })
            }
          }, 100)
          return
        }

        // 检查视频是否已转码
        if (!videoData.isTranscoded) {
          addLog('视频未经过转码，可能无法正常播放', 'warning')
        }

        const videoUrl = videoData.hlsUrl
        addLog(`生成的视频URL: ${videoUrl}`, 'info')

        // 配置Video.js播放器选项
        const playerConfig = {
          ...videoPlayerOptions.value,
          sources: [{
            src: videoUrl,
            type: 'application/x-mpegURL' // HLS格式
          }]
        }

        addLog('正在初始化Video.js播放器...', 'info')
        addLog(`播放器配置: ${JSON.stringify(playerConfig, null, 2)}`, 'info')
        
        player = videojs(videoPlayer.value, playerConfig)
        
        addLog('Video.js播放器初始化成功', 'success')
        addLog(`播放器实例: ${typeof player}`, 'info')
        console.log('Video.js initialized', player)

        // 添加事件监听
        player.ready(() => {
          addLog('播放器准备就绪', 'success')
        })

        player.on('loadstart', () => {
          addLog('开始加载视频', 'info')
        })

        player.on('loadeddata', () => {
          addLog('视频数据已加载', 'success')
        })
        
        player.on('loadedmetadata', () => {
          addLog('视频元数据已加载', 'success')
          try {
            const duration = player.duration()
            addLog(`视频时长: ${duration}秒`, 'info')
            const videoWidth = player.videoWidth() || 0
            const videoHeight = player.videoHeight() || 0
            addLog(`视频尺寸: ${videoWidth}x${videoHeight}`, 'info')
          } catch (e) {
            addLog(`获取视频信息失败: ${e.message}`, 'warning')
          }
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

        player.on('emptied', () => {
          addLog('视频元素已清空', 'warning')
        })

        player.on('abort', () => {
          addLog('视频加载被中止', 'warning')
        })

        player.on('suspend', () => {
          addLog('视频加载暂停', 'warning')
        })
        
        player.on('error', () => {
          const e = player.error()
          addLog(`Video.js播放器错误: ${JSON.stringify(e)}`, 'error')
          console.error('Video.js error:', e)
          
          if (e) {
            addLog(`视频元素错误码: ${e.code}`, 'error')
            addLog(`视频元素错误信息: ${e.message}`, 'error')
          }
          
          const errorMessage = e?.message || JSON.stringify(e) || '未知错误';
          addLog(`视频播放失败: ${errorMessage}`, 'error')
          error.value = `视频播放失败: ${errorMessage}`
        })
        
        player.on('ended', () => {
          addLog('视频播放结束', 'info')
          console.log('Video ended')
          // 自动播放下一个视频
          if (currentVideoIndex.value < videoList.value.length - 1) {
            switchVideo(currentVideoIndex.value + 1)
          }
        })

        // 添加延迟检查，确保播放器正确渲染
        setTimeout(() => {
          addLog('延迟检查播放器状态...', 'info')
          const videoElement = player.tech().el()
          if (videoElement) {
            addLog(`视频元素存在: ${videoElement.tagName}`, 'success')
            addLog(`视频元素src: ${videoElement.src}`, 'info')
            addLog(`视频元素readyState: ${videoElement.readyState}`, 'info')
            addLog(`视频元素networkState: ${videoElement.networkState}`, 'info')
          } else {
            addLog('未找到视频元素', 'error')
          }
          
          const playerElement = document.querySelector(`#${videoPlayer.value.id}`)
          if (playerElement) {
            addLog(`播放器容器存在，子元素数量: ${playerElement.children.length}`, 'info')
            addLog(`播放器容器内容: ${playerElement.innerHTML.substring(0, 200)}...`, 'info')
          } else {
            addLog('未找到播放器容器', 'error')
          }
        }, 1000)

      } catch (err) {
        addLog(`Video.js播放器初始化失败: ${err.message}`, 'error')
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

      try {
        addLog('获取视频URL...', 'info')
        const videoUrlResponse = await getVideoURL(event.value.id, currentVideo.value.fileName)
        
        if (!videoUrlResponse.success) {
          addLog(`获取视频URL失败: ${videoUrlResponse.message}`, 'error')
          return
        }

        const videoData = videoUrlResponse.data
        addLog(`视频状态: ${JSON.stringify(videoData)}`, 'info')
        
        if (videoData.isProcessing) {
          addLog('视频正在转码中', 'warning')
          return
        }

        if (!videoData.isTranscoded) {
          addLog('视频未经过转码，可能无法正常播放', 'warning')
        }

        const videoUrl = videoData.hlsUrl
        addLog(`生成的视频URL: ${videoUrl}`, 'info')
        
        // 测试视频URL
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
      } catch (err) {
        addLog(`获取视频URL错误: ${err.message}`, 'error')
      }
    }



    // 切换视频
    const switchVideo = async (index) => {
      if (index < 0 || index >= videoList.value.length || index === currentVideoIndex.value) {
        return
      }
      
      addLog(`切换到视频 ${index + 1}/${videoList.value.length}`, 'info')
      loading.value = true
      error.value = ''
      
      // 清理旧播放器
      if (player) {
        addLog('销毁现有播放器实例', 'info')
        try {
          player.dispose()
        } catch (e) {
          addLog(`销毁播放器实例出错: ${e.message}`, 'warning')
        }
        player = null
      }
      
      // 更新当前视频信息
      currentVideoIndex.value = index
      currentVideo.value = videoList.value[index]
      addLog(`切换到视频: ${currentVideo.value.fileName}`, 'info')
      
      // 更新URL但不重新加载页面
      const newPath = `/video-player/${event.value.id}/${index}`
      if (route.path !== newPath) {
        router.replace(newPath)
      }
      
      // 确保DOM完全更新
      await nextTick()
      await new Promise(resolve => setTimeout(resolve, 300))
      
      // 初始化新播放器
      try {
        // 直接初始化播放器，而不是重新加载全部数据
        await nextTick()
        await initializePlayer()
      } catch (e) {
        addLog(`初始化播放器失败: ${e.message}`, 'error')
        error.value = '切换视频失败，请重试'
      } finally {
        loading.value = false
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

    // 移除监听器，我们现在完全通过switchVideo控制播放器的初始化

    // 组件挂载
    onMounted(async () => {
      addLog('VideoPlayer组件已挂载', 'info')
      addLog(`当前环境: ${process.env.NODE_ENV}`, 'info')
      addLog(`用户代理: ${navigator.userAgent}`, 'info')
      
      // 检查Video.js播放器是否可用
      if (typeof videojs === 'undefined') {
        addLog('Video.js播放器未正确加载', 'error')
      } else {
        addLog(`Video.js播放器已加载`, 'success')
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
      getVideoURL
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
}

/* Video.js播放器样式覆盖 */
.video-js-container :deep(.video-js) {
  border-radius: 15px;
  background-color: #000;
}

.video-js-container :deep(.video-js .vjs-big-play-button) {
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.8);
  border: 3px solid rgba(255, 255, 255, 0.9);
  transition: all 0.3s ease;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}

.video-js-container :deep(.video-js:hover .vjs-big-play-button) {
  background: rgba(0, 0, 0, 0.9);
  transform: translate(-50%, -50%) scale(1.1);
}

.video-js-container :deep(.video-js .vjs-control-bar) {
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(10px);
  border-radius: 0 0 15px 15px;
}

.video-js-container :deep(.video-js .vjs-progress-control) {
  height: 6px;
}

.video-js-container :deep(.video-js .vjs-play-progress) {
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
.loading-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1001;
}

.loading-content {
  text-align: center;
  padding: 30px 40px;
  background: rgba(30, 60, 114, 0.9);
  border-radius: 15px;
  color: white;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.3);
}

.error-notification {
  position: fixed;
  top: 80px;
  left: 50%;
  transform: translateX(-50%);
  background: rgba(231, 76, 60, 0.9);
  border-radius: 10px;
  padding: 15px 20px;
  text-align: center;
  color: white;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
  z-index: 1000;
  max-width: 90%;
  backdrop-filter: blur(5px);
  display: flex;
  flex-direction: column;
  align-items: center;
}

.error-notification.transcoding {
  background: rgba(243, 156, 18, 0.9);
}

.close-error-btn {
  position: absolute;
  top: 5px;
  right: 5px;
  background: rgba(255, 255, 255, 0.2);
  border: none;
  color: white;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  font-size: 14px;
  line-height: 1;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.close-error-btn:hover {
  background: rgba(255, 255, 255, 0.3);
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

.retry-btn:hover,
.retry-transcode-btn:hover {
  background: rgba(255, 255, 255, 0.3);
  transform: translateY(-2px);
}

.retry-transcode-btn {
  display: inline-block;
  background: rgba(52, 152, 219, 0.2);
  border: 1px solid rgba(52, 152, 219, 0.4);
  color: white;
  padding: 5px 15px;
  border-radius: 20px;
  cursor: pointer;
  font-size: 14px;
  margin-left: 10px;
  backdrop-filter: blur(10px);
  transition: all 0.2s ease;
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
  
  .error-notification {
    top: 60px;
    padding: 10px 15px;
    font-size: 14px;
  }
  
  .error-icon {
    font-size: 32px;
    margin-bottom: 10px;
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


</style>
