<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick } from 'vue'
import { Button, Select, TextInput, CheckBox, theme } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useStorage } from '@vueuse/core'
import { VList } from 'virtua/vue'
import { getMusicList, getSources, getGenreMap, getJacketUrl, saveMusic, getExportMp3Url, ensureBackendUrl, copyMusic, importJacket, importChart, getExportChartUrl } from '@/api'
import type { MusicListItem } from '@/api'
import { play } from '@/store/player'
import { setStatus } from '@/store/status'
import { leftPanel, selectedSource, optionDirs, selectMusicId } from '@/store/refs'
import OptionDirsManager from '@/views/MusicList/OptionDirsManager/index'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

type SortMode = 'id' | 'name'

const sources = ref<string[]>([])
const savedSource = ref('A000')
const musicSortMode = useStorage<SortMode>('ccm-sort-mode', 'id')

let loadingMusic = false
let musicLoadedResolve: (() => void) | null = null

function waitForMusicLoaded(): Promise<void> {
  if (!loadingMusic) return Promise.resolve()
  return new Promise(r => { musicLoadedResolve = r })
}

watch(() => selectedSource.value, async (val) => {
  if (!val) return
  savedSource.value = val
  if (loadingMusic) return
  loadingMusic = true
  try {
    musicList.value = await getMusicList(val)
    selectedMusic.value = null
    setStatus(`${val}: ${t('music.trackCount', { count: musicList.value.length })}`)
  } finally {
    loadingMusic = false
    musicLoadedResolve?.()
    musicLoadedResolve = null
  }
})

const musicList = ref<MusicListItem[]>([])
const genreMap = ref<Record<number, string>>({})
const selectedMusic = ref<MusicListItem | null>(null)
const loading = ref(true)

const isA000 = computed(() => selectedMusic.value?.assetDir === 'A000')

const genreFilter = ref<string | number>('-1')
const diffFilter = ref<string | number>('-1')

const editName = ref('')
const editArtist = ref('')
const editGenreId = ref<string | number>('-1')
const editFumens = ref<{ enable: boolean; level: string; notesDesigner: string }[]>([])
const selectedDiff = ref(0)

const diffNames = ['Basic', 'Advanced', 'Expert', 'Master', 'Ultima', "World's End"]
const diffColors = ['#22BB5B', '#FB9C2D', '#F64861', '#9E45E2', '#1A1A1A', 'linear-gradient(135deg, #FF3C3C, #FFB400, #50DC32, #00B4FF, #783CFF, #DC32C8)']
const diffFgColors = ['#FFF', '#FFF', '#FFF', '#FFF', '#FFF', '#FFF']

const genreFilterOptions = computed<SelectOption[]>(() => [
  { label: t('music.allGenres'), value: '-1' },
  ...Object.entries(genreMap.value).map(([id, name]) => ({ label: name, value: id }))
])

const diffFilterOptions = computed<SelectOption[]>(() => [
  { label: t('music.allDifficulties'), value: '-1' },
  { label: t('music.hasUltima'), value: '4' },
  { label: t('music.worldsEnd'), value: '5' },
])

const genreEditOptions = computed<SelectOption[]>(() =>
  Object.entries(genreMap.value).map(([id, name]) => ({ label: name, value: id }))
)

const sortOptions = computed<SelectOption[]>(() => [
  { label: t('music.sortById'), value: 'id' },
  { label: t('music.sortByName'), value: 'name' },
])

const filteredList = computed(() => {
  let list = musicList.value
  const gf = Number(genreFilter.value)
  const df = Number(diffFilter.value)
  if (gf >= 0) list = list.filter(m => m.genreId === gf)
  if (df >= 0) list = list.filter(m => m.fumens[df]?.enable)

  const sorted = [...list]
  if (musicSortMode.value === 'name')
    sorted.sort((a, b) => a.name.localeCompare(b.name))
  else
    sorted.sort((a, b) => a.id - b.id)
  return sorted
})

onMounted(async () => {
  await ensureBackendUrl()
  const [srcList, genres] = await Promise.all([getSources(), getGenreMap()])
  sources.value = srcList
  genreMap.value = genres
  if (srcList.length > 0) {
    const restored = srcList.includes(savedSource.value) ? savedSource.value : srcList[0]
    selectedSource.value = restored
    musicList.value = await getMusicList(restored)
  }
  loading.value = false
  setStatus(t('music.statusLine', { tracks: musicList.value.length, options: sources.value.length }))
})

async function loadMusic() {
  musicList.value = await getMusicList(selectedSource.value)
}

async function refresh() {
  const [srcList, genres] = await Promise.all([getSources(), getGenreMap()])
  sources.value = srcList
  genreMap.value = genres
  if (srcList.length > 0) {
    const restored = srcList.includes(selectedSource.value) ? selectedSource.value : srcList[0]
    selectedSource.value = restored
    musicList.value = await getMusicList(restored)
  }
  setStatus(t('music.statusLine', { tracks: musicList.value.length, options: sources.value.length }))
}

defineExpose({ refresh })

const vlistRef = ref<InstanceType<typeof VList> | null>(null)

watch(selectMusicId, async (id) => {
  if (id < 0) return
  selectMusicId.value = -1
  leftPanel.value = 'musicList'
  await waitForMusicLoaded()
  await nextTick()
  const idx = filteredList.value.findIndex(m => m.id === id)
  if (idx >= 0) {
    selectMusic(filteredList.value[idx])
    vlistRef.value?.scrollToIndex(idx)
  }
})

function selectMusic(music: MusicListItem) {
  selectedMusic.value = music
  editName.value = music.name
  editArtist.value = music.artist
  editGenreId.value = String(music.genreId)
  editFumens.value = music.fumens.map(f => ({
    enable: f?.enable ?? false,
    level: f ? f.levelDecimal >= 70 ? `${f.level}+` : f.levelDecimal > 0 ? `${f.level}.${f.levelDecimal / 10}` : `${f.level}` : '0',
    notesDesigner: f?.notesDesigner ?? '',
  }))
  selectedDiff.value = Math.max(0, music.fumens.findIndex(f => f?.enable))
}

function parseLevel(text: string): { level: number; dec: number } {
  text = text.trim()
  if (text.endsWith('+')) {
    const lv = parseInt(text.slice(0, -1))
    return isNaN(lv) ? { level: 0, dec: 0 } : { level: lv, dec: 70 }
  }
  if (text.includes('.')) {
    const [l, d] = text.split('.')
    return { level: parseInt(l) || 0, dec: (parseInt(d) || 0) * 10 }
  }
  return { level: parseInt(text) || 0, dec: 0 }
}

async function onSave() {
  if (!selectedMusic.value) return
  const m = selectedMusic.value
  const fumens = editFumens.value.map((f, i) => {
    const { level, dec } = parseLevel(f.level)
    return { index: i, enable: f.enable, level, levelDecimal: dec, notesDesigner: f.notesDesigner }
  })
  await saveMusic(m.id, m.assetDir, {
    name: editName.value,
    artist: editArtist.value,
    genreId: Number(editGenreId.value),
    genreName: genreMap.value[Number(editGenreId.value)] || '',
    fumens,
  })
  m.name = editName.value
  m.artist = editArtist.value
  m.genreId = Number(editGenreId.value)
  fumens.forEach(fd => {
    const f = m.fumens[fd.index]
    if (f) { f.enable = fd.enable; f.level = fd.level; f.levelDecimal = fd.levelDecimal; f.notesDesigner = fd.notesDesigner }
  })
  setStatus(t('music.saved', { name: m.name }))
}

function exportMp3() {
  if (selectedMusic.value) window.open(getExportMp3Url(selectedMusic.value.id, selectedMusic.value.assetDir))
}

const copyTargetDir = ref('')

async function handleCopyTo() {
  if (!selectedMusic.value || !copyTargetDir.value) return
  try {
    await copyMusic(selectedMusic.value.id, selectedMusic.value.assetDir, copyTargetDir.value)
    setStatus(t('music.copiedTo', { dir: copyTargetDir.value }))
  } catch (e: any) {
    setStatus(t('music.copyFailed', { error: e?.response?.data || e?.message }))
  }
}

async function handleImportJacket() {
  if (!selectedMusic.value) return
  const res = await importJacket(selectedMusic.value.id, selectedMusic.value.assetDir)
  if (res.imported) {
    selectedMusic.value.hasJacket = true
    setStatus(t('music.jacketImported'))
  }
}

async function handleImportChart(diffIndex: number) {
  if (!selectedMusic.value) return
  const res = await importChart(selectedMusic.value.id, selectedMusic.value.assetDir, diffIndex)
  if (res.imported) {
    const suffix = res.convertedFrom ? ` (${res.convertedFrom.toUpperCase()} → C2S)` : ''
    setStatus(t('music.chartImported', { diff: diffNames[diffIndex], suffix }))
    await loadMusic()
    selectMusic(selectedMusic.value)
  }
}

function handleExportChart(diffIndex: number, format: 'c2s' | 'ugc' | 'sus') {
  if (!selectedMusic.value) return
  window.open(getExportChartUrl(selectedMusic.value.id, selectedMusic.value.assetDir, diffIndex, format), '_blank')
}

const copyDirOptions = computed(() =>
  optionDirs.value
    .filter(d => d.dirName !== 'A000' && d.dirName !== selectedMusic.value?.assetDir)
    .map(d => ({ label: d.dirName, value: d.dirName }))
)

function getDiffBadgeStyle(i: number) {
  const bg = diffColors[i]
  if (bg.startsWith('linear')) return { background: bg, color: diffFgColors[i] }
  return { backgroundColor: bg, color: diffFgColors[i] }
}

const diffBgColors = ['#22BB5B', '#FB9C2D', '#F64861', '#9E45E2', '#1A1A1A', '']

function getDiffTabStyle(i: number) {
  const selected = selectedDiff.value === i
  if (i === 5) {
    return {
      background: selected
        ? 'linear-gradient(135deg, #FF3C3C, #FFB400, #50DC32, #00B4FF, #783CFF, #DC32C8)'
        : 'linear-gradient(135deg, #FF3C3C66, #FFB40066, #50DC3266, #00B4FF66, #783CFF66, #DC32C866)',
    }
  }
  const color = diffBgColors[i]
  return {
    backgroundColor: `color-mix(in srgb, ${color}, transparent ${selected ? 0 : 40}%)`,
  }
}

function getDiffPanelStyle(i: number) {
  if (i === 5) {
    return {
      background: 'linear-gradient(135deg, #FF3C3C18, #FFB40018, #50DC3218, #00B4FF18, #783CFF18, #DC32C818)',
    }
  }
  const color = diffBgColors[i]
  return {
    backgroundColor: `color-mix(in srgb, ${color}, transparent 90%)`,
  }
}
</script>

<template>
  <div class="h-full flex">
    <div class="w-40em max-w-[40vw] border-r border-white/10 flex flex-col relative">
      <Transition
        enter-active-class="panel-transition"
        leave-active-class="panel-transition absolute inset-0"
        :enter-from-class="leftPanel === 'optionDirs' ? 'opacity-0 scale-104' : 'opacity-0 scale-96'"
        :leave-to-class="leftPanel === 'optionDirs' ? 'opacity-0 scale-96' : 'opacity-0 scale-104'"
      >
      <div v-if="leftPanel === 'musicList'" key="musicList" class="flex flex-col h-full">
        <div class="p-2 border-b border-white/10 flex flex-col gap-1.5 z-10 relative">
          <div class="flex items-center gap-2 min-w-0">
            <div
              :class="['grow w-0 flex items-center gap-1 px-3 py-1.5 rounded-12px transition-colors text-left truncate cursor-pointer border-none h-48px', theme.listItem, theme.listItemHover]"
              @click="leftPanel = 'optionDirs'"
            >
              <span class="truncate">{{ selectedSource || 'A000' }}</span>
            </div>
            <Select :options="sortOptions" v-model:value="musicSortMode" class="w-40! shrink-0" />
          </div>
          <div class="flex gap-1.5">
            <Select :options="genreFilterOptions" v-model:value="genreFilter" />
            <Select :options="diffFilterOptions" v-model:value="diffFilter" />
          </div>
        </div>
        <VList ref="vlistRef" class="flex-1 cst" :data="filteredList">
          <template #default="{ item: music }">
            <div
              :key="`${music.assetDir}-${music.id}`"
              :class="['flex gap-5 h-20 w-full p-2 my-1 rd-md relative cursor-pointer transition-colors', theme.listItemHover, (selectedMusic?.id === music.id && selectedMusic?.assetDir === music.assetDir) && theme.listItem]"
              @click="selectMusic(music)" @dblclick="play(music)"
            >
              <img v-if="music.hasJacket" :src="getJacketUrl(music.id, music.assetDir)" class="h-16 w-16 object-fill shrink-0" loading="lazy" />
              <div v-else class="h-16 w-16 shrink-0 bg-white/10 flex items-center justify-center text-xs op-40 rd">?</div>
              <div class="flex flex-col grow-1 w-0">
                <div class="text-xs op-50">{{ String(music.id).padStart(4, '0') }}</div>
                <div class="text-ellipsis of-hidden ws-nowrap">{{ music.name }}</div>
                <div class="flex gap-1 mt-auto items-center">
                  <template v-for="(f, i) in music.fumens" :key="i">
                    <span v-if="f?.enable" class="rounded-full px-2 text-sm leading-6 font-medium" :style="getDiffBadgeStyle(i)">{{ i === 5 && music.worldsEndTag ? music.worldsEndTag : f.levelDisplay }}</span>
                  </template>
                </div>
              </div>
            </div>
          </template>
        </VList>
        <div v-if="!loading && filteredList.length === 0" class="p-4 text-center op-50">{{ t('music.noData') }}</div>
      </div>
      <OptionDirsManager v-else key="optionDirs" />
      </Transition>
    </div>

    <!-- 右侧详情 -->
    <div class="flex-1 flex flex-col of-hidden" v-if="selectedMusic">
      <div class="flex items-center gap-2 shrink-0 p-3 border-b border-white/10">
        <span v-if="isA000" class="text-sm op-50">{{ t('music.a000Hint') }}</span>
        <div class="grow-1" />
        <Button @click="play(selectedMusic)">{{ t('music.play') }}</Button>
        <Button @click="exportMp3">{{ t('music.exportMp3') }}</Button>
        <Button v-if="!isA000" @click="handleImportJacket">{{ t('music.importJacket') }}</Button>
      </div>
      <div class="of-y-auto cst flex-1 min-h-0 p-6">
        <div class="flex gap-6 mb-6">
          <img v-if="selectedMusic.hasJacket" :src="getJacketUrl(selectedMusic.id, selectedMusic.assetDir)" class="w-48 h-48 rounded-lg object-cover shrink-0" />
          <div v-else class="w-48 h-48 rounded-lg bg-white/10 flex items-center justify-center op-30 text-2xl shrink-0">?</div>
          <div class="flex-1 min-w-0">
            <h2 class="text-xl font-bold mb-1">{{ selectedMusic.name }}</h2>
            <p class="op-60 mb-2">{{ selectedMusic.artist }}</p>
            <p class="text-sm op-40 mb-3">{{ genreMap[selectedMusic.genreId] || '' }}</p>
            <div class="flex gap-2 flex-wrap items-center">
              <template v-for="(f, i) in selectedMusic.fumens" :key="i">
                <span v-if="f?.enable" class="rounded-full px-2.5 py-0.5 text-sm font-medium" :style="getDiffBadgeStyle(i)">{{ diffNames[i] }} {{ i === 5 && selectedMusic.worldsEndTag ? selectedMusic.worldsEndTag : f.levelDisplay }}</span>
              </template>
            </div>
          </div>
        </div>

        <div class="space-y-3">
          <div><label class="block text-sm op-60 mb-1">{{ t('music.songTitle') }}</label><TextInput v-model:value="editName" :disabled="isA000" /></div>
          <div><label class="block text-sm op-60 mb-1">{{ t('music.artist') }}</label><TextInput v-model:value="editArtist" :disabled="isA000" /></div>
          <div><label class="block text-sm op-60 mb-1">{{ t('music.genre') }}</label><Select :options="genreEditOptions" v-model:value="editGenreId" :disabled="isA000" /></div>
        </div>

        <div class="mt-6">
          <div class="diff-tabs-grid">
            <span />
            <button v-for="(_, i) in 6" :key="i" class="diff-tab" :class="{ 'diff-tab-active': selectedDiff === i }" :style="getDiffTabStyle(i)" @click="selectedDiff = i">
              <div v-if="!editFumens[i]?.enable" class="diff-tab-disabled-overlay" />
              <span class="z-1 relative">{{ diffNames[i] }}</span>
            </button>
            <span />
          </div>
          <div v-if="editFumens[selectedDiff]" class="space-y-2 rounded-b-lg p-4" :style="getDiffPanelStyle(selectedDiff)">
            <div class="flex items-center gap-2">
              <CheckBox v-model:value="editFumens[selectedDiff].enable" :disabled="isA000" /><span class="text-sm">{{ t('music.enableDiff') }}</span>
            </div>
            <div><label class="block text-sm op-60 mb-1">{{ t('music.level') }}</label><TextInput v-model:value="editFumens[selectedDiff].level" :disabled="isA000" /></div>
            <div><label class="block text-sm op-60 mb-1">{{ t('music.notesDesigner') }}</label><TextInput v-model:value="editFumens[selectedDiff].notesDesigner" :disabled="isA000" /></div>
            <div class="flex gap-2 mt-3 pt-3 border-t border-white/10">
              <Button v-if="!isA000" @click="handleImportChart(selectedDiff)">{{ editFumens[selectedDiff].enable ? t('music.replaceChart') : t('music.importChart') }} (C2S/UGC/SUS)</Button>
              <Button v-if="editFumens[selectedDiff].enable" @click="handleExportChart(selectedDiff, 'c2s')">{{ t('music.exportC2S') }}</Button>
              <Button v-if="editFumens[selectedDiff].enable" @click="handleExportChart(selectedDiff, 'ugc')">{{ t('music.exportUGC') }}</Button>
              <Button v-if="editFumens[selectedDiff].enable" @click="handleExportChart(selectedDiff, 'sus')">{{ t('music.exportSUS') }}</Button>
            </div>
          </div>
        </div>

        <div v-if="!isA000" class="mt-4"><Button @click="onSave">{{ t('music.save') }}</Button></div>

        <div class="mt-6 border-t border-white/10 pt-4" v-if="copyDirOptions.length > 0">
          <label class="block text-sm op-60 mb-2">{{ t('music.copyToOption') }}</label>
          <div class="flex gap-2 items-center">
            <Select :options="copyDirOptions" v-model:value="copyTargetDir" class="flex-1" />
            <Button @click="handleCopyTo">{{ t('music.copy') }}</Button>
          </div>
        </div>
      </div>
    </div>

    <div v-else class="flex-1 flex items-center justify-center op-30 text-lg">{{ t('music.selectHint') }}</div>
  </div>
</template>

<style lang="sass" scoped>
.panel-transition
  transition: opacity 0.35s ease, transform 0.35s ease

.diff-tabs-grid
  display: grid
  grid-template-columns: 0.5em repeat(6, 1fr) 0.5em
  gap: 1em
  width: 100%
  align-items: end
  height: 4em

.diff-tab
  width: 100%
  padding: 12px 0
  display: flex
  justify-content: center
  border-radius: 0.5em 0.5em 0 0
  position: relative
  overflow: hidden
  cursor: pointer
  border: none
  transition: background-color 0.3s, padding-bottom 0.3s

  &.diff-tab-active
    color: white
    font-weight: 500
    padding-bottom: 16px

  .diff-tab-disabled-overlay
    position: absolute
    top: 0
    bottom: 0
    left: 0
    right: 0
    background: repeating-linear-gradient(-45deg, rgba(255,255,255,0.3) 0, rgba(255,255,255,0.3) 5%, rgba(255,255,255,0.05) 5%, rgba(255,255,255,0.05) 10%)
</style>
