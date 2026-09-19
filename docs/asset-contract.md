# 动画资产契约

当前清单处于 `hybrid` 阶段：`assets/character/formal-v1/` 保存 Idle、Walk、Rest、Sleep、Drag、Battle、Victory 共 77 张正式透明帧；`assets/character/generated/click-*` 仍保留 ClickSoft、ClickAnnoyed、ClickWarning 共 30 张粗加工占位帧。正式素材的原文件名、SHA-256、输出路径和画布变换记录在 `assets/character/formal-v1/source-map.json`。

- 逻辑画布：480×600；默认显示：240×300 DIP。
- foot anchor：(240,560)；所有人物帧保持同一画布和脚部基准。
- PNG 必须为带 Alpha 的 32 位格式，人物和细剑均不得裁切。
- 同一动作族的全部帧使用固定画布变换；不得按单帧人物边界自动裁图、改变缩放或改变窗口尺寸。源素材中的透明画布异常只能通过可审计的固定修复规则处理。
- 正式资产由 `tools/Charlotte.AssetPrep` 从只读参考目录生成 480×600 运行时副本。重新生成时必须保持映射顺序并重新执行资源检查。
- 原始 `Sleep_Loop (7).png` 的画布与构图比例异常，已使用版本化透明覆盖源 `assets/character/source/formal-overrides/sleep-loop-07.png`；工具仍对它应用 Sleep 动作族的固定变换，并在 source-map 中以 `project:` 来源记录，禁止静默修改参考目录。
- 发布校验严格拒绝错误清单或错误帧；运行时则只禁用损坏动作并回退到 Idle。Idle 或整份清单不可用时，程序使用不依赖磁盘资源的内置透明保底帧。
- 解码帧缓存默认限制为 96 MiB，按缩放后 Pbgra32 像素和配套 Alpha 掩码的实际内存计费并采用 LRU 淘汰；不同 DPI 使用不同像素宽度，切换后清理旧尺寸；大于总预算的单帧会被拒绝并触发 Idle 回退。
- 每个动作通过 `overlayEffects` 声明是否使用独立、完全穿透的特效窗口。正式 Sleep、Drag、Battle、Victory 已将气泡、玫瑰、花瓣或星光画入帧内，因此关闭程序叠加；占位动作仍可启用独立特效层。
- 资源路径必须位于 assets 根目录内；根目录以下的 Junction、符号链接或其他重解析点均不作为合法素材路径。

`config/animations.json` 的 `behavior` 节点同时保存自动行为契约：连击窗口 1500ms、3 分钟 Rest、10 分钟 Sleep、20–40 秒自动 Walk 间隔、24–72 DIP 距离、24 DIP/s 速度以及 Idle 静止/微动 70/30 比例。运行时从清单读取这些值；范围无效时整份清单降级到内置安全默认值。Walk 素材被禁用时不会产生无动画的窗口滑行。

Sleep 使用 `SleepEnter(5) → Sleep(8 循环) → SleepExit(5) → Idle`；对外面板命令仍使用 `Sleep`，调度器负责进入和退出过渡。Drag 使用 Start/Hold/Release 各 4 帧，Battle 使用 13 帧不可中断动作。所有帧时长位于清单中，不编码在文件名内。
