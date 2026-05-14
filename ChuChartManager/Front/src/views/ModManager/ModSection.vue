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
const isEnabled = computed({
  get: () => props.section.always_enabled || props.state.enabled,
  set: (value) => { if (!props.section.always_enabled) props.state.enabled = value },
})
</script>

<template>
  <div class="flex flex-col p-1 border-transparent border-solid border-1px rd hover:border-[oklch(0.68_0.17_var(--hue))] group">
    <div v-if="!section.always_enabled" class="flex gap-2 items-start">
      <div class="ml-1 text-lg w-9em shrink-0">{{ label }}</div>
      <div class="flex flex-col gap-2 w-full">
        <div class="flex gap-2 h-28px items-center">
          <CheckBox v-model:value="isEnabled">
            {{ isEnabled ? $t('mods.on') : $t('mods.off') }}
          </CheckBox>
        </div>
        <div v-if="description" class="text-sm op-80">{{ description }}</div>
      </div>
    </div>

    <div v-if="isEnabled && section.entries?.length" class="flex flex-col gap-2 mt-2">
      <ModEntry
        v-for="entry in section.entries"
        :key="entry.key"
        :entry="entry"
        :section-state="state"
      />
    </div>
  </div>
</template>
