import { defineComponent, PropType } from 'vue';
import { useI18n } from 'vue-i18n';
import { apiClient } from '@/api';
import { Button } from '@munet/ui';

export default defineComponent({
  props: {
    lanAddresses: { type: Array as PropType<string[]>, default: () => [] },
  },
  setup(props) {
    const { t } = useI18n();

    return () => (
      <div class="flex flex-col items-center justify-center h-full gap-6 px-12">
        <div class="i-material-symbols:check-circle-rounded text-5xl text-green-600" />
        <div class="text-xl font-bold op-90">{t('oobe.remoteReady')}</div>
        <div class="text-sm op-60">{t('oobe.lanAddresses')}</div>
        <div class="flex flex-col gap-2 items-center">
          {props.lanAddresses.map(addr => (
            <div class="px-4 py-2 rounded-lg bg-[oklch(0.92_0.02_var(--hue))] text-sm font-mono">http://{addr}:5001</div>
          ))}
        </div>
        <Button onClick={() => apiClient.post('/api/Config/OpenMainUI')}>
          {t('oobe.openMainUI')}
        </Button>
      </div>
    );
  },
});
