import { copyFileSync, mkdirSync, readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig, type Plugin } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueJsx from '@vitejs/plugin-vue-jsx'
import UnoCSS from 'unocss/vite'
import VueI18nPlugin from '@intlify/unplugin-vue-i18n/vite'

const __dirname = dirname(fileURLToPath(import.meta.url))
const sdkDriverDir = resolve(__dirname, '../../FreeMote-SDK/WebGL/driver')

function copyEmoteDriver(): Plugin {
  const files = ['FreeMoteDriver.js', 'emoteplayer.js']
  return {
    name: 'copy-emote-driver',
    writeBundle(options) {
      const outDir = resolve(__dirname, options.dir ?? '../wwwroot')
      const dest = resolve(outDir, 'emote-driver')
      mkdirSync(dest, { recursive: true })
      for (const file of files)
        copyFileSync(resolve(sdkDriverDir, file), resolve(dest, file))
    },
    configureServer(server) {
      server.middlewares.use('/emote-driver', (req, res, next) => {
        const name = req.url?.replace(/^\//, '').split('?')[0]
        if (name && files.includes(name)) {
          res.setHeader('Content-Type', 'application/javascript')
          res.end(readFileSync(resolve(sdkDriverDir, name)))
          return
        }
        next()
      })
    },
  }
}

export default defineConfig(({ command }) => ({
  plugins: [
    vue(),
    vueJsx(),
    UnoCSS(),
    VueI18nPlugin({
      include: [resolve(__dirname, './src/locales/*.yaml')],
    }),
    copyEmoteDriver(),
  ],
  resolve: {
    alias: {
      '@': '/src',
    },
  },
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
    sourcemap: command === 'serve',
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5000'
    },
    port: 5173,
    fs: {
      strict: false,
    }
  }
}))
