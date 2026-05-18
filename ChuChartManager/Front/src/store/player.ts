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

export function play(music: MusicListItem) {
  playingMusic.value = music
  audio.src = getAudioUrl(music.id, music.assetDir)
  audio.play()
  isPlaying.value = true
}

export function togglePlayPause() {
  if (isPlaying.value) { audio.pause(); isPlaying.value = false }
  else { audio.play(); isPlaying.value = true }
}

export function stop() {
  audio.pause()
  audio.currentTime = 0
  isPlaying.value = false
  playingMusic.value = null
}

export function seek(time: number) {
  audio.currentTime = time
  currentTime.value = time
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
  // 兜底：如果 seeked 未触发（如音频未加载），300ms 后强制解锁
  setTimeout(unlock, 300)
}
