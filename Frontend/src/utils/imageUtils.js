/**
 * 图片懒加载相关工具函数
 */

// 图片缓存管理器
class ImageCacheManager {
  constructor() {
    this.cache = new Map()
    this.loadingQueue = new Set()
    this.maxCacheSize = 50 // 最大缓存图片数量
  }

  // 预加载图片
  preloadImage(src) {
    return new Promise((resolve, reject) => {
      if (this.cache.has(src)) {
        resolve(this.cache.get(src))
        return
      }

      if (this.loadingQueue.has(src)) {
        // 如果正在加载，等待加载完成
        const checkLoading = () => {
          if (this.cache.has(src)) {
            resolve(this.cache.get(src))
          } else if (!this.loadingQueue.has(src)) {
            reject(new Error('Image load failed'))
          } else {
            setTimeout(checkLoading, 100)
          }
        }
        checkLoading()
        return
      }

      this.loadingQueue.add(src)

      const img = new Image()
      img.onload = () => {
        this.loadingQueue.delete(src)
        this.addToCache(src, img)
        resolve(img)
      }
      img.onerror = () => {
        this.loadingQueue.delete(src)
        reject(new Error('Image load failed'))
      }
      img.src = src
    })
  }

  // 添加到缓存
  addToCache(src, img) {
    if (this.cache.size >= this.maxCacheSize) {
      // 删除最旧的缓存
      const firstKey = this.cache.keys().next().value
      this.cache.delete(firstKey)
    }
    this.cache.set(src, img)
  }

  // 检查图片是否已缓存
  isCached(src) {
    return this.cache.has(src)
  }

  // 清理缓存
  clearCache() {
    this.cache.clear()
    this.loadingQueue.clear()
  }

  // 获取缓存状态
  getCacheStats() {
    return {
      cacheSize: this.cache.size,
      loadingCount: this.loadingQueue.size,
      maxCacheSize: this.maxCacheSize
    }
  }
}

// 全局图片缓存管理器实例
export const imageCacheManager = new ImageCacheManager()

/**
 * 预加载事件中的关键图片
 * @param {Array} events 事件列表
 * @param {number} priorityCount 优先加载的图片数量
 * @param {Function} getMediaUrl 获取媒体URL的函数
 */
export const preloadEventImages = (events, priorityCount = 10, getMediaUrl) => {
  if (!getMediaUrl) {
    console.warn('getMediaUrl函数未提供，跳过图片预加载')
    return
  }
  const imagesToPreload = []
  
  // 收集前几个事件的第一张图片
  events.slice(0, Math.ceil(priorityCount / 2)).forEach(event => {
    if (event.media?.images?.length > 0) {
      const firstImage = event.media.images[0]
      if (firstImage?.fileName) {
        imagesToPreload.push({
          src: getMediaUrl(event.id, firstImage.fileName,true),
          priority: 'high'
        })
      }
    }
  })

  // 收集其他图片
  events.forEach(event => {
    if (event.media?.images?.length > 1) {
      event.media.images.slice(1, 3).forEach(image => {
        if (image?.fileName && imagesToPreload.length < priorityCount) {
          imagesToPreload.push({
            src: getMediaUrl(event.id, image.fileName,true),
            priority: 'low'
          })
        }
      })
    }
  })

  // 按优先级预加载
  const highPriorityImages = imagesToPreload.filter(img => img.priority === 'high')
  const lowPriorityImages = imagesToPreload.filter(img => img.priority === 'low')

  // 立即开始加载高优先级图片
  highPriorityImages.forEach(img => {
    imageCacheManager.preloadImage(img.src).catch(err => {
      console.warn('预加载图片失败:', img.src, err)
    })
  })

  // 延迟加载低优先级图片
  setTimeout(() => {
    lowPriorityImages.forEach(img => {
      imageCacheManager.preloadImage(img.src).catch(err => {
        console.warn('预加载图片失败:', img.src, err)
      })
    })
  }, 1000)
}

/**
 * 获取图片的最优尺寸
 * @param {HTMLElement} container 容器元素
 * @returns {Object} 包含width和height的对象
 */
export const getOptimalImageSize = (container) => {
  if (!container) return { width: 300, height: 200 }
  
  const rect = container.getBoundingClientRect()
  const dpr = window.devicePixelRatio || 1
  
  return {
    width: Math.ceil(rect.width * dpr),
    height: Math.ceil(rect.height * dpr)
  }
}

/**
 * 生成带尺寸参数的图片URL（如果API支持）
 * @param {string} originalUrl 原始图片URL
 * @param {number} width 目标宽度
 * @param {number} height 目标高度
 * @returns {string} 优化后的图片URL
 */
export const getOptimizedImageUrl = (originalUrl, width, height) => {
  // 如果后端支持图片尺寸优化，可以在这里添加参数
  // 例如: return `${originalUrl}&w=${width}&h=${height}&q=80`
  // 暂时返回原始URL，保留参数以备后用
  console.debug('Image optimization parameters:', { width, height })
  return originalUrl
}

// getMediaUrl函数将作为参数传入，避免循环依赖

/**
 * 检查图片是否在可视区域附近
 * @param {HTMLElement} element 要检查的元素
 * @param {number} threshold 阈值（像素）
 * @returns {boolean} 是否在可视区域附近
 */
export const isNearViewport = (element, threshold = 200) => {
  if (!element) return false
  
  const rect = element.getBoundingClientRect()
  const windowHeight = window.innerHeight || document.documentElement.clientHeight
  const windowWidth = window.innerWidth || document.documentElement.clientWidth
  
  return (
    rect.bottom >= -threshold &&
    rect.top <= windowHeight + threshold &&
    rect.right >= -threshold &&
    rect.left <= windowWidth + threshold
  )
}
