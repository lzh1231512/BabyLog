import { createRouter, createWebHashHistory } from 'vue-router'
import Home from '../views/Home.vue'
import EventDetail from '../views/EventDetail.vue'
import AddEditEvent from '../views/AddEditEvent.vue'
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
