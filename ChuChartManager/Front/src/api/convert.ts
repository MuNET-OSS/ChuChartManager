import { apiClient } from './index'

export type ChartFormat = 'c2s' | 'ugc' | 'sus'

export interface ConvertResult {
  success: boolean
  output: string
  alerts: string[]
  error?: string
}

export async function convertChart(sourceFormat: ChartFormat, targetFormat: ChartFormat, content: string): Promise<ConvertResult> {
  const { data } = await apiClient.post('/api/Convert/ConvertChart', { sourceFormat, targetFormat, content })
  return data
}

export async function convertFile(file: File, targetFormat: ChartFormat): Promise<Blob> {
  const form = new FormData()
  form.append('file', file)
  form.append('targetFormat', targetFormat)
  const { data } = await apiClient.post('/api/Convert/ConvertFile', form, { responseType: 'blob' })
  return data
}
