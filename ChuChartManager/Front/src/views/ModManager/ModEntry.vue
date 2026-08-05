<script setup lang="ts">
import { computed } from 'vue'
import { Button, CheckBox, NumberInput, Select, TextInput } from '@munet/ui'
import { locale } from '@/locales'
import type { ManifestEntry, ModConfigSection } from '@/client/mod'

const props = defineProps<{
  entry: ManifestEntry
  sectionState: ModConfigSection
}>()

const language = computed(() => locale.value.startsWith('zh') ? 'zh' : 'en')
const label = computed(() => props.entry.label?.[language.value] || props.entry.key)
const description = computed(() => props.entry.description?.[language.value] || '')
const options = computed(() => (props.entry.options ?? []).map(option => ({
  value: option.value,
  label: option.label?.[language.value] || String(option.value),
})))

const value = computed({
  get: () => props.sectionState.entries[props.entry.key] ?? props.entry.default,
  set: (next) => { props.sectionState.entries[props.entry.key] = next },
})

const numericValue = computed({
  get: () => Number(value.value ?? 0),
  set: (next: number) => { value.value = next },
})

const boolValue = computed({
  get: () => Boolean(value.value),
  set: (next: boolean) => { value.value = next },
})

const stringValue = computed({
  get: () => String(value.value ?? ''),
  set: (next: string) => { value.value = next },
})

const optionValue = computed({
  get: () => value.value as string | number,
  set: (next: string | number) => { value.value = next },
})

const arrayValue = computed(() => Array.isArray(value.value)
  ? value.value.map(item => String(item ?? ''))
  : [])

function updateArrayItem(index: number, next: string) {
  const items = [...arrayValue.value]
  items[index] = next
  value.value = items
}

function addArrayItem() {
  value.value = [...arrayValue.value, '']
}

function removeArrayItem(index: number) {
  value.value = arrayValue.value.filter((_, itemIndex) => itemIndex !== index)
}
</script>

<template>
  <div class="grid grid-cols-[minmax(8rem,12rem)_minmax(0,1fr)] gap-2 items-start pl-2">
    <div class="min-w-0 text-sm break-words">{{ label }}</div>
    <div class="flex flex-col gap-2 min-w-0">
      <div class="flex gap-2 min-h-28px items-center">
        <Select
          v-if="options.length"
          v-model:value="optionValue"
          :options="options"
          class="w-full max-w-80"
        />
        <CheckBox v-else-if="entry.type === 'bool'" v-model:value="boolValue">
          {{ boolValue ? $t('mods.on') : $t('mods.off') }}
        </CheckBox>
        <NumberInput
          v-else-if="entry.type === 'int'"
          v-model:value="numericValue"
          :min="entry.min"
          :max="entry.max"
          :decimal="0"
          :step="1"
          class="w-full max-w-80"
        />
        <NumberInput
          v-else-if="entry.type === 'float'"
          v-model:value="numericValue"
          :min="entry.min"
          :max="entry.max"
          :decimal="4"
          :step="0.1"
          class="w-full max-w-80"
        />
        <div v-else-if="entry.type === 'string_array'" class="flex flex-col gap-1.5 w-full">
          <div v-for="(item, index) in arrayValue" :key="index" class="flex gap-1.5 items-center">
            <TextInput :value="item" class="flex-1 min-w-0" @update:value="updateArrayItem(index, $event)" />
            <Button
              variant="ghost"
              size="small"
              danger
              class="w-8 px-0! shrink-0"
              :title="$t('mods.removeListItem')"
              :aria-label="$t('mods.removeListItem')"
              @click="removeArrayItem(index)"
            >
              <span class="i-mdi-delete-outline text-4" />
            </Button>
          </div>
          <Button variant="ghost" size="small" class="self-start" @click="addArrayItem">
            <span class="i-mdi-plus mr-1" />{{ $t('mods.addListItem') }}
          </Button>
        </div>
        <TextInput v-else v-model:value="stringValue" class="w-full" />
      </div>
      <div v-if="description" class="text-sm op-80">{{ description }}</div>
    </div>
  </div>
</template>
