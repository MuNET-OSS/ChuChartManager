import { defineComponent, ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { Button, TextInput, Modal, addToast } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import {
  getResourceList, deleteResource, getResourcePreviewUrl,
  getSystemVoiceCueList, getSystemVoiceAudioUrl,
  getTrophyRankBackgroundUrl,
  type ResourceType, type ResourceListItem,
} from '@/api/customResource'
import { getStageList, deleteStage, getStagePreviewUrl } from '@/api/stage'

type TabKey = ResourceType | 'stage'

const RESOURCE_TYPES: { key: TabKey; icon: string; labelKey: string }[] = [
  { key: 'trophy', icon: 'i-mdi-trophy', labelKey: 'resource.trophy' },
  { key: 'namePlate', icon: 'i-mdi-card-account-details', labelKey: 'resource.namePlate' },
  { key: 'frame', icon: 'i-mdi-image-frame', labelKey: 'resource.frame' },
  { key: 'mapIcon', icon: 'i-mdi-map-marker', labelKey: 'resource.mapIcon' },
  { key: 'avatarAccessory', icon: 'i-mdi-hanger', labelKey: 'resource.avatarAccessory' },
  { key: 'chara', icon: 'i-mdi-account', labelKey: 'resource.chara' },
  { key: 'systemVoice', icon: 'i-mdi-microphone', labelKey: 'resource.systemVoice' },
  { key: 'stage', icon: 'i-mdi-terrain', labelKey: 'resource.stage' },
]

const ROW_HEIGHT = 80
const OVERSCAN = 10

export default defineComponent({
  setup() {
    const { t } = useI18n()

    const activeType = ref<TabKey>('trophy')
    const resources = ref<ResourceListItem[]>([])
    const search = ref('')
    const loading = ref(false)
    const deleteTarget = ref<{ type: string; id: number; name: string; assetDir: string } | null>(null)

    const playingVoice = ref<{ id: number; assetDir: string; cueIndex: number } | null>(null)
    const voiceCueCount = ref<Record<string, number>>({})
    let audioEl: HTMLAudioElement | null = null

    function stopAudio() {
      if (audioEl) {
        audioEl.pause()
        audioEl.src = ''
        audioEl = null
      }
      playingVoice.value = null
    }

    async function playVoiceCue(id: number, assetDir: string, cueIndex: number) {
      const key = `${id}-${assetDir}`
      if (!(key in voiceCueCount.value)) {
        try {
          const info = await getSystemVoiceCueList(id, assetDir)
          voiceCueCount.value[key] = info.cueCount
        } catch {
          addToast({ message: 'No audio found', type: 'error' })
          return
        }
      }

      const isPlaying = playingVoice.value?.id === id
        && playingVoice.value?.assetDir === assetDir
        && playingVoice.value?.cueIndex === cueIndex
      if (isPlaying) { stopAudio(); return }

      stopAudio()
      const url = getSystemVoiceAudioUrl(id, assetDir, cueIndex)
      audioEl = new Audio(url)
      audioEl.onended = () => { playingVoice.value = null }
      audioEl.onerror = () => { playingVoice.value = null }
      audioEl.play()
      playingVoice.value = { id, assetDir, cueIndex }
    }

    const scrollContainerRef = ref<HTMLElement | null>(null)
    const scrollTop = ref(0)
    const containerHeight = ref(600)

    const filteredItems = computed(() => {
      const q = search.value.toLowerCase()
      if (!q) return resources.value
      return resources.value.filter(r => r.name.toLowerCase().includes(q) || String(r.id).includes(q))
    })

    const totalHeight = computed(() => filteredItems.value.length * ROW_HEIGHT)

    const visibleRange = computed(() => {
      const start = Math.max(0, Math.floor(scrollTop.value / ROW_HEIGHT) - OVERSCAN)
      const visibleCount = Math.ceil(containerHeight.value / ROW_HEIGHT) + OVERSCAN * 2
      const end = Math.min(filteredItems.value.length, start + visibleCount)
      return { start, end }
    })

    const visibleItems = computed(() => {
      const { start, end } = visibleRange.value
      return filteredItems.value.slice(start, end).map((item, i) => ({
        ...item,
        _index: start + i,
      }))
    })

    function onScroll(e: Event) {
      const el = e.target as HTMLElement
      scrollTop.value = el.scrollTop
    }

    let resizeObs: ResizeObserver | null = null
    onMounted(() => {
      loadData()
      if (scrollContainerRef.value) {
        containerHeight.value = scrollContainerRef.value.clientHeight
        resizeObs = new ResizeObserver(entries => {
          for (const entry of entries)
            containerHeight.value = entry.contentRect.height
        })
        resizeObs.observe(scrollContainerRef.value)
      }
    })
    onBeforeUnmount(() => { resizeObs?.disconnect(); stopAudio() })

    async function loadData() {
      loading.value = true
      try {
        if (activeType.value === 'stage') {
          const stages = await getStageList()
          resources.value = stages.map(s => ({
            id: s.id,
            name: s.name,
            type: 'stage',
            assetDir: s.assetDir,
            dirPath: '',
            hasImage: s.hasImage,
          }))
        } else {
          resources.value = await getResourceList(activeType.value)
        }
      } finally {
        loading.value = false
        scrollTop.value = 0
        if (scrollContainerRef.value) scrollContainerRef.value.scrollTop = 0
      }
    }

    function confirmDelete(item: { type: string; id: number; name: string; assetDir: string }) {
      deleteTarget.value = item
    }

    async function handleDelete() {
      if (!deleteTarget.value) return
      try {
        if (deleteTarget.value.type === 'stage') {
          await deleteStage(deleteTarget.value.id, deleteTarget.value.assetDir)
        } else {
          await deleteResource(deleteTarget.value.type as ResourceType, deleteTarget.value.id, deleteTarget.value.assetDir)
        }
        addToast({ message: t('common.delete') + ' ✓', type: 'success' })
        deleteTarget.value = null
        await loadData()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    watch(activeType, () => { search.value = ''; stopAudio(); loadData() })

    function renderRow(item: ResourceListItem & { _index: number }) {
      const icon = RESOURCE_TYPES.find(rt => rt.key === item.type)?.icon ?? 'i-mdi-file'
      return (
        <div
          key={`${item.type}-${item.id}-${item.assetDir}`}
          class="flex items-center gap-4 px-4 border-b border-solid border-[oklch(0.93_0.01_var(--hue))] hover:bg-[oklch(0.97_0.02_var(--hue))] transition-colors"
          style={{ position: 'absolute', top: `${item._index * ROW_HEIGHT}px`, left: 0, right: 0, height: `${ROW_HEIGHT}px` }}
        >
          {item.hasImage ? (
            <img
              src={item.type === 'stage'
                ? getStagePreviewUrl(item.id, item.assetDir)
                : getResourcePreviewUrl(item.type as ResourceType, item.id, item.assetDir)}
              class="h-16 max-w-48 rounded-lg object-contain flex-shrink-0"
              loading="lazy"
              onError={(e: Event) => { (e.target as HTMLImageElement).style.display = 'none' }}
            />
          ) : item.type === 'trophy' ? (
            <div class="trophy-preview flex-shrink-0">
              <img
                src={getTrophyRankBackgroundUrl(item.rareType)}
                class="trophy-preview-bg"
                loading="lazy"
                onLoad={(e: Event) => {
                  const img = e.target as HTMLImageElement
                  const textEl = img.parentElement?.querySelector('.trophy-preview-text-inner') as HTMLElement
                  if (!textEl) return
                  textEl.classList.remove('is-overflow')
                  textEl.style.removeProperty('--scroll-distance')
                  textEl.style.removeProperty('--scroll-duration')
                  const parent = textEl.parentElement
                  if (!parent) return
                  const overflow = textEl.scrollWidth - parent.clientWidth
                  if (overflow > 2) {
                    const duration = Math.max(3, overflow / 20)
                    textEl.style.setProperty('--scroll-distance', `-${overflow}px`)
                    textEl.style.setProperty('--scroll-duration', `${duration}s`)
                    textEl.classList.add('is-overflow')
                  }
                }}
              />
              <span class="trophy-preview-text">
                <span class="trophy-preview-text-inner">
                  {item.name}
                </span>
              </span>
            </div>
          ) : (
            <div class="w-16 h-16 rounded-lg flex-shrink-0 bg-[oklch(0.95_0.01_var(--hue))] flex items-center justify-center">
              <span class={[icon, 'text-6 op-30']} />
            </div>
          )}
          <div class="flex-1 min-w-0">
            <div class="text-sm font-medium truncate">{item.name}</div>
            <div class="text-xs op-40 mt-0.5">ID: {item.id} · {item.assetDir}</div>
          </div>
          {item.type === 'systemVoice' && (() => {
            const key = `${item.id}-${item.assetDir}`
            const count = voiceCueCount.value[key] ?? 0
            const isActive = playingVoice.value?.id === item.id && playingVoice.value?.assetDir === item.assetDir
            return (
              <div class="flex items-center gap-1 flex-shrink-0">
                {count > 0
                  ? Array.from({ length: Math.min(count, 8) }, (_, i) => (
                      <button
                        key={i}
                        class={[
                          'w-6 h-6 rounded-full flex items-center justify-center text-xs border border-solid cursor-pointer transition-all',
                          isActive && playingVoice.value?.cueIndex === i
                            ? 'bg-[oklch(0.85_0.1_var(--hue))] border-[oklch(0.6_0.15_var(--hue))] text-[oklch(0.35_0.15_var(--hue))]'
                            : 'bg-transparent border-[oklch(0.88_0.02_var(--hue))] op-50 hover:op-80',
                        ]}
                        onClick={(e: Event) => { e.stopPropagation(); playVoiceCue(item.id, item.assetDir, i) }}
                      >
                        {isActive && playingVoice.value?.cueIndex === i
                          ? <span class="i-mdi-stop text-3" />
                          : i + 1}
                      </button>
                    ))
                  : <span
                      class="i-mdi-play-circle-outline text-5 op-30 hover:op-70 cursor-pointer transition-colors"
                      onClick={(e: Event) => { e.stopPropagation(); playVoiceCue(item.id, item.assetDir, 0) }}
                    />
                }
              </div>
            )
          })()}
          <span
            class="i-mdi-delete-outline text-4.5 op-25 hover:op-70 cursor-pointer flex-shrink-0 hover:text-red-5 transition-colors"
            onClick={() => confirmDelete({ type: item.type, id: item.id, name: item.name, assetDir: item.assetDir })}
          />
        </div>
      )
    }

    return () => (
      <div class="flex flex-col h-full">
        <div class="flex items-center gap-4 p-4 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
          <h2 class="text-xl font-bold m-0 flex-shrink-0">{t('resource.title')}</h2>
          <div class="flex gap-1 flex-wrap">
            {RESOURCE_TYPES.map(rt => (
              <button
                key={rt.key}
                class={[
                  'flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm border border-solid transition-all cursor-pointer',
                  activeType.value === rt.key
                    ? 'bg-[oklch(0.92_0.05_var(--hue))] border-[oklch(0.7_0.1_var(--hue))] text-[oklch(0.4_0.15_var(--hue))]'
                    : 'bg-transparent border-[oklch(0.88_0.02_var(--hue))] text-[oklch(0.5_0.02_var(--hue))] hover:bg-[oklch(0.96_0.02_var(--hue))]',
                ]}
                onClick={() => { activeType.value = rt.key }}
              >
                <span class={[rt.icon, 'text-4']} />
                {t(rt.labelKey)}
              </button>
            ))}
          </div>
        </div>

        <div class="flex items-center gap-3 px-4 py-3 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
          <TextInput v-model:value={search.value} placeholder={t('resource.search')} />
          <span class="text-xs op-40 flex-shrink-0">{filteredItems.value.length} {t('resource.items')}</span>
        </div>

        <div
          ref={scrollContainerRef}
          class="flex-1 of-y-auto"
          onScroll={onScroll}
        >
          {loading.value ? (
            <div class="text-center op-40 py-8">{t('common.loading')}</div>
          ) : filteredItems.value.length === 0 ? (
            <div class="text-center op-40 py-8">{t('resource.noItems')}</div>
          ) : (
            <div style={{ position: 'relative', height: `${totalHeight.value}px` }}>
              {visibleItems.value.map(renderRow)}
            </div>
          )}
        </div>

        <Modal
          show={deleteTarget.value !== null}
          title={t('resource.deleteConfirm')}
          width="min(90vw, 24em)"
          onClose={() => { deleteTarget.value = null }}
        >
          <div class="p-2">
            <p class="text-sm mb-4">
              {t('resource.deleteMessage', { name: deleteTarget.value?.name ?? '', id: deleteTarget.value?.id ?? 0 })}
            </p>
            <div class="flex justify-end gap-2">
              <Button onClick={() => { deleteTarget.value = null }}>{t('common.cancel')}</Button>
              <Button onClick={handleDelete}>{t('common.delete')}</Button>
            </div>
          </div>
        </Modal>
      </div>
    )
  },
})
