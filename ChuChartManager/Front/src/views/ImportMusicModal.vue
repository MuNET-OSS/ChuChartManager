<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { Button, TextInput, NumberInput, Select, Modal, addToast } from '@munet/ui'
import { getGenreMap, getSources, importMusicCheck, importMusicExecute } from '@/api'
import type { ImportCheckResult } from '@/api'
import { selectedSource } from '@/store/refs'
import BottomOverlay from '@/components/BottomOverlay.vue'
import FileTypeIcon from '@/components/FileTypeIcon.vue'

const emit = defineEmits<{ imported: [] }>()
const { t } = useI18n()

const loading = ref(false)
const step = ref<'idle' | 'picking' | 'checking' | 'form' | 'executing'>('idle')

const chartFiles = ref<File[]>([])
const audioFile = ref<File | null>(null)
const coverFile = ref<File | null>(null)
const checkResult = ref<ImportCheckResult | null>(null)

const title = ref('')
const artist = ref('')
const genreId = ref(0)
const targetDir = ref('')
const musicId = ref(8000)

const diffNames = ['BASIC', 'ADVANCED', 'EXPERT', 'MASTER', 'ULTIMA']

const genreMap = ref<Record<number, string>>({})
const sources = ref<string[]>([])

const showForm = computed(() => step.value === 'form' || step.value === 'executing')
const coverUrl = ref('')

let currentCoverUrl: string | null = null
watch(coverFile, (file) => {
  if (currentCoverUrl) {
    URL.revokeObjectURL(currentCoverUrl)
    currentCoverUrl = null
  }
  if (file) {
    currentCoverUrl = URL.createObjectURL(file)
    coverUrl.value = currentCoverUrl
  } else {
    coverUrl.value = ''
  }
})

onUnmounted(() => {
  if (currentCoverUrl) {
    URL.revokeObjectURL(currentCoverUrl)
  }
})

const genreOptions = computed(() =>
  Object.entries(genreMap.value).map(([id, name]) => ({ label: name, value: Number(id) }))
)
const sourceOptions = computed(() =>
  sources.value.filter(s => s !== 'A000').map(s => ({ label: s, value: s }))
)

onMounted(async () => {
  const [g, s] = await Promise.all([getGenreMap(), getSources()])
  genreMap.value = g
  sources.value = s
  if (sourceOptions.value.length > 0) {
    const current = selectedSource.value
    targetDir.value = sourceOptions.value.find(s => s.value === current)?.value ?? sourceOptions.value[0].value
  }
})

function findAllByExt(files: File[], exts: string[]): File[] {
  return files.filter(f => exts.some(e => f.name.toLowerCase().endsWith(e)))
}

function findByExt(files: File[], exts: string[]): File | null {
  return files.find(f => exts.some(e => f.name.toLowerCase().endsWith(e))) ?? null
}

function parseFolderName(name: string) {
  const cleaned = name.replace(/\s*\(v\d+\)\s*$/, '').trim()
  const sep = cleaned.indexOf(' - ')
  if (sep > 0) {
    title.value = cleaned.substring(0, sep).trim()
    artist.value = cleaned.substring(sep + 3).trim()
  } else {
    title.value = cleaned
    artist.value = ''
  }
}

async function startImport() {
  step.value = 'picking'
  let dirHandle: FileSystemDirectoryHandle
  try {
    dirHandle = await window.showDirectoryPicker({ id: 'import-chart', startIn: 'downloads' })
  } catch {
    step.value = 'idle'
    return
  }

  step.value = 'checking'

  const files: File[] = []
  for await (const entry of dirHandle.values()) {
    if (entry.kind === 'file') files.push(await (entry as FileSystemFileHandle).getFile())
  }

  chartFiles.value = findAllByExt(files, ['.ugc', '.c2s', '.sus'])
  audioFile.value = findByExt(files, ['.wav', '.mp3', '.ogg'])
  coverFile.value = findByExt(files, ['.png', '.jpg', '.jpeg'])

  if (chartFiles.value.length === 0) {
    addToast({ message: t('music.importNoChart'), type: 'error' })
    step.value = 'idle'
    return
  }
  if (!audioFile.value) {
    addToast({ message: t('music.importNoAudio'), type: 'error' })
    step.value = 'idle'
    return
  }

  try {
    checkResult.value = await importMusicCheck(chartFiles.value)
    musicId.value = checkResult.value.suggestedId
    if (checkResult.value.title) title.value = checkResult.value.title
    else parseFolderName(dirHandle.name)
    if (checkResult.value.artist) artist.value = checkResult.value.artist
    step.value = 'form'
  } catch (e: any) {
    addToast({ message: e.message || t('music.importFailed'), type: 'error' })
    step.value = 'idle'
  }
}

async function doImport() {
  if (chartFiles.value.length === 0 || !audioFile.value) return
  step.value = 'executing'
  loading.value = true

  try {
    const genreName = genreMap.value[genreId.value] || ''
    const result = await importMusicExecute({
      charts: chartFiles.value,
      audio: audioFile.value,
      cover: coverFile.value ?? undefined,
      id: musicId.value,
      title: title.value,
      artist: artist.value,
      genreId: genreId.value,
      genreName,
      targetDir: targetDir.value,
    })

    if (result.success) {
      addToast({ message: t('music.importSuccess'), type: 'success' })
      emit('imported')
      step.value = 'idle'
    } else {
      addToast({ message: (result as any).error || result.alerts?.join('\n') || t('music.importFailed'), type: 'error' })
      step.value = 'form'
    }
  } catch (e: any) {
    addToast({ message: e.response?.data?.error || e.message || t('music.importFailed'), type: 'error' })
    step.value = 'form'
  } finally {
    loading.value = false
  }
}

function close() {
  step.value = 'idle'
}

defineExpose({ startImport })
</script>

<template>
  <Button @click="startImport">{{ t('music.importMusic') }}</Button>

  <BottomOverlay :show="step === 'picking'" :title="t('music.importSelectFolder')">
    <div class="flex flex-col gap-3 items-center text-white">
      <div>{{ t('music.importFolderHint') }}</div>
      <div class="flex gap-8 items-end text-center">
        <div class="flex flex-col gap-2 items-center">
          <FileTypeIcon type="UGC" />
          <span class="text-sm op-70">.ugc / .c2s / .sus</span>
        </div>
        <div class="flex flex-col gap-2 items-center">
          <FileTypeIcon type="WAV" />
          <span class="text-sm op-70">.wav / .mp3</span>
        </div>
        <div class="flex flex-col gap-2 items-center">
          <FileTypeIcon type="PNG" />
          <span class="text-sm op-70">.png / .jpg</span>
        </div>
      </div>
    </div>
  </BottomOverlay>

  <Modal v-model:show="showForm" :title="t('music.importTitle')" width="min(90vw, 32em)">
    <div class="flex flex-col gap-3">
      <div v-if="step === 'checking'" class="flex items-center justify-center py-8 op-60">
        {{ t('music.importChecking') }}
      </div>

      <template v-if="showForm">
        <div v-if="coverFile" class="flex justify-center">
          <img :src="coverUrl" class="w-32 h-32 rounded-lg object-cover" />
        </div>

        <div class="flex flex-col gap-1 text-sm op-70 bg-white/5 rd p-3">
          <div>{{ t('music.importFoundFiles') }}:</div>
          <div class="flex gap-3 flex-wrap">
            <span v-for="f in chartFiles" :key="f.name">{{ f.name }}</span>
            <span v-if="audioFile">{{ audioFile.name }}</span>
            <span v-if="coverFile">{{ coverFile.name }}</span>
          </div>
        </div>

        <div v-if="checkResult?.difficulties?.length" class="text-sm bg-white/5 rd p-3">
          <div class="op-70 mb-2">{{ t('music.importDifficulties') }}:</div>
          <div class="flex gap-2 flex-wrap">
            <span v-for="d in checkResult.difficulties" :key="d.difficulty" class="px-2 py-0.5 rd bg-white/10">
              {{ diffNames[d.difficulty] || `DIFF ${d.difficulty}` }} {{ (d.level + d.levelDecimal / 100).toFixed(1) }}
            </span>
          </div>
        </div>

        <div v-if="checkResult?.alerts?.length" class="text-sm c-orange bg-orange/10 rd p-2">
          <div v-for="(a, i) in checkResult.alerts" :key="i">{{ a }}</div>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div class="col-span-2">
            <label class="block text-sm op-60 mb-1">{{ t('music.songTitle') }}</label>
            <TextInput v-model:value="title" :disabled="loading" />
          </div>
          <div class="col-span-2">
            <label class="block text-sm op-60 mb-1">{{ t('music.artist') }}</label>
            <TextInput v-model:value="artist" :disabled="loading" />
          </div>
          <div>
            <label class="block text-sm op-60 mb-1">{{ t('music.genre') }}</label>
            <Select :options="genreOptions" v-model:value="genreId" :disabled="loading" />
          </div>
          <div>
            <label class="block text-sm op-60 mb-1">{{ t('music.importTargetDir') }}</label>
            <Select :options="sourceOptions" v-model:value="targetDir" :disabled="loading" />
          </div>
          <div>
            <label class="block text-sm op-60 mb-1">{{ t('music.importId') }}</label>
            <NumberInput v-model:value="musicId" :min="1" :max="99999" :step="1" :disabled="loading" class="w-full" />
          </div>
        </div>
      </template>
    </div>

    <template #actions>
      <Button class="w-0 grow" @click="close">{{ t('common.cancel') }}</Button>
      <Button class="w-0 grow" @click="doImport" :disabled="loading || !title || !targetDir" :ing="loading">
        {{ t('common.confirm') }}
      </Button>
    </template>
  </Modal>
</template>
