<script setup lang="ts">
import { Teleport, Transition } from 'vue'

defineProps<{
  show: boolean
  title?: string
}>()
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-300 ease"
      leave-active-class="transition-opacity duration-300 ease"
      enter-from-class="op-0!"
      leave-to-class="op-0!"
    >
      <div v-if="show" class="fixed inset-0 z-999 backdrop-blur-lg flex flex-col justify-end items-start" style="background: rgba(0,0,0,0.6)">
        <div class="w-full max-w-200 p-6 pb-10 flex flex-col gap-3 items-start">
          <div v-if="title" class="text-lg op-80 text-white">{{ title }}</div>
          <slot />
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
