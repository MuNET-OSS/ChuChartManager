import { defineComponent, ref, computed, onMounted } from 'vue'
import { Button, Modal, TextInput, NumberInput, Select, addToast } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { optionDirs } from '@/store/refs'
import DirSelect from '@/components/DirSelect'
import { getMusicList, type MusicListItem } from '@/api'
import {
  getCourseList, getCourse, createCourse, saveCourse, deleteCourse,
  type CourseListItem, type CourseDetail, type CreateCourseMusicDto,
} from '@/api/course'

const DIFFICULTIES: { id: number; label: string }[] = [
  { id: 10, label: 'CLASS Ⅰ' },
  { id: 11, label: 'CLASS Ⅱ' },
  { id: 12, label: 'CLASS Ⅲ' },
  { id: 13, label: 'CLASS Ⅳ' },
  { id: 14, label: 'CLASS Ⅴ' },
  { id: 20, label: 'CLASS ∞' },
  { id: 22, label: 'CLASS SP' },
]

const DIFF_NAMES: { id: number; str: string; data: string }[] = [
  { id: 0, str: 'Basic', data: 'BASIC' },
  { id: 1, str: 'Advanced', data: 'ADVANCED' },
  { id: 2, str: 'Expert', data: 'EXPERT' },
  { id: 3, str: 'Master', data: 'MASTER' },
  { id: 4, str: 'Ultima', data: 'ULTIMA' },
  { id: 5, str: "World's End", data: "WORLD'S END" },
]

export default defineComponent({
  setup() {
    const { t } = useI18n()

    const courses = ref<CourseListItem[]>([])
    const selectedCourse = ref<CourseDetail | null>(null)
    const loading = ref(false)
    const saving = ref(false)
    const showCreateModal = ref(false)
    const showMusicPicker = ref(false)
    const musicPickerTarget = ref<'edit' | 'create'>('edit')
    const musicPickerIndex = ref(-1)

    const allMusic = ref<MusicListItem[]>([])
    const musicSearch = ref('')

    const editName = ref('')
    const editDiffId = ref(10)
    const editMusics = ref<CreateCourseMusicDto[]>([])

    const newCourseTargetDir = ref('')
    const newCourseId = ref(90000)
    const newCourseName = ref('')
    const newCourseDiffId = ref(10)
    const newCourseMusics = ref<CreateCourseMusicDto[]>([])

    const difficultyOptions = computed<SelectOption[]>(() =>
      DIFFICULTIES.map(d => ({ label: d.label, value: d.id }))
    )

    const musicDiffOptions = computed<SelectOption[]>(() =>
      DIFF_NAMES.map(d => ({ label: d.data, value: d.id }))
    )

    const filteredMusic = computed(() => {
      if (!musicSearch.value) return allMusic.value.slice(0, 100)
      const q = musicSearch.value.toLowerCase()
      return allMusic.value
        .filter(m => m.name.toLowerCase().includes(q) || m.artist.toLowerCase().includes(q) || String(m.id).includes(q))
        .slice(0, 100)
    })

    async function loadCourses() {
      loading.value = true
      try {
        courses.value = await getCourseList()
      } finally {
        loading.value = false
      }
    }

    async function selectCourse(item: CourseListItem) {
      try {
        const detail = await getCourse(item.id, item.assetDir)
        selectedCourse.value = detail
        editName.value = detail.name
        editDiffId.value = detail.difficultyId
        editMusics.value = detail.musics.map(m => ({
          musicId: m.musicId,
          musicName: m.musicName,
          diffId: m.diffId,
          diffName: DIFF_NAMES.find(d => d.id === m.diffId)?.str ?? 'Master',
          diffData: m.diffName,
        }))
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    async function handleSave() {
      if (!selectedCourse.value) return
      saving.value = true
      try {
        const c = selectedCourse.value
        await saveCourse(c.id, c.assetDir, {
          name: editName.value,
          difficultyId: editDiffId.value,
          difficulty: DIFFICULTIES.find(d => d.id === editDiffId.value)?.label ?? '',
          ruleId: c.ruleId,
          rewardId: c.rewardId,
          rewardName: c.rewardName,
          reward2ndId: c.reward2ndId,
          reward2ndName: c.reward2ndName,
          teamOnly: c.teamOnly,
          isMusicDuplicateAllowed: c.isMusicDuplicateAllowed,
          conditionsCourseId: c.conditionsCourseId,
          conditionsCourseName: c.conditionsCourseName,
          conditionsText: c.conditionsText,
          priority: c.priority,
          musics: editMusics.value,
        })
        addToast({ message: t('common.save') + ' ✓', type: 'success' })
        await loadCourses()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      } finally {
        saving.value = false
      }
    }

    async function handleDelete() {
      if (!selectedCourse.value) return
      try {
        await deleteCourse(selectedCourse.value.id, selectedCourse.value.assetDir)
        selectedCourse.value = null
        addToast({ message: t('common.delete') + ' ✓', type: 'success' })
        await loadCourses()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    function openCreateModal() {
      showCreateModal.value = true
      newCourseId.value = 90000
      newCourseName.value = ''
      newCourseDiffId.value = 10
      newCourseMusics.value = [
      { musicId: -1, musicName: '', diffId: 3, diffName: 'Master', diffData: 'MASTER' },
      { musicId: -1, musicName: '', diffId: 3, diffName: 'Master', diffData: 'MASTER' },
      { musicId: -1, musicName: '', diffId: 3, diffName: 'Master', diffData: 'MASTER' },
      ]
      const custom = optionDirs.value.filter(d => d.dirName !== 'A000')
      if (custom.length > 0 && !newCourseTargetDir.value)
        newCourseTargetDir.value = custom[0].dirName
    }

    async function handleCreate() {
      if (!newCourseTargetDir.value) return
      const validMusics = newCourseMusics.value.filter(m => m.musicName)
      if (validMusics.length === 0) {
        addToast({ message: t('course.needMusic'), type: 'error' })
        return
      }
      saving.value = true
      try {
        await createCourse({
          targetDir: newCourseTargetDir.value,
          id: newCourseId.value,
          name: newCourseName.value,
          difficultyId: newCourseDiffId.value,
          difficulty: DIFFICULTIES.find(d => d.id === newCourseDiffId.value)?.label ?? '',
          ruleId: 34,
          musics: validMusics,
        })
        showCreateModal.value = false
        addToast({ message: t('tools.createSuccess'), type: 'success' })
        await loadCourses()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      } finally {
        saving.value = false
      }
    }

    function openMusicPicker(idx: number, target: 'edit' | 'create') {
      musicPickerIndex.value = idx
      musicPickerTarget.value = target
      showMusicPicker.value = true
      musicSearch.value = ''
      if (allMusic.value.length === 0) {
        getMusicList().then(list => { allMusic.value = list })
      }
    }

    function pickMusic(m: MusicListItem) {
      const musics = musicPickerTarget.value === 'create' ? newCourseMusics.value : editMusics.value
      const idx = musicPickerIndex.value
      if (idx >= 0 && idx < musics.length) {
        musics[idx].musicId = m.id
        musics[idx].musicName = m.name
      }
      showMusicPicker.value = false
    }

    function addMusicSlot(musics: CreateCourseMusicDto[]) {
        musics.push({ musicId: -1, musicName: '', diffId: 3, diffName: 'Master', diffData: 'MASTER' })
    }

    function removeMusicSlot(musics: CreateCourseMusicDto[], idx: number) {
      if (musics.length > 1) musics.splice(idx, 1)
    }

    function onDiffChange(music: CreateCourseMusicDto, newDiffId: number) {
      music.diffId = newDiffId
      const diff = DIFF_NAMES.find(d => d.id === newDiffId)
      if (diff) {
        music.diffName = diff.str
        music.diffData = diff.data
      }
    }

    onMounted(loadCourses)

    function renderMusicSlots(musics: CreateCourseMusicDto[], target: 'edit' | 'create') {
      return (
        <div class="flex flex-col gap-2">
          {musics.map((m, i) => (
            <div key={i} class="flex items-center gap-2 p-2 rounded-lg bg-[oklch(0.97_0.005_var(--hue))] border border-solid border-[oklch(0.9_0.02_var(--hue))]">
              <span class="text-sm op-50 w-6 text-center flex-shrink-0">#{i + 1}</span>
              <div class="flex-1 min-w-0">
                <div
                  class="text-sm truncate cursor-pointer hover:text-[oklch(0.5_0.15_var(--hue))]"
                  onClick={() => openMusicPicker(i, target)}
                >
                  {m.musicName
                    ? <span>{m.musicName} <span class="op-40">({m.musicId})</span></span>
                    : <span class="op-40">{t('course.clickToSelect')}</span>
                  }
                </div>
              </div>
              <div class="w-32 flex-shrink-0">
                <Select
                  options={musicDiffOptions.value}
                  value={m.diffId}
                  onChange={(v: number) => onDiffChange(m, v)}
                />
              </div>
              <span
                class="i-mdi-close text-4 op-40 cursor-pointer hover:op-80 flex-shrink-0"
                onClick={() => removeMusicSlot(musics, i)}
              />
            </div>
          ))}
          <Button onClick={() => addMusicSlot(musics)}>
            <span class="i-mdi-plus mr-1" />{t('course.addMusic')}
          </Button>
        </div>
      )
    }

    return () => (
      <div class="flex h-full">
        <div class="w-80 flex-shrink-0 border-r border-solid border-[oklch(0.9_0.02_var(--hue))] flex flex-col">
          <div class="flex items-center justify-between p-4 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
            <h3 class="text-lg font-bold m-0">{t('course.title')}</h3>
            <Button onClick={openCreateModal}>
              <span class="i-mdi-plus mr-1" />{t('common.create')}
            </Button>
          </div>
          <div class="flex-1 of-y-auto p-2">
            {courses.value.length === 0 && !loading.value && (
              <div class="text-center op-40 mt-8">{t('course.noCourses')}</div>
            )}
            {courses.value.map(c => (
              <div
                key={`${c.id}-${c.assetDir}`}
                class={[
                  'p-3 rounded-lg cursor-pointer mb-1 transition-all duration-150',
                  selectedCourse.value?.id === c.id && selectedCourse.value?.assetDir === c.assetDir
                    ? 'bg-[oklch(0.92_0.05_var(--hue))] border border-solid border-[oklch(0.7_0.1_var(--hue))]'
                    : 'hover:bg-[oklch(0.96_0.02_var(--hue))] border border-solid border-transparent',
                ]}
                onClick={() => selectCourse(c)}
              >
                <div class="flex items-center gap-2">
                  <span class="text-sm font-medium truncate flex-1">{c.name}</span>
                  <span class="text-xs op-40">{c.assetDir}</span>
                </div>
                <div class="flex items-center gap-2 mt-1">
                  <span class="text-xs px-1.5 py-0.5 rounded bg-[oklch(0.9_0.04_var(--hue))]">{c.difficulty}</span>
                  <span class="text-xs op-40">{c.musicCount} {t('course.tracks')}</span>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div class="flex-1 of-y-auto p-6">
          {!selectedCourse.value ? (
            <div class="flex items-center justify-center h-full op-30">
              <div class="text-center">
                <span class="i-mdi-format-list-bulleted text-16 block mb-4" />
                <span>{t('course.selectCourse')}</span>
              </div>
            </div>
          ) : (
            <div class="max-w-2xl">
              <div class="flex items-center justify-between mb-6">
                <div>
                  <h2 class="text-xl font-bold m-0">{editName.value || selectedCourse.value.name}</h2>
                  <span class="text-xs op-40">ID: {selectedCourse.value.id} · {selectedCourse.value.assetDir}</span>
                </div>
                <div class="flex gap-2">
                  <Button onClick={handleDelete}>{t('common.delete')}</Button>
                  <Button onClick={handleSave} ing={saving.value}>{t('common.save')}</Button>
                </div>
              </div>

              <div class="grid grid-cols-2 gap-4 mb-6">
                <div>
                  <label class="block text-sm op-60 mb-1">{t('course.name')}</label>
                  <TextInput v-model:value={editName.value} />
                </div>
                <div>
                  <label class="block text-sm op-60 mb-1">{t('course.difficulty')}</label>
                  <Select options={difficultyOptions.value} v-model:value={editDiffId.value} />
                </div>
              </div>

              <h3 class="text-base font-bold mb-3">{t('course.musicList')}</h3>
              {renderMusicSlots(editMusics.value, 'edit')}
            </div>
          )}
        </div>

        <Modal
          show={showCreateModal.value}
          title={t('course.createCourse')}
          width="min(60vw, 42em)"
          onClose={() => { showCreateModal.value = false }}
        >
          <div class="flex flex-col gap-3 p-2">
            <div>
              <label class="block text-sm op-60 mb-1">{t('tools.targetDir')}</label>
              <DirSelect v-model:value={newCourseTargetDir.value} />
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-sm op-60 mb-1">ID</label>
                <NumberInput v-model:value={newCourseId.value} min={1} max={99999999} />
              </div>
              <div>
                <label class="block text-sm op-60 mb-1">{t('course.name')}</label>
                <TextInput v-model:value={newCourseName.value} />
              </div>
              <div class="col-span-2">
                <label class="block text-sm op-60 mb-1">{t('course.difficulty')}</label>
                <Select options={difficultyOptions.value} v-model:value={newCourseDiffId.value} />
              </div>
            </div>
            <h4 class="text-sm font-bold mt-2 mb-1">{t('course.musicList')}</h4>
            {renderMusicSlots(newCourseMusics.value, 'create')}
            <div class="flex justify-end gap-2 mt-2">
              <Button onClick={() => { showCreateModal.value = false }}>{t('common.cancel')}</Button>
              <Button onClick={handleCreate} ing={saving.value}>{t('common.create')}</Button>
            </div>
          </div>
        </Modal>

        <Modal
          show={showMusicPicker.value}
          title={t('course.selectMusic')}
          width="min(70vw, 48em)"
          onClose={() => { showMusicPicker.value = false }}
        >
          <div class="flex flex-col gap-3 p-2">
            <TextInput v-model:value={musicSearch.value} placeholder={t('course.searchMusic')} />
            <div class="max-h-80 of-y-auto">
              {filteredMusic.value.map(m => (
                <div
                  key={m.id}
                  class="flex items-center gap-3 p-2 rounded cursor-pointer hover:bg-[oklch(0.95_0.02_var(--hue))] transition-colors"
                  onClick={() => pickMusic(m)}
                >
                  <span class="text-xs op-40 w-12 text-right flex-shrink-0">{m.id}</span>
                  <span class="text-sm truncate flex-1">{m.name}</span>
                  <span class="text-xs op-50 truncate max-w-40">{m.artist}</span>
                </div>
              ))}
              {filteredMusic.value.length === 0 && (
                <div class="text-center op-40 py-6">{t('course.noResults')}</div>
              )}
            </div>
          </div>
        </Modal>
      </div>
    )
  },
})
