import { ref, watch } from 'vue'
import type { MusicListItem } from '@/api'
import { getAudioUrl } from '@/api'

const audio = new Audio()
audio.volume = 0.8

export const playingMusic = ref<MusicListItem | null>(null)
export const isPlaying = ref(false)
export const currentTime = ref(0)
export const duration = ref(0)
export const volume = ref(0.8)
export const isSeeking = ref(false)

audio.addEventListener('timeupdate', () => {
  if (!isSeeking.value) currentTime.value = audio.currentTime
})
audio.addEventListener('loadedmetadata', () => { duration.value = audio.duration })
audio.addEventListener('durationchange', () => { if (Number.isFinite(audio.duration)) duration.value = audio.duration })
audio.addEventListener('ended', () => { isPlaying.value = false })

watch(volume, (v) => { audio.volume = v })

export function loadMusic(music: MusicListItem) {
  if (playingMusic.value?.id === music.id && playingMusic.value?.assetDir === music.assetDir) return
  audio.pause()
  isPlaying.value = false
  currentTime.value = 0
  duration.value = 0
  playingMusic.value = music
  audio.src = getAudioUrl(music.id, music.assetDir)
}

export function play(music: MusicListItem) {
  if (playingMusic.value?.id !== music.id || playingMusic.value?.assetDir !== music.assetDir)
    loadMusic(music)
  audio.play()
  isPlaying.value = true
}

export function togglePlayPause() {
  if (!playingMusic.value) return
  if (isPlaying.value) { audio.pause(); isPlaying.value = false }
  else { audio.play(); isPlaying.value = true }
}

export function stop() {
  audio.pause()
  audio.currentTime = 0
  currentTime.value = 0
  isPlaying.value = false
}

export function startSeek() { isSeeking.value = true }
export function endSeek(time: number) {
  audio.currentTime = time
  currentTime.value = time
  const unlock = () => {
    isSeeking.value = false
    audio.removeEventListener('seeked', unlock)
  }
  audio.addEventListener('seeked', unlock, { once: true })
  setTimeout(unlock, 300)
}
