<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { Button, TextInput, NumberInput, Select, Modal, DropMenu, addToast } from '@munet/ui'
import { getGenreMap, getSources, importMusicCheck, importMusicExecute } from '@/api'
import { selectedSource } from '@/store/refs'
import type { ImportCheckResult } from '@/api'
import BottomOverlay from '@/components/BottomOverlay.vue'
import FileTypeIcon from '@/components/FileTypeIcon.vue'

const emit = defineEmits<{ imported: [] }>()
const { t } = useI18n()

const loading = ref(false)
const step = ref<'idle' | 'picking' | 'checking' | 'form' | 'executing'>('idle')

const chartFile = ref<File | null>(null)
const audioFile = ref<File | null>(null)
const coverFile = ref<File | null>(null)
const checkResult = ref<ImportCheckResult | null>(null)

const title = ref('')
const artist = ref('')
const genreId = ref(0)
const difficulty = ref(3)
const level = ref(10)
const levelDecimal = ref(0)
const constant = computed({
  get: () => level.value + levelDecimal.value / 100,
  set: (v: number) => {
    level.value = Math.floor(v)
    levelDecimal.value = Math.round((v - Math.floor(v)) * 100)
  },
})
const targetDir = ref('')
const musicId = ref(8000)

const genreMap = ref<Record<number, string>>({})
const sources = ref<string[]>([])

const showForm = computed(() => step.value === 'form' || step.value === 'executing')

const genreOptions = computed(() =>
  Object.entries(genreMap.value).map(([id, name]) => ({ label: name, value: Number(id) }))
)
const sourceOptions = computed(() =>
  sources.value.filter(s => s !== 'A000').map(s => ({ label: s, value: s }))
)
const diffOptions = [
  { label: 'BASIC', value: 0 },
  { label: 'ADVANCED', value: 1 },
  { label: 'EXPERT', value: 2 },
  { label: 'MASTER', value: 3 },
  { label: 'ULTIMA', value: 4 },
]

onMounted(async () => {
  const [g, s] = await Promise.all([getGenreMap(), getSources()])
  genreMap.value = g
  sources.value = s
  if (sourceOptions.value.length > 0) {
    const current = selectedSource.value
    targetDir.value = sourceOptions.value.find(s => s.value === current)?.value ?? sourceOptions.value[0].value
  }
})

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

function findByExt(files: File[], exts: string[]): File | null {
  return files.find(f => exts.some(e => f.name.toLowerCase().endsWith(e))) ?? null
}

async function startImport() {
  step.value = 'picking'
  let dirHandle: FileSystemDirectoryHandle
  try {
    dirHandle = await (window as any).showDirectoryPicker({ id: 'import-chart', startIn: 'downloads' })
  } catch {
    step.value = 'idle'
    return
  }

  step.value = 'checking'

  const files: File[] = []
  for await (const entry of (dirHandle as any).values()) {
    if (entry.kind === 'file') files.push(await entry.getFile())
  }

  chartFile.value = findByExt(files, ['.ugc', '.c2s', '.sus'])
  audioFile.value = findByExt(files, ['.wav', '.mp3', '.ogg'])
  coverFile.value = findByExt(files, ['.png', '.jpg', '.jpeg'])

  if (!chartFile.value) {
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
    checkResult.value = await importMusicCheck(chartFile.value)
    musicId.value = checkResult.value.suggestedId
    difficulty.value = checkResult.value.difficulty
    level.value = checkResult.value.level
    levelDecimal.value = checkResult.value.levelDecimal
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
  if (!chartFile.value || !audioFile.value) return
  step.value = 'executing'
  loading.value = true

  try {
    const genreName = genreMap.value[genreId.value] || ''
    const result = await importMusicExecute({
      chart: chartFile.value,
      audio: audioFile.value,
      cover: coverFile.value ?? undefined,
      id: musicId.value,
      title: title.value,
      artist: artist.value,
      genreId: genreId.value,
      genreName,
      difficulty: difficulty.value,
      level: level.value,
      levelDecimal: levelDecimal.value,
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
        <div class="flex flex-col gap-1 text-sm op-70 bg-white/5 rd p-3">
          <div>{{ t('music.importFoundFiles') }}:</div>
          <div class="flex gap-3 flex-wrap">
            <span v-if="chartFile">{{ chartFile.name }}</span>
            <span v-if="audioFile">{{ audioFile.name }}</span>
            <span v-if="coverFile">{{ coverFile.name }}</span>
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
            <label class="block text-sm op-60 mb-1">{{ t('music.importDifficulty') }}</label>
            <Select :options="diffOptions" v-model:value="difficulty" :disabled="loading" />
          </div>
          <div>
            <label class="block text-sm op-60 mb-1">{{ t('music.chartConstant') }}</label>
            <NumberInput v-model:value="constant" :min="0" :max="15.9" :step="0.1" :decimal="1" :disabled="loading" class="w-full" />
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
