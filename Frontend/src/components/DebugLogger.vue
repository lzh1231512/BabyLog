<template>
  <div v-if="visible" class="debug-panel">
    <div class="debug-header">
      <h4>调试日志 ({{ logs.length }})</h4>
      <div>
        <button @click="clearLogs" class="clear-log-btn">清空</button>
        <button @click="close" class="close-debug-btn">×</button>
      </div>
    </div>
    <div class="debug-content" ref="logContainer">
      <div 
        v-for="(log, index) in logs" 
        :key="index"
        :class="['log-entry', `log-${log.type}`]"
      >
        <span class="log-time">{{ log.time }}</span>
        <span class="log-message">{{ log.message }}</span>
      </div>
      <div v-if="logs.length === 0" class="log-entry">
        <span class="log-message">无日志记录</span>
      </div>
    </div>
  </div>
</template>

<script>
import { ref, watch, nextTick, onMounted, onBeforeUnmount } from 'vue'
import logger from '@/utils/videoLogger'

export default {
  name: 'DebugLogger',
  props: {
    visible: {
      type: Boolean,
      default: false
    }
  },
  emits: ['close'],
  setup(props, { emit }) {
    const logs = ref([]);
    const logContainer = ref(null);

    // Handle log updates
    const handleLogUpdate = (updatedLogs) => {
      logs.value = [...updatedLogs];
      scrollToBottom();
    };

    // Scroll to bottom when logs change
    const scrollToBottom = async () => {
      await nextTick();
      if (logContainer.value) {
        logContainer.value.scrollTop = logContainer.value.scrollHeight;
      }
    };

    // Clear logs
    const clearLogs = () => {
      logger.clear();
    };

    // Close debug panel
    const close = () => {
      emit('close');
    };

    // Watch logs for changes
    watch(() => props.visible, (newValue) => {
      if (newValue) {
        logs.value = logger.getLogs();
        scrollToBottom();
      }
    });

    // Component lifecycle
    onMounted(() => {
      logger.addListener(handleLogUpdate);
      logs.value = logger.getLogs();
      scrollToBottom();
    });

    onBeforeUnmount(() => {
      logger.removeListener(handleLogUpdate);
    });

    return {
      logs,
      logContainer,
      clearLogs,
      close
    };
  }
}
</script>
