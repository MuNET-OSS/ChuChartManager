<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { TextInput } from '@munet/ui'
import { locale } from '@/locales'
import type { Manifest, ManifestSection, ModConfig } from '@/client/mod'
import ModSection from './ModSection.vue'

const props = defineProps<{
  manifest: Manifest
  config: ModConfig
}>()

const { t } = useI18n()
const language = computed(() => locale.value.startsWith('zh') ? 'zh' : 'en')
const selectedGroupId = ref(props.manifest.ui.groups[0]?.id ?? '')
const search = ref('')

const displaySections = computed(() => props.manifest.config.sections.filter(section => !section.hidden))
const sectionsById = computed(() => new Map(displaySections.value.map(section => [section.id, section])))
const groups = computed(() => {
  const result = props.manifest.ui.groups.map(group => ({ ...group }))
  const groupedIds = new Set(result.flatMap(group => group.sections))
  const ungrouped = displaySections.value.filter(section => !groupedIds.has(section.id)).map(section => section.id)
  if (ungrouped.length)
    result.push({ id: '__other', label: { zh: t('mods.other'), en: t('mods.other') }, sections: ungrouped })
  return result.filter(group => group.sections.some(id => sectionsById.value.has(id)))
})
const selectedGroup = computed(() => groups.value.find(group => group.id === selectedGroupId.value) ?? groups.value[0])

const visibleSections = computed(() => {
  const query = search.value.trim().toLowerCase()
  const candidates = query
    ? displaySections.value
    : (selectedGroup.value?.sections ?? [])
      .map(id => sectionsById.value.get(id))
      .filter((section): section is ManifestSection => !!section)
  return candidates
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
      entries: {},
    }
  }
  return props.config[section.id]
}
</script>

<template>
  <div class="flex flex-col md:grid md:grid-cols-[15em_minmax(0,1fr)] gap-0 min-h-0 flex-1">
    <aside class="flex md:flex-col gap-0.5 of-x-auto md:of-y-auto md:of-x-hidden shrink-0 md:h-full pb-1 md:pb-0">
      <div
        v-for="group in groups"
        :key="group.id"
        :class="['px-3 py-1.5 rd cursor-pointer text-sm transition-colors whitespace-nowrap', selectedGroup?.id === group.id ? 'bg-[oklch(0.68_0.17_var(--hue)/0.22)] c-[oklch(0.58_0.17_var(--hue))]' : 'hover:bg-black/5']"
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
            :reveal-advanced="!!search.trim()"
          />
        </div>
      </div>
    </main>
  </div>
</template>
