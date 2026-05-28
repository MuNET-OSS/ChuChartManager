import { defineComponent, PropType, ref } from 'vue'
import { Button, Select, addToast } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { apiClient, getMusicList, type MusicListItem } from '@/api'

export default defineComponent({
  props: {
    selectedMusic: { type: Array as PropType<MusicListItem[]>, required: true },
    genreMap: { type: Object as PropType<Record<number, string>>, required: true },
    closeModal: { type: Function as PropType<() => void>, required: true },
    onListUpdated: { type: Function as PropType<(list: MusicListItem[]) => void>, required: true },
  },
  setup(props) {
    const { t } = useI18n()
    const genreId = ref(-1)
    const loading = ref(false)

    const genreOptions = (): SelectOption[] => [
      { label: t('batch.notChange'), value: -1 },
      ...Object.entries(props.genreMap).map(([id, name]) => ({ label: name, value: Number(id) })),
    ]

    const save = async () => {
      loading.value = true
      try {
        const ids = props.selectedMusic.map(m => ({ id: m.id, assetDir: m.assetDir }))
        await apiClient.post('/api/Music/BatchSetProps', {
          ids,
          genreId: genreId.value,
          genreName: props.genreMap[genreId.value] ?? '',
        })
        addToast({ message: t('batch.done'), type: 'success' })
        props.onListUpdated(await getMusicList())
        props.closeModal()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      } finally {
        loading.value = false
      }
    }

    return () => (
      <fieldset disabled={loading.value} class="border-none p-0 m-0 flex flex-col gap-3">
        <h3 class="text-lg font-bold m-0">{t('batch.editProps')}</h3>
        <p class="text-sm op-50 m-0">{t('batch.selected', { count: props.selectedMusic.length })}</p>
        <div class="mt-4 max-w-md">
          <label class="text-xs op-50 mb-1 block">{t('batch.genre')}</label>
          <Select v-model:value={genreId.value} options={genreOptions()} />
        </div>
        <div class="flex justify-end gap-2 mt-4">
          <Button onClick={props.closeModal} disabled={loading.value}>{t('batch.previous')}</Button>
          <Button ing={loading.value} onClick={save}>{t('common.save')}</Button>
        </div>
      </fieldset>
    )
  },
})
