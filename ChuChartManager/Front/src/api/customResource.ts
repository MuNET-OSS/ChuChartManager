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

export interface CreateTrophyParams {
  targetDir: string
  id: number
  name: string
  rareType: number
  explainText: string
  imagePath?: string
}

export async function createTrophy(params: CreateTrophyParams): Promise<void> {
  await apiClient.post('/api/CustomResource/CreateTrophy', params)
}

export interface CreateNamePlateParams {
  targetDir: string
  id: number
  name: string
  explainText: string
  imagePath: string
}

export async function createNamePlate(params: CreateNamePlateParams): Promise<void> {
  await apiClient.post('/api/CustomResource/CreateNamePlate', params)
}

export async function createFrame(params: CreateNamePlateParams): Promise<void> {
  await apiClient.post('/api/CustomResource/CreateFrame', params)
}

export interface CreateAvatarAccessoryParams {
  targetDir: string
  id: number
  name: string
  explainText: string
  category: number
  iconImagePath: string
  textureImagePath: string
}

export async function createAvatarAccessory(params: CreateAvatarAccessoryParams): Promise<void> {
  await apiClient.post('/api/CustomResource/CreateAvatarAccessory', params)
}

export interface CreateMapIconParams {
  targetDir: string
  id: number
  name: string
  explainText: string
  imagePath: string
}

export async function createMapIcon(params: CreateMapIconParams): Promise<void> {
  await apiClient.post('/api/CustomResource/CreateMapIcon', params)
}

export interface CreateCharaParams {
  targetDir: string
  id: number
  name: string
  works: string
  illustrator: string
  imagePath: string
  imagePathMid: string
  imagePathSmall: string
}

export async function createChara(params: CreateCharaParams): Promise<void> {
  await apiClient.post('/api/CustomResource/CreateChara', params)
}

export interface AddCharaVariantParams {
  targetDir: string
  baseId: number
  variant: number
  name: string
  imagePath: string
  imagePathMid: string
  imagePathSmall: string
  rank?: number
}

export async function addCharaVariant(params: AddCharaVariantParams): Promise<void> {
  await apiClient.post('/api/CustomResource/AddCharaVariant', params)
}

export interface CreateSystemVoiceParams {
  targetDir: string
  id: number
  name: string
  explainText: string
  imagePath: string
}

export async function createSystemVoice(params: CreateSystemVoiceParams): Promise<void> {
  await apiClient.post('/api/CustomResource/CreateSystemVoice', params)
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
