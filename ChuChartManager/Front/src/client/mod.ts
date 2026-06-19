import { apiClient } from '@/api'
import axios from 'axios'

export interface ModStatus {
  loaderInstalled: boolean
  mods: { name: string; version: string }[]
}

export interface LocalizedText {
  zh: string
  en: string
}

export interface ManifestSection {
  id: string
  label: LocalizedText
  description?: LocalizedText
  default_enabled: boolean
  always_enabled?: boolean
  entries?: ManifestEntry[]
}

export interface ManifestEntry {
  key: string
  type: 'bool' | 'int' | 'float' | 'string'
  default: unknown
  min?: number
  max?: number
  label: LocalizedText
  description?: LocalizedText
}

export interface Manifest {
  mod: { id: string; name: string; version: string }
  ui: { groups: { id: string; label: LocalizedText; sections: string[] }[] }
  config: { sections: ManifestSection[] }
}

export interface ModConfigSection {
  enabled: boolean
  entries: Record<string, unknown>
}

export interface ModConfig {
  [section: string]: ModConfigSection
}

interface ModConfigResponse {
  sections: ModConfig
}

export interface VersionInfo {
  latest: string
  installed: string
  downloadUrl: string
}

export interface LatestVersions {
  loader: VersionInfo
  applechu: VersionInfo
}

export async function getModStatus(): Promise<ModStatus> {
  const { data } = await apiClient.get('/api/mod/status')
  return data
}

export async function getLatestVersions(): Promise<LatestVersions | null> {
  try {
    const { data } = await apiClient.get('/api/mod/latest-versions')
    return data
  } catch {
    return null
  }
}

function isNotFound(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 404
}

export async function getModManifest(modId: string): Promise<Manifest | null> {
  try {
    const { data } = await apiClient.get(`/api/mod/manifest/${modId}`)
    return data
  } catch (error) {
    if (isNotFound(error)) return null
    throw error
  }
}

export async function getModConfig(modId: string): Promise<ModConfig | null> {
  try {
    const { data } = await apiClient.get<ModConfigResponse>(`/api/mod/config/${modId}`)
    return data.sections
  } catch (error) {
    if (isNotFound(error)) return null
    throw error
  }
}

export async function installLoader(url?: string): Promise<void> {
  await apiClient.post('/api/mod/install-loader', { url })
}

export async function installAppleChu(url?: string): Promise<void> {
  await apiClient.post('/api/mod/install-applechu', { url })
}

export async function saveModConfig(modId: string, sections: ModConfig): Promise<void> {
  await apiClient.put(`/api/mod/config/${modId}`, { sections })
}
