import { createRouter, createWebHashHistory } from 'vue-router'
import Home from '../views/Home.vue'
import EventDetail from '../views/EventDetail.vue'
import AddEditEvent from '../views/AddEditEvent.vue'
import VideoPlayer from '../views/VideoPlayer.vue'
import VideoTest1 from '../views/VideoTest1.vue'
import VideoTest2 from '../views/VideoTest2.vue'
import VideoTest3 from '../views/VideoTest3.vue'
import VideoTest4 from '../views/VideoTest4.vue'
import ChunkUploadDemo from '../views/ChunkUploadDemo.vue'
import config from '../config'
const routes = [
  {
    path: '/',
    name: 'HomePage',
    component: Home
  },
  {
    path: '/event/:id',
    name: 'EventDetail',
    component: EventDetail
  },
  {
    path: '/add',
    name: 'AddEvent',
    component: AddEditEvent
  },
  {
    path: '/edit/:id',
    name: 'EditEvent',
    component: AddEditEvent
  },
  {
    path: '/video-player/:id/:videoIndex?',
    name: 'VideoPlayer',
    component: VideoPlayer
  },
  {
    path: '/video-test-1',
    name: 'VideoTest1',
    component: VideoTest1
  },
  {
    path: '/video-test-2',
    name: 'VideoTest2',
    component: VideoTest2
  },
  {
    path: '/video-test-3',
    name: 'VideoTest3',
    component: VideoTest3
  },
  {
    path: '/video-test-4',
    name: 'VideoTest4',
    component: VideoTest4
  },
  {
    path: '/chunk-upload',
    name: 'ChunkUploadDemo',
    component: ChunkUploadDemo
  }
]

const router = createRouter({
  history: createWebHashHistory(config.BasePath),
  routes,
  scrollBehavior(to, from, savedPosition) {
    // 如果有保存的滚动位置（如浏览器前进后退），则恢复该位置
    if (savedPosition) {
      return savedPosition
    }
    // 否则滚动到页面顶部
    return { top: 0 }
  }
})

export default router
