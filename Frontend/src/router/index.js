import { createRouter, createWebHashHistory } from 'vue-router'
import Home from '../views/Home.vue'
import EventDetail from '../views/EventDetail.vue'
import AddEditEvent from '../views/AddEditEvent.vue'
import VideoPlayer from '../views/VideoPlayer.vue'
import ChunkUploadDemo from '../views/ChunkUploadDemo.vue'
import config from '../config'
import { login } from '../api/events'; 
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
    path: '/chunk-upload',
    name: 'ChunkUploadDemo',
    component: ChunkUploadDemo
  }
  ,{
    path: '/login',
    name: 'LoginPage',
    component: () => import('../views/Login.vue')
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

router.beforeEach(async (to, from, next) => {
  if (to.name === 'LoginPage') {
    // 登录页不需要校验
    return next();
  }
  try {
    const isLoggedIn = await login();
    if (isLoggedIn) {
      next();
    } else {
      next({ name: 'LoginPage' });
    }
  } catch (e) {
    next({ name: 'LoginPage' });
  }
});

export default router
