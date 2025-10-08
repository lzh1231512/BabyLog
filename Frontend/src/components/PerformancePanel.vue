<template>
  <div v-if="showPanel" class="performance-panel">
    <div class="panel-header">
      <h4>图片加载性能</h4>
      <button class="close-btn" @click="closePanel">×</button>
    </div>
    <div class="panel-content">
      <div class="stat-row">
        <span>总计:</span>
        <span>{{ stats.totalImages }}</span>
      </div>
      <div class="stat-row">
        <span>成功:</span>
        <span>{{ stats.loadedImages }}</span>
      </div>
      <div class="stat-row">
        <span>失败:</span>
        <span>{{ stats.failedImages }}</span>
      </div>
      <div class="stat-row">
        <span>成功率:</span>
        <span>{{ stats.successRate }}</span>
      </div>
      <div class="stat-row">
        <span>平均耗时:</span>
        <span>{{ stats.averageLoadTime }}</span>
      </div>
      <div class="recommendations" v-if="recommendations.length > 0">
        <h5>建议:</h5>
        <ul>
          <li v-for="(rec, index) in recommendations" :key="index">{{ rec }}</li>
        </ul>
      </div>
    </div>
  </div>
  
  <!-- 开发环境下的触发按钮 -->
  <button 
    v-if="isDevelopment && !showPanel" 
    class="performance-toggle"
    @click="togglePanel"
  >
    📊
  </button>
</template>

<script>
import { ref, onMounted, onUnmounted } from 'vue'
import { imagePerformanceMonitor } from '@/utils/imagePerformance'

export default {
  name: 'PerformancePanel',
  setup() {
    const showPanel = ref(false)
    const stats = ref({})
    const recommendations = ref([])
    const isDevelopment = process.env.NODE_ENV === 'development'
    let updateInterval = null

    const togglePanel = () => {
      showPanel.value = !showPanel.value
      if (showPanel.value) {
        updateStats()
        startAutoUpdate()
      } else {
        stopAutoUpdate()
      }
    }

    const closePanel = () => {
      showPanel.value = false
      stopAutoUpdate()
    }

    const updateStats = () => {
      stats.value = imagePerformanceMonitor.getStats()
      recommendations.value = imagePerformanceMonitor.getPerformanceRecommendations()
    }

    const startAutoUpdate = () => {
      updateInterval = setInterval(updateStats, 2000) // 每2秒更新一次
    }

    const stopAutoUpdate = () => {
      if (updateInterval) {
        clearInterval(updateInterval)
        updateInterval = null
      }
    }

    onMounted(() => {
      if (isDevelopment) {
        updateStats()
      }
    })

    onUnmounted(() => {
      stopAutoUpdate()
    })

    return {
      showPanel,
      stats,
      recommendations,
      isDevelopment,
      togglePanel,
      closePanel
    }
  }
}
</script>

<style scoped>
.performance-panel {
  position: fixed;
  top: 20px;
  right: 20px;
  width: 300px;
  background: rgba(0, 0, 0, 0.9);
  color: white;
  border-radius: 10px;
  padding: 15px;
  font-size: 12px;
  z-index: 9999;
  backdrop-filter: blur(10px);
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
  padding-bottom: 8px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.2);
}

.panel-header h4 {
  margin: 0;
  font-size: 14px;
  color: #4CAF50;
}

.close-btn {
  background: none;
  border: none;
  color: white;
  font-size: 18px;
  cursor: pointer;
  padding: 0;
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  transition: background-color 0.2s;
}

.close-btn:hover {
  background-color: rgba(255, 255, 255, 0.1);
}

.stat-row {
  display: flex;
  justify-content: space-between;
  margin-bottom: 5px;
  padding: 2px 0;
}

.stat-row span:first-child {
  color: #ccc;
}

.stat-row span:last-child {
  color: #4CAF50;
  font-weight: bold;
}

.recommendations {
  margin-top: 15px;
  padding-top: 10px;
  border-top: 1px solid rgba(255, 255, 255, 0.2);
}

.recommendations h5 {
  margin: 0 0 8px 0;
  font-size: 12px;
  color: #FF9800;
}

.recommendations ul {
  margin: 0;
  padding-left: 15px;
  list-style-type: disc;
}

.recommendations li {
  margin-bottom: 4px;
  line-height: 1.3;
  color: #ffeb3b;
  font-size: 10px;
}

.performance-toggle {
  position: fixed;
  top: 20px;
  right: 20px;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.7);
  color: white;
  border: none;
  font-size: 16px;
  cursor: pointer;
  z-index: 9998;
  backdrop-filter: blur(10px);
  transition: background-color 0.3s;
}

.performance-toggle:hover {
  background: rgba(0, 0, 0, 0.9);
}

/* 响应式 */
@media (max-width: 768px) {
  .performance-panel {
    width: 280px;
    right: 10px;
    top: 10px;
  }
  
  .performance-toggle {
    right: 10px;
    top: 10px;
    width: 36px;
    height: 36px;
    font-size: 14px;
  }
}
</style>
