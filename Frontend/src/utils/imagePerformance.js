/**
 * 图片加载性能监控工具
 */

class ImageLoadingPerformanceMonitor {
  constructor() {
    this.loadingStats = {
      totalImages: 0,
      loadedImages: 0,
      failedImages: 0,
      averageLoadTime: 0,
      loadTimes: []
    }
    this.loadingEvents = []
    this.isEnabled = process.env.NODE_ENV === 'development'
  }

  // 开始监控图片加载
  startMonitoring(src) {
    if (!this.isEnabled) return null

    const startTime = performance.now()
    const eventId = `${src}-${startTime}`
    
    this.loadingEvents.push({
      id: eventId,
      src,
      startTime,
      endTime: null,
      success: null
    })

    this.loadingStats.totalImages++
    return eventId
  }

  // 结束监控（成功）
  endMonitoring(eventId, success = true) {
    if (!this.isEnabled || !eventId) return

    const event = this.loadingEvents.find(e => e.id === eventId)
    if (!event) return

    const endTime = performance.now()
    const loadTime = endTime - event.startTime

    event.endTime = endTime
    event.success = success

    if (success) {
      this.loadingStats.loadedImages++
      this.loadingStats.loadTimes.push(loadTime)
      this.updateAverageLoadTime()
    } else {
      this.loadingStats.failedImages++
    }

    // 在开发环境下输出性能信息
    if (this.isEnabled) {
      console.debug(`图片加载${success ? '成功' : '失败'}:`, {
        src: event.src,
        loadTime: `${loadTime.toFixed(2)}ms`,
        stats: this.getStats()
      })
    }
  }

  // 更新平均加载时间
  updateAverageLoadTime() {
    if (this.loadingStats.loadTimes.length > 0) {
      const total = this.loadingStats.loadTimes.reduce((sum, time) => sum + time, 0)
      this.loadingStats.averageLoadTime = total / this.loadingStats.loadTimes.length
    }
  }

  // 获取统计信息
  getStats() {
    const successRate = this.loadingStats.totalImages > 0 
      ? (this.loadingStats.loadedImages / this.loadingStats.totalImages * 100).toFixed(1)
      : 0

    return {
      ...this.loadingStats,
      successRate: `${successRate}%`,
      averageLoadTime: `${this.loadingStats.averageLoadTime.toFixed(2)}ms`
    }
  }

  // 获取性能建议
  getPerformanceRecommendations() {
    const stats = this.getStats()
    const recommendations = []

    if (parseFloat(stats.successRate) < 90) {
      recommendations.push('图片加载成功率较低，请检查网络连接或图片URL')
    }

    if (this.loadingStats.averageLoadTime > 2000) {
      recommendations.push('图片加载时间较长，建议优化图片尺寸或使用CDN')
    }

    if (this.loadingStats.failedImages > 5) {
      recommendations.push('失败的图片数量较多，请检查图片资源的可用性')
    }

    const slowImages = this.loadingEvents.filter(e => 
      e.success && (e.endTime - e.startTime) > 3000
    )
    if (slowImages.length > 0) {
      recommendations.push(`发现${slowImages.length}张加载较慢的图片，建议优化这些图片`)
    }

    return recommendations
  }

  // 重置统计信息
  reset() {
    this.loadingStats = {
      totalImages: 0,
      loadedImages: 0,
      failedImages: 0,
      averageLoadTime: 0,
      loadTimes: []
    }
    this.loadingEvents = []
  }

  // 导出性能报告
  exportReport() {
    return {
      timestamp: new Date().toISOString(),
      stats: this.getStats(),
      recommendations: this.getPerformanceRecommendations(),
      events: this.loadingEvents.slice(-20) // 只导出最近20个事件
    }
  }
}

// 全局性能监控实例
export const imagePerformanceMonitor = new ImageLoadingPerformanceMonitor()

/**
 * 自适应图片质量加载策略
 */
export class AdaptiveImageLoader {
  constructor() {
    this.networkSpeed = 'fast' // fast, normal, slow
    this.deviceType = this.detectDeviceType()
    this.connectionType = this.detectConnectionType()
  }

  // 检测设备类型
  detectDeviceType() {
    const userAgent = navigator.userAgent.toLowerCase()
    if (/mobile|android|iphone|ipad|tablet/.test(userAgent)) {
      return 'mobile'
    }
    return 'desktop'
  }

  // 检测网络连接类型
  detectConnectionType() {
    if ('connection' in navigator) {
      const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection
      if (connection) {
        const effectiveType = connection.effectiveType
        switch (effectiveType) {
          case 'slow-2g':
          case '2g':
            return 'slow'
          case '3g':
            return 'medium'
          case '4g':
          default:
            return 'fast'
        }
      }
    }
    return 'fast'
  }

  // 获取推荐的图片质量
  getRecommendedQuality() {
    if (this.connectionType === 'slow' || this.deviceType === 'mobile') {
      return 'low'
    } else if (this.connectionType === 'medium') {
      return 'medium'
    }
    return 'high'
  }

  // 获取推荐的加载策略
  getLoadingStrategy() {
    const quality = this.getRecommendedQuality()
    
    return {
      quality,
      threshold: quality === 'low' ? 100 : quality === 'medium' ? 200 : 300,
      preloadCount: quality === 'low' ? 3 : quality === 'medium' ? 6 : 10,
      enablePreload: quality !== 'low'
    }
  }
}

// 全局自适应加载器实例
export const adaptiveImageLoader = new AdaptiveImageLoader()

/**
 * 图片加载错误处理器
 */
export const handleImageLoadError = (src, retryCount = 0, maxRetries = 2) => {
  return new Promise((resolve, reject) => {
    const img = new Image()
    
    img.onload = () => resolve(img)
    img.onerror = () => {
      if (retryCount < maxRetries) {
        console.warn(`图片加载失败，正在重试 (${retryCount + 1}/${maxRetries}):`, src)
        // 递归重试
        setTimeout(() => {
          handleImageLoadError(src, retryCount + 1, maxRetries)
            .then(resolve)
            .catch(reject)
        }, 1000 * (retryCount + 1)) // 递增延迟
      } else {
        console.error('图片加载失败，已达到最大重试次数:', src)
        reject(new Error(`Failed to load image after ${maxRetries} retries: ${src}`))
      }
    }
    
    img.src = src
  })
}
