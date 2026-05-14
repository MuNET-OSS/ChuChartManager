import { defineComponent, onMounted } from "vue";
import { Button } from "@munet/ui";
import { optionDirs, leftPanel, selectedSource, updateOptionDirs } from "@/store/refs";
import OptionDirDisplay from "./OptionDirDisplay";
import CreateButton from "./CreateButton";
import ImportLocalButton from "./ImportLocalButton";
import { useI18n } from 'vue-i18n';

export default defineComponent({
  setup() {
    const { t } = useI18n();

    onMounted(() => {
      updateOptionDirs();
    });

    const selectDir = (dirName: string) => {
      selectedSource.value = dirName;
      leftPanel.value = 'musicList';
    };

    return () => (
      <div class="flex flex-col gap-3 h-full p-3">
        <div class="flex items-center gap-2">
          <Button variant="secondary" onClick={() => leftPanel.value = 'musicList'}>
            <span class="i-ic-baseline-arrow-back text-lg" />
          </Button>
          <div class="font-medium">{t('optionDir.title')}</div>
          <div class="grow-1" />
          <CreateButton />
          <ImportLocalButton />
        </div>
        <div class="of-y-auto cst flex-1">
          {optionDirs.value.length === 0 ? (
            <div class="text-center op-50 p-4">{t('optionDir.noOptions')}</div>
          ) : (
            <div class="flex flex-col gap-1">
              {optionDirs.value.map(dir => (
                <OptionDirDisplay
                  dir={dir}
                  key={dir.dirName}
                  selected={selectedSource.value === dir.dirName}
                  onSelect={() => selectDir(dir.dirName)}
                />
              ))}
            </div>
          )}
        </div>
      </div>
    );
  }
});
