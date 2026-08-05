import { apiClient, getBaseUrl } from './index'

export interface EmoteDataItem {
  id: number
  name: string
  dataName: string
  assetDir: string
  fileName: string
  filePath: string
  fileSize: number
}

export async function getEmoteDataList(source?: string): Promise<EmoteDataItem[]> {
  const { data } = await apiClient.get('/api/Emote/GetEmoteDataList', { params: { source } })
  return data
}

export async function launchViewer(filePath: string): Promise<void> {
  await apiClient.post('/api/Emote/LaunchViewer', { filePath })
}

export function getEmoteWebGLDataUrl(filePath: string): string {
  return `${getBaseUrl()}/api/Emote/GetEmoteWebGLData?filePath=${encodeURIComponent(filePath)}`
}
