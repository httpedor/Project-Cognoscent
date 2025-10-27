import { createApp } from 'vue'
import App from './App.vue'
import PrimeVue from 'primevue/config'
import Aura from '@primeuix/themes/aura'
import { definePreset } from '@primeuix/themes'
import router from './router'
import { store } from './store'

const MyPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#fee2e2',
      100: '#fecaca',
      200: '#fca5a5',
      300: '#f87171',
      400: '#ef4444',
      500: '#dc2626',
      600: '#b91c1c',
      700: '#991b1b',
      800: '#7f1d1d',
      900: '#450a0a',
      950: '#1f0404',
    },
  },
})

const app = createApp(App)

app.use(router)
app.use(PrimeVue, {
  theme: {
    preset: MyPreset,
    options: {
      darkModeSelector: true,
    },
  },
})

store.loadUsername()

app.mount('#app')
