<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { Button, TextInput, NumberInput, Select, addToast } from '@munet/ui'
import { getGenreMap, getSources, importMusicCheck, importMusicExecute } from '@/api'
import type { ImportCheckResult } from '@/api'

const emit = defineEmits<{ imported: [] }>()
const { t } = useI18n()

const show = ref(false)
const loading = ref(false)
const step = ref<'idle' | 'checking' | 'form' | 'executing'>('idle')

const chartFile = ref<File | null>(null)
const audioFile = ref<File | null>(null)
const coverFile = ref<File | null>(null)
const checkResult = ref<ImportCheckResult | null>(null)

const title = ref('')
const artist = ref('')
const genreId = ref(99)
const difficulty = ref(3)
const level = ref(10)
const levelDecimal = ref(0)
const targetDir = ref('')
const musicId = ref(8000)

const genreMap = ref<Record<number, string>>({})
const sources = ref<string[]>([])

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
  if (sourceOptions.value.length > 0) targetDir.value = sourceOptions.value[0].value
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

async function pickFolder() {
  let dirHandle: FileSystemDirectoryHandle
  try {
    dirHandle = await (window as any).showDirectoryPicker({ id: 'import-chart', startIn: 'downloads' })
  } catch { return }

  step.value = 'checking'
  show.value = true

  const files: File[] = []
  for await (const entry of (dirHandle as any).values()) {
    if (entry.kind === 'file') files.push(await entry.getFile())
  }

  chartFile.value = findByExt(files, ['.ugc', '.c2s', '.sus'])
  audioFile.value = findByExt(files, ['.wav', '.mp3', '.ogg'])
  coverFile.value = findByExt(files, ['.png', '.jpg', '.jpeg'])

  if (!chartFile.value) {
    addToast({ message: t('music.importNoChart'), type: 'error' })
    show.value = false
    step.value = 'idle'
    return
  }
  if (!audioFile.value) {
    addToast({ message: t('music.importNoAudio'), type: 'error' })
    show.value = false
    step.value = 'idle'
    return
  }

  parseFolderName(dirHandle.name)

  try {
    checkResult.value = await importMusicCheck(chartFile.value)
    musicId.value = checkResult.value.suggestedId
    step.value = 'form'
  } catch (e: any) {
    addToast({ message: e.message || t('music.importFailed'), type: 'error' })
    show.value = false
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
      show.value = false
    } else {
      addToast({ message: result.alerts?.join('\n') || t('music.importFailed'), type: 'error' })
    }
  } catch (e: any) {
    addToast({ message: e.response?.data?.error || e.message || t('music.importFailed'), type: 'error' })
  } finally {
    loading.value = false
    step.value = 'idle'
  }
}

function close() {
  show.value = false
  step.value = 'idle'
}
</script>

<template>
  <Button @click="pickFolder">{{ t('music.importMusic') }}</Button>

  <Teleport to="body">
    <Transition enter-active-class="transition-opacity duration-200" leave-active-class="transition-opacity duration-200" enter-from-class="opacity-0" leave-to-class="opacity-0">
      <div v-if="show" class="fixed inset-0 z-1000 flex items-center justify-center" @click.self="close">
        <div class="absolute inset-0 bg-black/70" />
        <div class="relative bg-[rgba(30,30,30,0.95)] backdrop-blur-xl rd-lg p-6 w-120 max-w-[90vw] max-h-[80vh] of-y-auto flex flex-col gap-4">
          <div class="text-lg font-bold">{{ t('music.importTitle') }}</div>

          <div v-if="step === 'checking'" class="flex items-center justify-center py-8 op-60">
            {{ t('music.importChecking') }}
          </div>

          <template v-if="step === 'form' || step === 'executing'">
            <div class="flex flex-col gap-1 text-sm op-70 bg-white/5 rd p-3">
              <div>{{ t('music.importFoundFiles') }}:</div>
              <div class="flex gap-3">
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
                <label class="block text-sm op-60 mb-1">{{ t('music.title') }}</label>
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
                <label class="block text-sm op-60 mb-1">{{ t('music.importLevel') }}</label>
                <NumberInput v-model:value="level" :min="1" :max="15" :decimal="0" :step="1" :disabled="loading" />
              </div>
              <div>
                <label class="block text-sm op-60 mb-1">+0.</label>
                <NumberInput v-model:value="levelDecimal" :min="0" :max="99" :decimal="0" :step="10" :disabled="loading" />
              </div>
              <div>
                <label class="block text-sm op-60 mb-1">{{ t('music.importTargetDir') }}</label>
                <Select :options="sourceOptions" v-model:value="targetDir" :disabled="loading" />
              </div>
              <div>
                <label class="block text-sm op-60 mb-1">{{ t('music.importId') }}</label>
                <NumberInput v-model:value="musicId" :min="1" :max="99999" :decimal="0" :step="1" :disabled="loading" />
              </div>
            </div>

            <div v-if="step === 'executing'" class="text-center op-60 py-2">
              {{ t('music.importExecuting') }}
            </div>

            <div class="flex justify-end gap-2 mt-2">
              <Button @click="close" :disabled="loading">{{ $t('common.cancel') }}</Button>
              <Button @click="doImport" :disabled="loading || !title || !targetDir">
                {{ loading ? t('music.importExecuting') : t('music.importMusic') }}
              </Button>
            </div>
          </template>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
