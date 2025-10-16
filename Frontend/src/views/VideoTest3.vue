<template>
  <div>
    <h1>Video Test 3 - vue3VideoPlay (downloadVideo API)</h1>
    <vue3VideoPlay
      v-bind="videoOptions"
    />
  </div>
</template>

<script>
import { computed } from 'vue'
import vue3VideoPlay from 'vue3-video-play'
import 'vue3-video-play/dist/style.css'

export default {
  name: 'VideoTest3',
  components: {
    vue3VideoPlay
  },
  setup() {
    const videoOptions = computed(() => ({
      width: '800px',
      height: '600px',
      src: '/api/Files/downloadVideo?id=2&fileName=20251015082452089_h1dpm5.mp4',
      autoPlay: false,
      control: true
    }))

    return {
      videoOptions
    }
  }
}
</script>
