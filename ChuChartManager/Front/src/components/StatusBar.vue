<script setup lang="ts">
import { statusText } from '@/store/status'
</script>

<template>
  <div class="px-3 py-1 text-xs op-50 border-t border-white/5 shrink-0 truncate">
    {{ statusText }}
  </div>
</template>
