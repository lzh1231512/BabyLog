<template>
  <div id="app">
    <router-view/>
  </div>
</template>

<script>
export default {
  name: 'BabyLogApp',
  data() {
    return {
      currentTheme: 'light'
    }
  },
  created() {
    const saved = localStorage.getItem('babylog-theme') || 'light'
    this.applyTheme(saved)
  },
  methods: {
    applyTheme(theme) {
      this.currentTheme = theme
      document.documentElement.setAttribute('data-theme', theme)
      localStorage.setItem('babylog-theme', theme)
    },
    toggleTheme() {
      // 这里示例在 light <-> warm 之间切换，按需改成更多状态或循环
      const next = this.currentTheme === 'light' ? 'warm' : 'light'
      this.applyTheme(next)
    }
  }
}
</script>

<style>
@import './assets/styles/theme.css';
@import './assets/styles/videoPlayer.css';

* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  background-color: var(--color-background);
}

#app {
  min-height: 100vh;
}
</style>
