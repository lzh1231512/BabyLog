/**
 * 图片格式检测和优化工具
 */

// 检测浏览器支持的图片格式
class ImageFormatDetector {
  constructor() {
    this.supportedFormats = new Map()
    this.checkFormats()
  }

  // 检测支持的格式
  async checkFormats() {
    const formats = [
      { name: 'webp', data: 'data:image/webp;base64,UklGRiIAAABXRUJQVlA4IBYAAAAwAQCdASoBAAEADsD+JaQAA3AAAAAA' },
      { name: 'avif', data: 'data:image/avif;base64,AAAAIGZ0eXBhdmlmAAAAAGF2aWZtaWYxbWlhZk1BMUIAAADybWV0YQAAAAAAAAAoaGRscgAAAAAAAAAAcGljdAAAAAAAAAAAAAAAAGxpYmF2aWYAAAAADnBpdG0AAAAAAAEAAAAeaWxvYwAAAABEAAABAAEAAAABAAABGgAAAB0AAAAoaWluZgAAAAAAAQAAABppbmZlAgAAAAABAABhdjAxQ29sb3IAAAAAamlwcnAAAABLaXBjbwAAABRpc3BlAAAAAAAAAAIAAAACAAAAEHBpeGkAAAAAAwgICAAAAAxhdjFDgQ0MAAAAABNjb2xybmNseAACAAIAAYAAAAAXaXBtYQAAAAAAAAABAAEEAQKDBAAAACVtZGF0EgAKCBgABogQEAwgMg8f8D///8WfhwB8+ErK42A=' },
      { name: 'jpeg2000', data: 'data:image/jp2;base64,/0//UQAyAAAAAAABAAAAAgAAAAAAAAAAAAAABAAAAAQAAAAAAAAAAAAEBwEBBwEBBwEBBwEB' }
    ]

    for (const format of formats) {
      try {
        const supported = await this.testFormat(format.data)
        this.supportedFormats.set(format.name, supported)
      } catch (error) {
        this.supportedFormats.set(format.name, false)
      }
    }
  }

  // 测试单个格式
  testFormat(dataUrl) {
    return new Promise((resolve) => {
      const img = new Image()
      img.onload = () => resolve(img.width === 1 && img.height === 1)
      img.onerror = () => resolve(false)
      img.src = dataUrl
    })
  }

  // 获取最佳图片格式
  getBestFormat(originalUrl) {
    const url = new URL(originalUrl, window.location.origin)
    const pathname = url.pathname.toLowerCase()
    
    // 如果已经是现代格式，直接返回
    if (pathname.includes('.webp') || pathname.includes('.avif')) {
      return originalUrl
    }

    // 根据支持情况返回最佳格式
    if (this.supportedFormats.get('avif')) {
      return this.convertUrlFormat(originalUrl, 'avif')
    } else if (this.supportedFormats.get('webp')) {
      return this.convertUrlFormat(originalUrl, 'webp')
    }

    return originalUrl
  }

  // 转换URL格式（如果后端支持）
  convertUrlFormat(originalUrl, format) {
    // 这里可以根据实际的API来修改URL以请求不同格式
    // 例如：添加格式参数
    const url = new URL(originalUrl, window.location.origin)
    url.searchParams.set('format', format)
    return url.toString()
  }

  // 检查是否支持某种格式
  supports(format) {
    return this.supportedFormats.get(format) || false
  }
}

// 全局格式检测器实例
export const imageFormatDetector = new ImageFormatDetector()

/**
 * 响应式图片尺寸工具
 */
export class ResponsiveImageHelper {
  constructor() {
    this.breakpoints = {
      xs: 480,
      sm: 768,
      md: 1024,
      lg: 1200,
      xl: 1440
    }
  }

  // 获取当前设备的断点
  getCurrentBreakpoint() {
    const width = window.innerWidth
    
    if (width < this.breakpoints.xs) return 'xs'
    if (width < this.breakpoints.sm) return 'sm'
    if (width < this.breakpoints.md) return 'md'
    if (width < this.breakpoints.lg) return 'lg'
    return 'xl'
  }

  // 获取建议的图片尺寸
  getSuggestedSize(containerWidth, containerHeight) {
    const dpr = window.devicePixelRatio || 1
    const breakpoint = this.getCurrentBreakpoint()
    
    // 根据断点调整尺寸
    let multiplier = 1
    switch (breakpoint) {
      case 'xs':
        multiplier = 0.8
        break
      case 'sm':
        multiplier = 0.9
        break
      case 'md':
        multiplier = 1.0
        break
      case 'lg':
        multiplier = 1.1
        break
      case 'xl':
        multiplier = 1.2
        break
    }

    return {
      width: Math.ceil(containerWidth * dpr * multiplier),
      height: Math.ceil(containerHeight * dpr * multiplier),
      dpr,
      breakpoint
    }
  }

  // 生成srcset属性
  generateSrcSet(baseUrl, sizes = [1, 1.5, 2, 3]) {
    return sizes.map(size => {
      const url = this.addSizeParams(baseUrl, size)
      return `${url} ${size}x`
    }).join(', ')
  }

  // 添加尺寸参数到URL
  addSizeParams(baseUrl, scale = 1) {
    const url = new URL(baseUrl, window.location.origin)
    url.searchParams.set('scale', scale.toString())
    return url.toString()
  }
}

// 全局响应式图片助手实例
export const responsiveImageHelper = new ResponsiveImageHelper()

/**
 * 图片压缩质量管理器
 */
export class ImageQualityManager {
  constructor() {
    this.qualitySettings = {
      high: { quality: 90, maxWidth: 2048, maxHeight: 2048 },
      medium: { quality: 75, maxWidth: 1024, maxHeight: 1024 },
      low: { quality: 60, maxWidth: 512, maxHeight: 512 }
    }
  }

  // 根据网络条件获取建议质量
  getRecommendedQuality() {
    if ('connection' in navigator) {
      const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection
      if (connection) {
        switch (connection.effectiveType) {
          case 'slow-2g':
          case '2g':
            return 'low'
          case '3g':
            return 'medium'
          case '4g':
          default:
            return 'high'
        }
      }
    }

    // 根据设备类型默认设置
    const isMobile = /Mobi|Android/i.test(navigator.userAgent)
    return isMobile ? 'medium' : 'high'
  }

  // 应用质量设置到URL
  applyQualityToUrl(baseUrl, quality = null) {
    if (!quality) {
      quality = this.getRecommendedQuality()
    }

    const settings = this.qualitySettings[quality]
    if (!settings) return baseUrl

    const url = new URL(baseUrl, window.location.origin)
    url.searchParams.set('q', settings.quality.toString())
    url.searchParams.set('maxw', settings.maxWidth.toString())
    url.searchParams.set('maxh', settings.maxHeight.toString())
    
    return url.toString()
  }
}

// 全局质量管理器实例
export const imageQualityManager = new ImageQualityManager()

/**
 * 图片URL优化器
 */
export const optimizeImageUrl = (originalUrl, options = {}) => {
  let optimizedUrl = originalUrl

  // 应用格式优化
  if (options.formatOptimization !== false) {
    optimizedUrl = imageFormatDetector.getBestFormat(optimizedUrl)
  }

  // 应用质量优化
  if (options.quality || options.qualityOptimization !== false) {
    optimizedUrl = imageQualityManager.applyQualityToUrl(optimizedUrl, options.quality)
  }

  // 应用尺寸优化
  if (options.width && options.height) {
    const url = new URL(optimizedUrl, window.location.origin)
    url.searchParams.set('w', options.width.toString())
    url.searchParams.set('h', options.height.toString())
    optimizedUrl = url.toString()
  }

  return optimizedUrl
}
