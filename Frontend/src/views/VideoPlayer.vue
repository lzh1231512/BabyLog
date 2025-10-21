<template>
  <div class="video-player-page">
    <!-- Header with back button -->
    <header class="player-header">
      <button class="back-btn" @click="goBack">
        ← 返回
      </button>
      <h1 class="page-title">视频播放</h1>
    </header>

    <!-- Loading state -->
    <div v-if="loading" class="loading-overlay">
      <div class="loading-content">
        <div class="loading-spinner">⏳</div>
        <p>正在加载视频...</p>
      </div>
    </div>

    <!-- Error state -->
    <div v-if="error" class="error-notification" :class="{ 'transcoding': error.isProcessing }">
      <div class="error-icon">⚠️</div>
      <p>{{ error.message }}</p>
      <button v-if="!error.isProcessing" class="retry-btn" @click="reload">重试</button>
      <button v-else class="retry-btn" @click="startTranscodeCheck">自动重试</button>
      <button class="close-error-btn" @click="clearError">关闭</button>
    </div>

    <!-- Video player -->
    <div v-if="event && currentVideo" class="video-container">
      <div class="player-wrapper">
        <!-- Debug panel -->
        <DebugLogger 
          v-if="showDebugLog" 
          :visible="showDebugLog" 
          @close="showDebugLog = false" 
        />
        
        <!-- Debug controls -->
        <div v-if="debugEnabled" class="debug-controls">
          <button 
            @click="showDebugLog = !showDebugLog" 
            class="debug-toggle-btn"
            :class="{ active: showDebugLog }"
          >
            🐛 调试日志
          </button>
        </div>

        <!-- Video player -->
        <div 
          ref="videoContainer" 
          class="video-js-container"
          v-show="event"
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
          <h2 class="video-title">{{ currentVideo.desc || '视频' }}</h2>
          <div class="video-meta">
            <span class="event-title">来自: {{ event.title }}</span>
            <span class="video-date">{{ formatDate(event.date) }}</span>
          </div>
        </div>

        <!-- Video thumbnails -->
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
                :style="{ backgroundImage: `url(${getThumbnailUrl(event.id, video.fileName)})` }"
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
import { ref, onMounted, onBeforeUnmount, nextTick } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import dayjs from 'dayjs';
import videojs from 'video.js';
import 'video.js/dist/video-js.css';

import DebugLogger from '@/components/DebugLogger.vue';
import VideoService from '@/services/videoService';
import { formatDuration, initializeVideoPlayer } from '@/utils/videoPlayerUtils';
import logger from '@/utils/videoLogger';
import config from '@/config';

export default {
  name: 'VideoPlayer',
  components: {
    DebugLogger
  },
  setup() {
    const route = useRoute();
    const router = useRouter();
    
    // State
    const event = ref(null);
    const currentVideo = ref(null);
    const currentVideoIndex = ref(0);
    const videoList = ref([]);
    const loading = ref(true);
    const error = ref(null);
    const checkInterval = ref(null);
    const debugEnabled = ref(false);
    const showDebugLog = ref(false);
    
    // Refs
    const playerWrapper = ref(null);
    const videoContainer = ref(null);
    let player = null;
    
    // Load video data
    const loadData = async () => {
      loading.value = true;
      clearError();
      
      try {
        // Get config settings
        debugEnabled.value = config.ShowDebugLog;
        
        // Get event ID and video index from route
        const eventId = route.params.id;
        const videoIndex = parseInt(route.params.videoIndex) || 0;
        
        logger.info(`Loading data for event ${eventId}, video index ${videoIndex}`);
        
        // Load event and video data
        const result = await VideoService.loadVideoData(eventId, videoIndex);
        
        if (!result.success) {
          setError(result.error);
          return;
        }
        
        // Set state
        event.value = result.event;
        videoList.value = result.videoList;
        currentVideo.value = result.currentVideo;
        currentVideoIndex.value = result.currentVideoIndex;
        
        // Initialize player
        await nextTick();
        await initPlayer();
      } catch (err) {
        logger.error(`Error loading data: ${err.message}`);
        setError('加载视频数据失败，请稍后重试');
      } finally {
        loading.value = false;
      }
    };
    
    // Initialize video player
    const initPlayer = async () => {
      // Clean up previous player instance
      if (player) {
        try {
          player.dispose();
          player = null;
        } catch (e) {
          logger.warning(`Error disposing player: ${e.message}`);
        }
      }
      
      // Check if video data exists
      if (!currentVideo.value || !event.value || !playerWrapper.value) {
        logger.error('Missing video data or player element');
        setError('视频数据或播放器元素不可用');
        return;
      }
      
      logger.info(`Initializing player for video: ${currentVideo.value.fileName}`);
      
      try {
        // Get video URL
        const urlResult = await VideoService.getVideoUrl(
          event.value.id, 
          currentVideo.value.fileName
        );
        
        if (!urlResult.success) {
          if (urlResult.isProcessing) {
            setError('视频正在转码中，请稍后再试', true);
          } else {
            setError(urlResult.error);
          }
          return;
        }
        
        // Configure player
        const playerConfig = {
          sources: [{
            src: urlResult.videoUrl,
            type: 'application/x-mpegURL' // HLS format
          }]
        };
        
        // Initialize player
        player = initializeVideoPlayer(
          videojs, 
          playerWrapper.value, 
          playerConfig,
          onPlayerReady,
          onPlayerError
        );
        
        // Add additional event listeners
        setupPlayerEventListeners();
      } catch (err) {
        logger.error(`Error initializing player: ${err.message}`);
        setError('初始化播放器失败');
      }
    };
    
    // Player event callbacks
    const onPlayerReady = () => {
      logger.success('Player ready');
    };
    
    const onPlayerError = (err) => {
      const errorMessage = typeof err === 'string' ? err : (err?.message || JSON.stringify(err) || '未知错误');
      logger.error(`Player error: ${errorMessage}`);
      setError(`播放器错误: ${errorMessage}`);
    };
    
    // Setup additional player events
    const setupPlayerEventListeners = () => {
      if (!player) return;
      
      player.on('loadstart', () => logger.info('开始加载视频'));
      player.on('loadeddata', () => logger.success('视频数据已加载'));
      player.on('loadedmetadata', () => {
        logger.success('视频元数据已加载');
        const duration = player.duration();
        logger.info(`视频时长: ${duration}秒`);
      });
      
      player.on('play', () => logger.info('视频开始播放'));
      player.on('pause', () => logger.info('视频暂停'));
      player.on('ended', () => {
        logger.info('视频播放结束');
        // Auto-play next video if available
        if (currentVideoIndex.value < videoList.value.length - 1) {
          switchVideo(currentVideoIndex.value + 1);
        }
      });
    };
    
    // Switch to a different video
    const switchVideo = async (index) => {
      if (index < 0 || index >= videoList.value.length || index === currentVideoIndex.value) {
        return;
      }
      
      logger.info(`Switching to video ${index + 1}/${videoList.value.length}`);
      loading.value = true;
      clearError();
      
      // Clean up existing player
      if (player) {
        try {
          player.dispose();
          player = null;
        } catch (e) {
          logger.warning(`Error disposing player: ${e.message}`);
        }
      }
      
      // Update current video
      currentVideoIndex.value = index;
      currentVideo.value = videoList.value[index];
      
      // Update URL without reloading
      const newPath = `/video-player/${event.value.id}/${index}`;
      if (route.path !== newPath) {
        router.replace(newPath);
      }
      
      try {
        // Initialize new player
        await nextTick();
        await initPlayer();
      } catch (e) {
        logger.error(`Error switching video: ${e.message}`);
        setError('切换视频失败');
      } finally {
        loading.value = false;
      }
    };
    
    // Check transcoding status periodically
    const startTranscodeCheck = () => {
      if (checkInterval.value) {
        clearInterval(checkInterval.value);
      }
      
      logger.info('Starting automatic transcode check');
      
      let checkCount = 0;
      const maxChecks = 20; // Maximum 20 checks (about 1 minute)
      
      checkInterval.value = setInterval(async () => {
        checkCount++;
        logger.info(`Transcode check #${checkCount}`);
        
        if (!event.value || !currentVideo.value) {
          clearInterval(checkInterval.value);
          return;
        }
        
        try {
          const urlResult = await VideoService.getVideoUrl(
            event.value.id, 
            currentVideo.value.fileName
          );
          
          if (urlResult.success) {
            logger.success('Transcoding complete, initializing player');
            clearInterval(checkInterval.value);
            clearError();
            initPlayer();
          } else if (checkCount >= maxChecks) {
            logger.warning('Maximum transcode checks reached');
            clearInterval(checkInterval.value);
            setError('视频转码时间过长，请稍后刷新页面重试');
          } else if (!urlResult.isProcessing) {
            logger.error(`Error checking transcode status: ${urlResult.error}`);
            clearInterval(checkInterval.value);
            setError(urlResult.error);
          }
        } catch (err) {
          logger.error(`Error checking transcode status: ${err.message}`);
        }
      }, 3000);
    };
    
    // Error handling
    const setError = (message, isProcessing = false) => {
      error.value = {
        message,
        isProcessing
      };
      
      if (isProcessing) {
        logger.warning(`Processing error: ${message}`);
      } else {
        logger.error(`Error: ${message}`);
      }
    };
    
    const clearError = () => {
      error.value = null;
      if (checkInterval.value) {
        clearInterval(checkInterval.value);
        checkInterval.value = null;
      }
    };
    
    // Utils
    const formatDate = (date) => {
      return dayjs(date).format('YYYY年MM月DD日');
    };
    
    const getThumbnailUrl = (eventId, fileName) => {
      return VideoService.getThumbnailUrl(eventId, fileName);
    };
    
    const goBack = () => {
      router.back();
    };
    
    const reload = () => {
      loadData();
    };
    
    // Lifecycle hooks
    onMounted(() => {
      logger.info('VideoPlayer component mounted');
      loadData();
    });
    
    onBeforeUnmount(() => {
      logger.info('VideoPlayer component unmounting');
      
      if (checkInterval.value) {
        clearInterval(checkInterval.value);
      }
      
      if (player) {
        try {
          player.dispose();
          player = null;
        } catch (e) {
          logger.warning(`Error disposing player on unmount: ${e.message}`);
        }
      }
    });
    
    return {
      // State
      event,
      currentVideo,
      currentVideoIndex,
      videoList,
      loading,
      error,
      debugEnabled,
      showDebugLog,
      
      // Refs
      playerWrapper,
      videoContainer,
      
      // Methods
      switchVideo,
      formatDate,
      formatDuration,
      getThumbnailUrl,
      goBack,
      reload,
      clearError,
      startTranscodeCheck
    };
  }
};
</script>

<style src="@/assets/styles/videoPlayer.css"></style>
