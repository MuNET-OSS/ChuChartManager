<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { addToast, Button } from '@munet/ui'
import { debounce } from 'lodash-es'
import { ensureBackendUrl } from '@/api'
import { getModConfig, getModManifest, getModStatus, getLatestVersions, installAppleChu, installLoader, saveModConfig } from '@/client/mod'
import type { Manifest, ModConfig, ModStatus, LatestVersions } from '@/client/mod'
import ModConfigurator from './ModManager/ModConfigurator.vue'

const MOD_ID = 'AppleChu'

const { t } = useI18n()
const loading = ref(true)
const saving = ref(false)
const installing = ref(false)
const error = ref('')
const status = ref<ModStatus | null>(null)
const manifest = ref<Manifest | null>(null)
const config = ref<ModConfig | null>(null)
const versions = ref<LatestVersions | null>(null)
const hasLoadedConfig = ref(false)

const appleChu = computed(() => status.value?.mods.find(mod => mod.name.toLowerCase() === MOD_ID.toLowerCase()))
const loaderOk = computed(() => status.value?.loaderInstalled ?? false)
const proxyOk = computed(() => status.value?.proxyInstalled ?? false)
const modOk = computed(() => !!appleChu.value)
const configOk = computed(() => !!manifest.value && !!config.value)

const save = debounce(async () => {
  if (!config.value || !hasLoadedConfig.value) return
  saving.value = true
  try {
    await saveModConfig(MOD_ID, config.value)
    addToast({ message: t('mods.saveSuccess'), type: 'success' })
  } catch {
    addToast({ message: t('mods.saveFailed'), type: 'error' })
  } finally {
    saving.value = false
  }
}, 2000)

async function refreshModState() {
  loading.value = true
  error.value = ''
  hasLoadedConfig.value = false
  try {
    await ensureBackendUrl()
    const [s, m, c, v] = await Promise.all([
      getModStatus(),
      getModManifest(MOD_ID),
      getModConfig(MOD_ID),
      getLatestVersions(),
    ])
    status.value = s
    manifest.value = m
    config.value = c
    versions.value = v
    await nextTick()
    hasLoadedConfig.value = c != null
  } catch (e: unknown) {
    error.value = getErrorMessage(e)
  } finally {
    loading.value = false
  }
}

async function doInstall(target: 'loader' | 'applechu' | 'all') {
  installing.value = true
  try {
    if (target === 'loader' || target === 'all')
      await installLoader(versions.value?.loader.downloadUrl)
    if (target === 'applechu' || target === 'all')
      await installAppleChu(versions.value?.applechu.downloadUrl)
    await refreshModState()
    addToast({ message: t('mods.installSuccess'), type: 'success' })
  } catch (e: unknown) {
    addToast({ message: getErrorMessage(e), type: 'error' })
  } finally {
    installing.value = false
  }
}

function getErrorMessage(e: unknown): string {
  if (typeof e === 'object' && e && 'response' in e) {
    const r = (e as { response?: { data?: unknown } }).response
    if (typeof r?.data === 'string') return r.data
  }
  return e instanceof Error ? e.message : String(e)
}

onMounted(refreshModState)

watch(config, () => {
  if (hasLoadedConfig.value) save()
}, { deep: true })
</script>

<template>
  <div class="p-xy h-100dvh flex flex-col of-hidden">
    <div class="text-sm op-60 mb-3 px-2 py-1.5 bg-orange/10 c-orange rd">{{ t('mods.experimentalWarning') }}</div>
    <div v-if="!loading" class="flex gap-2 items-center flex-wrap shrink-0">
      <span>ChuModLoader:</span>
      <span :class="loaderOk ? 'c-green-6' : 'c-red-6'">{{ loaderOk ? t('mods.installed') : t('mods.notInstalled') }}</span>
      <Button v-if="!loaderOk" :disabled="installing" @click="doInstall('loader')">{{ t('mods.install') }}</Button>

      <div class="w-4" />

      <span>d3d9 proxy:</span>
      <span :class="proxyOk ? 'c-green-6' : 'c-red-6'">{{ proxyOk ? t('mods.installed') : t('mods.notInstalled') }}</span>

      <div class="w-4" />

      <span>AppleChu:</span>
      <template v-if="modOk">
        <span class="c-green-6">{{ t('mods.installed') }}</span>
      </template>
      <span v-else class="c-red-6">{{ t('mods.notInstalled') }}</span>

      <Button :disabled="installing" @click="doInstall(loaderOk && modOk ? 'applechu' : 'all')">
        {{ modOk ? t('mods.reinstall') : t('mods.install') }}
      </Button>

      <template v-if="versions?.applechu.latest">
        <span>{{ t('mods.latestVersion') }}:</span>
        <span :class="versions.applechu.latest !== (manifest?.mod?.version || '') ? 'c-orange' : ''">{{ versions.applechu.latest }}</span>
      </template>

      <span v-if="saving" class="c-green-6">{{ t('mods.saving') }}...</span>
    </div>

    <!-- 内容区 -->
    <div v-if="loading" class="flex-1 flex items-center justify-center op-45">
      {{ t('mods.wip') }}
    </div>
    <div v-else-if="error" class="flex-1 flex flex-col gap-2 justify-center items-center min-h-100">
      <div class="text-8">{{ t('common.error') }}</div>
      <div class="c-gray-5 text-lg">{{ error }}</div>
    </div>
    <div v-else-if="!loaderOk || !modOk" class="flex-1 flex flex-col gap-2 justify-center items-center min-h-100">
      <div class="text-8">{{ t('mods.needInstall') }}</div>
      <div class="c-gray-5 text-lg">{{ t('mods.installHint') }}</div>
    </div>
    <div v-else-if="!configOk" class="flex-1 flex flex-col gap-2 justify-center items-center min-h-100">
      <div class="text-8">{{ t('mods.configMissingTitle') }}</div>
      <div class="c-gray-5 text-lg">{{ t('mods.configMissingHint') }}</div>
    </div>
    <ModConfigurator v-else :manifest="manifest!" :config="config!" class="flex-1 min-h-0 mt-2" />
  </div>
</template>
