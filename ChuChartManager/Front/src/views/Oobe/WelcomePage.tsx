import { defineComponent } from 'vue';
import { useI18n } from 'vue-i18n';
import { Select } from '@munet/ui';
import { availableLocales, localeLabels, setLocale, locale } from '@/locales';
import type { Locale } from '@/locales';

export default defineComponent({
  setup() {
    const { t } = useI18n();

    const localeOptions = availableLocales.map(l => ({
      label: localeLabels[l],
      value: l,
    }));

    return () => (
      <div class="flex flex-col items-center justify-center h-full gap-6">
        <img src="/logo-full.png" class="w-48" />
        <div class="text-2xl font-bold op-90">ChuChartManager</div>
        <div class="text-lg op-70">{t('oobe.welcomeMessage')}</div>
        <div class="flex items-center gap-4">
          <div class="i-mdi-translate text-xl op-60" />
          <Select
            value={locale.value}
            options={localeOptions}
            onChange={(v: any) => setLocale(v as Locale)}
          />
        </div>
      </div>
    );
  },
});
