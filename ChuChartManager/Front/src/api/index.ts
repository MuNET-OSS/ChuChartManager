import axios from 'axios'

export const apiClient = axios.create({
  baseURL: '',
  timeout: 30000,
})

export function getBaseUrl(): string {
  return (globalThis as any).backendUrl || ''
}

apiClient.interceptors.request.use((config) => {
  const base = getBaseUrl()
  if (base) config.baseURL = base
  return config
})

export const isWebView = !!(window as any).chrome?.webview

export function ensureBackendUrl(): Promise<void> {
  return new Promise((resolve) => {
    if (!isWebView) { resolve(); return }
    if ((globalThis as any).backendUrl) { resolve(); return }
    const interval = setInterval(() => {
      if ((globalThis as any).backendUrl) {
        clearInterval(interval)
        resolve()
      }
    }, 50)
  })
}

export interface MusicListItem {
  id: number
  name: string
  artist: string
  genreId: number
  genres: string[]
  releaseTagId: number
  releaseTagStr: string
  assetDir: string
  hasJacket: boolean
  worldsEndTag: string
  isWorldsEnd: boolean
  fumens: (FumenSummary | null)[]
}

export interface FumenSummary {
  index: number
  enable: boolean
  level: number
  levelDecimal: number
  levelDisplay: string
  notesDesigner: string
  noteCount: number
}

export async function getMusicList(source?: string): Promise<MusicListItem[]> {
  const { data } = await apiClient.get('/api/Music/GetMusicList', { params: source ? { source } : {} })
  return data
}

export async function getSources(): Promise<string[]> {
  const { data } = await apiClient.get('/api/Music/GetSources')
  return data
}

export async function getGenreMap(): Promise<Record<number, string>> {
  const { data } = await apiClient.get('/api/Music/GetGenreMap')
  return data
}

export function getJacketUrl(id: number, assetDir: string): string {
  return `${getBaseUrl()}/api/Music/GetJacket?id=${id}&assetDir=${assetDir}`
}

export async function saveMusic(id: number, assetDir: string, dto: {
  name: string
  artist: string
  genreId?: number
  genreName?: string
  releaseTagId?: number
  releaseTagStr?: string
  fumens?: { index: number; enable: boolean; level: number; levelDecimal: number; notesDesigner: string }[]
}): Promise<void> {
  await apiClient.post(`/api/Music/SaveMusic?id=${id}&assetDir=${assetDir}`, dto)
}

export function getAudioUrl(id: number, assetDir: string): string {
  return `${getBaseUrl()}/api/Music/GetAudio?id=${id}&assetDir=${assetDir}`
}

export function getExportMp3Url(id: number, assetDir: string): string {
  return `${getBaseUrl()}/api/Music/ExportMp3?id=${id}&assetDir=${assetDir}`
}

export async function copyMusic(id: number, assetDir: string, targetDir: string): Promise<void> {
  await apiClient.post('/api/Music/CopyMusic', { id, assetDir, targetDir })
}

export async function createMusic(dto: {
  targetDir: string
  id: number
  name: string
  artist: string
  genreId: number
  genreName: string
  releaseTagId: number
  releaseTagStr?: string
}): Promise<void> {
  await apiClient.post('/api/Music/CreateMusic', dto)
}

export async function importJacket(id: number, assetDir: string): Promise<{ imported: boolean }> {
  const { data } = await apiClient.post(`/api/Music/ImportJacket?id=${id}&assetDir=${assetDir}`)
  return data
}

export async function importChart(id: number, assetDir: string, diffIndex: number): Promise<{ imported: boolean; convertedFrom?: string; alerts?: string[] }> {
  const { data } = await apiClient.post(`/api/Music/ImportChart?id=${id}&assetDir=${assetDir}&diffIndex=${diffIndex}`)
  return data
}

export function getExportChartUrl(id: number, assetDir: string, diffIndex: number, format: 'c2s' | 'ugc' | 'sus' = 'ugc'): string {
  return `${getBaseUrl()}/api/Music/ExportChart?id=${id}&assetDir=${assetDir}&diffIndex=${diffIndex}&format=${format}`
}

export interface ImportCheckResult {
  success: boolean
  alerts: string[]
  suggestedId: number
  title: string
  artist: string
  difficulties: { fileName: string; difficulty: number; level: number; levelDecimal: number; designer: string }[]
}

export interface ImportExecuteResult {
  success: boolean
  alerts: string[]
}

export async function importMusicCheck(charts: File[]): Promise<ImportCheckResult> {
  const form = new FormData()
  for (const chart of charts) form.append('charts', chart)
  const { data } = await apiClient.post('/api/Music/ImportMusicCheck', form)
  return data
}

export async function importMusicExecute(params: {
  charts: File[]
  audio: File
  cover?: File
  id: number
  title: string
  artist: string
  genreId: number
  genreName: string
  releaseTagId: number
  releaseTagStr: string
  targetDir: string
}): Promise<ImportExecuteResult> {
  const form = new FormData()
  for (const chart of params.charts) form.append('charts', chart)
  form.append('audio', params.audio)
  if (params.cover) form.append('cover', params.cover)
  form.append('id', params.id.toString())
  form.append('title', params.title)
  form.append('artist', params.artist)
  form.append('genreId', params.genreId.toString())
  form.append('genreName', params.genreName)
  form.append('releaseTagId', params.releaseTagId.toString())
  form.append('releaseTagStr', params.releaseTagStr)
  form.append('targetDir', params.targetDir)
  const { data } = await apiClient.post('/api/Music/ImportMusicExecute', form, { timeout: 120000 })
  return data
}

export function getExportOptUrl(id: number, assetDir: string): string {
  return `${getBaseUrl()}/api/Music/ExportOpt?id=${id}&assetDir=${assetDir}`
}

export function getExportCustomUrl(id: number, assetDir: string, format: 'ugc' | 'sus'): string {
  return `${getBaseUrl()}/api/Music/ExportCustom?id=${id}&assetDir=${assetDir}&format=${format}`
}

export async function openExplorer(id: number, assetDir: string): Promise<void> {
  await apiClient.post(`/api/Music/OpenExplorer?id=${id}&assetDir=${assetDir}`)
}

export async function openXml(id: number, assetDir: string): Promise<void> {
  await apiClient.post(`/api/Music/OpenXml?id=${id}&assetDir=${assetDir}`)
}

export async function changeId(id: number, assetDir: string, newId: number): Promise<void> {
  await apiClient.post(`/api/Music/ChangeId?id=${id}&assetDir=${assetDir}`, newId, {
    headers: { 'Content-Type': 'application/json' },
  })
}

export async function deleteMusic(id: number, assetDir: string): Promise<void> {
  await apiClient.post(`/api/Music/DeleteMusic?id=${id}&assetDir=${assetDir}`)
}

export async function setJacket(id: number, assetDir: string, file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file)
  await apiClient.put(`/api/Music/SetJacket?id=${id}&assetDir=${assetDir}`, form)
}

export async function setAudio(id: number, assetDir: string, file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file)
  await apiClient.put(`/api/Music/SetAudio?id=${id}&assetDir=${assetDir}`, form, { timeout: 120000 })
}

export async function replaceChart(id: number, assetDir: string, diffIndex: number, file: File): Promise<{ imported: boolean; convertedFrom?: string; alerts?: string[] }> {
  const form = new FormData()
  form.append('file', file)
  const { data } = await apiClient.put(`/api/Music/ReplaceChart?id=${id}&assetDir=${assetDir}&diffIndex=${diffIndex}`, form)
  return data
}
