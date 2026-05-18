<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { getJacketUrl } from '@/api'
import {
  currentTime,
  duration,
  endSeek,
  isPlaying,
  playingMusic,
  startSeek,
  stop,
  togglePlayPause,
  volume,
} from '@/store/player'

const isSeekingLocal = ref(false)
const isSeekHover = ref(false)
const isVolumeHover = ref(false)
const lastVolume = ref(0.8)
const seekTrackRef = ref<HTMLElement | null>(null)
const volumeTrackRef = ref<HTMLElement | null>(null)

const safeDuration = computed(() => Number.isFinite(duration.value) && duration.value > 0 ? duration.value : 0)
const safeCurrentTime = computed(() => Math.max(0, Math.min(currentTime.value, safeDuration.value || currentTime.value)))
const progressPercent = computed(() => safeDuration.value > 0 ? safeCurrentTime.value / safeDuration.value * 100 : 0)
const canSeek = computed(() => safeDuration.value > 0 || isSeekingLocal.value)
const volumePercent = computed(() => Math.max(0, Math.min(volume.value, 1)) * 100)
const volumeLevel = computed(() => {
  if (volume.value <= 0) return 'mute'
  if (volume.value < 0.34) return 'low'
  if (volume.value < 0.67) return 'medium'
  return 'high'
})

let stopSeekDrag: (() => void) | null = null
let stopVolumeDrag: (() => void) | null = null

function fmt(s: number): string {
  const safeSeconds = Number.isFinite(s) && s > 0 ? s : 0
  return `${Math.floor(safeSeconds / 60).toString().padStart(2, '0')}:${Math.floor(safeSeconds % 60).toString().padStart(2, '0')}`
}

function getPointerRatio(event: MouseEvent, element: HTMLElement): number {
  const rect = element.getBoundingClientRect()
  if (rect.width <= 0) return 0
  return Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width))
}

function previewSeek(event: MouseEvent) {
  if (!seekTrackRef.value) return
  const dur = safeDuration.value
  if (dur <= 0) return
  const ratio = getPointerRatio(event, seekTrackRef.value)
  currentTime.value = ratio * dur
}

function beginSeek(event: MouseEvent) {
  if (!seekTrackRef.value) return
  stopSeekDrag?.()
  isSeekingLocal.value = true
  startSeek()

  const dur = safeDuration.value
  if (dur > 0) {
    const ratio = getPointerRatio(event, seekTrackRef.value)
    currentTime.value = ratio * dur
  }

  const onMove = (moveEvent: MouseEvent) => previewSeek(moveEvent)
  const onUp = (upEvent: MouseEvent) => {
    const finalDur = safeDuration.value
    if (seekTrackRef.value && finalDur > 0) {
      const ratio = getPointerRatio(upEvent, seekTrackRef.value)
      endSeek(ratio * finalDur)
    }
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

function setVolumeFromPointer(event: MouseEvent) {
  if (!volumeTrackRef.value) return
  volume.value = getPointerRatio(event, volumeTrackRef.value)
  if (volume.value > 0) lastVolume.value = volume.value
}

function beginVolumeDrag(event: MouseEvent) {
  stopVolumeDrag?.()
  setVolumeFromPointer(event)

  const onMove = (moveEvent: MouseEvent) => setVolumeFromPointer(moveEvent)
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
  if (volume.value > 0) {
    lastVolume.value = volume.value
    volume.value = 0
    return
  }

  volume.value = lastVolume.value || 0.8
}

onBeforeUnmount(() => {
  stopSeekDrag?.()
  stopVolumeDrag?.()
})
</script>

<template>
  <Transition name="player-bar-transition">
    <div v-if="playingMusic" class="player-bar-shell">
      <div class="player-bar">
        <section class="track-meta" aria-label="当前播放曲目">
          <img
            v-if="playingMusic.hasJacket"
            :src="getJacketUrl(playingMusic.id, playingMusic.assetDir)"
            class="track-jacket"
            alt=""
          />
          <div v-else class="track-jacket track-jacket--empty" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
              <path d="M9 18V5l11-2v13" />
              <circle cx="6" cy="18" r="3" />
              <circle cx="17" cy="16" r="3" />
            </svg>
          </div>

          <div class="track-text">
            <div class="track-title" :title="playingMusic.name">{{ playingMusic.name }}</div>
            <div class="track-artist" :title="playingMusic.artist">{{ playingMusic.artist }}</div>
          </div>
        </section>

        <section class="seek-section" aria-label="播放进度">
          <div class="seek-topline">
            <span class="time-label">{{ fmt(currentTime) }}</span>
            <span class="time-label time-label--duration">{{ fmt(duration) }}</span>
          </div>

          <div
            ref="seekTrackRef"
            class="seek-track"
            :class="{ 'is-active': isSeekHover || isSeekingLocal }"
            role="slider"
            aria-label="播放进度"
            aria-valuemin="0"
            :aria-valuemax="Math.round(safeDuration)"
            :aria-valuenow="Math.round(safeCurrentTime)"
            @mouseenter="isSeekHover = true"
            @mouseleave="isSeekHover = false"
            @mousedown.prevent="beginSeek"
          >
            <div class="seek-fill" :style="{ width: `${progressPercent}%` }">
              <span class="seek-thumb" />
            </div>
          </div>
        </section>

        <section class="control-section" aria-label="播放控制">
          <button
            class="icon-button icon-button--primary"
            type="button"
            :title="isPlaying ? '暂停' : '播放'"
            :aria-label="isPlaying ? '暂停' : '播放'"
            @click="togglePlayPause"
          >
            <svg v-if="!isPlaying" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
              <path d="M8 5.5v13l10.5-6.5L8 5.5z" />
            </svg>
            <svg v-else viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
              <path d="M7 5h3.6v14H7V5zm6.4 0H17v14h-3.6V5z" />
            </svg>
          </button>

          <button class="icon-button" type="button" title="停止" aria-label="停止" @click="stop">
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
              <rect x="6.5" y="6.5" width="11" height="11" rx="1.6" />
            </svg>
          </button>

          <div class="volume-control">
            <button
              class="icon-button icon-button--volume"
              type="button"
              :title="volume > 0 ? '静音' : '恢复音量'"
              :aria-label="volume > 0 ? '静音' : '恢复音量'"
              @click="toggleMute"
            >
              <svg v-if="volumeLevel === 'mute'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M11 5 6.4 9H3v6h3.4l4.6 4V5z" />
                <path d="m17 9 4 4m0-4-4 4" />
              </svg>
              <svg v-else-if="volumeLevel === 'low'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M11 5 6.4 9H3v6h3.4l4.6 4V5z" />
                <path d="M15.5 9.5a4 4 0 0 1 0 5" />
              </svg>
              <svg v-else-if="volumeLevel === 'medium'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M11 5 6.4 9H3v6h3.4l4.6 4V5z" />
                <path d="M15.5 9.5a4 4 0 0 1 0 5" />
                <path d="M18.3 7a8 8 0 0 1 0 10" />
              </svg>
              <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M11 5 6.4 9H3v6h3.4l4.6 4V5z" />
                <path d="M15.5 9.5a4 4 0 0 1 0 5" />
                <path d="M18.3 7a8 8 0 0 1 0 10" />
                <path d="M20.8 4.8a12 12 0 0 1 0 14.4" />
              </svg>
            </button>

            <div
              ref="volumeTrackRef"
              class="volume-track"
              :class="{ 'is-active': isVolumeHover }"
              role="slider"
              aria-label="音量"
              aria-valuemin="0"
              aria-valuemax="100"
              :aria-valuenow="Math.round(volumePercent)"
              @mouseenter="isVolumeHover = true"
              @mouseleave="isVolumeHover = false"
              @mousedown.prevent="beginVolumeDrag"
            >
              <div class="volume-fill" :style="{ width: `${volumePercent}%` }">
                <span class="volume-thumb" />
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  </Transition>
</template>

<style lang="sass" scoped>
.player-bar-transition-enter-active, .player-bar-transition-leave-active
  transition: transform 0.25s ease, opacity 0.25s ease

.player-bar-transition-enter-from, .player-bar-transition-leave-to
  transform: translateY(16px)
  opacity: 0

.player-bar-shell
  padding: 8px 12px 6px
  flex-shrink: 0

.player-bar
  display: grid
  grid-template-columns: minmax(180px, 260px) minmax(180px, 1fr) auto
  align-items: center
  gap: 18px
  min-height: 64px
  padding: 10px 14px
  border: 1px solid rgba(255, 255, 255, 0.08)
  border-radius: 16px
  color: var(--text-color, rgba(255, 255, 255, 0.88))
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.055), rgba(255, 255, 255, 0.02)), rgba(0, 0, 0, 0.18)
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.18), inset 0 1px 0 rgba(255, 255, 255, 0.06)
  backdrop-filter: blur(18px)

.track-meta
  display: flex
  align-items: center
  min-width: 0
  gap: 12px

.track-jacket
  width: 48px
  height: 48px
  flex-shrink: 0
  border-radius: 12px
  object-fit: cover
  background: rgba(255, 255, 255, 0.05)
  box-shadow: 0 8px 18px rgba(0, 0, 0, 0.22)

  &--empty
    display: flex
    align-items: center
    justify-content: center
    color: currentColor
    opacity: 0.62

    svg
      width: 22px
      height: 22px

.track-text
  min-width: 0

.track-title
  overflow: hidden
  text-overflow: ellipsis
  white-space: nowrap
  font-size: 0.9rem
  font-weight: 700
  line-height: 1.25
  opacity: 0.94

.track-artist
  margin-top: 3px
  overflow: hidden
  text-overflow: ellipsis
  white-space: nowrap
  font-size: 0.74rem
  line-height: 1.25
  opacity: 0.48

.seek-section
  min-width: 0

.seek-topline
  display: flex
  justify-content: space-between
  margin-bottom: 7px

.time-label
  font-size: 0.68rem
  line-height: 1
  font-variant-numeric: tabular-nums
  opacity: 0.56

  &--duration
    opacity: 0.38

.seek-track, .volume-track
  position: relative
  display: flex
  align-items: center
  cursor: pointer

  &::before
    content: ''
    position: absolute
    left: 0
    right: 0
    height: 4px
    border-radius: 999px
    background: rgba(255, 255, 255, 0.12)
    transition: height 0.16s ease, background 0.16s ease

  &.is-active::before
    height: 6px
    background: rgba(255, 255, 255, 0.16)

.seek-track
  height: 18px

.seek-fill, .volume-fill
  position: absolute
  left: 0
  display: flex
  align-items: center
  justify-content: flex-end
  height: 4px
  min-width: 0
  max-width: 100%
  border-radius: 999px
  background: color-mix(in srgb, var(--text-color, #ffffff), transparent 38%)
  pointer-events: none
  transition: height 0.16s ease, background 0.16s ease

  .is-active &
    height: 6px
    background: color-mix(in srgb, var(--text-color, #ffffff), transparent 24%)

.seek-thumb, .volume-thumb
  width: 0
  height: 0
  flex-shrink: 0
  border-radius: 999px
  background: color-mix(in srgb, var(--text-color, #ffffff), transparent 6%)
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.28)
  opacity: 0
  transform: scale(0.7)
  transition: width 0.16s ease, height 0.16s ease, opacity 0.16s ease, transform 0.16s ease

  .is-active &
    width: 11px
    height: 11px
    opacity: 1
    transform: scale(1)

.control-section
  display: flex
  align-items: center
  gap: 6px
  min-width: 0

.icon-button
  display: flex
  align-items: center
  justify-content: center
  width: 34px
  height: 34px
  padding: 0
  border: 1px solid transparent
  border-radius: 999px
  color: currentColor
  background: rgba(255, 255, 255, 0.035)
  opacity: 0.72
  cursor: pointer
  transition: transform 0.12s ease, opacity 0.12s ease, background 0.12s ease, border-color 0.12s ease

  svg
    width: 16px
    height: 16px

  &:hover
    opacity: 1
    transform: translateY(-1px)
    background: color-mix(in srgb, var(--text-color, #ffffff), transparent 90%)
    border-color: rgba(255, 255, 255, 0.08)

  &:active
    transform: scale(0.94)

  &--primary
    width: 40px
    height: 40px
    opacity: 0.9
    background: color-mix(in srgb, var(--text-color, #ffffff), transparent 88%)

    svg
      width: 18px
      height: 18px

  &--volume
    width: 32px
    height: 32px

.volume-control
  display: flex
  align-items: center
  gap: 5px
  margin-left: 4px

.volume-track
  width: 76px
  height: 18px

.volume-fill
  background: color-mix(in srgb, var(--text-color, #ffffff), transparent 48%)

@media (max-width: 760px)
  .player-bar
    grid-template-columns: minmax(150px, 1fr) auto
    gap: 12px

  .seek-section
    grid-column: 1 / -1
    grid-row: 2

  .control-section
    justify-self: end

  .volume-track
    display: none
</style>
