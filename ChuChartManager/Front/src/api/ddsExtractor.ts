import { apiClient } from './index'

export interface ExtractResult {
  sourceFile: string
  ddsCount: number
  outputDir: string
  files: string[]
}

export async function openAfbFileDialog(): Promise<string> {
  const { data } = await apiClient.post('/api/DdsExtractor/OpenFileDialog')
  return data
}

export async function openAfbFolderDialog(): Promise<string> {
  const { data } = await apiClient.post('/api/DdsExtractor/OpenFolderDialog')
  return data
}

export async function extractDds(path: string, outputDir?: string): Promise<ExtractResult[]> {
  const { data } = await apiClient.post('/api/DdsExtractor/ExtractDds', { path, outputDir })
  return data
}
