import { defineComponent, ref, computed, onMounted, watch } from 'vue'
import { Button, Modal, TextInput, NumberInput, Select, addToast } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import DirSelect from '@/components/DirSelect'
import { optionDirs } from '@/store/refs'
import { openImageFileDialog, getLocalImagePreviewUrl } from '@/api/customResource'
import {
  getEventList, getEvent, saveEvent, createEvent, deleteEvent,
  getMapList, getMap, saveMap, createMap, deleteMap,
  getDdsMapPreviewUrl, createDdsMap,
  getEventInfoImagePreviewUrl, importEventInfoImage,
  SUBSTANCE_TYPE_NAMES,
  type EventListItem, type EventDetail,
  type MapListItem, type MapDetail, type MapAreaInfo,
} from '@/api/event'

type Tab = 'events' | 'maps'

export default defineComponent({
  setup() {
    const { t } = useI18n()

    const activeTab = ref<Tab>('events')
    const loading = ref(false)
    const saving = ref(false)

    const events = ref<EventListItem[]>([])
    const selectedEvent = ref<EventDetail | null>(null)
    const editEventName = ref('')
    const editEventText = ref('')
    const editEventPeriodDispType = ref(1)
    const editEventAlwaysOpen = ref(true)
    const editEventTeamOnly = ref(false)
    const editEventIsKop = ref(false)
    const editEventPriority = ref(0)
    const editEventSubstType = ref(0)
    const editEventFlagValue = ref(0)
    const infoImageKey = ref(0)

    const showCreateEvent = ref(false)
    const newEventTargetDir = ref('')
    const newEventId = ref(90000)
    const newEventName = ref('')
    const newEventSubstType = ref(6)
    const showDeleteEvent = ref(false)

    const maps = ref<MapListItem[]>([])
    const selectedMap = ref<MapDetail | null>(null)
    const editMapName = ref('')
    const editMapNetDispPeriod = ref(false)
    const editMapType = ref(0)
    const editMapHiddenType = ref(0)
    const editMapUnlockText = ref('')
    const editMapPriority = ref(0)
    const editMapAreas = ref<MapAreaInfo[]>([])
    const ddsPreviewKey = ref(0)
    const areaLocalPng = ref<Record<number, string>>({})
    const showCreateMap = ref(false)
    const newMapTargetDir = ref('')
    const newMapId = ref(90000000)
    const newMapName = ref('')
    const showDeleteMap = ref(false)

    const substTypeOptions = computed<SelectOption[]>(() =>
      Object.entries(SUBSTANCE_TYPE_NAMES).map(([k, v]) => ({
        label: `${k} - ${t(`event.substType.${v}`, v)}`,
        value: Number(k),
      }))
    )

    async function loadEvents() {
      loading.value = true
      try {
        events.value = await getEventList()
      } finally {
        loading.value = false
      }
    }

    async function loadMaps() {
      loading.value = true
      try {
        maps.value = await getMapList()
      } finally {
        loading.value = false
      }
    }

    async function selectEvent(item: EventListItem) {
      try {
        const detail = await getEvent(item.id, item.assetDir)
        selectedEvent.value = detail
        editEventName.value = detail.name
        editEventText.value = detail.text
        editEventPeriodDispType.value = detail.periodDispType
        editEventAlwaysOpen.value = detail.alwaysOpen
        editEventTeamOnly.value = detail.teamOnly
        editEventIsKop.value = detail.isKop
        editEventPriority.value = detail.priority
        editEventSubstType.value = detail.substanceType
        editEventFlagValue.value = detail.flagValue
        infoImageKey.value++
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    async function selectMap(item: MapListItem) {
      try {
        const detail = await getMap(item.id, item.assetDir)
        selectedMap.value = detail
        editMapName.value = detail.name
        editMapNetDispPeriod.value = detail.netDispPeriod
        editMapType.value = detail.mapType
        editMapHiddenType.value = detail.hiddenType
        editMapUnlockText.value = detail.unlockText
        editMapPriority.value = detail.priority
        editMapAreas.value = detail.areas.map(a => ({ ...a }))
        areaLocalPng.value = {}
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    async function handleSaveEvent() {
      if (!selectedEvent.value) return
      saving.value = true
      try {
        await saveEvent(selectedEvent.value.id, selectedEvent.value.assetDir, {
          name: editEventName.value,
          text: editEventText.value,
          periodDispType: editEventPeriodDispType.value,
          alwaysOpen: editEventAlwaysOpen.value,
          teamOnly: editEventTeamOnly.value,
          isKop: editEventIsKop.value,
          priority: editEventPriority.value,
          substanceType: editEventSubstType.value,
          flagValue: editEventFlagValue.value,
        })
        addToast({ message: t('common.save') + ' ✓', type: 'success' })
        await loadEvents()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      } finally {
        saving.value = false
      }
    }

    async function handleSaveMap() {
      if (!selectedMap.value) return
      saving.value = true
      try {
        await saveMap(selectedMap.value.id, selectedMap.value.assetDir, {
          name: editMapName.value,
          netDispPeriod: editMapNetDispPeriod.value,
          mapType: editMapType.value,
          hiddenType: editMapHiddenType.value,
          unlockText: editMapUnlockText.value,
          priority: editMapPriority.value,
          areas: editMapAreas.value,
        })
        addToast({ message: t('common.save') + ' ✓', type: 'success' })
        await loadMaps()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      } finally {
        saving.value = false
      }
    }

    async function handleDeleteEvent() {
      if (!selectedEvent.value) return
      try {
        await deleteEvent(selectedEvent.value.id, selectedEvent.value.assetDir)
        selectedEvent.value = null
        showDeleteEvent.value = false
        addToast({ message: t('common.delete') + ' ✓', type: 'success' })
        await loadEvents()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    async function handleDeleteMap() {
      if (!selectedMap.value) return
      try {
        await deleteMap(selectedMap.value.id, selectedMap.value.assetDir)
        selectedMap.value = null
        showDeleteMap.value = false
        addToast({ message: t('common.delete') + ' ✓', type: 'success' })
        await loadMaps()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    async function handleCreateEvent() {
      try {
        await createEvent({
          targetDir: newEventTargetDir.value,
          id: newEventId.value,
          name: newEventName.value,
          substanceType: newEventSubstType.value,
        })
        showCreateEvent.value = false
        addToast({ message: t('common.create') + ' ✓', type: 'success' })
        await loadEvents()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    async function handleCreateMap() {
      try {
        const targetDir = newMapTargetDir.value
        const id = newMapId.value
        await createMap({
          targetDir,
          id,
          name: newMapName.value,
        })
        showCreateMap.value = false
        addToast({ message: t('common.create') + ' ✓', type: 'success' })
        await loadMaps()
        selectMap({ id, name: newMapName.value, assetDir: targetDir, mapType: 0, areaCount: 0, filterName: '' })
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    function addMapArea() {
      editMapAreas.value.push({
        mapAreaId: 0,
        mapAreaName: '',
        ddsMapId: 0,
        ddsMapName: '',
        musicId: -1,
        musicName: 'Invalid',
        rewardId: -1,
        rewardName: 'Invalid',
        isHard: false,
        pageIndex: 0,
        indexInPage: editMapAreas.value.length,
        requiredAchievementCount: editMapAreas.value.length,
        gaugeId: 0,
        gaugeName: '',
      })
    }

    function removeMapArea(index: number) {
      editMapAreas.value.splice(index, 1)
    }

    function openCreateEvent() {
      showCreateEvent.value = true
      newEventId.value = 90000
      newEventName.value = ''
      newEventSubstType.value = 6
      const custom = optionDirs.value.filter(d => d.dirName !== 'A000')
      if (custom.length > 0 && !newEventTargetDir.value)
        newEventTargetDir.value = custom[0].dirName
    }

    function openCreateMap() {
      showCreateMap.value = true
      newMapId.value = 90000000
      newMapName.value = ''
      const custom = optionDirs.value.filter(d => d.dirName !== 'A000')
      if (custom.length > 0 && !newMapTargetDir.value)
        newMapTargetDir.value = custom[0].dirName
    }

    watch(activeTab, (tab) => {
      if (tab === 'events') loadEvents()
      else loadMaps()
    })

    onMounted(loadEvents)

    const renderEventList = () => (
      <div class="flex-1 of-y-auto">
        {loading.value ? (
          <div class="text-center op-40 py-6">{t('common.loading')}</div>
        ) : events.value.length === 0 ? (
          <div class="text-center op-40 py-6">{t('event.noEvents')}</div>
        ) : events.value.map(e => (
          <div
            key={`${e.id}-${e.assetDir}`}
            class={[
              'px-3 py-2.5 cursor-pointer border-b border-solid border-[oklch(0.93_0.01_var(--hue))] transition-colors',
              selectedEvent.value?.id === e.id && selectedEvent.value?.assetDir === e.assetDir
                ? 'bg-[oklch(0.92_0.05_var(--hue))]'
                : 'hover:bg-[oklch(0.97_0.02_var(--hue))]',
            ]}
            onClick={() => selectEvent(e)}
          >
            <div class="text-sm font-medium truncate">{e.name}</div>
            <div class="text-xs op-40 mt-0.5 flex items-center gap-2 flex-wrap">
              <span>ID: {e.id}</span>
              <span>·</span>
              <span>{e.assetDir}</span>
              <span>·</span>
              <span class="text-[oklch(0.6_0.12_var(--hue))]">{e.substanceTypeName}</span>
              {e.alwaysOpen && <span class="text-green-5">ON</span>}
              {e.teamOnly && <span class="text-amber-5">Team</span>}
            </div>
          </div>
        ))}
      </div>
    )

    const renderMapList = () => (
      <div class="flex-1 of-y-auto">
        {loading.value ? (
          <div class="text-center op-40 py-6">{t('common.loading')}</div>
        ) : maps.value.length === 0 ? (
          <div class="text-center op-40 py-6">{t('event.noMaps')}</div>
        ) : maps.value.map(m => (
          <div
            key={`${m.id}-${m.assetDir}`}
            class={[
              'px-3 py-2.5 cursor-pointer border-b border-solid border-[oklch(0.93_0.01_var(--hue))] transition-colors',
              selectedMap.value?.id === m.id && selectedMap.value?.assetDir === m.assetDir
                ? 'bg-[oklch(0.92_0.05_var(--hue))]'
                : 'hover:bg-[oklch(0.97_0.02_var(--hue))]',
            ]}
            onClick={() => selectMap(m)}
          >
            <div class="text-sm font-medium truncate">{m.name}</div>
            <div class="text-xs op-40 mt-0.5 flex items-center gap-2">
              <span>ID: {m.id}</span>
              <span>·</span>
              <span>{m.assetDir}</span>
              <span>·</span>
              <span>{t('event.areaCount', { count: m.areaCount })}</span>
              <span>·</span>
              <span>{m.filterName}</span>
            </div>
          </div>
        ))}
      </div>
    )

    const renderEventDetail = () => {
      const ev = selectedEvent.value
      if (!ev) return renderPlaceholder('i-mdi-calendar-star', t('event.title'))
      const isA000 = ev.assetDir === 'A000'

      return (
        <>
          <div class="flex items-center gap-3 p-4 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
            <h2 class="text-lg font-bold m-0 flex-1 truncate">{t('event.editEvent')}</h2>
            {isA000 && <span class="text-xs op-40">{t('event.readOnly')}</span>}
            {!isA000 && <Button onClick={handleSaveEvent} disabled={saving.value}>
              {saving.value ? t('common.loading') : t('common.save')}
            </Button>}
            {!isA000 && <Button onClick={() => { showDeleteEvent.value = true }}>
              {t('common.delete')}
            </Button>}
          </div>
          <div class="flex-1 of-y-auto p-4">
            <div class="grid grid-cols-2 gap-4 mb-4">
              <div class="col-span-2">
                <label class="text-xs op-50 mb-1 block">{t('event.eventName')}</label>
                <TextInput v-model:value={editEventName.value} disabled={isA000} />
              </div>
              <div class="col-span-2">
                <label class="text-xs op-50 mb-1 block">{t('event.eventText')}</label>
                <TextInput v-model:value={editEventText.value} disabled={isA000} />
              </div>
              <div>
                <label class="text-xs op-50 mb-1 block">{t('event.substanceType')}</label>
                <Select v-model:value={editEventSubstType.value} options={substTypeOptions.value} disabled={isA000} />
              </div>
              <div>
                <label class="text-xs op-50 mb-1 block">{t('event.periodDispType')}</label>
                <NumberInput v-model:value={editEventPeriodDispType.value} min={0} disabled={isA000} />
              </div>
              <div>
                <label class="text-xs op-50 mb-1 block">{t('event.priority')}</label>
                <NumberInput v-model:value={editEventPriority.value} min={0} disabled={isA000} />
              </div>
              <div>
                <label class="text-xs op-50 mb-1 block">{t('event.flagValue')}</label>
                <NumberInput v-model:value={editEventFlagValue.value} min={0} disabled={isA000} />
              </div>
              {ev.informationImagePath && (
                <div class="col-span-2">
                  <label class="text-xs op-50 mb-1 block">{t('event.infoImage')}</label>
                  <img
                    key={`info-${ev.id}-${infoImageKey.value}`}
                    src={`${getEventInfoImagePreviewUrl(ev.id, ev.assetDir)}&_t=${infoImageKey.value}`}
                    class="rounded-lg"
                    onError={(e: globalThis.Event) => { (e.target as HTMLImageElement).style.display = 'none' }}
                  />
                </div>
              )}
              {!isA000 && (
                <div class="col-span-2">
                  <Button onClick={async () => {
                    const imgPath = await openImageFileDialog()
                    if (!imgPath || !ev) return
                    try {
                      await importEventInfoImage(ev.id, ev.assetDir, imgPath)
                      infoImageKey.value++
                      addToast({ message: t('event.infoImageImported'), type: 'success' })
                    } catch (e: any) {
                      const msg = e?.response?.data
                      addToast({ message: typeof msg === 'string' ? msg : (msg?.title || e?.message), type: 'error' })
                    }
                  }}>
                    <span class={[ev.informationImagePath ? 'i-mdi-image-edit' : 'i-mdi-image-plus', 'text-3.5 mr-1']} />
                    {ev.informationImagePath ? t('event.replaceInfoImage') : t('event.importInfoImage')}
                  </Button>
                </div>
              )}
              <div class="col-span-2 flex gap-6">
                <label class="text-xs flex items-center gap-1 cursor-pointer">
                  <input type="checkbox" checked={editEventAlwaysOpen.value} disabled={isA000}
                    onChange={(e: Event) => { editEventAlwaysOpen.value = (e.target as HTMLInputElement).checked }} />
                  {t('event.alwaysOpen')}
                </label>
                <label class="text-xs flex items-center gap-1 cursor-pointer">
                  <input type="checkbox" checked={editEventTeamOnly.value} disabled={isA000}
                    onChange={(e: Event) => { editEventTeamOnly.value = (e.target as HTMLInputElement).checked }} />
                  {t('event.teamOnly')}
                </label>
                <label class="text-xs flex items-center gap-1 cursor-pointer">
                  <input type="checkbox" checked={editEventIsKop.value} disabled={isA000}
                    onChange={(e: Event) => { editEventIsKop.value = (e.target as HTMLInputElement).checked }} />
                  {t('event.isKop')}
                </label>
              </div>
            </div>

            {ev.mapRef && (
              <div class="border border-solid border-[oklch(0.9_0.02_var(--hue))] rounded-xl p-3 mb-3">
                <span class="text-xs font-bold op-50">{t('event.refMap')}</span>
                <div class="text-sm mt-1">{ev.mapRef.str} (ID: {ev.mapRef.id})</div>
              </div>
            )}
            {ev.dailyBonusPresetRef && (
              <div class="border border-solid border-[oklch(0.9_0.02_var(--hue))] rounded-xl p-3 mb-3">
                <span class="text-xs font-bold op-50">{t('event.refDailyBonus')}</span>
                <div class="text-sm mt-1">{ev.dailyBonusPresetRef.str} (ID: {ev.dailyBonusPresetRef.id})</div>
              </div>
            )}
            {ev.linkedVerseRef && (
              <div class="border border-solid border-[oklch(0.9_0.02_var(--hue))] rounded-xl p-3 mb-3">
                <span class="text-xs font-bold op-50">{t('event.refLinkedVerse')}</span>
                <div class="text-sm mt-1">{ev.linkedVerseRef.str} (ID: {ev.linkedVerseRef.id})</div>
              </div>
            )}
            {ev.cmissionRef && (
              <div class="border border-solid border-[oklch(0.9_0.02_var(--hue))] rounded-xl p-3 mb-3">
                <span class="text-xs font-bold op-50">{t('event.refCmission')}</span>
                <div class="text-sm mt-1">{ev.cmissionRef.str} (ID: {ev.cmissionRef.id})</div>
              </div>
            )}
            {ev.playRewardSetRef && (
              <div class="border border-solid border-[oklch(0.9_0.02_var(--hue))] rounded-xl p-3 mb-3">
                <span class="text-xs font-bold op-50">{t('event.refPlayRewardSet')}</span>
                <div class="text-sm mt-1">{ev.playRewardSetRef.str} (ID: {ev.playRewardSetRef.id})</div>
              </div>
            )}
            {ev.unlockChallengeRef && (
              <div class="border border-solid border-[oklch(0.9_0.02_var(--hue))] rounded-xl p-3 mb-3">
                <span class="text-xs font-bold op-50">{t('event.refUnlockChallenge')}</span>
                <div class="text-sm mt-1">{ev.unlockChallengeRef.str} (ID: {ev.unlockChallengeRef.id})</div>
              </div>
            )}

            <div class="text-xs op-30 mt-4">
              {t('event.dataName')}: {ev.dataName} · {t('event.assetDir')}: {ev.assetDir} · netOpen: {ev.netOpenName} ({ev.netOpenId})
            </div>
          </div>
        </>
      )
    }

    const renderMapDetail = () => {
      const m = selectedMap.value
      if (!m) return renderPlaceholder('i-mdi-map', t('event.mapTitle'))
      const isA000 = m.assetDir === 'A000'

      return (
        <>
          <div class="flex items-center gap-3 p-4 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
            <h2 class="text-lg font-bold m-0 flex-1 truncate">{t('event.editMap')}</h2>
            {isA000 && <span class="text-xs op-40">{t('event.readOnly')}</span>}
            {!isA000 && <Button onClick={handleSaveMap} disabled={saving.value}>
              {saving.value ? t('common.loading') : t('common.save')}
            </Button>}
            {!isA000 && <Button onClick={() => { showDeleteMap.value = true }}>
              {t('common.delete')}
            </Button>}
          </div>
          <div class="flex-1 of-y-auto p-4">
            <div class="grid grid-cols-2 gap-4 mb-4">
              <div class="col-span-2">
                <label class="text-xs op-50 mb-1 block">{t('event.mapName')}</label>
                <TextInput v-model:value={editMapName.value} disabled={isA000} />
              </div>
              <div>
                <label class="text-xs op-50 mb-1 block">{t('event.mapType')}</label>
                <NumberInput v-model:value={editMapType.value} min={0} disabled={isA000} />
              </div>
              <div>
                <label class="text-xs op-50 mb-1 block">{t('event.hiddenType')}</label>
                <NumberInput v-model:value={editMapHiddenType.value} min={0} disabled={isA000} />
              </div>
              <div>
                <label class="text-xs op-50 mb-1 block">{t('event.unlockText')}</label>
                <TextInput v-model:value={editMapUnlockText.value} disabled={isA000} />
              </div>
              <div>
                <label class="text-xs op-50 mb-1 block">{t('event.priority')}</label>
                <NumberInput v-model:value={editMapPriority.value} min={0} disabled={isA000} />
              </div>
              <div class="col-span-2">
                <label class="text-xs flex items-center gap-1 cursor-pointer">
                  <input type="checkbox" checked={editMapNetDispPeriod.value} disabled={isA000}
                    onChange={(e: Event) => { editMapNetDispPeriod.value = (e.target as HTMLInputElement).checked }} />
                  {t('event.netDispPeriod')}
                </label>
              </div>
            </div>

            <div class="flex items-center justify-between mb-3">
              <h3 class="text-sm font-bold m-0 op-70">{t('event.areaCount', { count: editMapAreas.value.length })}</h3>
              {!isA000 && <Button onClick={addMapArea}>
                <span class="i-mdi-plus text-3.5 mr-1" />
                {t('event.addArea')}
              </Button>}
            </div>

            {editMapAreas.value.map((area, i) => (
              <div
                key={i}
                class="border border-solid border-[oklch(0.9_0.02_var(--hue))] rounded-xl p-3 mb-3"
              >
                <div class="flex items-center justify-between mb-2">
                  <span class="text-xs font-bold op-50">#{i + 1} — {area.mapAreaName || `Area ${area.mapAreaId}`}</span>
                  {!isA000 && <span
                    class="i-mdi-close text-4 op-30 hover:op-70 cursor-pointer hover:text-red-5 transition-colors"
                    onClick={() => removeMapArea(i)}
                  />}
                </div>
                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="text-xs op-50 mb-1 block">{t('event.mapAreaId')}</label>
                    <NumberInput v-model:value={area.mapAreaId} min={0} disabled={isA000} />
                  </div>
                  <div>
                    <label class="text-xs op-50 mb-1 block">{t('event.mapAreaName')}</label>
                    <TextInput v-model:value={area.mapAreaName} disabled={isA000} />
                  </div>
                  <div>
                    <label class="text-xs op-50 mb-1 block">{t('event.ddsMapId')}</label>
                    <NumberInput v-model:value={area.ddsMapId} min={0} disabled={isA000} />
                  </div>
                  <div>
                    <label class="text-xs op-50 mb-1 block">{t('event.ddsMapName')}</label>
                    <TextInput v-model:value={area.ddsMapName} disabled={isA000} />
                  </div>
                  {!isA000 && (
                    <div class="col-span-2">
                      <Button onClick={async () => {
                        const imgPath = await openImageFileDialog()
                        if (!imgPath || !m) return
                        let ddsId = area.ddsMapId > 0 ? area.ddsMapId : area.mapAreaId
                        if (ddsId <= 0) ddsId = m.id + i + 1
                        try {
                          await createDdsMap({
                            targetDir: m.assetDir,
                            ddsMapId: ddsId,
                            ddsMapName: area.ddsMapName || area.mapAreaName || `Map_${ddsId}`,
                            imagePath: imgPath,
                          })
                          area.ddsMapId = ddsId
                          if (!area.ddsMapName) area.ddsMapName = `Map_${ddsId}`
                          areaLocalPng.value = { ...areaLocalPng.value, [i]: imgPath }
                          ddsPreviewKey.value++
                          editMapAreas.value = [...editMapAreas.value]
                          addToast({ message: t('event.ddsMapImported'), type: 'success' })
                        } catch (e: any) {
                          const msg = e?.response?.data
                          const detail = typeof msg === 'string' ? msg
                            : msg?.errors ? Object.entries(msg.errors).map(([k, v]) => `${k}: ${v}`).join('; ')
                            : (msg?.title || e?.message || JSON.stringify(msg))
                          addToast({ message: detail, type: 'error' })
                        }
                      }}>
                        <span class={[area.ddsMapId > 0 ? 'i-mdi-image-edit' : 'i-mdi-image-plus', 'text-3.5 mr-1']} />
                        {area.ddsMapId > 0 ? t('event.replaceDdsMap') : t('event.importDdsMap')}
                      </Button>
                    </div>
                  )}
                  {(areaLocalPng.value[i] || area.ddsMapId > 0) && (
                    <div class="col-span-2">
                      <img
                        key={`ddsmap-${i}-${area.ddsMapId}-${ddsPreviewKey.value}`}
                        src={areaLocalPng.value[i]
                          ? getLocalImagePreviewUrl(areaLocalPng.value[i])
                          : `${getDdsMapPreviewUrl(area.ddsMapId)}&_t=${ddsPreviewKey.value}`}
                        class="rounded-lg"
                        onError={(e: globalThis.Event) => { (e.target as HTMLImageElement).style.display = 'none' }}
                      />
                    </div>
                  )}
                  <div>
                    <label class="text-xs op-50 mb-1 block">{t('event.rewardId')}</label>
                    <NumberInput v-model:value={area.rewardId} disabled={isA000} />
                  </div>
                  <div>
                    <label class="text-xs op-50 mb-1 block">{t('event.rewardName')}</label>
                    <TextInput v-model:value={area.rewardName} disabled={isA000} />
                  </div>
                  <div>
                    <label class="text-xs op-50 mb-1 block">{t('event.pageIndex')}</label>
                    <NumberInput v-model:value={area.pageIndex} min={0} disabled={isA000} />
                  </div>
                  <div>
                    <label class="text-xs op-50 mb-1 block">{t('event.indexInPage')}</label>
                    <NumberInput v-model:value={area.indexInPage} min={0} disabled={isA000} />
                  </div>
                  <div>
                    <label class="text-xs op-50 mb-1 block">{t('event.requiredAchievement')}</label>
                    <NumberInput v-model:value={area.requiredAchievementCount} min={0} disabled={isA000} />
                  </div>
                  <div>
                    <label class="text-xs op-50 mb-1 block">{t('event.gaugeId')}</label>
                    <NumberInput v-model:value={area.gaugeId} min={0} disabled={isA000} />
                  </div>
                  <div class="flex items-end">
                    <label class="text-xs flex items-center gap-1 cursor-pointer">
                      <input type="checkbox" checked={area.isHard} disabled={isA000}
                        onChange={(e: Event) => { area.isHard = (e.target as HTMLInputElement).checked }} />
                      {t('event.isHard')}
                    </label>
                  </div>
                </div>
              </div>
            ))}

            <div class="text-xs op-30 mt-4">
              {t('event.dataName')}: {m.dataName} · {t('event.assetDir')}: {m.assetDir}
              · {t('event.mapFilter')}: {m.mapFilterName} ({m.mapFilterData})
              · {t('event.category')}: {m.categoryName}
            </div>
          </div>
        </>
      )
    }

    const renderPlaceholder = (icon: string, label: string) => (
      <div class="flex-1 flex items-center justify-center op-30">
        <div class="text-center">
          <span class={[icon, 'text-16 block mb-2']} />
          <span class="text-sm">{label}</span>
        </div>
      </div>
    )

    return () => (
      <div class="flex h-full">
        <div class="w-72 flex-shrink-0 border-r border-solid border-[oklch(0.9_0.02_var(--hue))] flex flex-col">
          <div class="flex border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
            <div
              class={['flex-1 text-center py-2.5 text-sm cursor-pointer transition-colors',
                activeTab.value === 'events' ? 'font-bold text-[oklch(0.55_0.15_var(--hue))] border-b-2 border-solid border-[oklch(0.55_0.15_var(--hue))]' : 'op-50 hover:op-70']}
              onClick={() => { activeTab.value = 'events' }}
            >
              {t('event.events')}
            </div>
            <div
              class={['flex-1 text-center py-2.5 text-sm cursor-pointer transition-colors',
                activeTab.value === 'maps' ? 'font-bold text-[oklch(0.55_0.15_var(--hue))] border-b-2 border-solid border-[oklch(0.55_0.15_var(--hue))]' : 'op-50 hover:op-70']}
              onClick={() => { activeTab.value = 'maps' }}
            >
              {t('event.maps')}
            </div>
          </div>

          <div class="flex items-center justify-between p-3 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
            <h3 class="text-base font-bold m-0">
              {activeTab.value === 'events' ? t('event.events') : t('event.maps')}
            </h3>
            <Button onClick={activeTab.value === 'events' ? openCreateEvent : openCreateMap}>
              <span class="i-mdi-plus text-4" />
            </Button>
          </div>

          {activeTab.value === 'events' ? renderEventList() : renderMapList()}
        </div>

        <div class="flex-1 min-w-0 flex flex-col">
          {activeTab.value === 'events' ? renderEventDetail() : renderMapDetail()}
        </div>

        <Modal
          show={showCreateEvent.value}
          title={t('event.createEvent')}
          width="min(90vw, 28em)"
          onClose={() => { showCreateEvent.value = false }}
        >
          <div class="p-4 flex flex-col gap-3">
            <div>
              <label class="text-xs op-50 mb-1 block">{t('tools.targetDir')}</label>
              <DirSelect v-model:value={newEventTargetDir.value} />
            </div>
            <div>
              <label class="text-xs op-50 mb-1 block">{t('event.eventId')}</label>
              <NumberInput v-model:value={newEventId.value} min={1} />
            </div>
            <div>
              <label class="text-xs op-50 mb-1 block">{t('event.eventName')}</label>
              <TextInput v-model:value={newEventName.value} />
            </div>
            <div>
              <label class="text-xs op-50 mb-1 block">{t('event.substanceType')}</label>
              <Select v-model:value={newEventSubstType.value} options={substTypeOptions.value} />
            </div>
            <div class="flex justify-end gap-2 mt-2">
              <Button onClick={() => { showCreateEvent.value = false }}>{t('common.cancel')}</Button>
              <Button onClick={handleCreateEvent}>{t('common.create')}</Button>
            </div>
          </div>
        </Modal>

        <Modal
          show={showCreateMap.value}
          title={t('event.createMap')}
          width="min(90vw, 28em)"
          onClose={() => { showCreateMap.value = false }}
        >
          <div class="p-4 flex flex-col gap-3">
            <div>
              <label class="text-xs op-50 mb-1 block">{t('tools.targetDir')}</label>
              <DirSelect v-model:value={newMapTargetDir.value} />
            </div>
            <div>
              <label class="text-xs op-50 mb-1 block">{t('event.mapId')}</label>
              <NumberInput v-model:value={newMapId.value} min={1} />
            </div>
            <div>
              <label class="text-xs op-50 mb-1 block">{t('event.mapName')}</label>
              <TextInput v-model:value={newMapName.value} />
            </div>
            <div class="flex justify-end gap-2 mt-2">
              <Button onClick={() => { showCreateMap.value = false }}>{t('common.cancel')}</Button>
              <Button onClick={handleCreateMap}>{t('common.create')}</Button>
            </div>
          </div>
        </Modal>

        <Modal
          show={showDeleteEvent.value}
          title={t('event.deleteEvent')}
          width="min(90vw, 24em)"
          onClose={() => { showDeleteEvent.value = false }}
        >
          <div class="p-2">
            <p class="text-sm mb-4">
              {t('event.deleteEventMessage', {
                name: selectedEvent.value?.name ?? '',
                id: selectedEvent.value?.id ?? 0,
              })}
            </p>
            <div class="flex justify-end gap-2">
              <Button onClick={() => { showDeleteEvent.value = false }}>{t('common.cancel')}</Button>
              <Button onClick={handleDeleteEvent}>{t('common.delete')}</Button>
            </div>
          </div>
        </Modal>

        <Modal
          show={showDeleteMap.value}
          title={t('event.deleteMap')}
          width="min(90vw, 24em)"
          onClose={() => { showDeleteMap.value = false }}
        >
          <div class="p-2">
            <p class="text-sm mb-4">
              {t('event.deleteMapMessage', {
                name: selectedMap.value?.name ?? '',
                id: selectedMap.value?.id ?? 0,
              })}
            </p>
            <div class="flex justify-end gap-2">
              <Button onClick={() => { showDeleteMap.value = false }}>{t('common.cancel')}</Button>
              <Button onClick={handleDeleteMap}>{t('common.delete')}</Button>
            </div>
          </div>
        </Modal>
      </div>
    )
  },
})
