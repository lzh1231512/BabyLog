import { createApp } from 'vue'
import App from './App.vue'
import {loadConfig} from './config'
loadConfig().then(async () => {
  const router = await import('./router/index').then(m => m.default);
  createApp(App).use(router).mount('#app')
})
