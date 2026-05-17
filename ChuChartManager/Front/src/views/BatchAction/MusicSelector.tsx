import { computed, defineComponent, PropType, reactive, ref, watch } from 'vue'
import { DataTableBaseColumn, DataTableColumns, NDataTable } from 'naive-ui'
import { Button, TextInput } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { getJacketUrl, type MusicListItem } from '@/api'
import { sidebarActive, selectedSource, selectMusicId } from '@/store/refs'
import * as _ from 'lodash-es'

const LEVEL_COLORS = ['#22c55e', '#f59e0b', '#ef4444', '#a855f7', '#1e1e1e', '#6366f1']

export default defineComponent({
  props: {
    musicList: { type: Array as PropType<MusicListItem[]>, required: true },
    genreMap: { type: Object as PropType<Record<number, string>>, required: true },
    selectedMusic: { type: Array as PropType<MusicListItem[]>, required: true },
    continue: { type: Function, required: true },
  },
  emits: ['update:selectedMusic'],
  setup(props, { emit }) {
    const filter = ref('')
    const { t } = useI18n()

    const nameColumn = reactive({
      title: t('batch.colTitle'), key: 'name',
      filterOptionValue: null as string | null,
      filter: (value: any, row: any) => {
        if (!value) return true
        value = value.toString().toLowerCase()
        return row.name.toLowerCase().includes(value) ||
          row.artist.toLowerCase().includes(value) ||
          row.id.toString().includes(value)
      },
    } satisfies DataTableBaseColumn<MusicListItem>)

    const columns = computed(() => [
      { type: 'selection' },
      {
        title: 'Opt', key: 'assetDir', width: 100,
        filterOptions: _.uniq(props.musicList.map(m => m.assetDir)).map(v => ({ label: v, value: v })),
        filter(value, row: MusicListItem) { return row.assetDir === value },
      },
      {
        title: 'ID', key: 'id', width: 100,
        sorter: (a: MusicListItem, b: MusicListItem) => a.id - b.id,
        filterOptions: [
          { label: 'Standard (0-7999)', value: 'std' },
          { label: "World's End (8000+)", value: 'we' },
        ],
        filter(value, row: MusicListItem) {
          return value === 'std' ? row.id < 8000 : row.id >= 8000
        },
      },
      {
        title: t('batch.colJacket'), key: 'jacket', width: '6rem',
        render: (row: MusicListItem) => (
          <img
            src={getJacketUrl(row.id, row.assetDir)}
            class="h-16 w-16 rounded-lg object-cover"
            loading="lazy"
            onError={(e: Event) => { (e.target as HTMLImageElement).style.display = 'none' }}
          />
        ),
      },
      nameColumn,
      {
        title: t('batch.colGenre'), key: 'genreId', width: 160,
        render: (row: MusicListItem) => <span>{row.genres.join(', ')}</span>,
        filterOptions: Object.entries(props.genreMap).map(([id, name]) => ({ label: name, value: Number(id) })),
        filter: 'default' as any,
      },
      {
        title: t('batch.colCharts'), key: 'charts', width: '16em',
        filterOptions: ['BASIC', 'ADVANCED', 'EXPERT', 'MASTER', 'ULTIMA', "WORLD'S END"].map((label, value) => ({ label, value })),
        filter(value, row: MusicListItem) {
          const f = row.fumens[value as number]
          return f != null && f.enable
        },
        render: (row: MusicListItem) => (
          <div class="flex gap-1">
            {row.fumens.map((f, i) =>
              f && f.enable && (
                <span
                  key={i}
                  class="text-white text-xs font-bold rounded-full w-7 h-7 flex items-center justify-center"
                  style={{ backgroundColor: LEVEL_COLORS[i] || '#999' }}
                >{f.levelDisplay}</span>
              )
            )}
          </div>
        ),
      },
      {
        title: t('batch.colJump'), key: 'jump', width: 60,
        render: (row: MusicListItem) => (
          <Button variant="ghost" class="p-2" onClick={() => {
            selectedSource.value = row.assetDir
            selectMusicId.value = row.id
            sidebarActive.value = 'charts'
          }}>
            <span class="i-mdi-open-in-new text-4 op-40" />
          </Button>
        ),
      },
    ] satisfies DataTableColumns<MusicListItem>)

    let debounceTimer: ReturnType<typeof setTimeout> | null = null
    watch(filter, (val) => {
      if (debounceTimer) clearTimeout(debounceTimer)
      debounceTimer = setTimeout(() => { nameColumn.filterOptionValue = val }, 300)
    })

    const checkedKeys = computed<string[]>({
      get: () => props.selectedMusic.map(m => `${m.assetDir}:${m.id}`),
      set: (value) => {
        const selected = value.map(k => {
          const [assetDir, id] = k.split(':')
          return props.musicList.find(m => m.assetDir === assetDir && m.id === Number(id))!
        }).filter(Boolean)
        emit('update:selectedMusic', selected)
      },
    })

    return () => (
      <div class="flex flex-col gap-3 h-full">
        <TextInput placeholder={t('batch.searchPlaceholder')} v-model:value={filter.value} />
        <NDataTable
          columns={columns.value}
          data={props.musicList}
          virtualScroll
          maxHeight="calc(100dvh - 12rem)"
          minRowHeight={80}
          rowKey={(row: MusicListItem) => `${row.assetDir}:${row.id}`}
          v-model:checkedRowKeys={checkedKeys.value}
        />
        <div class="flex items-center justify-between">
          <span class="text-xs op-40">{props.musicList.length} {t('batch.totalMusic')}</span>
          <Button onClick={() => props.continue()} disabled={!checkedKeys.value.length}>{t('batch.next')}</Button>
        </div>
      </div>
    )
  },
})
