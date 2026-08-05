import { computed, defineComponent, onMounted, ref, watch } from 'vue'
import { Select, addToast, type SelectOption } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import {
  getMateList,
  getMateThumbnailUrl,
  getMateWebGLDataUrl,
  type MateEntry,
} from '@/api/mate'
import EmotePlayerCanvas from '@/views/Emote/EmotePlayerCanvas'

function formatFileSize(bytes: number): string {
  return (bytes / 1024 / 1024).toFixed(2) + ' MB'
}

export default defineComponent({
  setup() {
    const { t } = useI18n()
    const loading = ref(true)
    const mates = ref<MateEntry[]>([])
    const selectedMate = ref<MateEntry | null>(null)
    const selectedSource = ref('')
    const failedThumbnails = ref(new Set<string>())

    const sourceOptions = computed<SelectOption[]>(() => [
      { label: t('mate.allSources'), value: '' },
      ...Array.from(new Set(mates.value.map(mate => mate.assetDir)))
        .sort((left, right) => left.localeCompare(right))
        .map(source => ({ label: source, value: source })),
    ])
    const visibleMates = computed(() => selectedSource.value
      ? mates.value.filter(mate => mate.assetDir === selectedSource.value)
      : mates.value)
    const lipsyncCount = computed(() => selectedMate.value?.actions.filter(action => action.hasLipSync).length ?? 0)

    watch(visibleMates, (items) => {
      if (!selectedMate.value || !items.some(item => item.id === selectedMate.value?.id && item.assetDir === selectedMate.value?.assetDir))
        selectedMate.value = items[0] ?? null
    }, { immediate: true })

    async function loadMates() {
      loading.value = true
      try {
        mates.value = await getMateList()
      } catch (error: unknown) {
        const message = error instanceof Error ? error.message : String(error)
        addToast({ message, type: 'error' })
      } finally {
        loading.value = false
      }
    }

    function mateKey(mate: MateEntry): string {
      return `${mate.assetDir}/${mate.id}`
    }

    function markThumbnailFailed(mate: MateEntry) {
      failedThumbnails.value = new Set(failedThumbnails.value).add(mateKey(mate))
    }

    onMounted(loadMates)

    return () => (
      <div class="h-full flex flex-col of-hidden">
        <header class="flex items-center gap-3 px-4 py-3 border-b border-solid border-[oklch(0.9_0.02_var(--hue))] shrink-0">
          <div class="min-w-0 flex-1">
            <h2 class="text-lg font-bold m-0">{t('mate.title')}</h2>
            <div class="text-xs op-45 mt-0.5">{t('mate.count', { count: visibleMates.value.length })}</div>
          </div>
          <Select
            class="w-44! shrink-0"
            options={sourceOptions.value}
            v-model:value={selectedSource.value}
          />
        </header>

        {loading.value ? (
          <div class="flex-1 flex items-center justify-center op-45">{t('common.loading')}</div>
        ) : visibleMates.value.length === 0 ? (
          <div class="flex-1 flex flex-col items-center justify-center op-35">
            <span class="i-mdi-account-heart-outline text-14 mb-2" />
            <span>{t('mate.noData')}</span>
          </div>
        ) : (
          <div class="grid grid-cols-[minmax(18rem,20rem)_minmax(0,1fr)] flex-1 min-h-0">
            <aside class="of-y-auto p-3 border-r border-solid border-[oklch(0.9_0.02_var(--hue))]">
              <div class="grid grid-cols-[repeat(auto-fill,minmax(8rem,1fr))] gap-2">
                {visibleMates.value.map(mate => {
                  const key = mateKey(mate)
                  const active = selectedMate.value?.id === mate.id && selectedMate.value?.assetDir === mate.assetDir
                  const showThumbnail = mate.hasThumbnail && !failedThumbnails.value.has(key)
                  return (
                    <button
                      key={key}
                      type="button"
                      class={[
                        'block w-full h-auto! text-left p-0 of-hidden cursor-pointer rd border border-solid bg-transparent color-inherit font-inherit transition-colors',
                        active
                          ? 'border-[oklch(0.68_0.17_var(--hue))] bg-[oklch(0.68_0.05_var(--hue)/0.1)]'
                          : 'border-[oklch(0.9_0.02_var(--hue))] hover:border-[oklch(0.68_0.17_var(--hue)/0.55)]',
                      ]}
                      onClick={() => { selectedMate.value = mate }}
                    >
                      <div class="aspect-[25/16] bg-[oklch(0.96_0.01_var(--hue))] flex items-center justify-center of-hidden">
                        {showThumbnail ? (
                          <img
                            src={getMateThumbnailUrl(mate)}
                            alt={mate.name}
                            loading="lazy"
                            class="w-full h-full object-contain"
                            onError={() => markThumbnailFailed(mate)}
                          />
                        ) : (
                          <span class="i-mdi-account-heart-outline text-10 op-25" />
                        )}
                      </div>
                      <div class="px-2 py-1.5 min-w-0">
                        <div class="text-sm font-medium truncate">{mate.name}</div>
                        <div class="text-xs op-40 truncate">{mate.assetDir} · ID {mate.numericId}</div>
                      </div>
                    </button>
                  )
                })}
              </div>
            </aside>

            <main class="min-w-0 min-h-0 of-y-auto p-4">
              {selectedMate.value && (
                <div class="max-w-6xl mx-auto">
                  <div class="flex items-start gap-3 mb-3">
                    <div class="min-w-0 flex-1">
                      <h3 class="text-lg font-bold m-0 truncate">{selectedMate.value.name}</h3>
                      <div class="text-xs op-45 mt-0.5">
                        {selectedMate.value.assetDir} / {selectedMate.value.id}
                      </div>
                    </div>
                    <div class="text-xs op-50 text-right shrink-0">
                      <div>{formatFileSize(selectedMate.value.emoteFileSize)}</div>
                      <div>{t('mate.actionCount', { count: selectedMate.value.actions.length })}</div>
                    </div>
                  </div>

                  <EmotePlayerCanvas
                    dataUrl={getMateWebGLDataUrl(selectedMate.value)}
                    width={640}
                    height={640}
                  />

                  <section class="mt-4 border-t border-solid border-[oklch(0.9_0.02_var(--hue))] pt-3">
                    <div class="flex items-center gap-4 text-sm mb-2">
                      <span class="font-medium">{t('mate.actions')}</span>
                      <span class="text-xs op-45">{t('mate.lipsyncCount', { count: lipsyncCount.value })}</span>
                    </div>
                    <div class="grid grid-cols-[repeat(auto-fill,minmax(12rem,1fr))] gap-x-4 gap-y-1">
                      {selectedMate.value.actions.map(action => (
                        <div key={action.id} class="flex items-center gap-2 min-w-0 py-1 text-xs border-b border-solid border-[oklch(0.94_0.01_var(--hue))]">
                          <span class="op-35 tabular-nums shrink-0">{action.type}</span>
                          <span class="truncate flex-1">{action.emote || t('mate.unnamedAction')}</span>
                          {action.hasLipSync && <span class="i-mdi-waveform text-4 op-50 shrink-0" title={t('mate.lipsync')} />}
                          {action.hasVoice && <span class="i-mdi-volume-high text-4 op-50 shrink-0" title={t('mate.voice')} />}
                        </div>
                      ))}
                    </div>
                  </section>
                </div>
              )}
            </main>
          </div>
        )}
      </div>
    )
  },
})
