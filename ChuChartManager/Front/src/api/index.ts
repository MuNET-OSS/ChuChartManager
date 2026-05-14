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
