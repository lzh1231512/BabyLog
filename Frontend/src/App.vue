<template>
  <div id="app">
    <router-view/>
  </div>
</template>

<script>
export default {
  name: 'BabyLogApp'
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
