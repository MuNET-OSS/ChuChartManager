# AGENTS.md — CHUNITHM Modding Ecosystem

## 简称

| 简称 | 全名 | 仓库 | 说明 |
|------|------|------|------|
| **CCM** | ChuChartManager | MuNET-OSS/ChuChartManager | 谱面与资源管理工具（本仓库） |
| **AppleChu** | AppleChu | MuNET-OSS/AppleChu | CHUNITHM 游戏 Mod（运行时安装） |

## 项目结构

```
ChuChartManager/              # CCM 主仓库（monorepo）
├── ChuChartManager/          # .NET 10 WinForms + ASP.NET Core 后端
│   ├── Front/                # Vue 3 + TypeScript 前端（Vite + UnoCSS + MuNET-UI）
│   ├── Controllers/          # REST API
├── ChuChartManager.CLI/      # 命令行工具
├── MuNET-UI/                 # 子模块 → UI 组件库
├── MuConvert/                # 子模块 → 谱面格式转换
├── SonicAudioTools/          # 子模块 → CRIWARE 音频处理
├── XV2-Tools/                # 子模块 → ACB/AWB 音频工具
├── DDSExtractor/             # 子模块 → DDS 纹理提取
├── FreeMote/                 # 子模块 → E-mote PSB 工具链
├── FreeMote-SDK/             # 子模块 → E-mote WebGL 驱动
└── Packaging/                # MSIX 打包脚本
```

## 技术栈

| 项目 | 语言 | 框架 |
|------|------|------|
| CCM 后端 | C# | .NET 10 + ASP.NET Core + WinForms |
| CCM 前端 | TypeScript | Vue 3 + Vite + UnoCSS + SASS |
| AppleChu | Rust (nightly, i686) | 外部运行时 Mod |

## 规则

### Git

- **不要自动 push** — 只在用户明确说 push 时才推
- **不要未经确认 commit** — 改完等用户 review
- commit 签名：`git -c gpg.format=ssh commit -S -m "..."`，如果失败就不签名
- commit message 用中文，conventional commits **不加 scope**：`type: description`

### AppleChu

- ExitConfirm 和 DeviceLostFix 是 `always_enabled`
- 代码注释只用于：逆向工程 magic number、COM vtable 偏移、汇编字节

### CCM

- 配置 schema 和默认配置从已安装 AppleChu 的 `.acmani` 元数据读取
- 保存配置时基于已安装版本携带的默认配置模板，和 Mod 生成的配置文件保持一致
- 配置文件不存在时自动从已安装 AppleChu 的默认配置生成

### 前端

- SASS **缩进语法**（不是 SCSS）
- 组件库：`@munet/ui`（MuNET-UI 子模块）
- 深色玻璃主题：`backdrop-filter: blur`、`rgba` 半透明、`var(--text-color)` 派生颜色
- 不引入新 npm 依赖除非必要

### 已知阻塞

| 功能 | 原因 |
|------|------|
| 自定义分辨率 | 未知 可能是写死的 |

## 构建

```bash
# CCM 前端
cd ChuChartManager/Front && pnpm install && pnpm build

# CCM 后端
dotnet build ChuChartManager.slnx

# MSIX 打包
.\Packaging\Build.ps1 -Mode Canary    # PowerShell
./Packaging/build.sh Canary            # Bash
```
