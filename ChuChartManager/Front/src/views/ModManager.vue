<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { addToast, DropMenu } from '@munet/ui'
import { debounce } from 'lodash-es'
import { ensureBackendUrl } from '@/api'
import { getModConfig, getModManifest, getModStatus, getLatestVersions, installAppleChu, saveModConfig } from '@/client/mod'
import type { AppleChuChannel, Manifest, ModConfig, ModStatus, LatestVersions } from '@/client/mod'
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

const modOk = computed(() => status.value?.installed ?? false)
const amdaemonOk = computed(() => status.value?.amdaemonInstalled ?? false)
const installationOk = computed(() => modOk.value && amdaemonOk.value)
const configOk = computed(() => !!manifest.value && !!config.value)
const installOptions = computed(() => {
  const release = versions.value?.applechu.latest
  const ci = versions.value?.ci
  return [
    {
      label: t('mods.releaseChannel'),
      desc: release || t('mods.channelUnavailable'),
      icon: 'i-mdi-tag-outline',
      disabled: installing.value || !release,
      action: () => doInstall('release'),
    },
    {
      label: t('mods.ciChannel'),
      desc: ci
        ? `${ci.version} · ${ci.commit.slice(0, 7)} · ${new Date(ci.createdAt).toLocaleDateString()}`
        : t('mods.channelUnavailable'),
      icon: 'i-mdi-source-branch',
      disabled: installing.value || !ci,
      action: () => doInstall('ci'),
    },
  ]
})

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
    const [s, v] = await Promise.all([getModStatus(), getLatestVersions()])
    status.value = s
    versions.value = v

    // 未检测到游戏侧代理时，不请求配置接口，避免把“未安装”显示成模板错误。
    if (!s.installed) {
      manifest.value = null
      config.value = null
      return
    }

    const [m, c] = await Promise.all([
      getModManifest(MOD_ID),
      getModConfig(MOD_ID),
    ])
    manifest.value = m
    config.value = c
    await nextTick()
    hasLoadedConfig.value = c != null
  } catch (e: unknown) {
    error.value = getErrorMessage(e)
  } finally {
    loading.value = false
  }
}

async function doInstall(channel: AppleChuChannel) {
  installing.value = true
  try {
    await installAppleChu(channel)
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
    <div v-if="!loading" class="flex items-center gap-x-6 gap-y-2 flex-wrap shrink-0 px-2 pb-3 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
      <div class="flex items-center gap-2 min-w-0">
        <span :class="modOk ? 'i-mdi-check-circle c-green-6' : 'i-mdi-alert-circle c-red-6'" class="text-5 shrink-0" />
        <div class="min-w-0">
          <div class="text-sm font-medium">AppleChu</div>
          <div class="text-xs op-55">
            {{ modOk ? t('mods.installed') : t('mods.notInstalled') }}
            <template v-if="status?.version"> · v{{ status.version }}</template>
            <template v-else-if="modOk"> · {{ t('mods.versionUnknown') }}</template>
            <template v-if="versions?.applechu.latest"> · {{ t('mods.latestVersion') }} {{ versions.applechu.latest }}</template>
          </div>
        </div>
      </div>

      <div class="flex items-center gap-2 min-w-0">
        <span :class="amdaemonOk ? 'i-mdi-check-circle c-green-6' : 'i-mdi-alert-circle c-red-6'" class="text-5 shrink-0" />
        <div class="min-w-0">
          <div class="text-sm font-medium">{{ t('mods.amdaemonProxy') }}</div>
          <div class="text-xs op-55">
            {{ amdaemonOk ? t('mods.installed') : t('mods.notInstalled') }}
            <template v-if="status?.amdaemonVersion"> · v{{ status.amdaemonVersion }}</template>
            <template v-else-if="amdaemonOk"> · {{ t('mods.versionUnknown') }}</template>
            <template v-if="versions?.amdaemon.latest"> · {{ t('mods.latestVersion') }} {{ versions.amdaemon.latest }}</template>
          </div>
        </div>
      </div>

      <DropMenu
        :button-text="installing ? t('mods.installing') : installationOk ? t('mods.reinstall') : t('mods.install')"
        :options="installOptions"
        align="left"
      />

      <span v-if="saving" class="text-xs c-green-6 ml-auto">
        <span class="i-mdi-content-save-outline mr-1" />{{ t('mods.saving') }}...
      </span>
    </div>

    <!-- 内容区 -->
    <div v-if="loading" class="flex-1 flex items-center justify-center op-45">
      {{ t('mods.wip') }}
    </div>
    <div v-else-if="error" class="flex-1 flex flex-col gap-2 justify-center items-center min-h-100">
      <div class="text-8">{{ t('common.error') }}</div>
      <div class="c-gray-5 text-lg">{{ error }}</div>
    </div>
    <div v-else-if="!modOk" class="flex-1 flex flex-col gap-2 justify-center items-center min-h-100">
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
