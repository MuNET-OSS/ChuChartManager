import { defineConfig, presetIcons, presetTypography, presetAttributify, transformerVariantGroup, transformerDirectives } from 'unocss'
import presetUno from '@unocss/preset-wind3'

export default defineConfig({
  content: {
    filesystem: ['../../MuNET-UI/src/**/*.{ts,tsx}'],
  },
  presets: [
    presetUno(),
    presetTypography(),
    presetIcons(),
    presetAttributify(),
  ],
  transformers: [
    transformerDirectives({
      applyVariable: ['--at-apply'],
    }),
    transformerVariantGroup(),
  ],
})
