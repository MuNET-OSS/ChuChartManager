import { BlobWriter, ZipReader } from '@zip.js/zip.js'
import { addToast } from '@munet/ui'
import { getBaseUrl, type MusicListItem } from '@/api'
import getSubDirFile from '@/utils/getSubDirFile'
import { sanitizeFsSegment } from '@/utils/sanitizeFsName'
import { OPTIONS, SUBDIR } from './ChooseAction'
import { STEP } from './index'
import { currentProcessItem, progressAll, progressCurrent } from './ProgressDisplay'

function getExportUrl(music: MusicListItem, action: OPTIONS): string {
  const base = `${getBaseUrl()}/api/Music`
  const params = `?id=${music.id}&assetDir=${encodeURIComponent(music.assetDir)}`
  switch (action) {
    case OPTIONS.ExportOpt:
      return `${base}/ExportOpt${params}`
    case OPTIONS.ExportUgcByName:
    case OPTIONS.ExportUgcById:
      return `${base}/ExportCustom${params}&format=ugc&stripRoot=true`
    case OPTIONS.ExportSusByName:
    case OPTIONS.ExportSusById:
      return `${base}/ExportCustom${params}&format=sus&stripRoot=true`
    default:
      throw new Error(`Unsupported action: ${action}`)
  }
}

function getMaxParallel(action: OPTIONS): number {
  const cpu = Math.max(1, navigator.hardwareConcurrency || 4)
  switch (action) {
    case OPTIONS.ExportUgcByName:
    case OPTIONS.ExportUgcById:
    case OPTIONS.ExportSusByName:
    case OPTIONS.ExportSusById:
      return Math.max(1, Math.floor(cpu / 4))
    default:
      return Math.max(1, Math.floor(cpu / 2))
  }
}

function getExportDir(
  music: MusicListItem,
  action: OPTIONS,
  subdir: SUBDIR,
  unknownLabel: string,
): string {
  if (action === OPTIONS.ExportOpt) return ''

  let parentDir = ''
  if (subdir === SUBDIR.Genre) {
    parentDir = sanitizeFsSegment(music.genres[0] || unknownLabel, unknownLabel)
  }

  const byId = action === OPTIONS.ExportUgcById || action === OPTIONS.ExportSusById
  if (byId) {
    return parentDir ? `${parentDir}/${music.id}` : `${music.id}`
  }

  const safeTitle = sanitizeFsSegment(music.name || unknownLabel, unknownLabel)
  return parentDir ? `${parentDir}/${safeTitle}` : safeTitle
}

export default async function remoteExport(
  setStep: (step: STEP) => void,
  musicList: MusicListItem[],
  action: OPTIONS,
  subdir: SUBDIR,
  t: (key: string, params?: any) => string,
) {
  let folderHandle: FileSystemDirectoryHandle
  try {
    folderHandle = await (window as any).showDirectoryPicker({
      id: 'batchExportSaveDir',
      mode: 'readwrite',
    })
  } catch {
    return
  }

  progressCurrent.value = 0
  progressAll.value = musicList.length
  currentProcessItem.value = ''
  setStep(STEP.Progress)

  const unknownLabel = t('batch.unknown')

  const exportOne = async (music: MusicListItem) => {
    const musicName = music.name || unknownLabel
    currentProcessItem.value = musicName
    const rootDir = getExportDir(music, action, subdir, unknownLabel)

    try {
      const response = await fetch(getExportUrl(music, action))
      if (!response.ok || !response.body) {
        throw new Error(`HTTP ${response.status} ${response.statusText}`)
      }

      const zipReader = new ZipReader(response.body)
      try {
        let hasError = false
        for await (const entry of zipReader.getEntriesGenerator()) {
          try {
            if (entry.filename.endsWith('/')) continue
            if (!('getData' in entry)) continue

            const filename = rootDir ? `${rootDir}/${entry.filename}` : entry.filename
            const fileHandle = await getSubDirFile(folderHandle, filename)
            const writable = await fileHandle.createWritable()
            try {
              const blob = await entry.getData!(new BlobWriter())
              await writable.write(blob)
            } finally {
              await writable.close()
            }
          } catch (e) {
            if (e instanceof TypeError && String(e.message).includes('Name is not allowed')) {
              continue
            }
            hasError = true
            console.error('Failed to export zip entry', { musicName, sourceFile: entry.filename, error: e })
          }
        }
        if (hasError) {
          addToast({ type: 'error', message: `${t('batch.exportFailed')}: ${musicName}` })
        }
      } finally {
        await zipReader.close()
      }
    } catch (e) {
      console.error(e)
      addToast({ type: 'error', message: `${t('batch.exportFailed')}: ${musicName}` })
    }
  }

  let nextIndex = 0
  let completedCount = 0
  const workerCount = Math.min(musicList.length, getMaxParallel(action))

  const worker = async () => {
    while (true) {
      const currentIndex = nextIndex++
      if (currentIndex >= musicList.length) return
      await exportOne(musicList[currentIndex])
      completedCount++
      progressCurrent.value = completedCount
    }
  }

  try {
    await Promise.all(Array.from({ length: workerCount }, () => worker()))
    addToast({ message: t('batch.exportSuccess'), type: 'success' })
  } catch (e) {
    console.error(e)
    addToast({ type: 'error', message: t('batch.exportFailed') })
  } finally {
    currentProcessItem.value = ''
    setStep(STEP.Select)
  }
}
