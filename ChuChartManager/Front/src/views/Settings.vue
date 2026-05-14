<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { Button, Select, Range } from '@munet/ui'
import { selectedThemeHue, selectedThemeName, UIThemes } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { apiClient, isWebView } from '@/api'
import { setStatus } from '@/store/status'
import { availableLocales, localeLabels, setLocale, locale } from '@/locales'
import type { Locale } from '@/locales'

const { t } = useI18n()

const gamePath = ref('')
const historyPaths = ref<string[]>([])
const switching = ref(false)
const error = ref('')

const uiZoom = ref(0)
const autoZoom = 100

const zoomDisplay = computed({
  get: () => uiZoom.value || autoZoom,
  set: (v: number) => {
    uiZoom.value = v
    postZoom(v)
  }
})

function postZoom(value: number) {
  if (isWebView) {
    ;(window as any).chrome.webview.postMessage(JSON.stringify({ type: 'setZoom', value }))
  }
}

const themeOptions = computed<SelectOption[]>(() => [
  { label: t('settings.themeAuto'), value: UIThemes.Auto },
  { label: t('settings.themeLight'), value: UIThemes.DynamicLight },
  { label: t('settings.themeDark'), value: UIThemes.Dark },
])

const localeOptions = computed(() =>
  availableLocales.map(l => ({ label: localeLabels[l], value: l }))
)

onMounted(async () => {
  try {
    const { data } = await apiClient.get('/api/Config/GetConfig')
    gamePath.value = data.gamePath || ''
    historyPaths.value = data.historyPaths || []
  } catch {}
})

function resetHue() {
  selectedThemeHue.value = 353
}

function resetZoom() {
  uiZoom.value = 0
  postZoom(0)
}

async function handleChangeDirectory() {
  error.value = ''
  try {
    const { data: selected } = await apiClient.post('/api/Config/OpenFolderDialog')
    if (!selected) return
    switching.value = true
    await apiClient.post('/api/Config/SetGamePath', JSON.stringify(selected), {
      headers: { 'Content-Type': 'application/json' }
    })
    gamePath.value = selected
    if (!historyPaths.value.includes(selected))
      historyPaths.value.push(selected)
    setStatus(t('settings.switchedTo', { path: selected }))
  } catch (e: any) {
    error.value = t('settings.changeDirectoryFailed')
  } finally {
    switching.value = false
  }
}

async function switchToHistory(path: string) {
  if (path === gamePath.value) return
  error.value = ''
  switching.value = true
  try {
    await apiClient.post('/api/Config/SetGamePath', JSON.stringify(path), {
      headers: { 'Content-Type': 'application/json' }
    })
    gamePath.value = path
    setStatus(t('settings.switchedTo', { path }))
  } catch {
    error.value = t('settings.changeDirectoryFailed')
  } finally {
    switching.value = false
  }
}

async function deleteHistory(path: string) {
  try {
    await apiClient.post('/api/Config/DeleteHistoryPath', JSON.stringify(path), {
      headers: { 'Content-Type': 'application/json' }
    })
    historyPaths.value = historyPaths.value.filter(p => p !== path)
  } catch {}
}
</script>

<template>
  <div class="h-full p-6 of-y-auto cst">
    <div class="mb-6">
      <div class="text-lg font-semibold mb-3 section-title">{{ t('settings.appearance') }}</div>
      <div class="section-card">
        <div class="flex items-center gap-3">
          <span class="shrink-0 op-60">{{ t('settings.theme') }}</span>
          <Select :options="themeOptions" v-model:value="selectedThemeName" />
        </div>
        <div class="flex items-center gap-2">
          <input
            type="range" min="0" max="360" step="1"
            :value="selectedThemeHue"
            @input="(e: Event) => selectedThemeHue = Number((e.target as HTMLInputElement).value)"
            class="hue-slider flex-1"
          />
          <Button @click="resetHue" class="shrink-0">{{ t('settings.reset') }}</Button>
        </div>
        <div v-if="isWebView" class="flex items-center gap-2">
          <span class="shrink-0 op-60">{{ t('settings.zoom') }}</span>
          <input
            type="range" min="50" max="250" step="5"
            :value="zoomDisplay"
            @input="(e: Event) => zoomDisplay = Number((e.target as HTMLInputElement).value)"
            class="flex-1"
          />
          <span class="ml-auto shrink-0 text-sm op-60 w-12">
            {{ uiZoom === 0 ? t('settings.zoomAuto') : `${uiZoom}%` }}
          </span>
          <Button @click="resetZoom" class="shrink-0">{{ t('settings.reset') }}</Button>
        </div>
        <div class="flex items-center gap-3">
          <span class="shrink-0 op-60">{{ t('settings.language') }}</span>
          <Select :options="localeOptions" :value="locale" @change="(v: any) => setLocale(v as Locale)" />
        </div>
      </div>
    </div>

    <div class="mb-6">
      <div class="text-lg font-semibold mb-3 section-title">{{ t('settings.gameDirectory') }}</div>
      <div class="section-card">
        <div class="flex items-center gap-3">
          <span class="shrink-0 op-60">{{ t('settings.currentPath') }}</span>
          <span class="text-sm break-all select-text">{{ gamePath || '--' }}</span>
        </div>
        <div class="flex items-center gap-3">
          <Button :disabled="switching" @click="handleChangeDirectory">{{ t('settings.changeDirectory') }}</Button>
          <Button @click="apiClient.post('/api/Config/SwitchToSetMode')">{{ t('settings.switchMode') }}</Button>
          <span v-if="error" class="text-red-500 text-sm">{{ error }}</span>
        </div>
        <div v-if="historyPaths.length > 0" class="flex flex-col gap-1 mt-2">
          <div class="text-sm op-50 mb-1">{{ t('settings.historyPath') }}</div>
          <div
            v-for="path in historyPaths" :key="path"
            class="flex items-center gap-2 px-3 py-1.5 rounded-lg cursor-pointer hover:bg-black/5 transition-colors"
            :class="{ 'bg-black/8': path === gamePath }"
            @click="switchToHistory(path)"
          >
            <span class="i-material-symbols:folder-outline-rounded text-lg op-70 shrink-0" />
            <span class="text-sm break-all grow">{{ path }}</span>
            <button class="op-50 hover:op-100 shrink-0" @click.stop="deleteHistory(path)">
              <span class="i-tabler:trash text-base" />
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style lang="sass" scoped>
.section-title
  color: oklch(0.55 0.15 var(--hue))

.section-card
  border-radius: 12px
  background: rgba(255, 255, 255, 0.6)
  padding: 16px
  display: flex
  flex-direction: column
  gap: 16px
  border: 1px solid #e5e7eb

.hue-slider
  outline: none
  border-radius: 0.5rem
  appearance: none
  height: 2.5rem
  background: linear-gradient(to right, oklch(85% 0.2 0), oklch(85% 0.2 60), oklch(85% 0.2 120), oklch(85% 0.2 180), oklch(85% 0.2 240), oklch(85% 0.2 300), oklch(85% 0.2 360))

  &::-webkit-slider-thumb
    width: 0.25rem
    height: 3rem
    appearance: none
    border-radius: 0.375rem
    background-color: #525252
    box-shadow: 0 10px 15px -3px rgb(0 0 0 / 0.1)
    border: 2px solid #737373
</style>
