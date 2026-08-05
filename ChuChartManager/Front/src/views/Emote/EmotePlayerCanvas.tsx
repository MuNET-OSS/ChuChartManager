import { defineComponent, ref, onMounted, onBeforeUnmount, watch, type PropType } from 'vue'
import { Button } from '@munet/ui'
import { useI18n } from 'vue-i18n'

declare global {
  class EmotePlayer {
    static createRenderCanvas(width: number, height: number): void
    static setRenderCanvas(canvas: HTMLCanvasElement): void
    static renderCanvas: HTMLCanvasElement | null
    static deviceRefCount: number
    static device: unknown
    constructor(canvas: HTMLCanvasElement | null)
    promiseLoadDataFromURL(...urls: string[]): Promise<void>
    unloadData(): void
    coord: [number, number]
    scale: number
    mainTimelineLabel: string
    get mainTimelineLabels(): string[]
    get diffTimelineLabels(): string[]
    diffTimelineSlot1: string
    diffTimelineSlot2: string
    diffTimelineSlot3: string
    diffTimelineSlot4: string
    charaBounds: { left: number; top: number; right: number; bottom: number }
    isCharaProfileAvailable: boolean
    initialized: boolean
    playerId: number | null
    speed: number
    meshDivisionRatio: number
    windSpeed: number
    loadData(...files: Uint8Array[]): void
  }
  function EmotePlayer_PlayTimeline(playerId: number, label: string, flags: number): void
  function EmotePlayer_StopTimeline(playerId: number, label: string): void
  function EmotePlayer_Skip(playerId: number): void
  function EmotePlayer_SetVariable(playerId: number, label: string, value: number, ms?: number, easing?: number): void
  function EmotePlayer_CountVariables(playerId: number): number
  function EmotePlayer_GetVariableLabelAt(playerId: number, index: number): string
}

type LoadState = 'idle' | 'loading' | 'ready' | 'error'

function loadScript(src: string): Promise<void> {
  return new Promise((resolve, reject) => {
    if (document.querySelector(`script[src="${src}"]`)) {
      resolve()
      return
    }
    const s = document.createElement('script')
    s.src = src
    s.onload = () => resolve()
    s.onerror = () => reject(new Error(`Failed to load: ${src}`))
    document.head.appendChild(s)
  })
}

// 照搬官方 SDK demo 初始化顺序（FreeMoteDriver.js → emoteplayer.js → EmotePlayer API）。
// 不调用 EmoteDriver_Start — 官方 demo 里没有这个 API，之前的 timing bug 就是它引起的。

let driverReady = false
let driverPromise: Promise<void> | null = null

function ensureDriver(): Promise<void> {
  if (driverReady) return Promise.resolve()
  if (driverPromise) return driverPromise

  driverPromise = new Promise<void>((resolve, reject) => {
    // TOTAL_MEMORY 必须在 FreeMoteDriver.js 加载前设置，asm.js 启动时读取此值分配 ArrayBuffer
    (window as { Module?: { TOTAL_MEMORY: number } }).Module = { TOTAL_MEMORY: 512 * 1024 * 1024 }

    loadScript('/emote-driver/FreeMoteDriver.js')
      .then(() => loadScript('/emote-driver/emoteplayer.js'))
      .then(() => {
        if (typeof EmotePlayer === 'undefined') {
          reject(new Error('EmotePlayer 未定义，emoteplayer.js 加载可能失败'))
          return
        }
        driverReady = true
        resolve()
      })
      .catch((err) => {
        driverPromise = null
        reject(err)
      })
  })

  return driverPromise
}

const TIMELINE_PLAY_PARALLEL = 1
const TIMELINE_PLAY_DIFFERENCE = 2

export default defineComponent({
  props: {
    dataUrl: { type: String as PropType<string>, default: '' },
    width: { type: Number, default: 640 },
    height: { type: Number, default: 480 },
  },
  setup(props) {
    const { t } = useI18n()
    const canvasRef = ref<HTMLCanvasElement | null>(null)
    const state = ref<LoadState>('idle')
    const errorMsg = ref('')
    const mainTimelines = ref<string[]>([])
    const diffTimelines = ref<string[]>([])
    const motionPanelOpen = ref(false)
    const activeMain = ref('')
    const activeDiffs = ref<string[]>([])
    const paused = ref(false)
    let player: EmotePlayer | null = null
    let loadingUrl = ''

    async function initPlayer() {
      if (!props.dataUrl || !canvasRef.value) return
      if (loadingUrl === props.dataUrl) return
      loadingUrl = props.dataUrl
      const targetUrl = props.dataUrl

      state.value = 'loading'
      errorMsg.value = ''
      mainTimelines.value = []
      diffTimelines.value = []
      motionPanelOpen.value = false
      activeMain.value = ''
      activeDiffs.value = []
      paused.value = false

      try {
        if (player) {
          player.unloadData()
          player = null
        }

        const [, dataBuffer] = await Promise.all([
          ensureDriver(),
          fetch(targetUrl).then(r => {
            if (!r.ok) throw new Error(`${r.status} ${r.statusText}`)
            return r.arrayBuffer()
          }).then(buf => new Uint8Array(buf)),
        ])

        const canvas = canvasRef.value
        if (!canvas || targetUrl !== props.dataUrl) return

        if (!EmotePlayer.renderCanvas) {
          EmotePlayer.createRenderCanvas(props.width, props.height)
          await new Promise(r => requestAnimationFrame(r))
        }

        player = new EmotePlayer(canvas)
        player.loadData(dataBuffer)

        player.windSpeed = 0.5
        if (player.playerId != null)
          EmotePlayer_SetVariable(player.playerId, 'fade_z', 256, 0, 0)

        if (player.isCharaProfileAvailable) {
          const bounds = player.charaBounds
          const charaW = bounds.right - bounds.left
          const charaH = bounds.bottom - bounds.top
          if (charaW > 0 && charaH > 0) {
            const scaleX = props.width / charaW
            const scaleY = props.height / charaH
            const scale = Math.min(scaleX, scaleY) * 0.85
            player.scale = scale
            const centerX = (bounds.left + bounds.right) / 2
            const centerY = (bounds.top + bounds.bottom) / 2
            player.coord = [-centerX * scale, -centerY * scale]
          }
        } else {
          player.scale = 0.25
          player.coord = [0, 100]
        }

        mainTimelines.value = player.mainTimelineLabels || []
        diffTimelines.value = player.diffTimelineLabels || []

        state.value = 'ready'
      } catch (e: unknown) {
        state.value = 'error'
        errorMsg.value = e instanceof Error ? e.message : String(e)
      }
    }

    function playMain(label: string) {
      if (!player?.initialized || player.playerId == null) return
      activeMain.value = label
      EmotePlayer_PlayTimeline(player.playerId, label, TIMELINE_PLAY_PARALLEL)
    }

    function playDiff(label: string) {
      if (!player?.initialized || player.playerId == null) return
      if (activeDiffs.value.includes(label)) {
        EmotePlayer_StopTimeline(player.playerId, label)
        activeDiffs.value = activeDiffs.value.filter(l => l !== label)
      } else {
        EmotePlayer_PlayTimeline(player.playerId, label, TIMELINE_PLAY_PARALLEL | TIMELINE_PLAY_DIFFERENCE)
        activeDiffs.value = [...activeDiffs.value, label]
      }
    }

    function togglePause() {
      if (!player?.initialized) return
      paused.value = !paused.value
      player.speed = paused.value ? 0 : 1
    }

    function stop() {
      if (!player?.initialized || player.playerId == null) return
      EmotePlayer_StopTimeline(player.playerId, '')
      EmotePlayer_Skip(player.playerId)
      EmotePlayer_SetVariable(player.playerId, 'fade_z', 256, 0, 0)
      activeMain.value = ''
      activeDiffs.value = []
    }

    function clear() {
      if (!player?.initialized || player.playerId == null) return
      const count = EmotePlayer_CountVariables(player.playerId)
      for (let i = 0; i < count; i++) {
        const label = EmotePlayer_GetVariableLabelAt(player.playerId, i)
        EmotePlayer_SetVariable(player.playerId, label, 0, 0, 0)
      }
      EmotePlayer_SetVariable(player.playerId, 'fade_z', 256, 0, 0)
    }

    function screenshot() {
      if (!canvasRef.value) return
      const link = document.createElement('a')
      link.download = 'emote-screenshot.png'
      link.href = canvasRef.value.toDataURL('image/png')
      link.click()
    }

    watch(() => props.dataUrl, (url) => {
      loadingUrl = ''
      if (url) initPlayer()
    })

    onMounted(() => {
      if (props.dataUrl) initPlayer()
    })

    onBeforeUnmount(() => {
      if (player) {
        player.unloadData()
        player = null
      }
    })

    const toolBtn = (icon: string, title: string, onClick: () => void) => (
      <Button onClick={onClick} title={title}>
        <span class={[icon, 'text-3.5 op-70']} />
      </Button>
    )

    return () => (
      <div class="w-full min-w-0">
        <div class="flex items-center gap-1.5 mb-3">
          {state.value === 'ready' && (
            <>
              {toolBtn(paused.value ? 'i-mdi-play' : 'i-mdi-pause', paused.value ? 'Play' : 'Pause', togglePause)}
              {toolBtn('i-mdi-stop', 'Stop', stop)}
              {toolBtn('i-mdi-eraser', 'Clear', clear)}
              {toolBtn('i-mdi-camera', 'Screenshot', screenshot)}
              <div class="flex-1" />
              <Button onClick={() => { motionPanelOpen.value = !motionPanelOpen.value }}>
                <span class={['i-mdi-animation-play text-3.5 mr-1', motionPanelOpen.value ? '' : 'op-70']} />
                Motion
              </Button>
            </>
          )}
        </div>

        <div
          class="grid gap-3 items-start"
          style={{ gridTemplateColumns: state.value === 'ready' && motionPanelOpen.value ? 'minmax(0, 1fr) minmax(9rem, 12rem)' : 'minmax(0, 1fr)' }}
        >
          <div class="min-w-0">
            <canvas
              ref={canvasRef}
              id="ccm-emote-player-canvas"
              width={props.width}
              height={props.height}
              class="block rounded-lg w-full h-auto"
              style={{ maxWidth: `${props.width}px`, aspectRatio: `${props.width} / ${props.height}`, background: 'oklch(0.97 0.005 var(--hue))' }}
            />
            {state.value === 'loading' && (
              <div class="text-center op-40 py-6">{t('common.loading')}</div>
            )}
            {state.value === 'error' && (
              <div class="text-center text-red-500 text-xs py-6 px-4">{errorMsg.value}</div>
            )}
          </div>

          {state.value === 'ready' && motionPanelOpen.value && (
            <div class="min-w-0 of-y-auto rounded-lg border border-solid border-[oklch(0.9_0.02_var(--hue))]" style={{ maxHeight: `${props.height}px` }}>
              {mainTimelines.value.length > 0 && (
                <div class="p-2">
                  <div class="text-[10px] font-bold op-40 mb-1 uppercase tracking-wide">Main</div>
                  {mainTimelines.value.map(label => (
                    <div
                      key={label}
                      class={[
                        'text-xs px-2 py-1.5 mb-0.5 rounded-md cursor-pointer truncate transition-colors',
                        activeMain.value === label
                          ? 'bg-[oklch(0.55_0.15_var(--hue))] text-white font-medium'
                          : 'hover:bg-[oklch(0.95_0.02_var(--hue))]',
                      ]}
                      onClick={() => playMain(label)}
                    >{label}</div>
                  ))}
                </div>
              )}
              {diffTimelines.value.length > 0 && (
                <div class="p-2 border-t border-solid border-[oklch(0.9_0.02_var(--hue))]">
                  <div class="text-[10px] font-bold op-40 mb-1 uppercase tracking-wide">Diff</div>
                  {diffTimelines.value.map(label => (
                    <div
                      key={label}
                      class={[
                        'text-xs px-2 py-1.5 mb-0.5 rounded-md cursor-pointer truncate transition-colors',
                        activeDiffs.value.includes(label)
                          ? 'bg-[oklch(0.55_0.15_var(--hue))] text-white font-medium'
                          : 'hover:bg-[oklch(0.95_0.02_var(--hue))]',
                      ]}
                      onClick={() => playDiff(label)}
                    >{label}</div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    )
  },
})
