import { apiClient } from './index'

export interface ReleaseTagItem {
  id: number
  versionStr: string
  titleName: string
  assetDir: string
  isCustom: boolean
}

export async function getAllReleaseTags(): Promise<ReleaseTagItem[]> {
  const { data } = await apiClient.get('/api/ReleaseTag/GetAllReleaseTags')
  return data
}

export async function getReleaseTagMap(): Promise<Record<number, string>> {
  const { data } = await apiClient.get('/api/ReleaseTag/GetReleaseTagMap')
  return data
}

export async function addReleaseTag(dto: { id: number; assetDir: string; versionStr?: string; titleName?: string }): Promise<void> {
  await apiClient.post('/api/ReleaseTag/AddReleaseTag', dto)
}

export async function editReleaseTag(id: number, dto: { versionStr: string; titleName: string }): Promise<void> {
  await apiClient.post(`/api/ReleaseTag/EditReleaseTag/${id}`, dto)
}

export async function deleteReleaseTag(id: number): Promise<void> {
  await apiClient.delete(`/api/ReleaseTag/DeleteReleaseTag/${id}`)
}
