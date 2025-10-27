<template>
  <div class="h-screen flex items-center justify-center">
    <Panel header="Login" class="w-fit">
      <div class="flex flex-col items-center gap-4">
        <InputText v-model="username" type="text" placeholder="Nome de Usuário" />
        <Button @click="login">Entrar</Button>
      </div>
    </Panel>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { store } from '../store'
import { Button, InputText, Panel } from 'primevue'

const router = useRouter()
const username = ref('')

const login = () => {
  if (username.value.trim()) {
    store.setUsername(username.value.trim())
    router.push('/')
  }
}
</script>
