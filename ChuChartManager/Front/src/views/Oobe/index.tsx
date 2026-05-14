import { defineComponent, ref, computed, Transition } from 'vue';
import { apiClient } from '@/api';
import WelcomePage from './WelcomePage';
import GameDirPage from './GameDirPage';
import ModeSelectPage from './ModeSelectPage';
import ServerRunningPage from './ServerRunningPage';
import './transitions.css';

enum Step {
  Welcome,
  GameDir,
  ModeSelect,
  ServerRunning,
}

export default defineComponent({
  props: {
    initStep: { type: String, default: 'welcome' },
  },
  setup(props) {
    let initStepValue = Step.Welcome;
    if (props.initStep === 'mode-select') initStepValue = Step.ModeSelect;

    const step = ref<Step>(initStepValue);
    const direction = ref<'forward' | 'backward'>('forward');

    const gamePath = ref('');
    const pathValid = ref(false);
    const initializing = ref(false);
    const completing = ref(false);
    const lanAddresses = ref<string[]>([]);

    const canGoNext = computed(() => {
      if (step.value === Step.Welcome) return true;
      if (step.value === Step.GameDir) return pathValid.value && !initializing.value;
      return false;
    });

    const goNext = async () => {
      if (step.value === Step.Welcome) {
        direction.value = 'forward';
        step.value = Step.GameDir;
      } else if (step.value === Step.GameDir) {
        initializing.value = true;
        try {
          await apiClient.post('/api/Config/InitializeGameData');
        } catch {
        }
        initializing.value = false;
        direction.value = 'forward';
        step.value = Step.ModeSelect;
      }
    };

    const goPrev = () => {
      if (step.value > Step.Welcome) {
        direction.value = 'backward';
        if (step.value === Step.ServerRunning) {
          step.value = Step.ModeSelect;
        } else {
          step.value--;
        }
        setTimeout(() => { lanAddresses.value = []; }, 500);
      }
    };

    const handleComplete = async (opts: {
      isRemote: boolean;
      useAuth: boolean;
      authUsername: string;
      authPassword: string;
    }) => {
      completing.value = true;
      try {
        await apiClient.post('/api/Config/CompleteSetup', {
          export: opts.isRemote,
          useAuth: opts.useAuth,
          authUsername: opts.authUsername || null,
          authPassword: opts.authPassword || null,
        });
        if (opts.isRemote) {
          let retries = 0;
          const maxRetries = 30;
          while (retries < maxRetries) {
            try {
              const { data } = await apiClient.get('/api/Config/GetLanAddresses');
              lanAddresses.value = data || [];
              direction.value = 'forward';
              step.value = Step.ServerRunning;
              completing.value = false;
              break;
            } catch {
              retries++;
              await new Promise(resolve => setTimeout(resolve, 500));
            }
          }
          if (retries >= maxRetries) completing.value = false;
        }
      } catch {
        completing.value = false;
      }
    };

    const transitionName = computed(() =>
      direction.value === 'forward' ? 'oobe-slide-forward' : 'oobe-slide-backward'
    );

    return () => (
      <div class="fixed inset-0 bg-[oklch(0.97_0.01_var(--hue))] flex flex-col">
        <div class="flex-1 overflow-hidden relative">
          <Transition name={transitionName.value}>
            {step.value === Step.Welcome && <WelcomePage key="welcome" />}
            {step.value === Step.GameDir &&
              <GameDirPage
                key="gamedir"
                gamePath={gamePath.value}
                onUpdate:gamePath={(v: string) => gamePath.value = v}
                pathValid={pathValid.value}
                onUpdate:pathValid={(v: boolean) => pathValid.value = v}
                initializing={initializing.value}
                onUpdate:initializing={(v: boolean) => initializing.value = v}
              />
            }
            {step.value === Step.ModeSelect &&
              <ModeSelectPage
                key="modeselect"
                completing={completing.value}
                onComplete={handleComplete}
              />
            }
            {step.value === Step.ServerRunning &&
              <ServerRunningPage
                key="serverrunning"
                lanAddresses={lanAddresses.value}
              />
            }
          </Transition>
        </div>
        <div class="relative h-16 shrink-0">
          {step.value > 0 && (
            <button
              class="fixed bottom-6 left-6 w-14 h-14 rounded-full bg-[oklch(0.6_0.15_var(--hue))]! hover:bg-[oklch(0.6_0.15_var(--hue)/0.8)]! text-white cursor-pointer border-none flex items-center justify-center"
              onClick={goPrev}
              disabled={completing.value}
            >
              <div class="i-mdi-arrow-left text-xl" />
            </button>
          )}
          {step.value < Step.ModeSelect && (
            <button
              class={['fixed bottom-6 right-6 w-14 h-14 rounded-full border-none flex items-center justify-center',
                canGoNext.value ? 'bg-[oklch(0.6_0.15_var(--hue))]! hover:bg-[oklch(0.6_0.15_var(--hue)/0.8)]! cursor-pointer text-white' : 'bg-gray-200! cursor-not-allowed text-gray-400']}
              onClick={() => canGoNext.value && goNext()}
              disabled={!canGoNext.value}
            >
              <div class="i-mdi-arrow-right text-6" />
            </button>
          )}
        </div>
      </div>
    );
  },
});
