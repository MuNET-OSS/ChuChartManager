<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { getExportMp3Url } from '@/api'
import {
  currentTime,
  duration,
  endSeek,
  isPlaying,
  playingMusic,
  startSeek,
  togglePlayPause,
  volume,
} from '@/store/player'

const seekTrackRef = ref<HTMLElement | null>(null)
const isSeekHover = ref(false)
const isSeekingLocal = ref(false)
const isVolumeHover = ref(false)
const volumeTrackRef = ref<HTMLElement | null>(null)
const lastVolume = ref(0.8)

const safeDuration = computed(() => Number.isFinite(duration.value) && duration.value > 0 ? duration.value : 0)
const safeCurrentTime = computed(() => Math.max(0, Math.min(currentTime.value, safeDuration.value || currentTime.value)))
const progressPercent = computed(() => safeDuration.value > 0 ? safeCurrentTime.value / safeDuration.value * 100 : 0)
const volumePercent = computed(() => Math.max(0, Math.min(volume.value, 1)) * 100)

let stopSeekDrag: (() => void) | null = null
let stopVolumeDrag: (() => void) | null = null

function fmt(s: number): string {
  const v = Number.isFinite(s) && s > 0 ? s : 0
  return `${Math.floor(v / 60)}:${Math.floor(v % 60).toString().padStart(2, '0')}`
}

function ratio(event: MouseEvent, el: HTMLElement): number {
  const r = el.getBoundingClientRect()
  return r.width > 0 ? Math.max(0, Math.min(1, (event.clientX - r.left) / r.width)) : 0
}

function beginSeek(event: MouseEvent) {
  if (!seekTrackRef.value) return
  stopSeekDrag?.()
  isSeekingLocal.value = true
  startSeek()
  if (safeDuration.value > 0) currentTime.value = ratio(event, seekTrackRef.value) * safeDuration.value

  const onMove = (e: MouseEvent) => {
    if (seekTrackRef.value && safeDuration.value > 0)
      currentTime.value = ratio(e, seekTrackRef.value) * safeDuration.value
  }
  const onUp = (e: MouseEvent) => {
    if (seekTrackRef.value && safeDuration.value > 0)
      endSeek(ratio(e, seekTrackRef.value) * safeDuration.value)
    isSeekingLocal.value = false
    cleanup()
  }
  const cleanup = () => {
    document.removeEventListener('mousemove', onMove)
    document.removeEventListener('mouseup', onUp)
    stopSeekDrag = null
  }
  document.addEventListener('mousemove', onMove)
  document.addEventListener('mouseup', onUp)
  stopSeekDrag = cleanup
}

function beginVolumeDrag(event: MouseEvent) {
  if (!volumeTrackRef.value) return
  stopVolumeDrag?.()
  volume.value = ratio(event, volumeTrackRef.value)
  if (volume.value > 0) lastVolume.value = volume.value

  const onMove = (e: MouseEvent) => {
    if (!volumeTrackRef.value) return
    volume.value = ratio(e, volumeTrackRef.value)
    if (volume.value > 0) lastVolume.value = volume.value
  }
  const onUp = () => cleanup()
  const cleanup = () => {
    document.removeEventListener('mousemove', onMove)
    document.removeEventListener('mouseup', onUp)
    stopVolumeDrag = null
  }
  document.addEventListener('mousemove', onMove)
  document.addEventListener('mouseup', onUp)
  stopVolumeDrag = cleanup
}

function toggleMute() {
  if (volume.value > 0) { lastVolume.value = volume.value; volume.value = 0 }
  else volume.value = lastVolume.value || 0.8
}

function downloadMp3() {
  if (playingMusic.value) window.open(getExportMp3Url(playingMusic.value.id, playingMusic.value.assetDir))
}

onBeforeUnmount(() => { stopSeekDrag?.(); stopVolumeDrag?.() })
</script>

<template>
  <div v-if="playingMusic" class="player-wrapper">
    <div class="player">
      <button class="btn btn-play" @click="togglePlayPause">
        <svg v-if="!isPlaying" viewBox="0 0 24 24" fill="currentColor"><path d="M8 5.5v13l10.5-6.5z" /></svg>
        <svg v-else viewBox="0 0 24 24" fill="currentColor"><path d="M7 5h3.5v14H7zm6.5 0H17v14h-3.5z" /></svg>
      </button>

      <span class="time">{{ fmt(currentTime) }}</span>

      <div
        ref="seekTrackRef"
        class="track seek"
        :class="{ active: isSeekHover || isSeekingLocal }"
        @mouseenter="isSeekHover = true" @mouseleave="isSeekHover = false"
        @mousedown.prevent="beginSeek"
      >
        <div class="track-fill" :style="{ width: `${progressPercent}%` }"><span class="thumb" /></div>
      </div>

      <span class="time time-dim">{{ fmt(duration) }}</span>

      <div class="divider" />

      <div class="volume-group">
        <button class="btn btn-sm" @click="toggleMute">
          <svg v-if="volume <= 0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M11 5 6.4 9H3v6h3.4l4.6 4V5z" /><path d="m17 9 4 4m0-4-4 4" /></svg>
          <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M11 5 6.4 9H3v6h3.4l4.6 4V5z" /><path d="M15.5 9.5a4 4 0 0 1 0 5" /><path v-if="volume >= 0.5" d="M18.3 7a8 8 0 0 1 0 10" /></svg>
        </button>
        <div
          ref="volumeTrackRef"
          class="track vol"
          :class="{ active: isVolumeHover }"
          @mouseenter="isVolumeHover = true" @mouseleave="isVolumeHover = false"
          @mousedown.prevent="beginVolumeDrag"
        >
          <div class="track-fill" :style="{ width: `${volumePercent}%` }"><span class="thumb" /></div>
        </div>
      </div>

      <div class="divider" />

      <button class="btn btn-sm" @click="downloadMp3">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v10m0 0-4-4m4 4 4-4" /><path d="M5 17h14" /></svg>
      </button>
    </div>
  </div>
</template>

<style lang="sass" scoped>
.player-wrapper
  display: flex
  justify-content: center
  width: 100%

.player
  --text-primary: oklch(0.3 0.02 var(--hue))
  --text-dim: oklch(0.5 0.02 var(--hue))
  --bg-glass: oklch(0.92 0.02 var(--hue) / 80%)
  --border-glass: oklch(0.8 0.03 var(--hue) / 20%)
  --accent: oklch(0.8 0.12 var(--hue))
  --accent-glow: oklch(0.7 0.15 var(--hue) / 40%)

  display: flex
  align-items: center
  gap: 12px
  width: 100%
  padding: 8px 16px 8px 8px
  border-radius: 50px
  background: var(--bg-glass)
  border: 1px solid var(--border-glass)
  box-shadow: 0 2px 12px oklch(0.5 0.05 var(--hue) / 8%)
  backdrop-filter: blur(20px) saturate(150%)
  color: var(--text-primary)

.divider
  width: 1px
  height: 18px
  background: var(--border-glass)
  flex-shrink: 0

.btn
  display: flex
  align-items: center
  justify-content: center
  width: 32px
  height: 32px
  padding: 0
  border: none
  border-radius: 50%
  background: transparent
  color: var(--text-primary)
  opacity: 0.7
  cursor: pointer
  flex-shrink: 0
  transition: opacity 0.2s, background 0.2s, transform 0.2s

  svg
    width: 16px
    height: 16px

  &:hover
    opacity: 1
    background: oklch(0.5 0.05 var(--hue) / 10%)
    transform: scale(1.08)

  &:active
    transform: scale(0.92)

.btn-play
  width: 40px
  height: 40px
  background: var(--accent)
  color: #111
  opacity: 1
  box-shadow: 0 4px 12px var(--accent-glow)

  svg
    width: 20px
    height: 20px

  &:hover
    background: oklch(0.85 0.14 var(--hue))
    box-shadow: 0 6px 16px var(--accent-glow)
    transform: scale(1.05)

.btn-sm
  width: 28px
  height: 28px

  svg
    width: 14px
    height: 14px

.volume-group
  display: flex
  align-items: center
  gap: 4px

.time
  font-size: 12px
  font-weight: 500
  font-variant-numeric: tabular-nums
  letter-spacing: 0.5px
  color: var(--text-primary)
  flex-shrink: 0
  min-width: 38px
  text-align: center

.time-dim
  color: var(--text-dim)

.track
  position: relative
  display: flex
  align-items: center
  cursor: pointer
  height: 24px

  &::before
    content: ''
    position: absolute
    left: 0
    right: 0
    height: 4px
    border-radius: 4px
    background: oklch(0.7 0.03 var(--hue) / 20%)
    transition: height 0.2s

  &.active::before
    height: 6px
    background: oklch(0.6 0.04 var(--hue) / 30%)

.seek
  flex: 1
  min-width: 120px

.vol
  width: 64px
  flex-shrink: 0

.track-fill
  position: absolute
  left: 0
  display: flex
  align-items: center
  justify-content: flex-end
  height: 4px
  max-width: 100%
  border-radius: 4px
  background: var(--accent)
  pointer-events: none
  transition: height 0.2s, box-shadow 0.2s

  .active &
    height: 6px
    box-shadow: 0 0 8px var(--accent-glow)

.thumb
  width: 12px
  height: 12px
  flex-shrink: 0
  border-radius: 50%
  background: var(--accent)
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.3)
  opacity: 0
  transform: scale(0.5) translate(50%, 0)
  transition: transform 0.2s cubic-bezier(0.175, 0.885, 0.32, 1.275), opacity 0.2s

  .active &
    opacity: 1
    transform: scale(1) translate(50%, 0)
</style>
