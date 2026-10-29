import { apiClient } from './index'

export type ResourceType = 'trophy' | 'namePlate' | 'frame' | 'mapIcon' | 'avatarAccessory' | 'chara' | 'systemVoice'

export interface ResourceListItem {
  id: number
  name: string
  type: string
  assetDir: string
  dirPath: string
  hasImage: boolean
  rareType: number
}

export async function getResourceList(type: ResourceType, source?: string): Promise<ResourceListItem[]> {
  const { data } = await apiClient.get('/api/CustomResource/GetResourceList', { params: { type, source } })
  return data
}

export async function deleteResource(type: ResourceType, id: number, assetDir: string): Promise<void> {
  await apiClient.post('/api/CustomResource/DeleteResource', null, { params: { type, id, assetDir } })
}

export async function deleteMusic(id: number, assetDir: string): Promise<void> {
  await apiClient.post('/api/CustomResource/DeleteMusic', null, { params: { id, assetDir } })
}

export function getResourcePreviewUrl(type: ResourceType, id: number, assetDir: string): string {
  const base = (globalThis as any).backendUrl || ''
  return `${base}/api/CustomResource/GetResourcePreview?type=${type}&id=${id}&assetDir=${assetDir}`
}

export async function openImageFileDialog(): Promise<string> {
  const { data } = await apiClient.post('/api/CustomResource/OpenImageFileDialog')
  return data
}

export function getLocalImagePreviewUrl(path: string): string {
  const base = (globalThis as any).backendUrl || ''
  return `${base}/api/CustomResource/GetLocalImagePreview?path=${encodeURIComponent(path)}`
}

export interface SystemVoiceCueInfo {
  cueCount: number
  id: number
  assetDir: string
}

export async function getSystemVoiceCueList(id: number, assetDir: string): Promise<SystemVoiceCueInfo> {
  const { data } = await apiClient.get('/api/CustomResource/GetSystemVoiceCueList', { params: { id, assetDir } })
  return data
}

export function getSystemVoiceAudioUrl(id: number, assetDir: string, cueIndex: number): string {
  const base = (globalThis as any).backendUrl || ''
  return `${base}/api/CustomResource/GetSystemVoiceAudio?id=${id}&assetDir=${assetDir}&cueIndex=${cueIndex}`
}

export function getTrophyRankBackgroundUrl(rareType: number): string {
  const base = (globalThis as any).backendUrl || ''
  return `${base}/api/CustomResource/GetTrophyRankBackground?rareType=${rareType}`
}
