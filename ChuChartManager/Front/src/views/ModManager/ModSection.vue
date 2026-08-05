<script setup lang="ts">
import { computed } from 'vue'
import { CheckBox } from '@munet/ui'
import { locale } from '@/locales'
import type { ManifestSection, ModConfigSection } from '@/client/mod'
import ModEntry from './ModEntry.vue'

const props = defineProps<{
  section: ManifestSection
  state: ModConfigSection
}>()

const language = computed(() => locale.value.startsWith('zh') ? 'zh' : 'en')
const label = computed(() => props.section.label?.[language.value] || props.section.id)
const description = computed(() => props.section.description?.[language.value] || '')
const entries = computed(() => props.section.entries?.filter(entry => !entry.hidden) ?? [])
</script>

<template>
  <div class="mod-section">
    <div class="section-header">
      <div class="section-label">{{ label }}</div>
      <div class="flex flex-col gap-2 min-w-0">
        <div class="flex gap-2 h-28px items-center">
          <CheckBox v-model:value="state.enabled">
            {{ state.enabled ? $t('mods.on') : $t('mods.off') }}
          </CheckBox>
        </div>
        <div v-if="description" class="text-sm op-80">{{ description }}</div>
      </div>
    </div>

    <div v-if="state.enabled && entries.length" class="flex flex-col gap-2 mt-2">
      <ModEntry
        v-for="entry in entries"
        :key="entry.key"
        :entry="entry"
        :section-state="state"
      />
    </div>
  </div>
</template>

<style lang="sass" scoped>
.mod-section
  display: flex
  flex-direction: column
  padding: 8px
  border: 1px solid transparent
  border-radius: 6px
  transition: border-color 0.2s, background-color 0.2s

  &:hover
    border-color: oklch(0.68 0.17 var(--hue) / 0.45)

.section-header
  display: grid
  grid-template-columns: minmax(8rem, 12rem) minmax(0, 1fr)
  gap: 8px
  align-items: start

.section-label
  min-width: 0
  font-size: 1rem
  overflow-wrap: anywhere
</style>
