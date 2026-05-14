<script setup lang="ts">
import { computed } from 'vue'
import { CheckBox, NumberInput, TextInput } from '@munet/ui'
import { locale } from '@/locales'
import type { ManifestEntry, ModConfigSection } from '@/client/mod'

const props = defineProps<{
  entry: ManifestEntry
  sectionState: ModConfigSection
}>()

const language = computed(() => locale.value.startsWith('zh') ? 'zh' : 'en')
const label = computed(() => props.entry.label?.[language.value] || props.entry.key)
const description = computed(() => props.entry.description?.[language.value] || '')

const value = computed({
  get: () => props.sectionState.entries[props.entry.key] ?? props.entry.default,
  set: (next) => { props.sectionState.entries[props.entry.key] = next },
})

const numericValue = computed({
  get: () => Number(value.value ?? 0),
  set: (next: number) => { value.value = next },
})
</script>

<template>
  <div class="flex gap-2 items-start pl-4">
    <div class="w-9em shrink-0 text-sm">{{ label }}</div>
    <div class="flex flex-col gap-2 w-full">
      <div class="flex gap-2 h-28px items-center">
        <CheckBox v-if="entry.type === 'bool'" v-model:value="value">
          {{ value ? $t('mods.on') : $t('mods.off') }}
        </CheckBox>
        <NumberInput
          v-else-if="entry.type === 'int'"
          v-model:value="numericValue"
          :min="entry.min"
          :max="entry.max"
          :decimal="0"
          :step="1"
        />
        <NumberInput
          v-else-if="entry.type === 'float'"
          v-model:value="numericValue"
          :min="entry.min"
          :max="entry.max"
          :decimal="4"
          :step="0.1"
        />
        <TextInput v-else v-model:value="value" class="w-full" />
      </div>
      <div v-if="description" class="text-sm op-80">{{ description }}</div>
    </div>
  </div>
</template>
