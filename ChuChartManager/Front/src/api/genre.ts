import { apiClient } from './index'

export interface GenreItem {
  id: number
  name: string
  assetDir: string
  colorR: number
  colorG: number
  colorB: number
  isCustom: boolean
}

export async function getAllGenres(): Promise<GenreItem[]> {
  const { data } = await apiClient.get('/api/Genre/GetAllGenres')
  return data
}

export async function addGenre(dto: { id: number; assetDir: string; name: string; colorR?: number; colorG?: number; colorB?: number }): Promise<void> {
  await apiClient.post('/api/Genre/AddGenre', dto)
}

export async function editGenre(id: number, dto: { name: string; colorR: number; colorG: number; colorB: number }): Promise<void> {
  await apiClient.post(`/api/Genre/EditGenre/${id}`, dto)
}

export async function deleteGenre(id: number): Promise<void> {
  await apiClient.delete(`/api/Genre/DeleteGenre/${id}`)
}
