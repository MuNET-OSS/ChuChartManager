<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick } from 'vue'
import { Button, Select, TextInput, CheckBox, DropMenu, NumberInput, Modal, theme } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useStorage } from '@vueuse/core'
import { VList } from 'virtua/vue'
import { getMusicList, getSources, getGenreMap, getJacketUrl, saveMusic, getExportMp3Url, ensureBackendUrl, importJacket, importChart, getExportChartUrl, getExportOptUrl, getExportCustomUrl, openExplorer, openXml, changeId, deleteMusic, setJacket, setAudio, replaceChart, isWebView, getBaseUrl } from '@/api'
import type { MusicListItem } from '@/api'
import { loadMusic as loadPlayerMusic, stop as stopPlayer } from '@/store/player'
import { setStatus } from '@/store/status'
import { leftPanel, selectedSource, optionDirs, selectMusicId } from '@/store/refs'
import OptionDirsManager from '@/views/MusicList/OptionDirsManager/index'
import ImportMusicModal from '@/views/ImportMusicModal.vue'
import PlayerBar from '@/components/PlayerBar.vue'
import BottomOverlay from '@/components/BottomOverlay.vue'
import FileTypeIcon from '@/components/FileTypeIcon.vue'
import { BlobWriter, ZipReader } from '@zip.js/zip.js'
import getSubDirFile from '@/utils/getSubDirFile'
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
  stopPlayer()
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

const isA000 = computed(() => selectedSource.value === 'A000')

const genreFilter = ref<string | number>('-1')
const diffFilter = ref<string | number>('-1')

const editName = ref('')
const editArtist = ref('')
const editGenreId = ref<string | number>('-1')
const editFumens = ref<{ enable: boolean; level: number; levelDecimal: number; notesDesigner: string; noteCount: number }[]>([])
const selectedDiff = ref(0)

const diffNames = ['Basic', 'Advanced', 'Expert', 'Master', 'Ultima', "World's End"]
const diffColors = ['#22BB5B', '#FB9C2D', '#F64861', '#9E45E2', '#1A1A1A', 'linear-gradient(135deg, #FF3C3C, #FFB400, #50DC32, #00B4FF, #783CFF, #DC32C8)']
const diffFgColors = ['#FFF', '#FFF', '#FFF', '#FFF', '#FFF', '#FFF']

const levelOptions: SelectOption[] = Array.from({ length: 16 }, (_, i) => ({ label: String(i), value: i }))

const editConstant = computed({
  get: () => {
    const f = editFumens.value[selectedDiff.value]
    return f ? f.level + f.levelDecimal / 100 : 0
  },
  set: (v: number) => {
    const f = editFumens.value[selectedDiff.value]
    if (!f) return
    f.level = Math.floor(v)
    f.levelDecimal = Math.round((v - f.level) * 100)
  },
})

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
    level: f?.level ?? 0,
    levelDecimal: f?.levelDecimal ?? 0,
    notesDesigner: f?.notesDesigner ?? '',
    noteCount: (f as any)?.noteCount ?? 0,
  }))
  selectedDiff.value = Math.max(0, music.fumens.findIndex(f => f?.enable))
  loadPlayerMusic(music)
}

async function onSave() {
  if (!selectedMusic.value) return
  const m = selectedMusic.value
  const fumens = editFumens.value.map((f, i) => {
    return { index: i, enable: f.enable, level: f.level, levelDecimal: f.levelDecimal, notesDesigner: f.notesDesigner }
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

async function exportToFolder(url: string) {
  if (!selectedMusic.value) return
  let folderHandle: FileSystemDirectoryHandle
  try {
    folderHandle = await window.showDirectoryPicker({ id: 'ccmExportDir', mode: 'readwrite' })
  } catch { return }

  try {
    const res = await fetch(url)
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`)
    const zipReader = new ZipReader(res.body!)
    try {
      const entries = zipReader.getEntriesGenerator()
      for await (const entry of entries) {
        if (entry.filename.endsWith('/')) continue
        if (!('getData' in entry)) continue
        const fileHandle = await getSubDirFile(folderHandle, entry.filename)
        const writable = await fileHandle.createWritable()
        try {
          const blob = await entry.getData!(new BlobWriter())
          await writable.write(blob)
        } finally {
          await writable.close()
        }
      }
      setStatus(t('music.exportSuccess'))
    } finally {
      await zipReader.close()
    }
  } catch (e: any) {
    setStatus(t('music.exportFailed', { error: e?.message || e }))
  }
}

async function handleSetJacket() {
  if (!selectedMusic.value) return
  let fileHandle: FileSystemFileHandle
  try {
    ;[fileHandle] = await window.showOpenFilePicker({
      id: 'ccmJacket',
      types: [{ description: 'Image', accept: { 'image/*': ['.png', '.jpg', '.jpeg', '.bmp'] } }],
    })
  } catch { return }
  const file = await fileHandle.getFile()
  await setJacket(selectedMusic.value.id, selectedMusic.value.assetDir, file)
  selectedMusic.value.hasJacket = true
  setStatus(t('music.jacketImported'))
}

const showAudioOverlay = ref(false)

async function handleReplaceAudio() {
  if (!selectedMusic.value) return
  showAudioOverlay.value = true
  let fileHandle: FileSystemFileHandle
  try {
    ;[fileHandle] = await window.showOpenFilePicker({
      id: 'ccmAudio',
      types: [{ description: '支持的文件类型', accept: { 'application/octet-stream': ['.wav', '.mp3', '.ogg', '.awb'] } }],
    })
  } catch {
    showAudioOverlay.value = false
    return
  }
  showAudioOverlay.value = false
  const file = await fileHandle.getFile()
  setStatus(t('music.audioImporting'))
  try {
    await setAudio(selectedMusic.value.id, selectedMusic.value.assetDir, file)
    setStatus(t('music.audioReplaced'))
  } catch (e: any) {
    setStatus(t('music.audioReplaceFailed', { error: e?.response?.data || e?.message }))
  }
}

const showChartOverlay = ref(false)

async function handleReplaceChart(diffIndex: number) {
  if (!selectedMusic.value) return
  showChartOverlay.value = true
  let fileHandle: FileSystemFileHandle
  try {
    ;[fileHandle] = await window.showOpenFilePicker({
      id: 'ccmChart',
      types: [{ description: 'Chart', accept: { 'application/octet-stream': ['.c2s', '.ugc', '.sus'] } }],
    })
  } catch {
    showChartOverlay.value = false
    return
  }
  showChartOverlay.value = false
  const file = await fileHandle.getFile()
  try {
    const res = await replaceChart(selectedMusic.value.id, selectedMusic.value.assetDir, diffIndex, file)
    if (res.imported) {
      const suffix = res.convertedFrom ? ` (${res.convertedFrom.toUpperCase()} → C2S)` : ''
      setStatus(t('music.chartImported', { diff: diffNames[diffIndex], suffix }))
      await loadMusic()
      selectMusic(selectedMusic.value)
    }
  } catch (e: any) {
    setStatus(t('music.chartReplaceFailed', { error: e?.response?.data?.error || e?.message }))
  }
}

function handleExportChart(diffIndex: number, format: 'c2s' | 'ugc' | 'sus') {
  if (!selectedMusic.value) return
  window.open(getExportChartUrl(selectedMusic.value.id, selectedMusic.value.assetDir, diffIndex, format), '_blank')
}

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

const deleteConfirm = ref(false)
const deleteLoading = ref(false)

async function handleDelete() {
  if (!selectedMusic.value) return
  if (!deleteConfirm.value) {
    deleteConfirm.value = true
    return
  }
  deleteConfirm.value = false
  deleteLoading.value = true
  try {
    await deleteMusic(selectedMusic.value.id, selectedMusic.value.assetDir)
    selectedMusic.value = null
    await refresh()
    setStatus(t('music.deleted'))
  } catch (e: any) {
    setStatus(t('music.deleteFailed', { error: e?.response?.data || e?.message }))
  } finally {
    deleteLoading.value = false
  }
}

const showChangeId = ref(false)
const newMusicId = ref(0)
const changeIdLoading = ref(false)

watch(showChangeId, (val) => {
  if (val && selectedMusic.value) newMusicId.value = selectedMusic.value.id
})

async function handleChangeId() {
  if (!selectedMusic.value || newMusicId.value === selectedMusic.value.id) return
  changeIdLoading.value = true
  try {
    await changeId(selectedMusic.value.id, selectedMusic.value.assetDir, newMusicId.value)
    await refresh()
    setStatus(t('music.idChanged', { oldId: selectedMusic.value.id, newId: newMusicId.value }))
    showChangeId.value = false
  } catch (e: any) {
    setStatus(t('music.idChangeFailed', { error: e?.response?.data || e?.message }))
  } finally {
    changeIdLoading.value = false
  }
}

const copyExportOptions = computed(() => {
  if (!selectedMusic.value) return []
  const m = selectedMusic.value
  const opts: { label: string; action: () => void }[] = []

  opts.push({
    label: t('music.copyTo'),
    action: () => exportToFolder(getExportOptUrl(m.id, m.assetDir)),
  })

  opts.push({
    label: t('music.exportZip'),
    action: () => window.open(getExportOptUrl(m.id, m.assetDir)),
  })

  opts.push({
    label: t('music.exportUgc'),
    action: () => exportToFolder(getExportCustomUrl(m.id, m.assetDir, 'ugc')),
  })

  opts.push({
    label: t('music.exportZipUgc'),
    action: () => window.open(getExportCustomUrl(m.id, m.assetDir, 'ugc')),
  })

  opts.push({
    label: t('music.exportSus'),
    action: () => exportToFolder(getExportCustomUrl(m.id, m.assetDir, 'sus')),
  })

  if (!isA000.value) {
    opts.push({
      label: t('music.changeId'),
      action: () => { showChangeId.value = true },
    })
  }

  if (isWebView) {
    opts.push({
      label: t('music.openExplorer'),
      action: () => openExplorer(m.id, m.assetDir),
    })
    opts.push({
      label: t('music.openXml'),
      action: () => openXml(m.id, m.assetDir),
    })
  }

  return opts
})
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
              @click="selectMusic(music)"
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

    <!-- 右侧 -->
    <div class="flex-1 flex flex-col of-hidden">
      <div class="flex items-center gap-2 shrink-0 p-3 border-b border-white/10">
        <div class="grow-1" />
        <template v-if="selectedMusic">
          <DropMenu :options="copyExportOptions" :button-text="t('music.copyAndExport')" />
          <template v-if="isA000">
            <span class="text-sm op-50">{{ t('music.a000Hint') }}</span>
          </template>
          <template v-else>
            <Button :class="deleteConfirm && 'bg-red-300!'" :ing="deleteLoading" @click="handleDelete" @mouseleave="deleteConfirm = false">{{ deleteConfirm ? t('music.deleteConfirm') : t('common.delete') }}</Button>
            <Button @click="onSave">{{ t('common.save') }}</Button>
          </template>
        </template>
        <template v-else-if="isA000">
          <span class="text-sm op-50">{{ t('music.a000Hint') }}</span>
        </template>
        <ImportMusicModal v-if="!isA000" @imported="refresh" />
      </div>

      <div v-if="selectedMusic" class="of-y-auto cst flex-1 min-h-0 p-6">
        <div class="flex gap-6 mb-6">
          <img v-if="selectedMusic.hasJacket" :src="getJacketUrl(selectedMusic.id, selectedMusic.assetDir)" class="w-48 h-48 rounded-lg object-cover shrink-0 cursor-pointer hover:op-80 transition-opacity" :title="t('music.clickToReplaceJacket')" @click="!isA000 && handleSetJacket()" />
          <div v-else class="w-48 h-48 rounded-lg bg-white/10 flex items-center justify-center op-30 text-2xl shrink-0 cursor-pointer hover:op-50 transition-opacity" @click="!isA000 && handleSetJacket()">?</div>
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

        <div class="flex items-center gap-2 mt-4">
          <PlayerBar class="grow" />
          <Button v-if="!isA000" class="ws-nowrap shrink-0" @click="handleReplaceAudio">{{ t('music.replaceAudio') }}</Button>
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
          <div v-if="editFumens[selectedDiff]" class="space-y-2 rounded-b-lg p-4 relative" :style="getDiffPanelStyle(selectedDiff)">
            <div class="flex items-center gap-2">
              <CheckBox v-model:value="editFumens[selectedDiff].enable" :disabled="isA000" /><span class="text-sm">{{ t('music.enableDiff') }}</span>
            </div>
            <div v-if="!isA000" class="absolute right-0 top-0 m-4 z-2 flex gap-2">
              <Button @click="handleReplaceChart(selectedDiff)">{{ t('music.replaceChart') }}</Button>
            </div>
            <div><label class="block text-sm op-60 mb-1">{{ t('music.chartAuthor') }}</label><TextInput v-model:value="editFumens[selectedDiff].notesDesigner" :disabled="isA000" /></div>
            <div><label class="block text-sm op-60 mb-1">{{ t('music.chartLevel') }}</label><Select :options="levelOptions" v-model:value="editFumens[selectedDiff].level" :disabled="isA000" /></div>
            <div><label class="block text-sm op-60 mb-1">{{ t('music.chartConstant') }}</label><NumberInput v-model:value="editConstant" :step="0.1" :min="0" :max="15.9" class="w-full" :disabled="isA000" /></div>
            <div><label class="block text-sm op-60 mb-1">{{ t('music.chartNoteCount') }}</label><NumberInput v-model:value="editFumens[selectedDiff].noteCount" :min="0" class="w-full" disabled /></div>
          </div>
        </div>
      </div>
      <div v-else class="flex-1 flex items-center justify-center op-30 text-lg">{{ t('music.selectHint') }}</div>
    </div>

    <Modal v-model:show="showChangeId" :title="t('music.changeId')" width="min(30vw,25em)">
      <div class="flex flex-col gap-3">
        <div>
          <label class="block text-sm op-60 mb-1">{{ t('music.newId') }}</label>
          <NumberInput v-model:value="newMusicId" :min="1" :max="99999" class="w-full" />
        </div>
      </div>
      <template #actions>
        <Button class="w-0 grow" @click="handleChangeId" :disabled="!selectedMusic || newMusicId === selectedMusic.id" :ing="changeIdLoading">{{ t('common.confirm') }}</Button>
      </template>
    </Modal>

    <BottomOverlay :show="showAudioOverlay" :title="t('music.importSelectAudio')">
      <div class="flex gap-10 justify-center text-white text-4em">
        <FileTypeIcon type="WAV" />
        <FileTypeIcon type="MP3" />
        <FileTypeIcon type="OGG" />
        <FileTypeIcon type="AWB" />
      </div>
    </BottomOverlay>

    <BottomOverlay :show="showChartOverlay" :title="t('music.importSelectChart')">
      <div class="flex gap-10 justify-center text-white text-4em">
        <FileTypeIcon type="C2S" />
        <FileTypeIcon type="UGC" />
        <FileTypeIcon type="SUS" />
      </div>
    </BottomOverlay>
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
