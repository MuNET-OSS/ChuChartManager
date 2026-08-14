# QA 经验

- 内置 Browser 插件在本机初始化时会因为运行时重复注入 `process` 失败；本次改用系统 Edge + 临时 Playwright 测试驱动真实页面。
- Vite 开发服务器对 YAML 模块返回的 MIME 在该环境下会阻止入口加载；页面 QA 改用已通过构建的 `vite preview` 生产产物。
- Playwright 在页面层 mock `/api`，返回与 AppleChu `.acmani` 相同的 `Enable` entry 和 `advanced` 元数据，覆盖 375/768/1280、点击展开、搜索揭示和 reduced-motion。
- PowerShell 通过反射调用接收 `byte[]` 的 C# 方法时，需要用 `[object[]]@(,[object]$bytes)` 防止字节数组被展开成多个参数。
