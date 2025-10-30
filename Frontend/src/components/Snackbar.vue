<template>
  <transition name="snackbar-fade">
    <div v-if="visible" class="snackbar" :class="typeClass">
      <div class="snackbar-icon" v-if="showIcon">{{ icon }}</div>
      <div class="snackbar-content">
        <div class="snackbar-message">{{ message }}</div>
        <div class="snackbar-subtext" v-if="subtext">{{ subtext }}</div>
      </div>
      <button v-if="showCloseButton" class="snackbar-close" @click="close">✕</button>
    </div>
  </transition>
</template>

<script>
import { ref, computed, watch, onMounted } from 'vue';

export default {
  name: 'AppSnackbar',
  props: {
    message: {
      type: String,
      required: true
    },
    subtext: {
      type: String,
      default: ''
    },
    type: {
      type: String,
      default: 'info',
      validator: (value) => ['success', 'info', 'warning', 'error'].includes(value)
    },
    duration: {
      type: Number,
      default: 3000
    },
    showIcon: {
      type: Boolean,
      default: true
    },
    showCloseButton: {
      type: Boolean,
      default: true
    },
    modelValue: {
      type: Boolean,
      default: false
    }
  },
  emits: ['update:modelValue', 'closed'],
  setup(props, { emit }) {
    const visible = ref(props.modelValue);
    let timer = null;

    const typeClass = computed(() => {
      return {
        'snackbar-success': props.type === 'success',
        'snackbar-info': props.type === 'info',
        'snackbar-warning': props.type === 'warning',
        'snackbar-error': props.type === 'error'
      };
    });

    const icon = computed(() => {
      switch (props.type) {
        case 'success':
          return '✓';
        case 'info':
          return 'ℹ️';
        case 'warning':
          return '⚠️';
        case 'error':
          return '✕';
        default:
          return 'ℹ️';
      }
    });

    const close = () => {
      visible.value = false;
      clearTimeout(timer);
      emit('update:modelValue', false);
      emit('closed');
    };

    // Start timer when snackbar becomes visible
    const startTimer = () => {
      if (props.duration > 0) {
        clearTimeout(timer);
        timer = setTimeout(() => {
          close();
        }, props.duration);
      }
    };

    // Watch for changes in the modelValue prop
    watch(() => props.modelValue, (newValue) => {
      visible.value = newValue;
      if (newValue && props.duration > 0) {
        startTimer();
      }
    });

    // Initial setup
    onMounted(() => {
      if (visible.value && props.duration > 0) {
        startTimer();
      }
    });

    return {
      visible,
      typeClass,
      icon,
      close
    };
  }
};
</script>

<style scoped>
.snackbar {
  position: fixed;
  top: 100px;
  left: 50%;
  transform: translateX(-50%);
  min-width: 300px;
  max-width: 80%;
  display: flex;
  align-items: center;
  padding: 14px 16px;
  border-radius: 8px;
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.12);
  background-color: #323232;
  color: white;
  z-index: 9999;
}

.snackbar-icon {
  margin-right: 12px;
  font-size: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.snackbar-content {
  flex-grow: 1;
}

.snackbar-message {
  font-size: 14px;
  font-weight: 500;
}

.snackbar-subtext {
  font-size: 12px;
  opacity: 0.8;
  margin-top: 4px;
}

.snackbar-close {
  background: transparent;
  border: none;
  color: white;
  margin-left: 12px;
  padding: 4px;
  cursor: pointer;
  opacity: 0.7;
  transition: opacity 0.2s;
  font-size: 16px;
}

.snackbar-close:hover {
  opacity: 1;
}

/* Types */
.snackbar-success {
  background-color: #4caf50;
}

.snackbar-info {
  background-color: #2196f3;
}

.snackbar-warning {
  background-color: #ff9800;
}

.snackbar-error {
  background-color: #f44336;
}

/* Animation */
.snackbar-fade-enter-active,
.snackbar-fade-leave-active {
  transition: opacity 0.3s, transform 0.3s;
}

.snackbar-fade-enter-from,
.snackbar-fade-leave-to {
  opacity: 0;
  transform: translate(-50%, 20px);
}
</style>
