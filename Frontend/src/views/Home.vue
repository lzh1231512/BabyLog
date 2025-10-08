<template>
  <div class="home">
    <!-- 头部信息 -->
    <header class="header">
      <div class="baby-info">
        <div class="avatar">👶</div>
        <div class="info">
          <h1 class="baby-name">刘知许小朋友</h1>
          <p class="baby-age">{{ currentAge }}</p>
        </div>
      </div>
      <div class="stats">
        <div class="stat-item">
          <div class="stat-number">{{ totalEvents }}</div>
          <div class="stat-label">个事件</div>
        </div>
        <div class="stat-item">
          <div class="stat-number">{{ totalPhotos }}</div>
          <div class="stat-label">张照片</div>
        </div>
        <div class="stat-item">
          <div class="stat-number">{{ totalVideos }}</div>
          <div class="stat-label">个视频</div>
        </div>
        <div class="stat-item">
          <div class="stat-number">{{ totalAudios }}</div>
          <div class="stat-label">段录音</div>
        </div>
      </div>
    </header>

    <!-- 时间线 -->
    <div class="timeline-container">
      <div class="timeline-header">
        <h2 class="timeline-title">成长时光轴</h2>
        <div class="sort-control">
          <label class="sort-checkbox">
            <input 
              type="checkbox" 
              v-model="sortAscending" 
              @change="onSortChange"
            />
            <span class="checkmark"></span>
            <span class="sort-label">{{ sortAscending ? '最新在下' : '最新在上' }}</span>
          </label>
        </div>
      </div>
      
      <!-- 加载状态 -->
      <div v-if="loading" class="loading-container">
        <div class="loading-spinner">⏳</div>
        <p>正在加载成长记录...</p>
      </div>
      
      <!-- 错误状态 -->
      <div v-else-if="error" class="error-container">
        <div class="error-icon">⚠️</div>
        <p>{{ error }}</p>
        <button class="retry-btn" @click="loadEventsList">重试</button>
      </div>
      
      <!-- 时间线内容 -->
      <div v-else class="timeline">
        <div 
          v-for="(period, index) in sortedTimelinePeriods" 
          :key="index"
          class="timeline-period"
        >
          <div class="period-header">
            <div class="period-age">{{ period.age }}</div>
            <div class="period-date">{{ period.date }}</div>
          </div>
          <div class="events-grid">
            <div 
              v-for="event in period.events" 
              :key="event.id"
              class="event-card"
              :data-event-id="event.id"
              @click="viewEvent(event)"
            >
              <div class="event-photos">
                <LazyImage
                  v-for="(image, photoIndex) in event.media.images.slice(0, 4)" 
                  :key="photoIndex"
                  :src="getMediaUrl(event.id, image.fileName)"
                  :alt="`${event.title} - 图片${photoIndex + 1}`"
                  :small="event.media.images.length > 1"
                  :preload="shouldPreloadImage(index, photoIndex)"
                  :priority="getImagePriority(index, photoIndex)"
                  :threshold="loadingStrategy.threshold"
                  class="photo-item"
                  :class="{ 'small': event.media.images.length > 1 }"
                />
                <div v-if="event.media.images.length > 4" class="more-photos">
                  +{{ event.media.images.length - 4 }}
                </div>
              </div>
              <div class="event-info">
                <h3 class="event-title">{{ event.title }}</h3>
                <p class="event-description">{{ event.description }}</p>
                <div class="event-meta">
                  <span v-if="event.media.audios.length > 0" class="audio-indicator">🎵</span>
                  <span v-if="event.media.videos.length > 0" class="video-indicator">🎬</span>
                  <span class="photo-count">{{ event.media.images.length }}张照片</span>
                  <span v-if="event.media.videos.length > 0" class="video-count">{{ event.media.videos.length }}个视频</span>
                  <span v-if="event.media.audios.length > 0" class="audio-count">{{ event.media.audios.length }}段音频</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 快速添加按钮 -->
    <button class="fab" @click="addEvent">
      ➕
    </button>

    <!-- 性能监控面板（仅开发环境） -->
    <PerformancePanel />
  </div>
</template>

<script>
import { ref, computed, onMounted, nextTick } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import dayjs from 'dayjs'
import { getEventsList, getStats, getMediaUrl } from '@/api/events'
import LazyImage from '@/components/LazyImage.vue'
import PerformancePanel from '@/components/PerformancePanel.vue'
import { preloadEventImages } from '@/utils/imageUtils'
import { adaptiveImageLoader } from '@/utils/imagePerformance'

export default {
  name: 'HomePage',
  components: {
    LazyImage,
    PerformancePanel
  },
  setup() {
    const router = useRouter()
    const route = useRoute()
    const babyBirthDate = ref(dayjs('2025-05-09')) // 宝宝出生日期：2025年5月9日
    
    // 计算当前年龄
    const currentAge = computed(() => {
      const now = dayjs()
      const birth = babyBirthDate.value
      
      // 如果出生日期在未来，返回"即将出生"
      if (now.isBefore(birth)) {
        return '即将出生'
      }
      
      const totalMonths = now.diff(birth, 'month')
      
      if (totalMonths < 12) {
        // 计算准确的天数：当前日期减去这个月开始时对应的出生月日
        const monthStart = birth.add(totalMonths, 'month')
        const days = now.diff(monthStart, 'day')
        
        if (totalMonths === 0) {
          const totalDays = now.diff(birth, 'day')
          return `${totalDays}天`
        } else {
          return `${totalMonths}个月${days > 0 ? ` ${days}天` : ''}`
        }
      } else {
        const years = Math.floor(totalMonths / 12)
        const remainingMonths = totalMonths % 12
        return `${years}岁${remainingMonths > 0 ? ` ${remainingMonths}个月` : ''}`
      }
    })

    // 从localStorage获取排序状态
    const getSortPreference = () => {
      try {
        const saved = localStorage.getItem('baby-log-sort-ascending')
        return saved !== null ? JSON.parse(saved) : false
      } catch (error) {
        console.warn('获取排序偏好失败:', error)
        return false
      }
    }

    // 保存排序状态到localStorage
    const saveSortPreference = (ascending) => {
      try {
        localStorage.setItem('baby-log-sort-ascending', JSON.stringify(ascending))
      } catch (error) {
        console.warn('保存排序偏好失败:', error)
      }
    }

    // 响应式数据
    const timelinePeriods = ref([])
    const loading = ref(true)
    const error = ref('')
    const sortAscending = ref(getSortPreference()) // 从localStorage获取初始状态
    const stats = ref({
      totalEvents: 0,
      totalPhotos: 0,
      totalVideos: 0,
      totalAudios: 0
    })

    // 获取自适应加载策略
    const loadingStrategy = adaptiveImageLoader.getLoadingStrategy()

    // 加载事件列表
    const loadEventsList = async () => {
      try {
        loading.value = true
        error.value = ''
        
        const response = await getEventsList()
        if (response.success) {
          timelinePeriods.value = response.data
          
          // 预加载关键图片（根据网络情况调整数量）
          const allEvents = response.data.flatMap(period => period.events)
          if (allEvents.length > 0 && loadingStrategy.enablePreload) {
            preloadEventImages(allEvents, loadingStrategy.preloadCount, getMediaUrl)
          }
        } else {
          error.value = response.message || '获取事件列表失败'
        }
      } catch (err) {
        error.value = '网络错误，请稍后重试'
        console.error('加载事件列表失败:', err)
      } finally {
        loading.value = false
      }
    }

    // 加载统计数据
    const loadStats = async () => {
      try {
        const response = await getStats()
        if (response.success) {
          stats.value = response.data
        }
      } catch (err) {
        console.error('加载统计数据失败:', err)
      }
    }

    // 计算统计数据（使用API获取的stats数据）
    const totalEvents = computed(() => stats.value.totalEvents)
    const totalPhotos = computed(() => stats.value.totalPhotos)
    const totalVideos = computed(() => stats.value.totalVideos)
    const totalAudios = computed(() => stats.value.totalAudios)

    // 计算排序后的时间线数据
    const sortedTimelinePeriods = computed(() => {
      if (!timelinePeriods.value || timelinePeriods.value.length === 0) {
        return []
      }
      
      const sorted = [...timelinePeriods.value]
      
      if (sortAscending.value) {
        // 正序：最旧的在上，最新的在下
        return sorted.sort((a, b) => {
          // 使用period中第一个事件的日期进行排序
          const dateA = new Date(a.events[0]?.date || '1970-01-01')
          const dateB = new Date(b.events[0]?.date || '1970-01-01')
          return dateA - dateB
        })
      } else {
        // 倒序：最新的在上，最旧的在下（默认）
        return sorted.sort((a, b) => {
          const dateA = new Date(a.events[0]?.date || '1970-01-01')
          const dateB = new Date(b.events[0]?.date || '1970-01-01')
          return dateB - dateA
        })
      }
    })

    // 方法
    const viewEvent = (event) => {
      console.log('查看事件:', event)
      // 跳转到事件详情页
      router.push(`/event/${event.id}`)
    }

    const addEvent = () => {
      console.log('添加新事件')
      // 跳转到添加事件页面
      router.push('/add')
    }

    const onSortChange = () => {
      console.log('排序方式改变:', sortAscending.value ? '正序' : '倒序')
      // 保存用户的排序偏好到localStorage
      saveSortPreference(sortAscending.value)
    }

    // 判断是否应该预加载图片
    const shouldPreloadImage = (eventIndex, photoIndex) => {
      // 为前2个事件的第一张图片启用预加载
      return eventIndex < 2 && photoIndex === 0
    }

    // 获取图片加载优先级
    const getImagePriority = (eventIndex, photoIndex) => {
      // 前3个事件的第一张图片为高优先级
      if (eventIndex < 3 && photoIndex === 0) {
        return 'high'
      }
      // 前6个事件的其他图片为普通优先级
      if (eventIndex < 6) {
        return 'normal'
      }
      // 其余为低优先级
      return 'low'
    }

    // 自动定位到指定事件
    const scrollToEvent = async (eventId) => {
      await nextTick()
      
      // 查找包含该事件的卡片元素
      const eventCard = document.querySelector(`[data-event-id="${eventId}"]`)
      if (eventCard) {
        // 滚动到指定位置并高亮显示
        eventCard.scrollIntoView({ 
          behavior: 'smooth', 
          block: 'center' 
        })
      
      } else {
        console.warn('未找到指定的事件卡片:', eventId)
      }
    }

    // 查找最新事件ID
    const findLatestEventId = () => {
      if (timelinePeriods.value && timelinePeriods.value.length > 0) {
        // 获取所有事件并按日期排序，找到最新的
        const allEvents = timelinePeriods.value.flatMap(period => period.events)
        if (allEvents.length > 0) {
          const latestEvent = allEvents.sort((a, b) => new Date(b.date) - new Date(a.date))[0]
          return latestEvent.id
        }
      }
      return null
    }

    // 查找ID数字最接近的事件
    const findClosestEventId = (targetId) => {
      if (!timelinePeriods.value || timelinePeriods.value.length === 0) {
        return null
      }

      const allEvents = timelinePeriods.value.flatMap(period => period.events)
      if (allEvents.length === 0) {
        return null
      }

      const targetIdNum = parseInt(targetId, 10)
      if (isNaN(targetIdNum)) {
        // 如果目标ID不是数字，返回最新事件
        return findLatestEventId()
      }

      // 找到ID数字最接近的事件
      let closestEvent = allEvents[0]
      let minDifference = Math.abs(parseInt(closestEvent.id, 10) - targetIdNum)

      for (const event of allEvents) {
        const eventIdNum = parseInt(event.id, 10)
        if (!isNaN(eventIdNum)) {
          const difference = Math.abs(eventIdNum - targetIdNum)
          if (difference < minDifference) {
            minDifference = difference
            closestEvent = event
          }
        }
      }

      return closestEvent.id
    }

    // 页面初始化
    onMounted(async () => {
      await Promise.all([
        loadEventsList(),
        loadStats()
      ])
      
      // 检查是否需要定位到特定事件
      const highlightId = route.query.highlight
      if (highlightId) {
        // 检查该事件是否存在
        const allEvents = timelinePeriods.value.flatMap(period => period.events)
        const targetEvent = allEvents.find(event => String(event.id) === String(highlightId))
        
        if (targetEvent) {
          // 事件存在，定位到该事件
          scrollToEvent(highlightId)
        } else {
          // 事件不存在，寻找ID数字最接近的事件
          const closestEventId = findClosestEventId(highlightId)
          if (closestEventId) {
            scrollToEvent(closestEventId)
          }
        }
      }
    })

    return {
      currentAge,
      timelinePeriods,
      sortedTimelinePeriods,
      sortAscending,
      totalEvents,
      totalPhotos,
      totalVideos,
      totalAudios,
      loading,
      error,
      viewEvent,
      addEvent,
      loadEventsList,
      onSortChange,
      getSortPreference,
      saveSortPreference,
      getMediaUrl,
      scrollToEvent,
      findLatestEventId,
      findClosestEventId,
      shouldPreloadImage,
      getImagePriority,
      loadingStrategy
    }
  }
}
</script>

<style scoped>
.home {
  min-height: 100vh;
  background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
  padding: 20px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

/* 头部信息 */
.header {
  background: white;
  border-radius: 20px;
  padding: 25px;
  margin: 0 auto 30px auto;
  max-width: 800px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
  backdrop-filter: blur(10px);
}

.baby-info {
  display: flex;
  align-items: center;
  margin-bottom: 20px;
}

.avatar {
  width: 80px;
  height: 80px;
  border-radius: 50%;
  background: linear-gradient(135deg, #ff9a9e 0%, #fecfef 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 40px;
  margin-right: 20px;
}

.baby-name {
  font-size: 28px;
  font-weight: 600;
  color: #2c3e50;
  margin: 0 0 5px 0;
}

.baby-age {
  font-size: 16px;
  color: #7f8c8d;
  margin: 0;
}

.stats {
  display: flex;
  gap: 20px;
  flex-wrap: wrap;
  justify-content: center;
}

.stat-item {
  text-align: center;
}

.stat-number {
  font-size: 32px;
  font-weight: 700;
  color: #e74c3c;
  line-height: 1;
}

.stat-label {
  font-size: 14px;
  color: #7f8c8d;
  margin-top: 5px;
}

/* 时间线 */
.timeline-container {
  max-width: 800px;
  margin: 0 auto;
}

.timeline-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 30px;
}

.timeline-title {
  font-size: 24px;
  font-weight: 600;
  color: #2c3e50;
  margin: 0;
}

.sort-control {
  display: flex;
  align-items: center;
}

.sort-checkbox {
  display: flex;
  align-items: center;
  cursor: pointer;
  font-size: 14px;
  color: #7f8c8d;
  user-select: none;
}

.sort-checkbox input[type="checkbox"] {
  display: none;
}

.checkmark {
  width: 18px;
  height: 18px;
  border: 2px solid #bdc3c7;
  border-radius: 4px;
  margin-right: 8px;
  position: relative;
  transition: all 0.3s ease;
  background: white;
}

.sort-checkbox input[type="checkbox"]:checked + .checkmark {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-color: #667eea;
}

.sort-checkbox input[type="checkbox"]:checked + .checkmark::after {
  content: '✓';
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  color: white;
  font-size: 12px;
  font-weight: bold;
}

.sort-label {
  font-weight: 500;
  transition: color 0.3s ease;
}

.sort-checkbox:hover .sort-label {
  color: #5a6c7d;
}

.timeline-period {
  margin-bottom: 40px;
}

.period-header {
  display: flex;
  align-items: center;
  margin-bottom: 20px;
}

.period-age {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 10px 20px;
  border-radius: 25px;
  font-weight: 600;
  font-size: 16px;
  margin-right: 15px;
}

.period-date {
  color: #7f8c8d;
  font-size: 14px;
}

/* 事件网格 */
.events-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 20px;
}

.event-card {
  background: white;
  border-radius: 15px;
  overflow: hidden;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
  transition: transform 0.3s ease, box-shadow 0.3s ease;
  cursor: pointer;
}

.event-card:hover {
  transform: translateY(-5px);
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.15);
}

.event-card.highlight-event {
  animation: highlightPulse 3s ease-in-out;
  border: 3px solid #3498db;
  box-shadow: 0 0 20px rgba(52, 152, 219, 0.3);
}

@keyframes highlightPulse {
  0% {
    border-color: #3498db;
    box-shadow: 0 0 20px rgba(52, 152, 219, 0.3);
  }
  50% {
    border-color: #2980b9;
    box-shadow: 0 0 30px rgba(41, 128, 185, 0.5);
  }
  100% {
    border-color: #3498db;
    box-shadow: 0 0 20px rgba(52, 152, 219, 0.3);
  }
}

.event-photos {
  height: 150px;
  background: #f8f9fa;
  position: relative;
  display: flex;
  flex-wrap: wrap;
  padding: 10px;
  gap: 5px;
}

.photo-item {
  flex: 1;
  min-height: 60px;
  border-radius: 8px;
  overflow: hidden;
}

.photo-item.small {
  max-width: 48%;
  max-height: 65px;
}

.more-photos {
  position: absolute;
  bottom: 15px;
  right: 15px;
  background: rgba(0, 0, 0, 0.7);
  color: white;
  padding: 5px 10px;
  border-radius: 15px;
  font-size: 12px;
  font-weight: 600;
}

.event-info {
  padding: 20px;
}

.event-title {
  font-size: 18px;
  font-weight: 600;
  color: #2c3e50;
  margin: 0 0 10px 0;
}

.event-description {
  font-size: 14px;
  color: #7f8c8d;
  line-height: 1.6;
  margin: 0 0 15px 0;
}

.event-meta {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 12px;
  color: #95a5a6;
}

.audio-indicator, .video-indicator {
  font-size: 14px;
}

.video-indicator {
  color: #8e44ad;
}

.audio-indicator {
  color: #e67e22;
}

/* 悬浮添加按钮 */
.fab {
  position: fixed;
  bottom: 30px;
  right: 30px;
  width: 60px;
  height: 60px;
  border-radius: 50%;
  background: linear-gradient(135deg, #ff6b6b 0%, #ee5a52 100%);
  color: white;
  border: none;
  font-size: 24px;
  cursor: pointer;
  box-shadow: 0 4px 20px rgba(255, 107, 107, 0.4);
  transition: transform 0.3s ease, box-shadow 0.3s ease;
}

.fab:hover {
  transform: scale(1.1);
  box-shadow: 0 6px 25px rgba(255, 107, 107, 0.5);
}

/* 加载和错误状态 */
.loading-container, .error-container {
  text-align: center;
  padding: 60px 20px;
  color: #7f8c8d;
}

.loading-spinner, .error-icon {
  font-size: 48px;
  margin-bottom: 20px;
}

.retry-btn {
  background: linear-gradient(135deg, #3498db 0%, #2980b9 100%);
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 20px;
  cursor: pointer;
  font-size: 14px;
  margin-top: 15px;
  transition: transform 0.2s ease;
}

.retry-btn:hover {
  transform: translateY(-2px);
}

/* 响应式设计 */
@media (max-width: 768px) {
  .home {
    padding: 15px;
  }
  
  .header {
    padding: 20px;
  }
  
  .baby-info {
    flex-direction: column;
    text-align: center;
  }
  
  .avatar {
    margin-right: 0;
    margin-bottom: 15px;
  }
  
  .stats {
    justify-content: center;
    gap: 15px;
  }
  
  .stat-item {
    flex: 1;
    min-width: 60px;
  }
  
  .events-grid {
    grid-template-columns: 1fr;
  }
  
  .period-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 10px;
  }

  .timeline-header {
    flex-direction: column;
    gap: 15px;
    align-items: center;
  }

  .timeline-title {
    text-align: center;
  }
}
</style>
