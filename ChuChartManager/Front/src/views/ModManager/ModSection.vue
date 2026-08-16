<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { CheckBox } from '@munet/ui'
import { locale } from '@/locales'
import type { ManifestSection, ModConfigSection } from '@/client/mod'
import ModEntry from './ModEntry.vue'

const props = defineProps<{
  section: ManifestSection
  state: ModConfigSection
  revealAdvanced: boolean
}>()

const language = computed(() => locale.value.startsWith('zh') ? 'zh' : 'en')
const label = computed(() => props.section.label?.[language.value] || props.section.id)
const description = computed(() => props.section.description?.[language.value] || '')
const entries = computed(() => props.section.entries?.filter(entry =>
  !entry.hidden && entry.key.replaceAll('_', '').toLowerCase() !== 'enable') ?? [])
const basicEntries = computed(() => entries.value.filter(entry => !entry.advanced))
const advancedEntries = computed(() => entries.value.filter(entry => entry.advanced))
const advancedExpanded = ref(false)
const advancedOpen = computed(() => props.revealAdvanced || advancedExpanded.value)
const advancedContentId = `advanced-${useId()}`
const enabled = computed({
  get: () => props.section.always_enabled ? true : props.state.enabled,
  set: value => { props.state.enabled = props.section.always_enabled ? true : value },
})
</script>

<template>
  <div class="mod-section" :class="{ 'section-disabled': !enabled, 'always-enabled': section.always_enabled }">
    <div class="section-header">
      <CheckBox v-if="!section.always_enabled" v-model:value="enabled" class="section-switch">
        <span class="section-label">{{ label }}</span>
      </CheckBox>
      <div v-else class="section-label">{{ label }}</div>
      <div v-if="description" class="text-sm op-80 min-w-0">{{ description }}</div>
    </div>

    <div class="section-options">
      <div v-if="basicEntries.length" class="flex flex-col gap-2 mt-2">
        <ModEntry
          v-for="entry in basicEntries"
          :key="entry.key"
          :entry="entry"
          :section-state="state"
        />
      </div>

      <template v-if="advancedEntries.length">
        <button
          v-if="!revealAdvanced"
          type="button"
          class="advanced-toggle"
          :aria-expanded="advancedOpen"
          :aria-controls="advancedContentId"
          @click="advancedExpanded = !advancedExpanded"
        >
          <span
            class="i-mdi-chevron-down advanced-chevron"
            :class="{ expanded: advancedOpen }"
            aria-hidden="true"
          />
          <span>{{ $t('mods.advancedOptions') }}</span>
          <span class="op-55">{{ advancedEntries.length }}</span>
        </button>
        <div v-else class="advanced-label">
          <span class="i-mdi-tune-variant text-4" aria-hidden="true" />
          <span>{{ $t('mods.advancedOptions') }}</span>
        </div>
        <div
          v-show="advancedOpen"
          :id="advancedContentId"
          class="flex flex-col gap-2 mt-2"
        >
          <ModEntry
            v-for="entry in advancedEntries"
            :key="entry.key"
            :entry="entry"
            :section-state="state"
          />
        </div>
      </template>
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

  &.always-enabled
    background: oklch(0.68 0.17 var(--hue) / 0.05)

  &.section-disabled .section-options
    opacity: 0.48

.section-options
  transition: opacity 0.2s ease

.section-header
  display: grid
  grid-template-columns: minmax(8rem, 12rem) minmax(0, 1fr)
  gap: 8px
  align-items: start

.section-label
  min-width: 0
  font-size: 1rem
  overflow-wrap: anywhere

.section-switch
  min-width: 0

.advanced-toggle,
.advanced-label
  display: flex
  align-items: center
  align-self: stretch
  gap: 4px
  min-height: 32px
  margin-top: 8px
  padding: 4px 8px
  border: 0
  border-top: 1px solid color-mix(in oklch, var(--text-color) 12%, transparent)
  background: transparent
  color: color-mix(in oklch, var(--text-color) 68%, transparent)
  font: inherit
  font-size: 0.875rem
  text-align: left

.advanced-toggle
  cursor: pointer

  &:hover,
  &:focus-visible
    color: var(--text-color)
    background: color-mix(in oklch, var(--text-color) 5%, transparent)

  &:focus-visible
    outline: 2px solid oklch(0.68 0.17 var(--hue) / 0.55)
    outline-offset: -2px

.advanced-chevron
  flex-shrink: 0
  font-size: 1rem
  transition: transform 0.2s ease

  &.expanded
    transform: rotate(180deg)

@media (prefers-reduced-motion: reduce)
  .advanced-chevron
    transition: none
</style>
