# ChuChartManager

CHUNITHM 谱面与资源管理工具

## 功能

### 谱面管理

- 浏览本地谱面列表，按 ID / 名称排序，按流派 / 难度筛选
- 修改谱面基础信息（曲名、曲师、流派、等级、谱面设计）
- 谱面导入 / 导出（C2S / UGC 格式互转）
- 封面导入、BGM 导出 MP3
- 批量操作（修改属性、导出封面 / 音频）

### 资源管理

- 创建自定义资源（称号、名牌、角色、地图图标、衣装、系统语音）
- 资源浏览器（称号、名牌、边框、角色、衣装、系统语音、舞台背景）
- 资源 ID 冲突检测

### 活动与地图

- 活动 / 地图浏览与编辑，创建自定义活动与地图
- 地图背景 DDS 导入 / 替换，活动广告图导入 / 替换

### 其他

- 段位认定课程编辑
- 登录奖励编辑
- DDS 纹理提取（从 AFB / SVO 文件）
- E-mote 模型 WebGL 预览
- Option 目录管理（创建、导入、删除、自制谱标记）
- 多语言（中文 / English / 日本語）
- 远程模式（局域网访问）

## 项目结构

| 目录 | 说明 |
|------|------|
| `ChuChartManager/` | WinForms (.NET 10) 主程序 + ASP.NET Core 后端 |
| `ChuChartManager/Front/` | Vue 3 + TypeScript 前端（Vite + UnoCSS + MuNET-UI） |
| `ChuChartManager.CLI/` | 命令行工具 |

### 子模块

| 子模块 | 说明 |
|--------|------|
| `MuNET-UI` | UI 组件库 |
| `MuConvert` | 谱面格式转换库 |
| `SonicAudioTools` | CRIWARE 音频处理库 |
| `XV2-Tools` | ACB/AWB 音频工具 |
| `DDSExtractor` | DDS 纹理提取库 |
| `FreeMote` | E-mote PSB 工具链 |
| `FreeMote-SDK` | E-mote WebGL 驱动 |

## 构建

需要：
- .NET 10 SDK
- Node.js 18+、pnpm
- .NET Framework 4.8.1 Targeting Pack（SonicAudioTools 依赖）

```bash
# 初始化子模块
git submodule update --init --recursive

# 构建前端
cd ChuChartManager/Front
pnpm install
pnpm build

# 构建后端
cd ../..
dotnet build ChuChartManager.slnx

# 编译 FreeMote 工具链
dotnet build FreeMote/FreeMote.Tools.PsbDecompile -c Release
dotnet build FreeMote/FreeMote.Tools.PsBuild -c Release
```

## 致谢

- [MuNET-UI](https://github.com/MuNET-OSS/MuNET-UI) — UI 组件库
- [FreeMote](https://github.com/UlyssesWu/FreeMote) — E-mote PSB 工具链
- [DDSExtractor](https://github.com/XNTech/DDSExtractor) — DDS 纹理提取库
