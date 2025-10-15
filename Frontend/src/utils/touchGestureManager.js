/**
 * 触摸手势管理器
 * 支持单指滑动、双指缩放、图片拖拽等手势
 */

export class TouchGestureManager {
  constructor(options = {}) {
    this.options = {
      minScale: 0.5,
      maxScale: 3,
      swipeThreshold: 50,
      maxVerticalDistance: 100,
      doubleTapThreshold: 300,
      ...options
    }
    
    // 触摸状态
    this.isActive = false
    this.touches = []
    this.lastTouchTime = 0
    this.tapCount = 0
    
    // 单指滑动状态
    this.startX = 0
    this.startY = 0
    this.currentX = 0
    this.currentY = 0
    
    // 双指缩放状态
    this.initialDistance = 0
    this.initialScale = 1
    this.currentScale = 1
    
    // 拖拽状态
    this.isDragging = false
    this.dragStartX = 0
    this.dragStartY = 0
    this.translateX = 0
    this.translateY = 0
    
    // 事件回调
    this.callbacks = {
      onSwipeLeft: null,
      onSwipeRight: null,
      onSwipeUp: null,
      onSwipeDown: null,
      onScale: null,
      onDrag: null,
      onDoubleTap: null,
      onSingleTap: null
    }
    
    // 绑定方法上下文
    this.handleTouchStart = this.handleTouchStart.bind(this)
    this.handleTouchMove = this.handleTouchMove.bind(this)
    this.handleTouchEnd = this.handleTouchEnd.bind(this)
  }
  
  /**
   * 激活手势管理器
   */
  activate() {
    this.isActive = true
  }
  
  /**
   * 停用手势管理器
   */
  deactivate() {
    this.isActive = false
    this.reset()
  }
  
  /**
   * 重置所有状态
   */
  reset() {
    this.touches = []
    this.currentScale = 1
    this.translateX = 0
    this.translateY = 0
    this.isDragging = false
    this.tapCount = 0
  }
  
  /**
   * 设置回调函数
   */
  on(event, callback) {
    const eventName = `on${this.capitalizeFirst(event)}`
    if (Object.prototype.hasOwnProperty.call(this.callbacks, eventName)) {
      this.callbacks[eventName] = callback
    }
  }
  
  /**
   * 设置缩放值
   */
  setScale(scale) {
    this.currentScale = Math.max(this.options.minScale, Math.min(this.options.maxScale, scale))
    return this.currentScale
  }
  
  /**
   * 设置平移值
   */
  setTranslate(x, y) {
    this.translateX = x
    this.translateY = y
  }
  
  /**
   * 获取当前变换状态
   */
  getTransform() {
    return {
      scale: this.currentScale,
      translateX: this.translateX,
      translateY: this.translateY
    }
  }
  
  /**
   * 处理触摸开始事件
   */
  handleTouchStart(e) {
    if (!this.isActive) return
    
    e.preventDefault()
    this.touches = Array.from(e.touches)
    
    const now = Date.now()
    const timeSinceLastTap = now - this.lastTouchTime
    
    if (this.touches.length === 1) {
      // 单指触摸
      const touch = this.touches[0]
      this.startX = touch.clientX
      this.startY = touch.clientY
      this.currentX = touch.clientX
      this.currentY = touch.clientY
      
      // 检测双击
      if (timeSinceLastTap < this.options.doubleTapThreshold) {
        this.tapCount++
        if (this.tapCount === 2) {
          this.handleDoubleTap(touch)
          this.tapCount = 0
          return
        }
      } else {
        this.tapCount = 1
      }
      
      this.lastTouchTime = now
      
      // 如果图片已缩放，准备拖拽
      if (this.currentScale > 1) {
        this.isDragging = true
        this.dragStartX = touch.clientX
        this.dragStartY = touch.clientY
      }
    } else if (this.touches.length === 2) {
      // 双指触摸，准备缩放
      this.initialDistance = this.getDistance(this.touches[0], this.touches[1])
      this.initialScale = this.currentScale
      this.isDragging = false
    }
  }
  
  /**
   * 处理触摸移动事件
   */
  handleTouchMove(e) {
    if (!this.isActive) return
    
    e.preventDefault()
    this.touches = Array.from(e.touches)
    
    if (this.touches.length === 1) {
      // 单指移动
      const touch = this.touches[0]
      this.currentX = touch.clientX
      this.currentY = touch.clientY
      
      if (this.isDragging && this.currentScale > 1) {
        // 拖拽图片
        const deltaX = touch.clientX - this.dragStartX
        const deltaY = touch.clientY - this.dragStartY
        
        this.translateX += deltaX
        this.translateY += deltaY
        
        this.dragStartX = touch.clientX
        this.dragStartY = touch.clientY
        
        this.triggerCallback('onDrag', {
          translateX: this.translateX,
          translateY: this.translateY
        })
      }
    } else if (this.touches.length === 2) {
      // 双指缩放
      const currentDistance = this.getDistance(this.touches[0], this.touches[1])
      const scaleChange = currentDistance / this.initialDistance
      const newScale = this.initialScale * scaleChange
      
      this.setScale(newScale)
      
      // 如果缩放到1或以下，重置平移
      if (this.currentScale <= 1) {
        this.translateX = 0
        this.translateY = 0
      }
      
      this.triggerCallback('onScale', {
        scale: this.currentScale,
        translateX: this.translateX,
        translateY: this.translateY
      })
    }
  }
  
  /**
   * 处理触摸结束事件
   */
  handleTouchEnd(e) {
    if (!this.isActive) return
    
    const remainingTouches = Array.from(e.touches)
    
    if (remainingTouches.length === 0) {
      // 所有手指离开
      if (this.touches.length === 1) {
        // 单指滑动检测
        if (!this.isDragging || this.currentScale <= 1) {
          this.handleSwipe()
        }
        
        // 单击检测
        const deltaX = Math.abs(this.currentX - this.startX)
        const deltaY = Math.abs(this.currentY - this.startY)
        
        if (deltaX < 10 && deltaY < 10 && this.tapCount === 1) {
          setTimeout(() => {
            if (this.tapCount === 1) {
              this.triggerCallback('onSingleTap', {
                x: this.currentX,
                y: this.currentY
              })
              this.tapCount = 0
            }
          }, this.options.doubleTapThreshold)
        }
      }
      
      this.isDragging = false
      this.touches = []
    } else if (remainingTouches.length === 1 && this.touches.length === 2) {
      // 从双指变为单指，可能开始拖拽
      if (this.currentScale > 1) {
        this.isDragging = true
        this.dragStartX = remainingTouches[0].clientX
        this.dragStartY = remainingTouches[0].clientY
      }
      this.touches = remainingTouches
    }
  }
  
  /**
   * 处理滑动手势
   */
  handleSwipe() {
    const deltaX = this.currentX - this.startX
    const deltaY = this.currentY - this.startY
    const absDeltaX = Math.abs(deltaX)
    const absDeltaY = Math.abs(deltaY)
    
    // 检查是否为有效滑动
    if (absDeltaX > this.options.swipeThreshold && 
        absDeltaX > absDeltaY && 
        absDeltaY < this.options.maxVerticalDistance) {
      
      if (deltaX > 0) {
        this.triggerCallback('onSwipeRight', { deltaX, deltaY })
      } else {
        this.triggerCallback('onSwipeLeft', { deltaX, deltaY })
      }
    } else if (absDeltaY > this.options.swipeThreshold && 
               absDeltaY > absDeltaX) {
      
      if (deltaY > 0) {
        this.triggerCallback('onSwipeDown', { deltaX, deltaY })
      } else {
        this.triggerCallback('onSwipeUp', { deltaX, deltaY })
      }
    }
  }
  
  /**
   * 处理双击
   */
  handleDoubleTap(touch) {
    this.triggerCallback('onDoubleTap', {
      x: touch.clientX,
      y: touch.clientY,
      currentScale: this.currentScale
    })
  }
  
  /**
   * 计算两点间距离
   */
  getDistance(touch1, touch2) {
    const dx = touch2.clientX - touch1.clientX
    const dy = touch2.clientY - touch1.clientY
    return Math.sqrt(dx * dx + dy * dy)
  }
  
  /**
   * 触发回调函数
   */
  triggerCallback(name, data) {
    if (this.callbacks[name] && typeof this.callbacks[name] === 'function') {
      this.callbacks[name](data)
    }
  }
  
  /**
   * 首字母大写
   */
  capitalizeFirst(str) {
    return str.charAt(0).toUpperCase() + str.slice(1)
  }
}

/**
 * 创建图片查看器手势管理器的便捷函数
 */
export function createPhotoViewerGesture(options = {}) {
  return new TouchGestureManager({
    minScale: 0.5,
    maxScale: 3,
    swipeThreshold: 50,
    maxVerticalDistance: 100,
    doubleTapThreshold: 300,
    ...options
  })
}
