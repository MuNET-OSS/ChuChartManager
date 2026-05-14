import { apiClient } from './index'

export interface PresetListItem {
  id: number
  name: string
  assetDir: string
  bonusCount: number
  disabled: boolean
}

export interface BonusEntry {
  id: number
  name: string
  presentId: number
  presentName: string
  itemNum: number
  needLoginDayCount: number
  categoryType: number
  disabled: boolean
}

export interface PresetDetail {
  id: number
  name: string
  dataName: string
  assetDir: string
  disabled: boolean
  bonuses: BonusEntry[]
}

export async function getPresetList(source?: string): Promise<PresetListItem[]> {
  const { data } = await apiClient.get('/api/LoginBonus/GetPresetList', { params: { source } })
  return data
}

export async function getPreset(id: number, assetDir: string): Promise<PresetDetail> {
  const { data } = await apiClient.get('/api/LoginBonus/GetPreset', { params: { id, assetDir } })
  return data
}

export async function savePreset(id: number, assetDir: string, body: {
  name: string
  disabled: boolean
  bonuses: BonusEntry[]
}): Promise<void> {
  await apiClient.post('/api/LoginBonus/SavePreset', body, { params: { id, assetDir } })
}

export async function createPreset(body: {
  targetDir: string
  id: number
  name: string
}): Promise<void> {
  await apiClient.post('/api/LoginBonus/CreatePreset', body)
}

export async function deletePreset(id: number, assetDir: string): Promise<void> {
  await apiClient.post('/api/LoginBonus/DeletePreset', null, { params: { id, assetDir } })
}
