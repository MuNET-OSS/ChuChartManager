import { apiClient, getBaseUrl } from './index'

export interface MateAction {
  id: number
  type: number
  emote: string
  hasVoice: boolean
  hasLipSync: boolean
  isSpecialMotion: boolean
  durationMs: number
}

export interface MateEntry {
  id: string
  numericId: number
  name: string
  assetDir: string
  hasThumbnail: boolean
  emoteFileSize: number
  actions: MateAction[]
}

export async function getMateList(source?: string): Promise<MateEntry[]> {
  const { data } = await apiClient.get('/api/Mate/GetMateList', { params: { source } })
  return data
}

function getMateAssetUrl(action: 'GetMateThumbnail' | 'GetMateWebGLData', mate: MateEntry): string {
  const params = new URLSearchParams({ assetDir: mate.assetDir, mateId: mate.id })
  return `${getBaseUrl()}/api/Mate/${action}?${params}`
}

export function getMateThumbnailUrl(mate: MateEntry): string {
  return getMateAssetUrl('GetMateThumbnail', mate)
}

export function getMateWebGLDataUrl(mate: MateEntry): string {
  return getMateAssetUrl('GetMateWebGLData', mate)
}
