<script setup lang="ts">
import type { Stat } from '@/rpg';
import { defineProps } from 'vue'


defineProps<{
  stat: Stat;
  type: 'health' | 'skill' | 'resource';
}>()
</script>

<template>
  <div class="flex flex-col items-center p-4 border border-gray-200 rounded-lg shadow-sm hover:shadow-md transition-shadow duration-200">
    <h4 class="text-lg font-semibold mb-2">{{ stat.id }}</h4>
    <p class="text-3xl font-bold text-gray-800">{{ stat.finalValue }}</p>
  </div>
</template>
