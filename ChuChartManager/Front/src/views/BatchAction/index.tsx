import { defineComponent, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { getMusicList, getGenreMap, type MusicListItem } from '@/api'
import MusicSelector from './MusicSelector'
import ChooseAction from './ChooseAction'
import EditProps from './EditProps'
import ProgressDisplay from './ProgressDisplay'

export enum STEP {
  None,
  Select,
  ChooseAction,
  EditProps,
  Progress,
}

export default defineComponent({
  setup() {
    useI18n()
    const step = ref(STEP.None)
    const allMusic = ref<MusicListItem[]>([])
    const genreMap = ref<Record<number, string>>({})
    const selected = ref<MusicListItem[]>([])

    const reset = () => {
      step.value = STEP.Select
      selected.value = []
    }

    onMounted(async () => {
      const [music, genres] = await Promise.all([getMusicList(), getGenreMap()])
      allMusic.value = music
      genreMap.value = genres
      step.value = STEP.Select
    })

    return () => (
      <div class="flex flex-col h-100dvh p-4">
        {step.value === STEP.Select && (
          <MusicSelector
            musicList={allMusic.value}
            genreMap={genreMap.value}
            selectedMusic={selected.value}
            onUpdate:selectedMusic={(v: MusicListItem[]) => { selected.value = v }}
            continue={() => { step.value = STEP.ChooseAction }}
          />
        )}

        {step.value === STEP.ChooseAction && (
          <ChooseAction
            selectedMusic={selected.value}
            continue={(s: STEP) => { step.value = s }}
            onListUpdated={(list: MusicListItem[]) => { allMusic.value = list; selected.value = [] }}
          />
        )}

        {step.value === STEP.EditProps && (
          <EditProps
            selectedMusic={selected.value}
            genreMap={genreMap.value}
            closeModal={reset}
            onListUpdated={(list: MusicListItem[]) => { allMusic.value = list }}
          />
        )}

        {step.value === STEP.Progress && <ProgressDisplay />}
      </div>
    )
  },
})
