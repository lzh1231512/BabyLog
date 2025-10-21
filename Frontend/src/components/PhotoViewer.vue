<template>
  <div class="photo-modal" v-if="show">
    <div class="modal-backdrop" @click="close"></div>
    <div class="modal-content">
      <button class="modal-close" @click="close">✕</button>
      <button class="nav-btn prev" @click.stop="prevPhoto" v-if="currentIndex > 0">‹</button>
      <button class="nav-btn next" @click.stop="nextPhoto" v-if="currentIndex < totalImages - 1">›</button>
      <p class="photo-counter">{{ currentIndex + 1 }} / {{ totalImages }}</p>
      <div class="swipe-hint">
        <span class="hint-text" v-if="imageScale <= 1">← 滑动切换 →</span>
        <span class="hint-text" v-else>双击重置缩放</span>
      </div>
      <!-- PC端缩放控制按钮 -->
      <div class="zoom-controls desktop-only">
        <button class="zoom-btn" @click="zoomOut" :disabled="imageScale <= minScale">−</button>
        <span class="zoom-level">{{ Math.round(imageScale * 100) }}%</span>
        <button class="zoom-btn" @click="zoomIn" :disabled="imageScale >= maxScale">+</button>
        <button class="zoom-btn reset" @click="resetZoom" v-if="imageScale !== 1">重置</button>
      </div>
      
      <div class="photo-viewer" @touchstart="handleTouchStart" @touchmove="handleTouchMove" @touchend="handleTouchEnd">
        <div class="current-photo">
          <div 
            class="large-photo-container"
            @dblclick="handleDoubleClick"
          >
            <div 
              class="large-photo-placeholder"
              :style="{
                backgroundImage: `url(${getImageUrl(currentIndex)})`,
                transform: `scale(${imageScale}) translate(${imageTranslateX}px, ${imageTranslateY}px)`,
                transition: imageScale === 1 ? 'transform 0.3s ease' : 'none'
              }"
            >
              <span v-if="!getCurrentImageFilename()" class="photo-icon">📷</span>
            </div>
          </div>
          
          
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { ref, watch, computed, onMounted, onBeforeUnmount } from 'vue'
import { createPhotoViewerGesture } from '@/utils/touchGestureManager'

export default {
  name: 'PhotoViewer',
  props: {
    show: {
      type: Boolean,
      default: false
    },
    images: {
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
    getMediaUrl: {
      type: Function,
      required: true
    }
  },
  emits: ['close', 'indexChange'],
  setup(props, { emit }) {
    const currentIndex = ref(props.initialIndex)
    
    // 图片缩放相关
    const imageScale = ref(1)
    const imageTranslateX = ref(0)
    const imageTranslateY = ref(0)
    const minScale = 0.5
    const maxScale = 3
    
    // 触摸手势管理器
    const gestureManager = ref(null)
    
    // 同步 initialIndex 变化
    watch(() => props.initialIndex, (newVal) => {
      currentIndex.value = newVal
    })
    
    // 监听 show 属性变化，当打开时初始化手势管理器
    watch(() => props.show, (newVal) => {
      if (newVal) {
        setTimeout(() => {
          initGestureManager()
          gestureManager.value.activate()
        }, 100)
      } else {
        if (gestureManager.value) {
          gestureManager.value.deactivate()
        }
      }
    })
    
    // 关闭查看器
    const close = () => {
      // 停用手势管理器
      if (gestureManager.value) {
        gestureManager.value.deactivate()
      }
      
      // 重置缩放状态
      resetImageTransform()
      
      // 通知父组件关闭
      emit('close')
    }
    
    // 获取当前图片URL
    const getImageUrl = (index) => {
      if (props.images && props.images[index]) {
        return props.getMediaUrl(props.eventId, props.images[index].fileName)
      }
      return ''
    }
    
    // 获取当前图片文件名
    const getCurrentImageFilename = () => {
      if (props.images && props.images[currentIndex.value]) {
        return props.images[currentIndex.value].fileName
      }
      return null
    }
    
    // 总图片数量
    const totalImages = computed(() => props.images ? props.images.length : 0)
    
    // 重置图片变换
    const resetImageTransform = () => {
      imageScale.value = 1
      imageTranslateX.value = 0
      imageTranslateY.value = 0
    }
    
    // 放大图片
    const zoomIn = () => {
      if (imageScale.value < maxScale) {
        imageScale.value = Math.min(imageScale.value * 1.5, maxScale)
        // 同步手势管理器状态
        if (gestureManager.value) {
          gestureManager.value.setScale(imageScale.value)
        }
      }
    }
    
    // 缩小图片
    const zoomOut = () => {
      if (imageScale.value > minScale) {
        imageScale.value = Math.max(imageScale.value / 1.5, minScale)
        // 如果缩小后超出边界，重置位置
        if (imageScale.value <= 1) {
          imageTranslateX.value = 0
          imageTranslateY.value = 0
        }
        // 同步手势管理器状态
        if (gestureManager.value) {
          gestureManager.value.setScale(imageScale.value)
          gestureManager.value.setTranslate(imageTranslateX.value, imageTranslateY.value)
        }
      }
    }
    
    // 重置缩放
    const resetZoom = () => {
      resetImageTransform()
      // 同步手势管理器状态
      if (gestureManager.value) {
        gestureManager.value.setScale(1)
        gestureManager.value.setTranslate(0, 0)
      }
    }
    
    // 双击缩放（保留用于桌面端）
    const handleDoubleClick = () => {
      if (imageScale.value === 1) {
        imageScale.value = 2
        if (gestureManager.value) {
          gestureManager.value.setScale(2)
        }
      } else {
        resetImageTransform()
        if (gestureManager.value) {
          gestureManager.value.setScale(1)
          gestureManager.value.setTranslate(0, 0)
        }
      }
    }

    // 上一张照片
    const prevPhoto = () => {
      if (currentIndex.value > 0) {
        currentIndex.value--
        resetImageTransform()
        // 同步手势管理器状态
        if (gestureManager.value) {
          gestureManager.value.setScale(1)
          gestureManager.value.setTranslate(0, 0)
        }
        // 通知父组件索引变化
        emit('indexChange', currentIndex.value)
      }
    }

    // 下一张照片
    const nextPhoto = () => {
      if (currentIndex.value < props.images.length - 1) {
        currentIndex.value++
        resetImageTransform()
        // 同步手势管理器状态
        if (gestureManager.value) {
          gestureManager.value.setScale(1)
          gestureManager.value.setTranslate(0, 0)
        }
        // 通知父组件索引变化
        emit('indexChange', currentIndex.value)
      }
    }

    // 初始化手势管理器
    const initGestureManager = () => {
      if (gestureManager.value) {
        gestureManager.value.deactivate()
      }
      
      gestureManager.value = createPhotoViewerGesture({
        minScale: minScale,
        maxScale: maxScale
      })
      
      // 设置手势回调
      gestureManager.value.on('swipeLeft', () => {
        nextPhoto()
      })
      
      gestureManager.value.on('swipeRight', () => {
        prevPhoto()
      })
      
      gestureManager.value.on('scale', (data) => {
        imageScale.value = data.scale
        imageTranslateX.value = data.translateX
        imageTranslateY.value = data.translateY
      })
      
      gestureManager.value.on('drag', (data) => {
        imageTranslateX.value = data.translateX
        imageTranslateY.value = data.translateY
      })
      
      gestureManager.value.on('doubleTap', (data) => {
        if (data.currentScale === 1) {
          // 放大到2倍
          const newScale = 2
          imageScale.value = newScale
          gestureManager.value.setScale(newScale)
        } else {
          // 重置缩放
          resetZoom()
          gestureManager.value.setScale(1)
          gestureManager.value.setTranslate(0, 0)
        }
      })
    }

    // 触摸事件代理函数
    const handleTouchStart = (e) => {
      if (!props.show || !gestureManager.value) return
      gestureManager.value.handleTouchStart(e)
    }

    const handleTouchMove = (e) => {
      if (!props.show || !gestureManager.value) return
      gestureManager.value.handleTouchMove(e)
    }

    const handleTouchEnd = (e) => {
      if (!props.show || !gestureManager.value) return
      gestureManager.value.handleTouchEnd(e)
    }

    // 键盘事件处理
    const handleKeyDown = (e) => {
      if (!props.show) return
      
      switch (e.key) {
        case 'ArrowLeft':
          e.preventDefault()
          prevPhoto()
          break
        case 'ArrowRight':
          e.preventDefault()
          nextPhoto()
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
    })

    return {
      currentIndex,
      imageScale,
      imageTranslateX,
      imageTranslateY,
      minScale,
      maxScale,
      close,
      getImageUrl,
      getCurrentImageFilename,
      totalImages,
      resetImageTransform,
      zoomIn,
      zoomOut,
      resetZoom,
      handleDoubleClick,
      prevPhoto,
      nextPhoto,
      handleTouchStart,
      handleTouchMove,
      handleTouchEnd
    }
  }
}
</script>

<style scoped>
/* 模态框通用样式 */
.photo-modal {
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
  max-width: 95vw;
  max-height: 95vh;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

/* 修改图片容器高度 */
.large-photo-container {
  width: 80vw;
  max-width: 1200px;
  height: 80vh;
  max-height: 90vh;
  overflow: hidden;
  border-radius: 15px;
  position: relative;
  margin-bottom: 15px;
  background: rgba(255, 255, 255, 0.1);
}

/* 移动端响应式调整 */
@media (max-width: 768px) {
  .large-photo-container {
    width: 100vw;
    height: 85vh;
    margin-bottom: 0;
    border-radius: 0;
  }
}

@media (max-width: 480px) {
  .large-photo-container {
    height: 75vh;
  }
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

.photo-viewer {
  display: flex;
  align-items: center;
  gap: 20px;
}

.current-photo {
  text-align: center;
}

.large-photo-container {
  width: 80vw; /* 使用视口宽度的80% */
  max-width: 1200px; /* 但不超过这个最大宽度 */
  height: 70vh; /* 使用视口高度的70% */
  max-height: 800px; /* 但不超过这个最大高度 */
  overflow: hidden;
  border-radius: 15px;
  position: relative;
  margin-bottom: 15px;
  background: rgba(255, 255, 255, 0.1);
}

.large-photo-placeholder {
  width: 100%;
  height: 100%;
  background: linear-gradient(135deg, #84fab0 0%, #8fd3f4 100%);
  background-size: contain; /* Keep as contain for the modal view to show full image */
  background-position: center;
  background-repeat: no-repeat;
  border-radius: 15px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 80px;
  color: white;
  cursor: grab;
  transform-origin: center;
  margin: 0;
}

.large-photo-placeholder:active {
  cursor: grabbing;
}

.photo-counter {
  color: white;
  font-size: 16px;
  margin: 0;
}

.swipe-hint {
  margin-top: 10px;
  opacity: 0.7;
}

.hint-text {
  color: white;
  font-size: 12px;
  background: rgba(0, 0, 0, 0.5);
  padding: 4px 12px;
  border-radius: 15px;
  display: inline-block;
}

/* 照片容器添加触摸样式 */
.large-photo-placeholder {
  user-select: none;
  -webkit-user-select: none;
  -moz-user-select: none;
  -ms-user-select: none;
  touch-action: pan-x pan-y pinch-zoom;
}

/* 缩放控制按钮 */
.zoom-controls {
  position: absolute;
  top: 10px;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: 10px;
  background: rgba(0, 0, 0, 0.7);
  padding: 8px 15px;
  border-radius: 25px;
  z-index: 1002;
}

.zoom-btn {
  background: rgba(255, 255, 255, 0.2);
  border: none;
  color: white;
  font-size: 16px;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.2s ease;
}

.zoom-btn:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.3);
}

.zoom-btn:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.zoom-btn.reset {
  font-size: 12px;
  width: auto;
  padding: 0 12px;
  border-radius: 16px;
}

.zoom-level {
  color: white;
  font-size: 14px;
  min-width: 45px;
  text-align: center;
}

/* 桌面端显示 */
.desktop-only {
  display: block;
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

.photo-icon {
  text-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
}

/* 响应式设计 */
@media (max-width: 768px) {
  .large-photo-container {
    width: 100vw;
    height: 85vh;
    margin-bottom: 0;
    border-radius: 0;
  }
  
  .large-photo-placeholder {
    font-size: 60px;
    border-radius: 0;
  }
  
  /* 移动端隐藏缩放控制按钮 */
  .desktop-only {
    display: none;
  }
  
  .modal-content {
    max-width: 100vw;
    max-height: 100vh;
    width: 100%;
    height: 100%;
  }
  
  .photo-viewer {
    flex-direction: column;
    gap: 0;
    align-items: center;
    width: 100%;
    height: 100%;
    position: relative;
    padding: 0;  /* 移除左右填充，使用全屏 */
  }
  
  .modal-content .nav-btn {
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

  .modal-content .nav-btn.prev {
    left: 10px;
  }

  .modal-content .nav-btn.next {
    right: 10px;
  }

  .modal-content .nav-btn:hover,
  .modal-content .nav-btn:active {
    background: rgba(0, 0, 0, 0.8);
    border-color: rgba(255, 255, 255, 0.5);
  }

  .modal-content .nav-btn:active {
    transform: translateY(-50%) scale(0.95);
  }
  
  .swipe-hint {
    display: block;
    position: absolute;
    bottom: 10px;
    left: 0;
    right: 0;
    text-align: center;
  }
  
  .photo-counter {
    position: absolute;
    bottom: 40px;
    left: 0;
    right: 0;
    text-align: center;
  }
  
  .hint-text {
    font-size: 11px;
    opacity: 0.8;
    animation: fadeInOut 3s ease-in-out infinite;
  }
  
  .nav-btn {
    font-size: 30px;
    padding: 15px 10px;
  }
}

/* 动画定义 */
@keyframes fadeInOut {
  0%, 100% { opacity: 0.4; }
  50% { opacity: 1; }
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
  
  .large-photo-container {
    width: 95vw;
    height: 85vh;
  }
  
  .large-photo-placeholder {
    font-size: 50px;
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
  
  .current-photo {
    width: 100%;
    height: 100vh;
    display: flex;
    flex-direction: column;
    justify-content: center;
  }
}
</style>
