import { reactive } from 'vue'
import type { Entity } from './rpg'

function getCookie(name: string): string | null {
  const value = `; ${document.cookie}`
  const parts = value.split(`; ${name}=`)
  if (parts.length === 2) return parts.pop()?.split(';').shift() || null
  return null
}

function setCookie(name: string, value: string, days: number) {
  const date = new Date()
  date.setTime(date.getTime() + days * 24 * 60 * 60 * 1000)
  document.cookie = `${name}=${value}; expires=${date.toUTCString()}; path=/`
}

export const store = reactive({
  username: '',
  entities: [] as Entity[],
  setUsername(name: string) {
    this.username = name
    setCookie('username', name, 30) // 30 days
  },
  loadUsername() {
    const name = getCookie('username')
    if (name) this.username = name
  },
})
