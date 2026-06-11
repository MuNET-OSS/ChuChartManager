import { computed, defineComponent, ref, watch } from 'vue'
import { Button, Modal, NumberInput, addToast } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import WaveSurfer from 'wavesurfer.js'
import RegionsPlugin, { type Region } from 'wavesurfer.js/dist/plugins/regions.esm.js'
import { getAudioPreview, setAudioPreview, getAudioUrl } from '@/api'
import { globalCapture } from '@/utils/globalCapture'

export default defineComponent({
  props: {
    id: { type: Number, required: true },
    assetDir: { type: String, required: true },
  },
  setup(props) {
    const { t } = useI18n()
    const show = ref(false)
    const container = ref<HTMLDivElement>()
    const dataLoad = ref(true)
    const saving = ref(false)
    const isPlaying = ref(false)
    const isPlaySection = ref(false)
    const startTime = ref(0)
    const endTime = ref(0)
    const duration = ref(0)

    let ws: WaveSurfer | undefined
    let region: Region | undefined
    let updatingFromInput = false

    const playIcon = computed(() => isPlaying.value ? 'i-mdi-pause' : 'i-mdi-play')

    const syncFromRegion = () => {
      if (region && !updatingFromInput) {
        startTime.value = region.start
        endTime.value = region.end
      }
    }

    const onStartChange = () => {
      if (!region) return
      if (startTime.value >= endTime.value) {
        addToast({ message: t('music.previewStartGtEnd'), type: 'error' })
        startTime.value = region.start
        return
      }
      updatingFromInput = true
      region.setOptions({ start: startTime.value })
      updatingFromInput = false
    }

    const onEndChange = () => {
      if (!region) return
      if (endTime.value <= startTime.value) {
        addToast({ message: t('music.previewEndLtStart'), type: 'error' })
        endTime.value = region.end
        return
      }
      updatingFromInput = true
      region.setOptions({ end: endTime.value, start: region.start })
      updatingFromInput = false
    }

    const destroy = () => {
      ws?.destroy()
      ws = undefined
      region = undefined
      isPlaying.value = false
      isPlaySection.value = false
    }

    const init = async () => {
      dataLoad.value = true
      let saved = { startMs: -1, endMs: -1 }
      try {
        saved = await getAudioPreview(props.id, props.assetDir)
      } catch {
        saved = { startMs: -1, endMs: -1 }
      }

      const regions = RegionsPlugin.create()
      ws = WaveSurfer.create({
        container: container.value!,
        waveColor: 'rgb(107,203,152)',
        progressColor: 'rgb(33,194,118)',
        url: getAudioUrl(props.id, props.assetDir),
        plugins: [regions],
      })

      ws.on('decode', dur => {
        duration.value = dur
        region = regions.addRegion({
          start: saved.startMs >= 0 ? saved.startMs / 1000 : 0,
          end: saved.endMs >= 0 ? saved.endMs / 1000 : dur,
          drag: true,
          resize: true,
          id: 'preview',
        })
        syncFromRegion()
        region.on('update', syncFromRegion)
        region.on('update-end', syncFromRegion)
        dataLoad.value = false
      })

      regions.on('region-out', () => {
        if (isPlaySection.value) region?.play()
      })

      ws.on('finish', () => { isPlaying.value = false })
    }

    watch(show, async value => {
      if (value) {
        await new Promise(r => setTimeout(r, 0))
        await init()
      } else {
        destroy()
      }
    })

    const save = async () => {
      if (!region) return
      saving.value = true
      try {
        await setAudioPreview(props.id, props.assetDir, Math.round(region.start * 1000), Math.round(region.end * 1000))
        addToast({ message: t('music.previewSaved'), type: 'success' })
        show.value = false
      } catch (e) {
        globalCapture(e, t('music.previewSaveFailed'))
      } finally {
        saving.value = false
      }
    }

    return () => (
      <>
        <Button onClick={() => show.value = true}>{t('music.editPreview')}</Button>
        <Modal width="min(90vw,55em)" title={t('music.editPreview')} v-model:show={show.value}>
          {{
            default: () => (
              <div class="relative flex flex-col gap-3">
                {dataLoad.value && (
                  <div class="absolute inset-0 flex items-center justify-center bg-black/10 z-10">
                    <div class="i-mdi-loading animate-spin text-2xl" />
                  </div>
                )}
                <div class="text-sm op-60">{t('music.previewHint')}</div>
                <div ref={container} />
                <div class="flex gap-2 justify-center">
                  <Button
                    onClick={() => {
                      isPlaySection.value = false
                      if (isPlaying.value) ws?.pause()
                      else ws?.play()
                      isPlaying.value = !isPlaying.value
                    }}
                  >
                    <span class={`text-lg ${playIcon.value}`} />
                  </Button>
                  <Button
                    onClick={() => {
                      isPlaySection.value = true
                      isPlaying.value = true
                      region?.play()
                    }}
                  >
                    <span class="i-mdi-play text-lg mr-1" />
                    {t('music.previewPlayRegion')}
                  </Button>
                </div>
                <div class="flex gap-4 items-center">
                  <div class="flex flex-col gap-1 w-0 grow">
                    <div class="ml-1 text-sm">{t('music.previewStart')}</div>
                    <NumberInput v-model:value={startTime.value} min={0} max={duration.value} step={0.001} decimal={3} onChange={onStartChange} />
                  </div>
                  <div class="flex flex-col gap-1 w-0 grow">
                    <div class="ml-1 text-sm">{t('music.previewEnd')}</div>
                    <NumberInput v-model:value={endTime.value} min={0} max={duration.value} step={0.001} decimal={3} onChange={onEndChange} />
                  </div>
                </div>
              </div>
            ),
            actions: () => (
              <Button class="w-0 grow" ing={saving.value} disabled={dataLoad.value} onClick={save}>
                {t('common.save')}
              </Button>
            ),
          }}
        </Modal>
      </>
    )
  },
})
