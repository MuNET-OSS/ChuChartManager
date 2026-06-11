<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { modalShowing, GlobalElementsContainer } from '@munet/ui'
import Sidebar from '@/components/Sidebar.vue'
import StatusBar from '@/components/StatusBar.vue'
import StartupErrorDialog from '@/components/StartupErrorDialog'
import ChangelogModal from '@/components/ChangelogModal'
import MusicList from '@/views/MusicList.vue'
import Course from '@/views/Course/index'
import ResourceManager from '@/views/ResourceManager/index'
import GenreManager from '@/views/GenreManager/index'
import ModManager from '@/views/ModManager.vue'
import LoginBonus from '@/views/LoginBonus/index'
import EventManager from '@/views/Event/index'
import EmoteManager from '@/views/Emote/index'
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
      <div class="main-layout">
        <Sidebar v-model:active="sidebarActive" @refresh="handleRefresh" />
        <div class="main-content">
          <MusicList v-show="sidebarActive === 'charts'" ref="musicListRef" />
          <Course v-if="sidebarActive === 'course'" />
          <ResourceManager v-if="sidebarActive === 'resources'" />
          <GenreManager v-if="sidebarActive === 'genre'" />
          <ModManager v-if="sidebarActive === 'mods'" />
          <EventManager v-if="sidebarActive === 'event'" />
          <EmoteManager v-if="sidebarActive === 'emote'" />
          <LoginBonus v-if="sidebarActive === 'loginBonus'" />
          <BatchAction v-if="sidebarActive === 'batch'" />
          <Tools v-if="sidebarActive === 'tools'" />
          <Settings v-if="sidebarActive === 'settings'" />
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
</style>
