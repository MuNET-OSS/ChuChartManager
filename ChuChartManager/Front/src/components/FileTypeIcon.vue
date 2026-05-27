<script setup lang="ts">
defineProps<{ type: string }>()
</script>

<template>
  <div class="file-icon">
    <svg viewBox="0 0 24 30" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
      <path d="M3 2h12l6 6v20H3V2z" fill="rgba(255,255,255,0.06)" />
      <path d="M15 2v6h6" />
    </svg>
    <span class="file-ext">{{ type }}</span>
  </div>
</template>

<style lang="sass" scoped>
.file-icon
  position: relative
  display: inline-flex
  width: 3.5em
  height: 4.5em

  svg
    width: 100%
    height: 100%

.file-ext
  position: absolute
  left: 0.3em
  bottom: 0.4em
  font-size: 0.7em
  font-weight: 700
  line-height: 1
  padding: 0.15em 0.3em
  background: rgba(255, 255, 255, 0.12)
  border-radius: 3px
  color: currentColor
</style>
