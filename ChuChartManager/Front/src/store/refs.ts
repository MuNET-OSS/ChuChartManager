import { ref } from 'vue'
import type { OptionDirInfo } from '@/api/option'
import { getOptionDirs } from '@/api/option'
import type { SidebarKey } from '@/components/Sidebar.vue'

export type LeftPanel = 'musicList' | 'optionDirs'

export const leftPanel = ref<LeftPanel>('musicList')

export const optionDirs = ref<OptionDirInfo[]>([])
export const selectedSource = ref('A000')
export const sidebarActive = ref<SidebarKey>('charts')
/** 设为正数时 MusicList 会跳转到该 ID 并选中 */
export const selectMusicId = ref(-1)

export async function updateOptionDirs() {
  optionDirs.value = await getOptionDirs()
}
