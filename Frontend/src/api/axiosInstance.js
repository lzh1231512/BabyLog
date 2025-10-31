import axios from 'axios'

// 创建 axios 实例
const axiosInstance = axios.create({
  // 可设置默认 baseURL
  // baseURL: '你的默认后端地址',
  // 可设置默认 headers
  // headers: { 'Authorization': 'Bearer xxx' }
})

// 可添加请求/响应拦截器
axiosInstance.interceptors.request.use(config => {
  // 在这里统一设置 header，比如 token
  // config.headers['Authorization'] = 'Bearer ' + localStorage.getItem('token')
  config.headers['APIKEY']= localStorage.getItem('APIKEY') || ''
  return config
})

export default axiosInstance