<template>
  <div class="min-h-screen flex flex-col">
    <Toolbar class="px-4">
      <template #start>
        <div class="flex items-center gap-4 overflow-x-auto">
          <h1 class="text-lg font-semibold whitespace-nowrap">Bem-vindo, {{ store.username }}</h1>
          <span v-if="loading" class="text-sm text-gray-600 whitespace-nowrap">Carregando personagens…</span>
          <Message v-else-if="error" severity="error" class="!m-0">Erro: {{ error }}</Message>
          <div v-else class="flex items-center gap-2">
            <Avatar
              v-for="character in data"
              :key="character.id"
              shape="circle"
              size="large"
              :label="character.display ? undefined : (character.name?.charAt(0).toUpperCase() ?? '?')"
              :image="character.display ? 'data:image/unknown;base64,' + character.display : undefined"
              :style="{
                backgroundColor: selectedCharacter && selectedCharacter.id === character.id ? '#dc2626' : '#9ca3af',
                color: 'white',
                cursor: 'pointer',
                boxShadow: selectedCharacter && selectedCharacter.id === character.id ? '0 0 0 3px rgba(220,38,38,0.5)' : 'none'
              }"
              :title="character.name"
              @click="selectedCharacter = character"
            />
          </div>
        </div>
      </template>
      <template #end>
        <Button label="Sair" @click="logout"/>
      </template>
    </Toolbar>

    <main class="flex-1 container mx-auto p-4">
      <div v-if="selectedCharacter" class="rounded-lg border border-gray-200 p-4">
        <h3 class="text-xl font-semibold mb-2"> {{ selectedCharacter.name }} </h3>
      </div>
      <h2 v-else class="text-2xl font-bold">Selecione um personagem</h2>
    </main>
  </div>

</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Avatar, Button, Message, Toolbar } from 'primevue'
import { store } from '../store'
import type { Entity } from '@/rpg'

const loading = ref(true)
const error = ref('')
const data = ref<Entity[]>([])
const selectedCharacter = ref<Entity | null>(null)

onMounted(() => {
  fetch('/api/entities?owner=' + encodeURIComponent(store.username))
    .then((res) => res.json())
    .then((entities: Entity[]) => {
      data.value = entities
    })
    .catch((err) => {
      if (err instanceof SyntaxError) error.value = 'Formato de dados inválido'
      else error.value = err.message
    })
    .finally(() => {
      loading.value = false
    })
})

function logout() {
  store.setUsername('')
  window.location.reload()
}

// When a character is selected, the main section can react to changes.
</script>
