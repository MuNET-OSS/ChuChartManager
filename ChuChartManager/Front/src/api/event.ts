import { apiClient } from './index'

export const SUBSTANCE_TYPE_NAMES: Record<number, string> = {
  0: 'information',
  1: 'map',
  2: 'music',
  3: 'advertiseMovie',
  4: 'recommendMusic',
  5: 'release',
  6: 'course',
  7: 'quest',
  8: 'duel',
  9: 'cmission',
  10: 'changeSurfBoardUI',
  11: 'avatarAccessoryGacha',
  12: 'rightsInfo',
  13: 'dailyBonusPreset',
  14: 'matchingBonus',
  15: 'unlockChallenge',
  16: 'playRewardSet',
  17: 'linkedVerse',
}

export interface StringIdRef {
  id: number
  str: string
}

export interface EventListItem {
  id: number
  name: string
  assetDir: string
  substanceType: number
  substanceTypeName: string
  alwaysOpen: boolean
  teamOnly: boolean
  isKop: boolean
}

export interface EventDetail {
  id: number
  name: string
  dataName: string
  assetDir: string
  netOpenName: string
  netOpenId: number
  text: string
  ddsBannerId: number
  ddsBannerName: string
  informationImagePath: string
  periodDispType: number
  alwaysOpen: boolean
  teamOnly: boolean
  isKop: boolean
  priority: number
  substanceType: number
  substanceTypeName: string
  flagValue: number
  mapRef: StringIdRef | null
  dailyBonusPresetRef: StringIdRef | null
  linkedVerseRef: StringIdRef | null
  cmissionRef: StringIdRef | null
  playRewardSetRef: StringIdRef | null
  unlockChallengeRef: StringIdRef | null
  avatarAccessoryGachaRef: StringIdRef | null
  duelRef: StringIdRef | null
  matchingBonusRef: StringIdRef | null
}

export interface MapListItem {
  id: number
  name: string
  assetDir: string
  mapType: number
  areaCount: number
  filterName: string
}

export interface MapAreaInfo {
  mapAreaId: number
  mapAreaName: string
  ddsMapId: number
  ddsMapName: string
  musicId: number
  musicName: string
  rewardId: number
  rewardName: string
  isHard: boolean
  pageIndex: number
  indexInPage: number
  requiredAchievementCount: number
  gaugeId: number
  gaugeName: string
}

export interface MapDetail {
  id: number
  name: string
  dataName: string
  assetDir: string
  netDispPeriod: boolean
  mapType: number
  hiddenType: number
  unlockText: string
  mapFilterId: number
  mapFilterName: string
  mapFilterData: string
  categoryId: number
  categoryName: string
  stopPageIndex: number
  stopReleaseEventId: number
  stopReleaseEventName: string
  priority: number
  areas: MapAreaInfo[]
}

export async function getEventList(source?: string): Promise<EventListItem[]> {
  const { data } = await apiClient.get('/api/Event/GetEventList', { params: { source } })
  return data
}

export async function getEvent(id: number, assetDir: string): Promise<EventDetail> {
  const { data } = await apiClient.get('/api/Event/GetEvent', { params: { id, assetDir } })
  return data
}

export async function saveEvent(id: number, assetDir: string, body: {
  name: string
  text: string
  periodDispType: number
  alwaysOpen: boolean
  teamOnly: boolean
  isKop: boolean
  priority: number
  substanceType: number
  flagValue: number
}): Promise<void> {
  await apiClient.post('/api/Event/SaveEvent', body, { params: { id, assetDir } })
}

export async function createEvent(body: {
  targetDir: string
  id: number
  name: string
  substanceType: number
}): Promise<void> {
  await apiClient.post('/api/Event/CreateEvent', body)
}

export async function deleteEvent(id: number, assetDir: string): Promise<void> {
  await apiClient.post('/api/Event/DeleteEvent', null, { params: { id, assetDir } })
}

export async function getMapList(source?: string): Promise<MapListItem[]> {
  const { data } = await apiClient.get('/api/Event/GetMapList', { params: { source } })
  return data
}

export async function getMap(id: number, assetDir: string): Promise<MapDetail> {
  const { data } = await apiClient.get('/api/Event/GetMap', { params: { id, assetDir } })
  return data
}

export async function saveMap(id: number, assetDir: string, body: {
  name: string
  netDispPeriod: boolean
  mapType: number
  hiddenType: number
  unlockText: string
  priority: number
  areas: MapAreaInfo[]
}): Promise<void> {
  await apiClient.post('/api/Event/SaveMap', body, { params: { id, assetDir } })
}

export async function createMap(body: {
  targetDir: string
  id: number
  name: string
}): Promise<void> {
  await apiClient.post('/api/Event/CreateMap', body)
}

export async function deleteMap(id: number, assetDir: string): Promise<void> {
  await apiClient.post('/api/Event/DeleteMap', null, { params: { id, assetDir } })
}

export function getDdsMapPreviewUrl(ddsMapId: number): string {
  const base = apiClient.defaults.baseURL || ''
  return `${base}/api/Event/GetDdsMapPreview?ddsMapId=${ddsMapId}`
}

export function getEventInfoImagePreviewUrl(id: number, assetDir: string): string {
  const base = apiClient.defaults.baseURL || ''
  return `${base}/api/Event/GetEventInfoImagePreview?id=${id}&assetDir=${assetDir}`
}

export async function importEventInfoImage(id: number, assetDir: string, imagePath: string): Promise<void> {
  await apiClient.post('/api/Event/ImportEventInfoImage', { imagePath }, { params: { id, assetDir } })
}

export async function createDdsMap(body: {
  targetDir: string
  ddsMapId: number
  ddsMapName: string
  imagePath: string
}): Promise<void> {
  await apiClient.post('/api/Event/CreateDdsMap', body)
}
