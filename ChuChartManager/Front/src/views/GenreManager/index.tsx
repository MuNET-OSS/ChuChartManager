import { computed, defineComponent, onMounted, ref } from "vue";
import { Button, CheckBox, Modal, NumberInput, Select, TextInput, showTransactionalDialog } from "@munet/ui";
import { getAllGenres, addGenre, editGenre, deleteGenre, type GenreItem } from "@/api/genre";
import { getAllReleaseTags, addReleaseTag, editReleaseTag, deleteReleaseTag, type ReleaseTagItem } from "@/api/releaseTag";
import { getOptionDirs, type OptionDirInfo } from "@/api/option";
import { notifyGenreChanged, notifyReleaseTagChanged } from "@/store/refs";
import { useI18n } from 'vue-i18n';

type EditType = 'genre' | 'releaseTag';

export default defineComponent({
  setup() {
    const { t } = useI18n();
    const genres = ref<GenreItem[]>([]);
    const releaseTags = ref<ReleaseTagItem[]>([]);
    const optionDirs = ref<OptionDirInfo[]>([]);
    const showBuiltIn = ref(true);
    const activeTab = ref<EditType>('genre');
    const editingId = ref(-1);
    const showCreate = ref(false);
    const newId = ref(0);
    const newAssetDir = ref('');
    const deleteLoad = ref(false);
    const confirmDeleteId = ref(-1);

    const load = async () => {
      const [g, r, o] = await Promise.all([getAllGenres(), getAllReleaseTags(), getOptionDirs()]);
      genres.value = g;
      releaseTags.value = r;
      optionDirs.value = o;
    };
    onMounted(load);

    const list = computed(() =>
      showBuiltIn.value ? genres.value : genres.value.filter(it => it.isCustom)
    );

    const releaseTagList = computed(() =>
      showBuiltIn.value ? releaseTags.value : releaseTags.value.filter(it => it.isCustom)
    );

    const existingIds = computed(() => new Set(genres.value.map(g => g.id)));
    const existingReleaseTagIds = computed(() => new Set(releaseTags.value.map(g => g.id)));

    const openCreate = () => {
      newAssetDir.value = optionDirs.value.find(o => o.dirName !== 'A000')?.dirName || '';
      const ids = activeTab.value === 'genre' ? genres.value.map(g => g.id) : releaseTags.value.map(g => g.id);
      const start = activeTab.value === 'genre' ? 100 : 20;
      for (let i = start; i < 1000; i++) { if (ids.includes(i)) continue; newId.value = i; break; }
      showCreate.value = true;
    };

    const doCreate = async () => {
      if (activeTab.value === 'genre' && existingIds.value.has(newId.value)) {
        showTransactionalDialog(t('common.error'), t('genre.idConflict'), undefined, true);
        return;
      }
      if (activeTab.value === 'releaseTag' && existingReleaseTagIds.value.has(newId.value)) {
        showTransactionalDialog(t('common.error'), t('releaseTag.idConflict'), undefined, true);
        return;
      }
      if (!newAssetDir.value) {
        showTransactionalDialog(t('common.error'), 'Opt 不能为空', undefined, true);
        return;
      }
      try {
        if (activeTab.value === 'genre') {
          await addGenre({ id: newId.value, assetDir: newAssetDir.value, name: 'New Genre' });
          notifyGenreChanged();
        } else {
          await addReleaseTag({ id: newId.value, assetDir: newAssetDir.value });
          notifyReleaseTagChanged();
        }
        showCreate.value = false;
        editingId.value = newId.value;
        await load();
      } catch (e: unknown) {
        showTransactionalDialog(t('common.error'), getErrorMessage(e), undefined, true);
      }
    };

    const saveGenre = async (g: GenreItem) => {
      editingId.value = -1;
      try {
        await editGenre(g.id, { name: g.name, colorR: g.colorR, colorG: g.colorG, colorB: g.colorB });
        await load();
        notifyGenreChanged();
      } catch (e: unknown) {
        showTransactionalDialog(t('common.error'), getErrorMessage(e), undefined, true);
      }
    };

    const saveReleaseTag = async (tag: ReleaseTagItem) => {
      editingId.value = -1;
      try {
        await editReleaseTag(tag.id, { versionStr: tag.versionStr, titleName: tag.titleName });
        await load();
        notifyReleaseTagChanged();
      } catch (e: unknown) {
        showTransactionalDialog(t('common.error'), getErrorMessage(e), undefined, true);
      }
    };

    const del = async (id: number) => {
      deleteLoad.value = true;
      try {
        if (activeTab.value === 'genre') {
          await deleteGenre(id);
          notifyGenreChanged();
        } else {
          await deleteReleaseTag(id);
          notifyReleaseTagChanged();
        }
        editingId.value = -1;
        await load();
      }
      catch (e: unknown) { showTransactionalDialog(t('common.error'), getErrorMessage(e), undefined, true); }
      finally { deleteLoad.value = false; }
    };

    return () => (
      <div class="flex flex-col p-xy h-100dvh">
        <div class="flex gap-2 items-center mb-2">
          <Button variant={activeTab.value === 'genre' ? 'primary' : 'secondary'} onClick={() => { activeTab.value = 'genre'; editingId.value = -1; confirmDeleteId.value = -1; }}>{t('genre.management')}</Button>
          <Button variant={activeTab.value === 'releaseTag' ? 'primary' : 'secondary'} onClick={() => { activeTab.value = 'releaseTag'; editingId.value = -1; confirmDeleteId.value = -1; }}>{t('releaseTag.management')}</Button>
          <div class="grow-1" />
          <CheckBox v-model:value={showBuiltIn.value}>{t('genre.showBuiltIn')}</CheckBox>
          <Button onClick={openCreate}>{t('common.create')}</Button>
        </div>

        <div class="of-y-auto cst grow-1">
          <div class="flex flex-col gap-1">
            {activeTab.value === 'genre' && list.value.map(it => {
              const isEditing = editingId.value === it.id && it.isCustom;
              const disabled = editingId.value >= 0 && editingId.value !== it.id;
              const isConfirming = confirmDeleteId.value === it.id;

              return (
                <div key={it.id} class={disabled ? 'op-30' : ''} style={{ transition: 'opacity 0.3s' }}>
                  <div class="grid cols-[10em_2.4fr_7em] items-center gap-5 m-x">
                    <div class="flex gap-1 c-gray-6">{it.id}<span class="op-60">@</span><span class="op-80">{it.assetDir}</span></div>

                    <TextInput v-model:value={it.name} disabled={!isEditing} />

                    {it.isCustom ? (
                      isEditing ? (
                        <Button variant="primary" onClick={() => saveGenre(it)}>
                          <span class="i-material-symbols-done text-6 c-gray-6" />
                        </Button>
                      ) : isConfirming ? (
                        <Button danger={!deleteLoad.value} variant="secondary"
                          onClick={() => del(it.id)} ing={deleteLoad.value}
                          onMouseleave={() => confirmDeleteId.value = -1}>
                          {!deleteLoad.value && <span class="i-material-symbols-delete-outline text-6 c-gray-6" />}
                        </Button>
                      ) : (
                        <div class="flex gap-2">
                          <Button class="w-0 grow-1" variant="secondary" onClick={() => editingId.value = it.id}>
                            <span class="i-material-symbols-edit text-6 c-gray-6" />
                          </Button>
                          <Button class="w-0 grow-1" variant="secondary" onClick={() => confirmDeleteId.value = it.id}>
                            <span class="i-material-symbols-delete-outline text-6 c-gray-6" />
                          </Button>
                        </div>
                      )
                    ) : (
                      <div class="i-material-symbols-edit-off text-6 c-gray-6" />
                    )}
                  </div>
                </div>
              );
            })}
            {activeTab.value === 'releaseTag' && releaseTagList.value.map(it => {
              const isEditing = editingId.value === it.id && it.isCustom;
              const disabled = editingId.value >= 0 && editingId.value !== it.id;
              const isConfirming = confirmDeleteId.value === it.id;

              return (
                <div key={it.id} class={disabled ? 'op-30' : ''} style={{ transition: 'opacity 0.3s' }}>
                  <div class="grid cols-[10em_1.2fr_1.2fr_7em] items-center gap-5 m-x">
                    <div class="flex gap-1 c-gray-6">{it.id}<span class="op-60">@</span><span class="op-80">{it.assetDir}</span></div>
                    <TextInput v-model:value={it.versionStr} disabled={!isEditing} />
                    <TextInput v-model:value={it.titleName} disabled={!isEditing} />

                    {it.isCustom ? (
                      isEditing ? (
                        <Button variant="primary" onClick={() => saveReleaseTag(it)}>
                          <span class="i-material-symbols-done text-6 c-gray-6" />
                        </Button>
                      ) : isConfirming ? (
                        <Button danger={!deleteLoad.value} variant="secondary"
                          onClick={() => del(it.id)} ing={deleteLoad.value}
                          onMouseleave={() => confirmDeleteId.value = -1}>
                          {!deleteLoad.value && <span class="i-material-symbols-delete-outline text-6 c-gray-6" />}
                        </Button>
                      ) : (
                        <div class="flex gap-2">
                          <Button class="w-0 grow-1" variant="secondary" onClick={() => editingId.value = it.id}>
                            <span class="i-material-symbols-edit text-6 c-gray-6" />
                          </Button>
                          <Button class="w-0 grow-1" variant="secondary" onClick={() => confirmDeleteId.value = it.id}>
                            <span class="i-material-symbols-delete-outline text-6 c-gray-6" />
                          </Button>
                        </div>
                      )
                    ) : (
                      <div class="i-material-symbols-edit-off text-6 c-gray-6" />
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        <Modal show={showCreate.value} onUpdate:show={(v: boolean) => showCreate.value = v}
          title={`${t('common.create')}${activeTab.value === 'genre' ? t('genre.management') : t('releaseTag.management')}`} width="min(30vw,25em)">
          {{
            default: () => (
              <div class="flex flex-col gap-3">
                <div>
                  <div class="ml-1 text-sm">ID</div>
                  <NumberInput v-model:value={newId.value} class="w-full" min={1} />
                </div>
                <div>
                  <div class="ml-1 text-sm">Opt</div>
                  <Select
                    v-model:value={newAssetDir.value}
                    options={optionDirs.value.filter(o => o.dirName !== 'A000').map(o => ({ label: o.dirName, value: o.dirName }))}
                  />
                </div>
              </div>
            ),
            actions: () => <Button onClick={doCreate}>{t('common.confirm')}</Button>,
          }}
        </Modal>
      </div>
    );
  },
});

function getErrorMessage(error: unknown): string {
  if (error instanceof Error) return error.message;
  if (typeof error === 'string') return error;
  return String(error);
}
