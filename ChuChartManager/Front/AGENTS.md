# FRONTEND KNOWLEDGE BASE

## OVERVIEW

Vue 3 + TypeScript 前端 SPA，通过 axios 调用本地 ASP.NET Core 后端 API，内嵌于 WinForms WebView2 窗口。

## STRUCTURE

```
Front/
├── src/
│   ├── api/            # axios API 客户端（手写，按域分文件）
│   │   ├── index.ts        # apiClient 实例 + Music 相关 API + WebView2 baseUrl 适配
│   │   ├── convert.ts      # 谱面格式转换（c2s/ugc/sus）
│   │   ├── course.ts       # 课题模式
│   │   ├── customResource.ts  # 自制资源（删除曲目等）
│   │   ├── ddsExtractor.ts # DDS 纹理提取
│   │   ├── emote.ts        # FreeMote E-mote
│   │   ├── event.ts        # 活动管理
│   │   ├── loginBonus.ts   # 登录奖励
│   │   ├── option.ts       # Option 目录管理（A000/A001/...）
│   │   └── stage.ts        # 舞台管理
│   ├── client/
│   │   └── mod.ts          # AppleChu Mod 配置 API
│   ├── components/     # 可复用组件：Sidebar.vue, StatusBar.vue, PlayerBar.vue, BottomOverlay.vue, FileTypeIcon.vue, DirSelect.tsx
│   ├── locales/        # i18n YAML：zh.yaml / en.yaml / ja.yaml + index.ts
│   ├── store/          # 全局状态：refs.ts（侧栏、option 目录）、player.ts（音频播放器）、status.ts（状态栏文本）
│   ├── utils/          # 工具：getSubDirFile.ts（File System Access）、sanitizeFsName.ts
│   ├── views/          # 页面（按域子目录）：BatchAction/, Charts (MusicList.vue), Course/, Emote/, Event/, LoginBonus/, ModManager/, MusicList/OptionDirsManager/, ResourceManager/, Tools/, Oobe/, ImportMusicModal.vue, Settings.vue
│   ├── App.vue         # 根组件，按 sidebarActive 切换视图
│   ├── main.ts         # 入口：初始化主题 + i18n + WebView2 message 监听
│   ├── fs-access.d.ts  # File System Access API 类型补丁
│   └── yaml.d.ts       # yaml import 类型
├── vite.config.ts      # Vite 配置（输出 ../wwwroot，dev 代理 5000，FreeMote driver 复制插件）
├── uno.config.ts       # UnoCSS 配置
├── package.json        # pnpm workspace 成员（ccm-frontend）
└── tsconfig.json       # TypeScript strict，@ → ./src，jsxImportSource: vue
```

## WHERE TO LOOK

| 任务 | 位置 |
|------|------|
| 调用后端 API | `src/api/*.ts`（按域分文件），统一 `apiClient` axios 实例 |
| 添加新页面 | `src/views/` 对应子目录，挂到 `src/components/Sidebar.vue` 的 `SidebarKey` 并在 `App.vue` 加 `v-if` |
| 全局状态 | `src/store/refs.ts`（侧栏 + option dirs）、`player.ts`（音频）、`status.ts`（状态文本） |
| 复用组件 | `src/components/` |
| 国际化文本 | `src/locales/{zh,en,ja}.yaml`，三语必须同步 |
| 切换语言 | `setLocale()`，同时写入后端 `Config/SetLocale` 持久化 |
| 前端入口 | `src/main.ts` → `App.vue`（按 `sidebarActive` 切视图） |
| 文件保存到本地 | `utils/getSubDirFile.ts` + `showDirectoryPicker`（File System Access API） |
| 文件名清理 | `utils/sanitizeFsName.ts` 的 `sanitizeFsSegment` |
| Emote 播放器 | `views/Emote/EmotePlayerCanvas.tsx` 通过 `/emote-driver/` 加载 FreeMote-SDK WebGL driver |

## DATA FLOW

1. 页面/store 调用 `src/api/*.ts` 中的函数
2. 函数用 `apiClient`（axios 实例）请求后端 `/api/*` 端点
3. WebView2 启动时后端通过 `chrome.webview.postMessage` 推送 backendUrl，`main.ts` 监听并设置 `apiClient.defaults.baseURL`
4. 状态变更通过 `store/refs.ts` 中的 `updateOptionDirs()` 等触发 UI 刷新
5. 错误处理：组件内部 try/catch + `addToast` 提示（来自 `@munet/ui`）

WebView2 集成：
- `main.ts` 监听 `chrome.webview` message 事件 → 写入 `globalThis.backendUrl` + 动态 import `./api` 更新 baseURL
- `api/index.ts` 的 `isWebView = !!(window as any).chrome?.webview`
- `ensureBackendUrl()` 在 `App.vue onMounted` 等待 backendUrl 就绪

## CONVENTIONS

- 包管理器：**pnpm**（workspace 成员，根仓库 `pnpm-workspace.yaml`），不用 npm / yarn
- UI 组件库：**@munet/ui**（workspace:* 引用 MuNET-UI 子模块），核心组件 Button/Radio/Select/Popover/CheckBox/Modal/Progress/addToast
- Naive UI 仅用于 `BatchAction/MusicSelector.tsx` 的 `NDataTable`（虚拟滚动+过滤）
- 样式方案：**UnoCSS** 原子化 + **SASS 缩进语法**（不是 SCSS）
- 主题：`@munet/ui` 的 `selectedThemeName = UIThemes.DynamicLight`，hue 默认 353
- 路径别名：`@` → `./src`
- TypeScript strict，JSX 用 `defineComponent(...)`（兼容 Vue + JSX，`jsxImportSource: vue`）
- 视图混用 `.vue`（template）和 `.tsx`（JSX setup），优先 `.tsx`（参考 MCM Front 写法）
- 状态管理极简，全局 `ref` 散在 `store/*.ts`（不使用 Pinia）
- API 客户端 **全手写**（无 swagger 生成），按业务域分文件
- 代码注释使用中文
- 三语 i18n 必须同步：新增 key 时 `zh.yaml` / `en.yaml` / `ja.yaml` 都加

## ANTI-PATTERNS

- 不要手写 UI 图标 emoji，统一用 iconify class（`i-mdi-*` / `i-tabler:*` / `i-ri:*`）或 `components/FileTypeIcon.vue`
- 不要引入新 npm 依赖除非确实必要
- 不要在视图组件里直接 fetch，统一走 `src/api/*.ts`
- 不要在 `Models/` 风格的地方写业务逻辑（前端无强 Model 层，但 store 只放纯 ref）
- 不要破坏三语 i18n 同步
- 不要为深色主题写适配（当前仅支持浅色，相关选项已在设置页移除）

## BUILD

```bash
# 安装依赖（在 Front 目录）
pnpm install

# 开发（端口 5173，代理后端 5000）
pnpm dev

# 构建（输出到 ../wwwroot）
pnpm build

# 预览构建产物
pnpm preview
```

## NOTES

- 后端 dev 端口 **5000**（不是 MCM 的 5181），dev 代理 `/api` 到 localhost:5000
- 入口 hash `#oobe` / `#mode-select` 切换到引导页 (`views/Oobe/`)
- Player 是单例 `Audio` 元素，存于 `store/player.ts`，状态 ref 化
- `views/Emote/` 嵌入 FreeMote-SDK WebGL driver，vite 启动时通过 `copyEmoteDriver` 插件从 `../../FreeMote-SDK/WebGL/driver` 复制 / 反代
- File System Access API 类型由 `fs-access.d.ts` 补齐，仅 Chromium 浏览器支持（WebView2 OK）
- 批量导出走流式 ZipReader + `showDirectoryPicker` 边解压边写盘（参考 `views/BatchAction/remoteExport.tsx`）
