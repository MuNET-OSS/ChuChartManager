<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { rescanOptions } from '@/api/option'
import VersionInfo from '@/components/VersionInfo'

export type SidebarKey = 'charts' | 'course' | 'resources' | 'genre' | 'event' | 'emote' | 'mate' | 'mods' | 'loginBonus' | 'batch' | 'tools' | 'settings'

const { t } = useI18n()

const props = defineProps<{ active: SidebarKey }>()
const emit = defineEmits<{ 'update:active': [val: SidebarKey], 'refresh': [] }>()

const items = computed(() => [
  { key: 'charts' as const, icon: 'i-mdi-music-note', label: t('sidebar.charts') },
  { key: 'resources' as const, icon: 'i-mdi-package-variant-closed', label: t('sidebar.resources') },
  { key: 'genre' as const, icon: 'i-mdi-tag-multiple', label: t('sidebar.genre') },
  { key: 'batch' as const, icon: 'i-mdi-playlist-edit', label: t('sidebar.batch') },
  { key: 'mods' as const, icon: 'i-mdi-puzzle', label: t('sidebar.mods') },
  { key: 'course' as const, icon: 'i-mdi-trophy-variant', label: t('sidebar.course') },
  { key: 'event' as const, icon: 'i-mdi-calendar-star', label: t('sidebar.event') },
  { key: 'emote' as const, icon: 'i-mdi-emoticon-outline', label: t('sidebar.emote') },
  { key: 'mate' as const, icon: 'i-mdi-account-heart-outline', label: t('sidebar.mate') },
  { key: 'loginBonus' as const, icon: 'i-mdi-gift', label: t('sidebar.loginBonus') },
  { key: 'tools' as const, icon: 'i-ri:tools-fill', label: t('sidebar.tools') },
])

const refreshing = ref(false)
const handleRefresh = async () => {
  if (refreshing.value) return
  refreshing.value = true
  try {
    await rescanOptions()
    emit('refresh')
  } finally {
    refreshing.value = false
  }
}
</script>

<template>
  <div class="sidebar">
    <div
      v-for="item in items" :key="item.key"
      class="sidebar-item"
      :class="{ active: props.active === item.key }"
      @click="emit('update:active', item.key)"
    >
      <div v-if="props.active === item.key" class="indicator" />
      <span :class="item.icon" class="text-6" />
      <span class="tooltip">{{ item.label }}</span>
    </div>
    <div class="mt-auto" />
    <div
      class="sidebar-item"
      :class="{ active: props.active === 'settings' }"
      @click="emit('update:active', 'settings')"
    >
      <div v-if="props.active === 'settings'" class="indicator" />
      <span class="i-mdi-cog text-6" />
      <span class="tooltip">{{ t('sidebar.settings') }}</span>
    </div>
    <div
      class="sidebar-item"
      :class="{ active: refreshing }"
      @click="handleRefresh"
    >
      <span class="i-ic-baseline-refresh text-6" :class="{ 'animate-spin': refreshing }" />
      <span class="tooltip">{{ t('sidebar.refresh') }}</span>
    </div>
    <VersionInfo />
  </div>
</template>

<style lang="sass" scoped>
.sidebar
  display: flex
  flex-direction: column
  align-items: center
  width: 64px
  padding: 8px 0
  gap: 4px
  flex-shrink: 0
  border-right: 1px solid oklch(0.9 0.02 var(--hue))
  background: oklch(0.98 0.01 var(--hue))
  z-index: 20
  overflow: visible

.sidebar-item
  width: 48px
  height: 48px
  display: flex
  align-items: center
  justify-content: center
  border-radius: 8px
  cursor: pointer
  flex-shrink: 0
  transition: all 0.2s
  position: relative
  color: #999
  background: transparent

  &:hover
    color: #666
    background: oklch(0.6 0.1 var(--hue) / 15%)

  &.active
    background: oklch(0.9 0.05 var(--hue))
    color: oklch(0.55 0.15 var(--hue))

  .indicator
    position: absolute
    left: 0
    top: 6px
    bottom: 6px
    width: 3px
    border-radius: 0 3px 3px 0
    background: oklch(0.55 0.15 var(--hue))

  .tooltip
    position: absolute
    left: 100%
    margin-left: 8px
    padding: 4px 12px
    border-radius: 8px
    background: oklch(0.7 0.13 var(--hue))
    color: white
    font-size: 0.85rem
    white-space: nowrap
    opacity: 0
    pointer-events: none
    transition: opacity 0.2s
    z-index: 100

  &:hover .tooltip
    opacity: 1
</style>
