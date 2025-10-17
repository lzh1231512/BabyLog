// 事件相关的API接口
import axios from 'axios'
import config from '../config'
// API配置
const API_CONFIG = {
  baseURL: config.API_BASE_URL,
  timeout: 10000,
}

// 创建axios实例
const apiClient = axios.create(API_CONFIG)

// 请求拦截器
apiClient.interceptors.request.use(
  config => {
    // 可以在这里添加认证token等
    return config
  },
  error => {
    return Promise.reject(error)
  }
)

// 响应拦截器
apiClient.interceptors.response.use(
  response => {
    return response
  },
  error => {
    console.error('API请求错误:', error)
    return Promise.reject(error)
  }
)

// API接口函数

/**
 * 获取所有事件列表（时间线格式）
 */
export const getEventsList = async () => {
  try {
    const response = await apiClient.get('/api/Events')
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '获取事件列表失败'
    }
  }
}

/**
 * 根据ID获取单个事件详情
 */
export const getEventById = async (id) => {
  try {
    const response = await apiClient.get(`/api/Events/${id}`)
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    if (error.response?.status === 404) {
      return {
        success: false,
        data: null,
        message: '事件不存在'
      }
    }
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '获取事件详情失败'
    }
  }
}

export const getVideoRotation = async (id, fileName) => {
  try {
    const response = await apiClient.get(`/api/files/getVideoRotation?id=${id}&fileName=${encodeURIComponent(fileName)}`)
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    if (error.response?.status === 404) {
      return {
        success: false,
        data: null,
        message: '事件不存在'
      }
    }
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '获取视频旋转角度失败'
    }
  }
}

/**
 * 创建新事件
 */
export const createEvent = async (eventData) => {
  try {
    const response = await apiClient.post('/api/Events', eventData)
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '创建事件失败'
    }
  }
}

/**
 * 更新事件
 */
export const updateEvent = async (id, eventData) => {
  try {
    const response = await apiClient.post(`/api/Events/update/${id}`, eventData)
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    if (error.response?.status === 404) {
      return {
        success: false,
        data: null,
        message: '事件不存在'
      }
    }
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '更新事件失败'
    }
  }
}

/**
 * 删除事件
 */
export const deleteEvent = async (id) => {
  try {
    const response = await apiClient.post(`/api/Events/delete/${id}`)
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    if (error.response?.status === 404) {
      return {
        success: false,
        data: null,
        message: '事件不存在'
      }
    }
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '删除事件失败'
    }
  }
}

/**
 * 上传单个文件
 */
export const uploadFile = async (file) => {
  try {
    const formData = new FormData()
    formData.append('file', file)
    
    const response = await apiClient.post('/api/Files/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '文件上传失败'
    }
  }
}

/**
 * 批量上传文件
 */
export const uploadFiles = async (files) => {
  try {
    const formData = new FormData()
    files.forEach(file => {
      formData.append('files', file)
    })
    
    const response = await apiClient.post('/api/Files/upload-multiple', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '批量上传失败'
    }
  }
}

/**
 * 获取媒体文件下载URL
 */
export const getMediaUrl = (id, fileName, thumbnail) => {
  if(!thumbnail)
    thumbnail = false
  const baseURL = config.API_BASE_URL
  return `${baseURL}/api/Files/download?id=${encodeURIComponent(id)}&fileName=${encodeURIComponent(fileName)}&thumbnail=${thumbnail}`
}


export const getVideoUrl = (id, fileName) => {
  const baseURL = config.API_BASE_URL
  return `${baseURL}/api/Files/downloadVideo?id=${encodeURIComponent(id)}&fileName=${encodeURIComponent(fileName)}`
}

/**
 * 获取统计数据
 */
export const getStats = async () => {
  try {
    // 如果后端有专门的统计接口，可以调用
    // const response = await apiClient.get('/api/Stats')
    // return { success: true, data: response.data, message: '获取统计数据成功' }
    
    // 临时方案：通过获取所有事件来计算统计数据
    const eventsResponse = await getEventsList()
    if (!eventsResponse.success) {
      return {
        success: false,
        data: null,
        message: '获取统计数据失败'
      }
    }
    
    const allEvents = eventsResponse.data.flatMap(group => group.events)
    const totalEvents = allEvents.length
    const totalPhotos = allEvents.reduce((total, event) => 
      total + (event.media?.images?.length || 0), 0)
    const totalVideos = allEvents.reduce((total, event) => 
      total + (event.media?.videos?.length || 0), 0)
    const totalAudios = allEvents.reduce((total, event) => 
      total + (event.media?.audios?.length || 0), 0)
    
    return {
      success: true,
      data: {
        totalEvents,
        totalPhotos,
        totalVideos,
        totalAudios
      },
      message: '获取统计数据成功'
    }
  } catch (error) {
    return {
      success: false,
      data: null,
      message: '获取统计数据失败'
    }
  }
}


/**
 * 初始化分块上传
 * @param {Object} request - 请求参数
 * 请求参数示例（C#）：
 * public class InitChunkUploadRequest
 * {
 *   public string FileName { get; set; }
 *   public string FileType { get; set; }
 *   public long FileSize { get; set; }
 *   public int ChunkCount { get; set; }
 *   public string FileMD5 { get; set; }                  // 客户端提供的文件MD5
 * }
     * @returns {Promise<Object>} - API响应
     * 响应示例：（C#）
 * public class InitChunkUploadResponse
 * {
 *   public string TaskId { get; set; }
 * }
 */
export const initChunk = async (request) => {
  try {
    const response = await apiClient.post('/api/Chunk/init', request)
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '创建事件失败'
    }
  }
}

/** 上传分块
 * @param {string} taskId - 任务ID
 * @param {number} chunkIndex - 分块索引
 * @param {File} file - 分块文件
 * @returns {Promise<Object>} - API响应
 * 响应示例：（C#）
 * public class UploadChunkResponse
 * {
 *   public int ChunkIndex { get; set; }
 *   public int CompletedChunks { get; set; }
 *   public int TotalChunks { get; set; }
 * }
 */
export const unloadChunk = async (taskId, chunkIndex, file) => {
  try {
    const formData = new FormData()
    formData.append('file', file)

    const response = await apiClient.post(`/api/Chunk/unload?taskId=${encodeURIComponent(taskId)}&chunkIndex=${chunkIndex}`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '创建事件失败'
    }
  }
}

/** 完成分块上传
 * @param {string} taskId - 任务ID
 * @returns {Promise<Object>} - API响应
 * 响应示例：（C#）
 * public class CompleteChunkUploadResponse
 * {
 *   public string OriginalName { get; set; }
 *   public string ServerFileName { get; set;
 *   public long Size { get; set; }
 *   public string MD5 { get; set; }
 *   public bool MD5Verified { get; set; }
 *   public string ExpectedMD5 { get; set; }
 * }
 */
export const completeChunk = async (taskId) => {
  try {
    const response = await apiClient.post(`/api/Chunk/complete?taskId=${encodeURIComponent(taskId)}`)
    // 直接返回API响应，因为后端已经是标准格式
    return response.data
  } catch (error) {
    return {
      success: false,
      data: null,
      message: error.response?.data?.message || '创建事件失败'
    }
  }
}