import { apiClient, isWebView } from './index'

export interface OptionDirInfo {
  dirName: string
  musicCount: number
  isCustom: boolean
  version: string
}

export interface ConflictEntry {
  musicId: number
  musicName: string
  dir: string
  conflictDir: string
}

export async function getOptionDirs(): Promise<OptionDirInfo[]> {
  const { data } = await apiClient.get('/api/Option/GetOptionDirs')
  return data
}

export async function createOptionDir(dirName: string): Promise<void> {
  await apiClient.post('/api/Option/CreateOptionDir', JSON.stringify(dirName), {
    headers: { 'Content-Type': 'application/json' },
  })
}

export async function deleteOptionDir(dirName: string): Promise<void> {
  await apiClient.post('/api/Option/DeleteOptionDir', JSON.stringify(dirName), {
    headers: { 'Content-Type': 'application/json' },
  })
}

export async function toggleCustomMark(dirName: string): Promise<void> {
  await apiClient.post('/api/Option/ToggleCustomMark', JSON.stringify(dirName), {
    headers: { 'Content-Type': 'application/json' },
  })
}

export async function checkConflict(dirName: string): Promise<ConflictEntry[]> {
  const { data } = await apiClient.get('/api/Option/CheckConflict', { params: { dirName } })
  return data
}

export async function importLocalOptionDir(): Promise<{ imported: boolean; dirName?: string }> {
  const { data } = await apiClient.post('/api/Option/ImportLocalOptionDir')
  return data
}

export async function rescanOptions(): Promise<void> {
  await apiClient.post('/api/Option/RescanOptions')
}
