import { defineComponent, PropType, ref } from 'vue'
import { Button, Radio, Select, Popover, addToast } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { useStorage } from '@vueuse/core'
import { apiClient, getMusicList, type MusicListItem } from '@/api'
import { deleteMusic } from '@/api/customResource'
import { STEP } from './index'
import remoteExport from './remoteExport'

export enum OPTIONS {
  None,
  EditProps,
  Delete,
  ExportOpt,
  ExportUgcByName,
  ExportUgcById,
  ExportSusByName,
  ExportSusById,
  ExportJackets,
  ExportMp3,
}

export enum SUBDIR {
  None,
  Genre,
}

const SUPPORTS_SUBDIR = (a: OPTIONS) =>
  a === OPTIONS.ExportUgcByName ||
  a === OPTIONS.ExportUgcById ||
  a === OPTIONS.ExportSusByName ||
  a === OPTIONS.ExportSusById

const DISABLE_ON_A000 = (a: OPTIONS) => a === OPTIONS.EditProps || a === OPTIONS.Delete

export default defineComponent({
  props: {
    selectedMusic: { type: Array as PropType<MusicListItem[]>, required: true },
    continue: { type: Function as PropType<(step: STEP) => void>, required: true },
    onListUpdated: { type: Function as PropType<(list: MusicListItem[]) => void>, required: true },
  },
  setup(props) {
    const { t } = useI18n()
    const selected = ref(OPTIONS.None)
    const subdir = useStorage<SUBDIR>('batchExportSubdir', SUBDIR.None)
    const loading = ref(false)
    const hasA000 = () => props.selectedMusic.some(m => m.assetDir === 'A000')

    const subdirOptions = (): SelectOption[] => [
      { label: t('batch.subdir.none'), value: SUBDIR.None },
      { label: t('batch.subdir.genre'), value: SUBDIR.Genre },
    ]

    const items: { key: OPTIONS; label: string }[] = [
      { key: OPTIONS.EditProps, label: t('batch.editProps') },
      { key: OPTIONS.Delete, label: t('common.delete') },
      { key: OPTIONS.ExportOpt, label: t('batch.exportOpt') },
      { key: OPTIONS.ExportUgcByName, label: t('batch.exportUgcByName') },
      { key: OPTIONS.ExportUgcById, label: t('batch.exportUgcById') },
      { key: OPTIONS.ExportSusByName, label: t('batch.exportSusByName') },
      { key: OPTIONS.ExportSusById, label: t('batch.exportSusById') },
      { key: OPTIONS.ExportJackets, label: t('batch.exportJackets') },
      { key: OPTIONS.ExportMp3, label: t('batch.exportMp3') },
    ]

    const proceed = async () => {
      switch (selected.value) {
        case OPTIONS.EditProps:
          props.continue(STEP.EditProps)
          return
        case OPTIONS.Delete: {
          loading.value = true
          try {
            for (const m of props.selectedMusic) await deleteMusic(m.id, m.assetDir)
            addToast({ message: t('batch.done'), type: 'success' })
            props.onListUpdated(await getMusicList())
            props.continue(STEP.Select)
          } catch (e: any) {
            addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
          } finally {
            loading.value = false
          }
          return
        }
        case OPTIONS.ExportJackets:
        case OPTIONS.ExportMp3: {
          const endpoint = selected.value === OPTIONS.ExportJackets ? 'BatchExportJackets' : 'BatchExportMp3'
          const filename = selected.value === OPTIONS.ExportJackets ? 'jackets.zip' : 'audio.zip'
          loading.value = true
          try {
            const ids = props.selectedMusic.map(m => ({ id: m.id, assetDir: m.assetDir }))
            const resp = await apiClient.post(`/api/Music/${endpoint}`, { ids }, { responseType: 'blob', timeout: 600000 })
            const url = URL.createObjectURL(resp.data)
            const a = document.createElement('a')
            a.href = url
            a.download = filename
            a.click()
            URL.revokeObjectURL(url)
            addToast({ message: t('batch.exportSuccess'), type: 'success' })
            props.continue(STEP.Select)
          } catch (e: any) {
            addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
          } finally {
            loading.value = false
          }
          return
        }
        case OPTIONS.ExportOpt:
        case OPTIONS.ExportUgcByName:
        case OPTIONS.ExportUgcById:
        case OPTIONS.ExportSusByName:
        case OPTIONS.ExportSusById:
          await remoteExport(props.continue, props.selectedMusic, selected.value, subdir.value, t)
          return
      }
    }

    return () => (
      <div class="flex flex-col gap-3">
        <h3 class="text-lg font-bold m-0">{t('batch.chooseAction')}</h3>
        <p class="text-sm op-50 m-0">{t('batch.selected', { count: props.selectedMusic.length })}</p>

        <fieldset disabled={loading.value} class="border-none p-0 m-0">
          <div class="flex flex-col gap-2">
            {items.map(opt => {
              const disabled = DISABLE_ON_A000(opt.key) && hasA000()
              if (disabled) {
                return (
                  <Popover key={opt.key} trigger="hover">
                    {{
                      trigger: () => (
                        <div class="flex gap-2 items-center op-50">
                          <input type="radio" disabled />
                          <label>{opt.label}</label>
                        </div>
                      ),
                      default: () => t('batch.a000Warning'),
                    }}
                  </Popover>
                )
              }
              return (
                <Radio key={opt.key} k={opt.key} v-model:value={selected.value}>
                  {opt.label}
                </Radio>
              )
            })}

            {SUPPORTS_SUBDIR(selected.value) && (
              <div class="flex items-center gap-2 mt-2 max-w-xs">
                <span class="text-sm op-60 shrink-0">{t('batch.subdir.label')}</span>
                <Select v-model:value={subdir.value} options={subdirOptions()} />
              </div>
            )}
          </div>
        </fieldset>

        <div class="flex justify-end gap-2 mt-4">
          <Button onClick={() => props.continue(STEP.Select)} disabled={loading.value}>
            {t('batch.previous')}
          </Button>
          <Button ing={loading.value} disabled={selected.value === OPTIONS.None} onClick={proceed}>
            {t('batch.next')}
          </Button>
        </div>
      </div>
    )
  },
})
