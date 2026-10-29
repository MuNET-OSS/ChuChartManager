<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { modalShowing, GlobalElementsContainer } from '@munet/ui'
import Sidebar from '@/components/Sidebar.vue'
import StatusBar from '@/components/StatusBar.vue'
import StartupErrorDialog from '@/components/StartupErrorDialog'
import ChangelogModal from '@/components/ChangelogModal'
import ErrorDialog from '@/components/ErrorDialog'
import MusicList from '@/views/MusicList.vue'
import ResourceManager from '@/views/ResourceManager/index'
import GenreManager from '@/views/GenreManager/index'
import ModManager from '@/views/ModManager.vue'
import EmoteManager from '@/views/Emote/index'
import MateManager from '@/views/Mate/index'
import BatchAction from '@/views/BatchAction/index'
import Tools from '@/views/Tools/index'
import Settings from '@/views/Settings.vue'
import Oobe from '@/views/Oobe/index'
import { ensureBackendUrl } from '@/api'
import { loadLocaleFromBackend } from '@/locales'
import { updateOptionDirs, sidebarActive, updateAppVersion } from '@/store/refs'
import { checkAppUpdate } from '@/store/appUpdate'

const hash = window.location.hash.replace('#', '')
const isOobeWindow = hash === 'oobe' || hash === 'mode-select'
const oobeInitStep = hash === 'mode-select' ? 'mode-select' as const : 'welcome' as const

const ready = ref(false)
const musicListRef = ref<InstanceType<typeof MusicList> | null>(null)

onMounted(async () => {
  await ensureBackendUrl()
  await loadLocaleFromBackend()
  updateOptionDirs()
  await updateAppVersion()
  checkAppUpdate()
  ready.value = true
})

const handleRefresh = () => {
  musicListRef.value?.refresh()
}
</script>

<template>
  <template v-if="ready">
    <Oobe v-if="isOobeWindow" :initStep="oobeInitStep" />
    <div v-else class="content-root" :class="{ 'modal-open': modalShowing }">
      <GlobalElementsContainer />
      <StartupErrorDialog />
      <ChangelogModal :ready="ready" />
      <ErrorDialog />
      <div class="main-layout">
        <Sidebar v-model:active="sidebarActive" @refresh="handleRefresh" />
        <div class="main-content">
          <!-- 谱面页用显隐切换而不是销毁重建（保留列表滚动与选中），所以它的淡入淡出走 class -->
          <MusicList
            ref="musicListRef"
            class="view-pane"
            :class="{ 'view-pane-hidden': sidebarActive !== 'charts' }"
          />
          <Transition name="view-fade">
            <div v-if="sidebarActive !== 'charts'" :key="sidebarActive" class="view-pane">
              <ResourceManager v-if="sidebarActive === 'resources'" />
              <GenreManager v-else-if="sidebarActive === 'genre'" />
              <ModManager v-else-if="sidebarActive === 'mods'" />
              <EmoteManager v-else-if="sidebarActive === 'emote'" />
              <MateManager v-else-if="sidebarActive === 'mate'" />
              <BatchAction v-else-if="sidebarActive === 'batch'" />
              <Tools v-else-if="sidebarActive === 'tools'" />
              <Settings v-else-if="sidebarActive === 'settings'" />
            </div>
          </Transition>
        </div>
      </div>
      <StatusBar />
    </div>
  </template>
</template>

<style lang="sass">
html, body
  margin: 0
  padding: 0
  width: 100%
  height: 100%
  overflow: hidden

#app
  width: 100%
  height: 100%

.content-root
  display: flex
  flex-direction: column
  height: 100%
  overflow: hidden
  transform-origin: center
  transition: transform 0.2s ease, filter 0.2s ease-in-out

  &.modal-open
    transform: scale(0.9)
    filter: blur(25px)

.main-layout
  display: flex
  flex: 1
  min-height: 0
  overflow: hidden

.main-content
  flex: 1
  min-width: 0
  height: 100%
  overflow: hidden
  // 所有页面叠在同一格里，切换时才能交叉淡入淡出
  display: grid
  grid-template-areas: "pane"

  > *
    grid-area: pane
    min-width: 0
    min-height: 0

.view-pane
  transition: opacity 0.2s cubic-bezier(0.4, 0, 0.2, 1)

.view-pane-hidden
  opacity: 0
  pointer-events: none

.view-fade-enter-active,
.view-fade-leave-active
  transition: opacity 0.2s cubic-bezier(0.4, 0, 0.2, 1), transform 0.2s cubic-bezier(0.4, 0, 0.2, 1)

.view-fade-enter-from,
.view-fade-leave-to
  opacity: 0
  transform: translateY(6px)
</style>
