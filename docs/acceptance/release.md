# 发布验收记录

日期：2026-09-19
分支：`feat/charlotte-mvp`  
素材阶段：`hybrid`（77 张 formal-v1 + 30 张 Click placeholder）

## 已验证

- 项目本地 .NET SDK 10.0.401。
- Release 构建 0 错误；离线环境无法读取 NuGet 漏洞元数据时出现 NU1900，不属于编译错误。
- 自动测试 171 项通过。
- 资源清单 14 个动作、107 帧通过路径、帧数、时序、480×600 画布和透明/可见像素检查。
- win-x64 自包含目录发布成功，发布目录内资源再次校验通过。
- 发布目录采用临时目录生成、完整校验后替换；陈旧文件探针证明重复发布不会保留上一版本的多余文件，替换失败时保留上一份完整产物。
- 发布目录复制到 `artifacts/release smoke` 后，从系统临时目录作为工作目录启动；进程持续运行且响应正常。
- `scripts/test-publish-layout.ps1` 自动刷新上述含空格路径副本，并从系统临时目录再次通过 14 动作、107 帧资源加载检查。
- 同一脚本只在 `artifacts/release asset replacement` 隔离副本中用另一张 formal-v1 Idle 帧替换首帧；替换前后 SHA-256 不同、资源检查仍通过，标准发布目录哈希保持不变。
- `ManifestLoaderTests.Missing_battle_frame_disables_only_battle` 覆盖单动作坏帧：Battle 被禁用，其余动作和 Idle 保持可用。
- 单实例进程级测试：首实例持续运行，第二实例收到唤醒协议后退出。
- `scripts/test-asset-fallback.ps1` 在隔离发布副本中破坏动画清单；真实进程仍显示内置保底帧、记录降级事件并正常保存退出。
- `scripts/test-ambient-walk.ps1` 使用隔离发布副本缩短调度时间，在启动位置稳定后观测真实窗口横向移动，验证自动 Walk 已接入窗口层。
- `scripts/test-effect-window.ps1` 以 formal-v1 Battle 启动真实发布进程；主体保持 240×300 px，播放期间没有出现重复的独立特效窗口，证明 baked 特效策略生效。
- 旧 placeholder 性能长测详见 `docs/acceptance/performance.md`；formal-v1 仍使用相同运行时画布和缓存预算，但完整长测尚未重跑。
- 管理面板的 WPF/VM 自动回归已覆盖默认 Tab、重复打开保留会话、任务完成路由、输入错误/成功路径、键盘 Esc/Enter 和持久化命令；新增探针脚本见 `scripts/test-control-panel.ps1`。
- 单实例自动回归使用真实 Windows 命名 Mutex 与 CurrentUserOnly 命名管道，覆盖同身份排他和主监听延迟时的有界通知重试。
- 全屏候选策略自动回归覆盖整屏、仅工作区、隐藏、最小化、Shell/自身排除；运行时已接入前台 WinEvent 与 500ms 兜底轮询。
- 退出与恢复自动回归覆盖待写保存阻塞、重复退出、Flush 异常终结、新意图冻结、3×2MiB 日志轮换、未处理异常紧急保存及当前/备份双损坏。

## 尚未宣称通过

- 当前控制通道未暴露原生应用 UI，且本轮 GUI 运行额度不足，无法执行 `scripts/test-control-panel.ps1` 的完整右键面板点击回归；该脚本保留为解锁额度后的人工/自动复验入口。
- 发布布局回归不做逐动作视觉判定；“从含空格路径且不依赖 SDK 启动、正常退出后无残留进程”沿用本分支此前的真实进程证据，formal-v1 的循环接缝与转换节奏仍需人工复验。
- 跨进程透明区域快速点击、真实全屏应用进出、混合 DPI、负坐标副屏和显示器热插拔仍需在对应硬件/系统环境实测。
- Win10 22H2 尚未实测。
- ClickSoft、ClickAnnoyed、ClickWarning 共 30 帧仍是粗加工占位动画。
- Sleep Loop 第 7 帧的原始文件采用异常横向画布且构图比例偏小；项目已改用版本化透明覆盖源并保留 source-map 哈希记录，静态联系表与相邻帧边界已复核，连续播放观感仍列入人工验收。

这些项目保持为“未测”，不以单元测试或本机单屏结果替代。
