import { apiClient } from '.'

export interface StageListItem {
  id: number
  name: string
  assetDir: string
  hasImage: boolean
}

export interface StageDetail {
  id: number
  name: string
  dataName: string
  assetDir: string
  notesFieldLine: string
  notesFieldLineId: number
  defaultHave: boolean
}

export interface CreateStageDto {
  targetDir: string
  id: number
  name: string
  imagePath: string
  notesFieldLineId: number
  notesFieldLine: string
}

export interface SaveStageDto {
  name: string
  notesFieldLineId: number
  notesFieldLine: string
}

export async function getStageList(source?: string): Promise<StageListItem[]> {
  const { data } = await apiClient.get('/api/Stage/GetStageList', { params: source ? { source } : {} })
  return data
}

export async function getStage(id: number, assetDir: string): Promise<StageDetail> {
  const { data } = await apiClient.get('/api/Stage/GetStage', { params: { id, assetDir } })
  return data
}

export async function createStage(dto: CreateStageDto): Promise<void> {
  await apiClient.post('/api/Stage/CreateStage', dto)
}

export async function saveStage(id: number, assetDir: string, dto: SaveStageDto): Promise<void> {
  await apiClient.post(`/api/Stage/SaveStage?id=${id}&assetDir=${assetDir}`, dto)
}

export async function deleteStage(id: number, assetDir: string): Promise<void> {
  await apiClient.post(`/api/Stage/DeleteStage?id=${id}&assetDir=${assetDir}`)
}

export function getStagePreviewUrl(id: number, assetDir: string): string {
  const base = (globalThis as any).backendUrl || ''
  return `${base}/api/Stage/GetStagePreview?id=${id}&assetDir=${assetDir}`
}
