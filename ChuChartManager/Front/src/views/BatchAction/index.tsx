import { defineComponent, onMounted, ref } from 'vue'
import { Button, Select, addToast } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { getMusicList, getGenreMap, apiClient, type MusicListItem } from '@/api'
import { deleteMusic } from '@/api/customResource'
import MusicSelector from './MusicSelector'

enum STEP { Select, ChooseAction, EditProps, Progress }
type ActionType = 'editProps' | 'delete' | 'exportJackets' | 'exportMp3'

export default defineComponent({
  setup() {
    const { t } = useI18n()
    const step = ref(STEP.Select)
    const allMusic = ref<MusicListItem[]>([])
    const genreMap = ref<Record<number, string>>({})
    const selectedMusic = ref<MusicListItem[]>([])
    const selectedAction = ref<ActionType | ''>('')
    const genreId = ref(-1)
    const executing = ref(false)
    const progressCurrent = ref(0)
    const progressTotal = ref(0)
    const progressItem = ref('')

    const hasA000 = () => selectedMusic.value.some(m => m.assetDir === 'A000')

    const genreOptions: () => SelectOption[] = () => [
      { label: t('batch.notChange'), value: -1 },
      ...Object.entries(genreMap.value).map(([id, name]) => ({ label: name, value: Number(id) })),
    ]

    async function proceed() {
      if (!selectedAction.value) return
      if (selectedAction.value === 'editProps') {
        step.value = STEP.EditProps
        return
      }
      const ids = selectedMusic.value.map(m => ({ id: m.id, assetDir: m.assetDir }))
      executing.value = true
      try {
        if (selectedAction.value === 'delete') {
          step.value = STEP.Progress
          progressTotal.value = ids.length
          progressCurrent.value = 0
          for (const item of ids) {
            progressItem.value = selectedMusic.value.find(m => m.id === item.id)?.name ?? `ID: ${item.id}`
            await deleteMusic(item.id, item.assetDir)
            progressCurrent.value++
          }
          addToast({ message: t('batch.done') + ' ✓', type: 'success' })
          selectedMusic.value = []
          allMusic.value = await getMusicList()
          step.value = STEP.Select
        } else {
          const endpoint = selectedAction.value === 'exportJackets' ? '/api/Music/BatchExportJackets' : '/api/Music/BatchExportMp3'
          const filename = selectedAction.value === 'exportJackets' ? 'jackets.zip' : 'audio.zip'
          step.value = STEP.Progress
          progressTotal.value = 1
          progressCurrent.value = 0
          progressItem.value = t('batch.downloading')
          const resp = await apiClient.post(endpoint, { ids }, { responseType: 'blob', timeout: 300000 })
          const url = URL.createObjectURL(resp.data)
          const a = document.createElement('a')
          a.href = url; a.download = filename; a.click()
          URL.revokeObjectURL(url)
          progressCurrent.value = 1
          addToast({ message: t('batch.done') + ' ✓', type: 'success' })
          step.value = STEP.Select
        }
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
        step.value = STEP.ChooseAction
      } finally {
        executing.value = false
      }
    }

    async function saveEditProps() {
      if (genreId.value < 0) return
      executing.value = true
      try {
        const ids = selectedMusic.value.map(m => ({ id: m.id, assetDir: m.assetDir }))
        await apiClient.post('/api/Music/BatchSetProps', { ids, genreId: genreId.value, genreName: genreMap.value[genreId.value] ?? '' })
        addToast({ message: t('batch.done') + ' ✓', type: 'success' })
        allMusic.value = await getMusicList()
        selectedMusic.value = []
        step.value = STEP.Select
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      } finally {
        executing.value = false
      }
    }

    onMounted(async () => {
      const [music, genres] = await Promise.all([getMusicList(), getGenreMap()])
      allMusic.value = music
      genreMap.value = genres
    })

    return () => (
      <div class="flex flex-col h-full p-4">
        {step.value === STEP.Select && (
          <MusicSelector
            musicList={allMusic.value}
            genreMap={genreMap.value}
            selectedMusic={selectedMusic.value}
            onUpdate:selectedMusic={(v: MusicListItem[]) => { selectedMusic.value = v }}
            continue={() => { selectedAction.value = ''; step.value = STEP.ChooseAction }}
          />
        )}

        {step.value === STEP.ChooseAction && (
          <div class="flex flex-col gap-3">
            <h3 class="text-lg font-bold m-0">{t('batch.chooseAction')}</h3>
            <p class="text-sm op-50 m-0">{t('batch.selected', { count: selectedMusic.value.length })}</p>
            <div class="flex flex-col gap-2 mt-2">
              {([
                { key: 'editProps', icon: 'i-mdi-pencil', label: t('batch.editProps'), disableOnA000: true },
                { key: 'delete', icon: 'i-mdi-delete', label: t('common.delete'), disableOnA000: true },
                { key: 'exportJackets', icon: 'i-mdi-image-multiple', label: t('batch.exportJackets'), disableOnA000: false },
                { key: 'exportMp3', icon: 'i-mdi-music', label: t('batch.exportMp3'), disableOnA000: false },
              ] as const).map(opt => {
                const disabled = opt.disableOnA000 && hasA000()
                return (
                  <label
                    key={opt.key}
                    class={[
                      'flex items-center gap-3 px-4 py-3 rounded-xl border border-solid cursor-pointer transition-all',
                      selectedAction.value === opt.key
                        ? 'border-[oklch(0.7_0.1_var(--hue))] bg-[oklch(0.95_0.03_var(--hue))]'
                        : disabled ? 'border-[oklch(0.9_0.02_var(--hue))] op-40 cursor-not-allowed'
                        : 'border-[oklch(0.9_0.02_var(--hue))] hover:bg-[oklch(0.97_0.02_var(--hue))]',
                    ]}
                  >
                    <input type="radio" name="action" value={opt.key} checked={selectedAction.value === opt.key} disabled={disabled} onChange={() => { selectedAction.value = opt.key as ActionType }} />
                    <span class={[opt.icon, 'text-4.5']} />
                    <span>{opt.label}</span>
                  </label>
                )
              })}
              {hasA000() && <p class="text-xs text-red-4 m-0 mt-1">{t('batch.a000Warning')}</p>}
            </div>
            <div class="flex justify-end gap-2 mt-4">
              <Button onClick={() => { step.value = STEP.Select }}>{t('batch.previous')}</Button>
              <Button disabled={!selectedAction.value} onClick={proceed}>{t('batch.next')}</Button>
            </div>
          </div>
        )}

        {step.value === STEP.EditProps && (
          <div class="flex flex-col gap-3">
            <h3 class="text-lg font-bold m-0">{t('batch.editProps')}</h3>
            <p class="text-sm op-50 m-0">{t('batch.selected', { count: selectedMusic.value.length })}</p>
            <div class="mt-4 max-w-md">
              <label class="text-xs op-50 mb-1 block">{t('batch.genre')}</label>
              <Select v-model:value={genreId.value} options={genreOptions()} />
            </div>
            <div class="flex justify-end gap-2 mt-4">
              <Button onClick={() => { step.value = STEP.ChooseAction }}>{t('batch.previous')}</Button>
              <Button disabled={executing.value} onClick={saveEditProps}>{executing.value ? t('batch.executing') : t('common.save')}</Button>
            </div>
          </div>
        )}

        {step.value === STEP.Progress && (
          <div class="flex flex-col gap-3">
            <h3 class="text-lg font-bold m-0">{t('batch.executing')}</h3>
            <div class="text-sm op-70 mt-4">{progressItem.value}</div>
            <div class="w-full h-2 rounded-full bg-[oklch(0.93_0.01_var(--hue))] overflow-hidden">
              <div class="h-full rounded-full bg-[oklch(0.6_0.15_var(--hue))] transition-all duration-300" style={{ width: progressTotal.value > 0 ? `${(progressCurrent.value / progressTotal.value) * 100}%` : '0%' }} />
            </div>
            <div class="text-xs op-40">{progressCurrent.value} / {progressTotal.value}</div>
          </div>
        )}
      </div>
    )
  },
})
