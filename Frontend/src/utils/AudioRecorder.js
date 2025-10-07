/**
 * 音频录制工具类
 */
export class AudioRecorder {
  constructor() {
    this.mediaRecorder = null
    this.audioChunks = []
    this.isRecording = false
    this.stream = null
    this.onDataAvailable = null
    this.onStop = null
    this.onError = null
    this.startTime = null
    this.recordingTimer = null
    this.onTimeUpdate = null
  }

  /**
   * 检查浏览器是否支持录音
   */
  static isSupported() {
    return !!(navigator.mediaDevices && 
              navigator.mediaDevices.getUserMedia && 
              window.MediaRecorder)
  }

  /**
   * 开始录音
   */
  async startRecording() {
    try {
      if (this.isRecording) {
        throw new Error('录音已在进行中')
      }

      if (!AudioRecorder.isSupported()) {
        throw new Error('当前浏览器不支持录音功能')
      }

      // 获取麦克风权限
      this.stream = await navigator.mediaDevices.getUserMedia({ 
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        } 
      })

      // 创建MediaRecorder实例
      const options = { mimeType: this.getSupportedMimeType() }
      this.mediaRecorder = new MediaRecorder(this.stream, options)
      
      // 重置音频数据
      this.audioChunks = []
      
      // 设置事件监听
      this.mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          this.audioChunks.push(event.data)
        }
        if (this.onDataAvailable) {
          this.onDataAvailable(event)
        }
      }

      this.mediaRecorder.onstop = () => {
        this.isRecording = false
        this.stopTimer()
        
        // 创建音频文件
        const audioBlob = new Blob(this.audioChunks, { 
          type: this.getSupportedMimeType() 
        })
        
        if (this.onStop) {
          this.onStop(audioBlob, this.getRecordingDuration())
        }
        
        // 清理资源
        this.cleanup()
      }

      this.mediaRecorder.onerror = (event) => {
        console.error('录音错误:', event.error)
        this.isRecording = false
        this.stopTimer()
        this.cleanup()
        
        if (this.onError) {
          this.onError(event.error)
        }
      }

      // 开始录音
      this.mediaRecorder.start(1000) // 每秒触发一次dataavailable事件
      this.isRecording = true
      this.startTime = Date.now()
      this.startTimer()
      
      return true
    } catch (error) {
      console.error('开始录音失败:', error)
      this.cleanup()
      throw error
    }
  }

  /**
   * 停止录音
   */
  stopRecording() {
    if (!this.isRecording || !this.mediaRecorder) {
      return false
    }

    try {
      this.mediaRecorder.stop()
      return true
    } catch (error) {
      console.error('停止录音失败:', error)
      this.cleanup()
      return false
    }
  }

  /**
   * 取消录音
   */
  cancelRecording() {
    if (!this.isRecording) {
      return false
    }

    try {
      this.isRecording = false
      this.stopTimer()
      
      if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
        this.mediaRecorder.stop()
      }
      
      this.cleanup()
      return true
    } catch (error) {
      console.error('取消录音失败:', error)
      this.cleanup()
      return false
    }
  }

  /**
   * 获取录音时长（秒）
   */
  getRecordingDuration() {
    if (!this.startTime) return 0
    return Math.floor((Date.now() - this.startTime) / 1000)
  }

  /**
   * 获取支持的MIME类型
   */
  getSupportedMimeType() {
    const types = [
      'audio/webm;codecs=opus',
      'audio/webm',
      'audio/ogg;codecs=opus',
      'audio/ogg',
      'audio/wav',
      'audio/mp4',
      'audio/mpeg'
    ]

    for (const type of types) {
      if (MediaRecorder.isTypeSupported(type)) {
        return type
      }
    }

    return 'audio/webm' // 默认类型
  }

  /**
   * 获取文件扩展名
   */
  getFileExtension() {
    const mimeType = this.getSupportedMimeType()
    
    if (mimeType.includes('webm')) return 'webm'
    if (mimeType.includes('ogg')) return 'ogg'
    if (mimeType.includes('wav')) return 'wav'
    if (mimeType.includes('mp4')) return 'm4a'
    if (mimeType.includes('mpeg')) return 'mp3'
    
    return 'webm'
  }

  /**
   * 开始计时器
   */
  startTimer() {
    this.recordingTimer = setInterval(() => {
      if (this.onTimeUpdate) {
        this.onTimeUpdate(this.getRecordingDuration())
      }
    }, 1000)
  }

  /**
   * 停止计时器
   */
  stopTimer() {
    if (this.recordingTimer) {
      clearInterval(this.recordingTimer)
      this.recordingTimer = null
    }
  }

  /**
   * 清理资源
   */
  cleanup() {
    this.stopTimer()
    
    if (this.stream) {
      this.stream.getTracks().forEach(track => track.stop())
      this.stream = null
    }
    
    this.mediaRecorder = null
    this.audioChunks = []
    this.startTime = null
  }

  /**
   * 销毁实例
   */
  destroy() {
    this.cancelRecording()
    this.onDataAvailable = null
    this.onStop = null
    this.onError = null
    this.onTimeUpdate = null
  }
}

export default AudioRecorder
