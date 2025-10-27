import { createRouter, createWebHistory } from 'vue-router'
import { store } from '../store'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      component: () => import('../views/IndexPage.vue'),
      beforeEnter: () => {
        if (!store.username) return '/login'
      },
    },
    {
      path: '/login',
      component: () => import('../views/LoginPage.vue'),
    },
  ],
})

export default router
