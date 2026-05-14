<script setup lang="ts">
import { computed, ref } from 'vue'
import { TextInput } from '@munet/ui'
import { locale } from '@/locales'
import type { Manifest, ManifestSection, ModConfig } from '@/client/mod'
import ModSection from './ModSection.vue'

const props = defineProps<{
  manifest: Manifest
  config: ModConfig
}>()

const language = computed(() => locale.value.startsWith('zh') ? 'zh' : 'en')
const selectedGroupId = ref(props.manifest.ui.groups[0]?.id ?? '')
const search = ref('')

const sectionsById = computed(() => new Map(props.manifest.config.sections.map(section => [section.id, section])))
const selectedGroup = computed(() => props.manifest.ui.groups.find(group => group.id === selectedGroupId.value) ?? props.manifest.ui.groups[0])

const visibleSections = computed(() => {
  const query = search.value.trim().toLowerCase()
  const ids = selectedGroup.value?.sections ?? []
  return ids
    .map(id => sectionsById.value.get(id))
    .filter((section): section is ManifestSection => !!section)
    .filter(section => {
      if (!query) return true
      const label = section.label?.[language.value] ?? ''
      const description = section.description?.[language.value] ?? ''
      return [section.id, label, description, ...(section.entries ?? []).map(entry => `${entry.key} ${entry.label?.[language.value] ?? ''}`)]
        .some(text => text.toLowerCase().includes(query))
    })
})

function ensureState(section: ManifestSection) {
  if (!props.config[section.id]) {
    props.config[section.id] = {
      enabled: section.default_enabled,
      entries: Object.fromEntries((section.entries ?? []).map(entry => [entry.key, entry.default])),
    }
  }
  for (const entry of section.entries ?? []) {
    if (!(entry.key in props.config[section.id].entries))
      props.config[section.id].entries[entry.key] = entry.default
  }
  return props.config[section.id]
}
</script>

<template>
  <div class="grid grid-cols-[15em_auto] gap-0 min-h-0 flex-1">
    <aside class="flex flex-col gap-0.5 of-y-auto h-full">
      <div
        v-for="group in manifest.ui.groups"
        :key="group.id"
        :class="['px-3 py-1.5 rd cursor-pointer text-sm transition-colors', selectedGroupId === group.id ? 'bg-[oklch(0.68_0.17_var(--hue)/0.22)] c-[oklch(0.78_0.17_var(--hue))]' : 'hover:bg-white/8']"
        @click="selectedGroupId = group.id"
      >
        {{ group.label?.[language] || group.id }}
      </div>
    </aside>

    <main class="flex flex-col min-h-0">
      <div class="flex gap-2 p-2 shrink-0">
        <TextInput v-model:value="search" :placeholder="$t('mods.search')" class="flex-1" />
      </div>
      <div class="of-y-auto cst flex-1 p-2 pt-0 text-14px">
        <div class="flex flex-col gap-1">
          <ModSection
            v-for="section in visibleSections"
            :key="section.id"
            :section="section"
            :state="ensureState(section)"
          />
        </div>
      </div>
    </main>
  </div>
</template>
