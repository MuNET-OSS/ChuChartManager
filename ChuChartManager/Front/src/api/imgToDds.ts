import { apiClient } from './index'

export interface ConvertImgToDdsRequest {
  sourcePath: string
  format?: 'bc1' | 'bc3' | 'bc7'
  width?: number
  height?: number
  generateMipMaps?: boolean
}

export async function convertImgToDds(params: ConvertImgToDdsRequest): Promise<void> {
  const response = await apiClient.post('/api/Tools/ConvertImageToDds', params, {
    responseType: 'blob',
  })

  const contentDisposition = response.headers['content-disposition']
  let fileName = 'output.dds'
  if (contentDisposition) {
    const match = contentDisposition.match(/filename\*?=(?:UTF-8'')?([^;\s]+)/i)
    if (match) fileName = decodeURIComponent(match[1])
  }

  const blob = new Blob([response.data], { type: 'application/octet-stream' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}
