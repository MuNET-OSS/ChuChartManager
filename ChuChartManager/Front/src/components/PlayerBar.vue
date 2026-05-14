<script setup lang="ts">
import { Button } from '@munet/ui'
import { playingMusic, isPlaying, currentTime, duration, volume, togglePlayPause, stop, startSeek, endSeek } from '@/store/player'
import { getJacketUrl } from '@/api'

function onSeekInput(e: Event) {
  currentTime.value = parseFloat((e.target as HTMLInputElement).value)
}
function onSeekEnd(e: Event) {
  endSeek(parseFloat((e.target as HTMLInputElement).value))
}
function onVolume(e: Event) { volume.value = parseFloat((e.target as HTMLInputElement).value) }
function fmt(s: number): string { return `${Math.floor(s / 60).toString().padStart(2, '0')}:${Math.floor(s % 60).toString().padStart(2, '0')}` }
</script>

<template>
  <div v-if="playingMusic" class="player-bar">
    <img v-if="playingMusic.hasJacket" :src="getJacketUrl(playingMusic.id, playingMusic.assetDir)" class="player-jacket" />
    <div class="player-info">
      <div class="text-sm truncate font-medium">{{ playingMusic.name }}</div>
      <div class="text-xs op-50 truncate">{{ playingMusic.artist }}</div>
    </div>
    <div class="player-controls">
      <Button @click="togglePlayPause" class="player-btn">{{ isPlaying ? 'II' : 'Play' }}</Button>
      <Button @click="stop" class="player-btn">Stop</Button>
    </div>
    <span class="text-xs op-60 w-11 text-right shrink-0">{{ fmt(currentTime) }}</span>
    <input
      type="range" class="player-progress" min="0" :max="duration || 1" step="0.1"
      :value="currentTime"
      @mousedown="startSeek" @touchstart="startSeek"
      @input="onSeekInput"
      @mouseup="onSeekEnd" @touchend="onSeekEnd"
    />
    <span class="text-xs op-60 w-11 shrink-0">{{ fmt(duration) }}</span>
    <div class="player-volume">
      <span class="text-xs op-40">Vol</span>
      <input type="range" class="player-volume-slider" min="0" max="1" step="0.01" :value="volume" @input="onVolume" />
    </div>
  </div>
</template>

<style lang="sass" scoped>
.player-bar
  display: flex
  align-items: center
  gap: 12px
  padding: 8px 16px
  border-top: 1px solid rgba(255, 255, 255, 0.08)
  background: rgba(0, 0, 0, 0.15)
  backdrop-filter: blur(10px)
  flex-shrink: 0

.player-jacket
  width: 40px
  height: 40px
  border-radius: 6px
  object-fit: cover
  flex-shrink: 0

.player-info
  min-width: 0
  width: 160px
  flex-shrink: 0

.player-controls
  display: flex
  gap: 4px
  flex-shrink: 0

.player-btn
  height: 2em !important
  padding: 0 0.8em !important
  font-size: 0.8em !important

.player-progress
  flex: 1
  height: 4px
  -webkit-appearance: none
  appearance: none
  background: rgba(255, 255, 255, 0.15)
  border-radius: 2px
  outline: none
  cursor: pointer

  &::-webkit-slider-thumb
    -webkit-appearance: none
    width: 12px
    height: 12px
    border-radius: 50%
    background: var(--text-color, #666)
    cursor: pointer

.player-volume
  display: flex
  align-items: center
  gap: 4px
  flex-shrink: 0

.player-volume-slider
  width: 70px
  height: 3px
  -webkit-appearance: none
  appearance: none
  background: rgba(255, 255, 255, 0.15)
  border-radius: 2px
  outline: none
  cursor: pointer

  &::-webkit-slider-thumb
    -webkit-appearance: none
    width: 10px
    height: 10px
    border-radius: 50%
    background: var(--text-color, #666)
    cursor: pointer
</style>
