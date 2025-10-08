<template>
  <div 
    class="lazy-image-container"
    :class="{ 
      'loaded': imageLoaded, 
      'error': imageError,
      'small': small 
    }"
    ref="containerRef"
  >
    <!-- 加载中状态 -->
    <div v-if="!imageLoaded && !imageError" class="loading-placeholder">
      <div class="loading-spinner"></div>
      <span class="loading-text">加载中...</span>
    </div>
    
    <!-- 错误状态 -->
    <div v-else-if="imageError" class="error-placeholder">
      <span class="error-icon">📷</span>
      <span class="error-text">加载失败</span>
      <button class="retry-button" @click="retryLoad" v-if="retryCount < maxRetries">重试</button>
    </div>
    
    <!-- 实际图片 -->
    <img
      v-if="shouldLoad"
      :src="optimizedSrc"
      :alt="alt"
      @load="onImageLoad"
      @error="onImageError"
      class="lazy-image"
      :style="{ opacity: imageLoaded ? 1 : 0 }"
      loading="lazy"
    />
  </div>
</template>

<script>
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { imagePerformanceMonitor } from '@/utils/imagePerformance'
import { optimizeImageUrl } from '@/utils/imageOptimization'

export default {
  name: 'LazyImage',
  props: {
    src: {
      type: String,
      required: true
    },
    alt: {
      type: String,
      default: ''
    },
    small: {
      type: Boolean,
      default: false
    },
    // 触发加载的距离（像素）
    threshold: {
      type: Number,
      default: 200
    },
    // 是否启用预加载
    preload: {
      type: Boolean,
      default: false
    },
    // 加载优先级（high, normal, low）
    priority: {
      type: String,
      default: 'normal',
      validator: (value) => ['high', 'normal', 'low'].includes(value)
    }
  },
  setup(props) {
    const containerRef = ref(null)
    const shouldLoad = ref(false)
    const imageLoaded = ref(false)
    const imageError = ref(false)
    const retryCount = ref(0)
    const maxRetries = 2
    const currentSrc = ref(props.src)
    let observer = null
    let performanceEventId = null

    // 优化后的图片URL
    const optimizedSrc = computed(() => {
      const options = {
        quality: props.priority === 'low' ? 'low' : props.priority === 'high' ? 'high' : 'medium',
        formatOptimization: true,
        qualityOptimization: true
      }
      
      return optimizeImageUrl(currentSrc.value, options)
    })

    // 创建Intersection Observer
    const createObserver = () => {
      if (!window.IntersectionObserver) {
        // 如果不支持IntersectionObserver，直接加载
        shouldLoad.value = true
        return
      }

      observer = new IntersectionObserver(
        (entries) => {
          entries.forEach(entry => {
            if (entry.isIntersecting || entry.intersectionRatio > 0) {
              // 根据优先级决定加载延迟
              const delay = props.priority === 'high' ? 0 : 
                           props.priority === 'normal' ? 100 : 300
              
              setTimeout(() => {
                shouldLoad.value = true
                // 开始性能监控
                performanceEventId = imagePerformanceMonitor.startMonitoring(props.src)
              }, delay)
              
              // 开始加载后取消观察
              if (observer) {
                observer.unobserve(entry.target)
                observer = null
              }
            }
          })
        },
        {
          rootMargin: `${props.threshold}px`,
          threshold: 0.01
        }
      )

      if (containerRef.value) {
        observer.observe(containerRef.value)
      }
    }

    const onImageLoad = () => {
      imageLoaded.value = true
      imageError.value = false
      // 结束性能监控（成功）
      if (performanceEventId) {
        imagePerformanceMonitor.endMonitoring(performanceEventId, true)
      }
    }

    const onImageError = () => {
      retryCount.value++
      if (retryCount.value <= maxRetries) {
        // 延迟重试
        setTimeout(() => {
          // 更新图片源触发重新加载
          currentSrc.value = `${props.src}?retry=${retryCount.value}&t=${Date.now()}`
        }, 1000 * retryCount.value)
      } else {
        imageLoaded.value = false
        imageError.value = true
        // 结束性能监控（失败）
        if (performanceEventId) {
          imagePerformanceMonitor.endMonitoring(performanceEventId, false)
        }
      }
    }

    onMounted(() => {
      if (props.preload) {
        // 如果启用预加载，直接开始加载
        shouldLoad.value = true
        // 开始性能监控
        performanceEventId = imagePerformanceMonitor.startMonitoring(props.src)
      } else {
        createObserver()
      }
    })

    onUnmounted(() => {
      if (observer) {
        observer.disconnect()
      }
    })

    const retryLoad = () => {
      imageError.value = false
      imageLoaded.value = false
      
      // 重新开始性能监控
      performanceEventId = imagePerformanceMonitor.startMonitoring(props.src)
      
      // 更新当前图片源，添加重试参数
      currentSrc.value = `${props.src}?retry=${retryCount.value}&t=${Date.now()}`
    }

    return {
      containerRef,
      shouldLoad,
      imageLoaded,
      imageError,
      retryCount,
      maxRetries,
      currentSrc,
      optimizedSrc,
      onImageLoad,
      onImageError,
      retryLoad
    }
  }
}
</script>

<style scoped>
.lazy-image-container {
  position: relative;
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
  border-radius: 8px;
  overflow: hidden;
  transition: all 0.3s ease;
}

.lazy-image {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: opacity 0.3s ease;
}

.loading-placeholder,
.error-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  color: #6c757d;
  font-size: 12px;
  text-align: center;
  padding: 10px;
}

.loading-spinner {
  width: 20px;
  height: 20px;
  border: 2px solid #e9ecef;
  border-top: 2px solid #6c757d;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 8px;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

/* 骨架屏效果 */
.lazy-image-container::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: linear-gradient(90deg, 
    transparent 0%, 
    rgba(255, 255, 255, 0.6) 50%, 
    transparent 100%);
  transform: translateX(-100%);
  animation: shimmer 2s ease-in-out infinite;
  z-index: 1;
  pointer-events: none;
}

.lazy-image-container.loaded::before,
.lazy-image-container.error::before {
  animation: none;
  opacity: 0;
  transition: opacity 0.3s ease;
}

@keyframes shimmer {
  0% {
    transform: translateX(-100%);
  }
  50% {
    transform: translateX(0%);
  }
  100% {
    transform: translateX(100%);
  }
}

/* 脉冲效果作为备选 */
.lazy-image-container::after {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(108, 117, 125, 0.1);
  animation: pulse 1.5s ease-in-out infinite alternate;
  z-index: 0;
}

.lazy-image-container.loaded::after,
.lazy-image-container.error::after {
  display: none;
}

@keyframes pulse {
  0% {
    opacity: 0.4;
  }
  100% {
    opacity: 0.8;
  }
}

.loading-text,
.error-text {
  font-size: 10px;
  font-weight: 500;
  opacity: 0.8;
}

.error-icon {
  font-size: 20px;
  margin-bottom: 5px;
  opacity: 0.6;
}

.error-placeholder {
  background: linear-gradient(135deg, #f8d7da 0%, #f5c6cb 100%);
  color: #721c24;
}

.retry-button {
  background: #dc3545;
  color: white;
  border: none;
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 8px;
  cursor: pointer;
  margin-top: 5px;
  transition: background-color 0.2s ease;
}

.retry-button:hover {
  background: #c82333;
}

/* 小图片样式 */
.lazy-image-container.small {
  min-height: 60px;
}

.lazy-image-container.small .loading-spinner {
  width: 16px;
  height: 16px;
  border-width: 1.5px;
}

.lazy-image-container.small .loading-text,
.lazy-image-container.small .error-text {
  font-size: 9px;
}

.lazy-image-container.small .error-icon {
  font-size: 16px;
}

/* 加载完成后的样式 */
.lazy-image-container.loaded {
  background: transparent;
}

/* 悬停效果 */
.lazy-image-container:hover {
  transform: scale(1.02);
}

/* 响应式 */
@media (max-width: 768px) {
  .loading-text,
  .error-text {
    font-size: 9px;
  }
  
  .loading-spinner {
    width: 16px;
    height: 16px;
  }
}
</style>
