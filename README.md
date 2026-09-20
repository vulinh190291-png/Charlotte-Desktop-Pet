# Charlotte Desktop Pet

Charlotte Desktop Pet 是一个面向 Windows 10/11 x64 的透明桌面宠物项目。它使用 WPF 构建，以外置逐帧 PNG 驱动角色动画，并提供桌面停留、拖拽、多显示器位置恢复、闲置状态、每日任务、日程和开机启动等功能。

项目目前处于 **MVP 持续完善阶段**：程序主体、状态机、数据保存、资源校验和发布流程已经可运行；当前 10 个动画、69 张透明帧已完成工程接入，但角色动画仍计划使用 Figma 重新设计关键帧，右键管理界面也会继续进行视觉与交互升级。

> 本仓库当前没有提供开源许可证。公开源码不等于自动授予代码、角色形象或动画素材的复制、修改和再分发权利，详情见[授权说明](#授权说明)。

## 目录

- [当前功能](#当前功能)
- [操作方式](#操作方式)
- [动画状态](#动画状态)
- [系统要求](#系统要求)
- [快速开始](#快速开始)
- [开发与验证](#开发与验证)
- [发布](#发布)
- [项目结构](#项目结构)
- [核心设计](#核心设计)
- [动画素材规范](#动画素材规范)
- [数据、日志与隐私](#数据日志与隐私)
- [测试与性能](#测试与性能)
- [已知限制](#已知限制)
- [后续计划](#后续计划)
- [参与开发](#参与开发)
- [授权说明](#授权说明)

## 当前功能

### 桌面宠物

- 透明、无边框桌面窗口，默认始终置顶。
- 角色可拖动并停留在任意桌面位置。
- 启动时优先出现在鼠标指针所在的显示器。
- 退出时记录角色的横向相对位置；下次启动时映射到当前显示器并限制在可见工作区内。
- 纵向位置不恢复旧绝对坐标，而是根据当前显示器工作区底部重新计算，避免分辨率、缩放或显示器变化后悬空。
- 角色移动到另一块显示器后，边界检测、自动行为和面板定位均以角色当前所在显示器为准。
- 支持透明像素点击穿透，角色可见区域仍保留鼠标交互。
- 其他应用覆盖角色所在显示器的完整显示区域时暂时隐藏，离开全屏后恢复。

### 动画与状态

- 使用外置透明 PNG 逐帧播放，当前共 10 个动作、69 帧。
- 无交互 3 分钟后进入 Rest，10 分钟后进入 Sleep。
- Sleep 自动执行 `SleepEnter → Sleep 循环 → SleepExit → Idle`。
- 拖拽分为 `DragStart → DragHold → DragRelease` 三个阶段。
- Battle、Victory 等演出动作支持不可中断播放和结束状态回归。
- 使用单调时钟计算帧进度，避免系统时间变化影响动画节奏。
- 使用后台解码、最新帧门控和 96 MiB LRU 帧缓存降低播放期间的磁盘与解码压力。
- 单个动画资源损坏时只禁用对应动作；完整清单或 Idle 不可用时使用内置保底帧。

### 管理面板与个人整理

- 右键角色打开统一管理面板。
- 管理每日任务与日程。
- 每日任务最多 7 条。
- 完成任务时触发 Victory 动画。
- 删除任务后提供 5 秒单槽撤销。
- 支持动画预览、当前用户开机启动和退出程序。
- 面板支持 `Esc` 关闭，并会限制在角色当前显示器的可见工作区内。

### 稳定性与恢复

- 单实例运行；再次启动时通知现有实例，而不是创建第二只桌宠。
- JSON 原子保存，并保留备份用于损坏恢复。
- 普通退出、Windows 注销/关机和严重异常路径都包含有界保存流程。
- 保存失败采用有限重试，不会无限阻止程序退出。
- 诊断日志自动轮换，避免持续增长。
- 管理面板关闭后可以再次打开，避免重复右键导致已关闭 WPF 窗口被复用而崩溃。

## 操作方式

| 操作 | 行为 |
| --- | --- |
| 左键单击角色 | 唤醒 Rest/Sleep，并重置闲置计时 |
| 左键按住并拖动 | 移动桌宠；释放后停留在当前位置 |
| 右键角色 | 打开或切换管理面板 |
| 面板中的动画操作 | 预览当前可用动作 |
| 完成每日任务 | 播放 Victory |
| 面板中的“开机启动” | 管理当前 Windows 用户的 HKCU Run 项 |
| 面板中的“退出” | 保存当前状态并关闭桌宠 |

当前版本没有 Walk 和 Click 动画。左键点击只负责唤醒与重置闲置时间，不会触发点击演出；自动散步也处于关闭状态。

## 动画状态

所有帧的实际停留时间由 [`config/animations.json`](config/animations.json) 管理，不编码在文件名中。

| 动作 | 帧数 | 类型 | 当前总时长 |
| --- | ---: | --- | ---: |
| Idle | 8 | 循环、可中断 | 4.320 秒 |
| Rest | 8 | 循环、可中断 | 3.840 秒 |
| SleepEnter | 5 | 单次、不可中断 | 1.650 秒 |
| Sleep | 8 | 循环、可中断 | 4.080 秒 |
| SleepExit | 5 | 单次、不可中断 | 1.650 秒 |
| DragStart | 4 | 单次、不可中断 | 0.720 秒 |
| DragHold | 4 | 循环、不可中断 | 0.720 秒 |
| DragRelease | 4 | 单次、不可中断 | 0.840 秒 |
| Battle | 13 | 单次、不可中断 | 2.535 秒 |
| Victory | 10 | 单次、不可中断 | 2.100 秒 |

工程校验已经覆盖路径、帧数、透明通道、画布、时序和脚底锚点。连续播放观感、循环接缝和正式美术质量仍需在重新制作关键帧后完成实机验收。

## 系统要求

### 运行发布版

- Windows 10/11 x64。
- 不需要预先安装 .NET；发布脚本生成的是自包含版本。
- 必须保留整个发布目录，不能只复制 `Charlotte.Windows.exe`，因为动画和配置采用外置文件。

### 参与开发

- Windows 10/11 x64。
- Git。
- Windows PowerShell 5.1 或 PowerShell 7。
- 首次引导和 NuGet 恢复需要网络连接；之后可使用项目目录中的 SDK 和缓存。
- 不要求修改或替换系统全局安装的 .NET SDK。

项目通过 [`global.json`](global.json) 锁定 .NET SDK `10.0.401`，引导脚本会下载到被 Git 忽略的 `.tools/dotnet` 目录，并在解压前校验 SHA-512。

## 快速开始

### 从源码运行

克隆仓库后，在仓库根目录打开 PowerShell：

```powershell
.\scripts\bootstrap.ps1
.\scripts\dotnet.ps1 run --project .\src\Charlotte.Windows\Charlotte.Windows.csproj
```

`bootstrap.ps1` 只在本地 SDK 不存在时下载固定版本。应用运行数据不会写入仓库，默认保存在 `%LocalAppData%\CharlotteDesktopPet`。

### 获取可分发版本

本仓库不提交 `artifacts/`、`bin/`、`obj/` 或本地 SDK。需要先按[发布](#发布)章节生成自包含目录，再复制完整的 `artifacts/publish/win-x64`。

## 开发与验证

执行统一验证入口：

```powershell
.\scripts\verify.ps1
```

该脚本按顺序执行：

1. 以锁定模式恢复 NuGet 依赖。
2. 运行 Release 自动测试。
3. 构建完整解决方案。
4. 运行资源检查器，验证动画清单和全部 PNG。

也可以通过项目内 SDK 执行标准 .NET 命令：

```powershell
.\scripts\dotnet.ps1 test .\Charlotte.sln -c Release
.\scripts\dotnet.ps1 build .\Charlotte.sln -c Release
```

最新发布验收记录为 179 项自动测试通过，以及 10 个动作、69 帧资源检查通过。历史证据和仍待实机验证的项目记录在 [`docs/acceptance/release.md`](docs/acceptance/release.md) 与 [`docs/acceptance/matrix.md`](docs/acceptance/matrix.md)。

## 发布

生成 Windows x64 自包含发布目录：

```powershell
.\scripts\publish.ps1
```

输出位置：

```text
artifacts/publish/win-x64/
```

发布脚本会先运行完整验证，在临时目录中构建并检查发布内容，确认可执行文件和动画资源存在后再安全替换正式发布目录。旧版本不会直接与新文件混合。

发布布局回归：

```powershell
.\scripts\test-publish-layout.ps1
```

该脚本会从包含空格的路径和不同工作目录启动隔离副本，并验证外置透明帧可以安全替换。

## 项目结构

```text
Charlotte/
├─ assets/
│  └─ character/
│     ├─ formal-v1/              # 10 个动作、69 张运行时透明帧
│     └─ source/                 # 项目内基础图和审计覆盖源
├─ config/
│  ├─ animations.json            # 运行时动画清单、时序和行为参数
│  └─ behavior.json              # 保留的行为配置资料
├─ docs/
│  ├─ acceptance/                # 发布、性能、恢复和实机验收证据
│  ├─ superpowers/               # 已确认的设计与实施计划
│  ├─ asset-contract.md          # 动画资产工程契约
│  └─ implementation-status.md   # 按日期记录的实施状态
├─ scripts/                      # 引导、验证、发布、性能和 UI 探针
├─ src/
│  ├─ Charlotte.Core/            # 动画、几何、任务与持久化领域逻辑
│  └─ Charlotte.Windows/         # WPF 窗口、平台服务和 Win32 接入
├─ tests/
│  └─ Charlotte.Tests/           # 自动化测试
├─ tools/
│  ├─ Charlotte.AssetCheck/      # 发布前动画资源检查
│  ├─ Charlotte.AssetPrep/       # 正式素材预处理
│  ├─ Charlotte.FullscreenProbe/ # 全屏行为探针
│  └─ Charlotte.WindowInputProbe/# 透明像素输入探针
├─ Charlotte.sln
├─ global.json
└─ README.md
```

以下目录均可重新生成，已被 `.gitignore` 排除，不应上传 GitHub：

```text
.tools/
artifacts/
**/bin/
**/obj/
.vs/
```

## 核心设计

### 分层

- `Charlotte.Core` 保存尽量不依赖 WPF 的动画调度、位置策略、透明命中计算、任务、日程和撤销逻辑。
- `Charlotte.Windows` 负责窗口生命周期、DPI、显示器、Win32 消息、前台窗口监测、资源解码和数据落盘。
- `tools` 提供可重复的素材处理、资源检查和真实窗口探针。
- `tests` 对核心状态、异常恢复和关键 WPF 行为提供自动回归。

### 显示器与位置恢复

程序启动瞬间以系统判定的鼠标所在显示器作为初始显示器。保存时只记录桌宠横向相对位置；恢复时将该比例映射到当前显示器工作区并执行 Clamp。纵向位置始终根据工作区底部和动画脚底锚点重新计算。

这种策略避免保存绝对像素坐标导致角色在更换显示器、调整分辨率或修改 DPI 后出现在屏幕外或悬空。

### 透明输入

程序根据当前帧的 Alpha 掩码判断鼠标所在像素。透明区域允许点击穿透到下方窗口，可见角色区域继续接收点击、右键和拖拽。掩码与解码帧共同缓存，并随 DPI 版本更新。

### 动画调度

状态机负责闲置、睡眠、拖拽和演出动作的优先级与中断规则。逐帧时间线使用单调时钟，不依赖系统日期时间；帧解码在后台完成，过期结果由最新帧门控丢弃。窗口隐藏时停止新的解码请求。

### 数据持久化

每日任务、日程、面板会话和位置设置分别保存为 JSON。写入采用临时文件与原子替换，并保留备份；当前文件损坏时尝试读取备份，两者都不可用时回退到安全默认值，同时保留损坏文件用于诊断。

## 动画素材规范

完整规则见 [`docs/asset-contract.md`](docs/asset-contract.md)。任何替换帧至少必须满足：

- 逻辑画布固定为 `480 × 600 px`。
- 默认显示尺寸为 `240 × 300 DIP`。
- 脚底锚点固定为 `(240, 540)`。
- 使用带 Alpha 的 32 位 PNG。
- 同一动作族保持固定画布、缩放和锚点，禁止逐帧自动裁切人物边界。
- 角色、头发、衣摆、武器和特效不能越界裁断。
- 文件按动作目录和两位数字命名，例如 `idle/01.png`。
- 修改帧数或文件名时必须同步更新 `config/animations.json`。
- 替换后必须执行 `scripts/verify.ps1`。

当前建议的 Figma 工作流：

1. 使用固定的 480×600 顶层画板和脚底参考线。
2. 将后发、身体、四肢、头部、五官、前发和饰品分层。
3. 每个动画使用独立组件集，每个 Variant 对应一张最终帧。
4. 先完成关键姿势，再补中间帧；避免所有部位同时位移。
5. 以 1×、透明背景、禁止裁切的 PNG 导出。
6. 先用 Idle 做完整接入和实机验收，再扩展到 Sleep、Drag、Battle 和 Victory。

`assets/character/formal-v1/source-map.json` 记录正式帧的源映射和变换信息；`assets/character/source/formal-overrides/` 保存需要审计的项目内覆盖源。

## 数据、日志与隐私

默认用户数据目录：

```text
%LocalAppData%\CharlotteDesktopPet
```

主要内容包括：

```text
data.json             # 每日任务、日程等数据
settings.json         # 位置、面板和偏好设置
backups/              # JSON 恢复备份
logs/charlotte.log    # 轮换诊断日志
```

- 程序不会把用户数据写入发布目录或源码仓库。
- 当前 MVP 不包含 AI 对话功能，也不会在运行时建立 WebSocket 或 HTTP 对话连接。
- 如果以后加入对话模块，已确定默认使用 HTTP 协议，并应单独说明端点、授权和隐私策略。
- 诊断日志只记录受限的事件类型和错误信息，不写入每日任务正文。
- 开机启动只修改当前用户的注册表项，不请求管理员权限。
- 开发脚本可能访问 .NET 与 NuGet 下载源；这与桌宠运行时网络行为不同。

## 测试与性能

### 自动验证

`scripts/verify.ps1` 是提交前的统一验证门。测试覆盖的主要领域包括：

- 动画调度、时序、缓存和资源降级。
- 拖拽、透明命中、脚底基准线和多显示器位置策略。
- 管理面板生命周期、连续右键、任务和撤销。
- 单实例、自启动、全屏隐藏和 Windows 消息分类。
- JSON 保存、备份恢复、退出顺序和异常保存。
- 动画清单、路径安全、PNG 画布和 Alpha 像素。

### 实机探针

部分功能依赖真实 Windows 桌面，不能只用单元测试代替：

```powershell
.\scripts\test-control-panel.ps1
.\scripts\test-fullscreen.ps1
.\scripts\test-window-input.ps1
.\scripts\test-ground-baseline.ps1
.\scripts\test-effect-window.ps1
.\scripts\test-graceful-shutdown.ps1
.\scripts\test-asset-fallback.ps1
```

需要前台窗口或真实输入的脚本应在已解锁、可交互的 Windows 桌面运行。

### 性能测量

先发布，再运行：

```powershell
.\scripts\measure-performance.ps1
```

默认流程包含 5 次冷启动、30 秒预热、5 分钟 Idle 和 1 分钟隐藏采样，结果写入 `artifacts/performance`。测试使用受限的 `--data-dir` 隔离资料，不污染正式用户数据。

只采集环境信息而不启动桌宠：

```powershell
.\scripts\measure-performance.ps1 -EnvironmentOnly
```

最新性能证据和测试环境见 [`docs/acceptance/performance.md`](docs/acceptance/performance.md)。

## 已知限制

- 当前 69 张正式帧已经满足工程契约，但连续播放观感、循环接缝和最终 Q 版日漫美术仍需重新设计和实机验收。
- 右键管理面板功能已接通，但视觉层级、动效和整体交互仍计划重新设计。
- Windows 10、混合 DPI、多显示器负坐标、任务栏不同方向和显示器热插拔尚未完成完整硬件矩阵验证。
- 透明边缘的快速点击、双击和按住后拖出仍需要在真实交互桌面进行人工时序验收。
- 当前完整性能长测仅覆盖一台 Windows 11 单屏设备；正式动画更新后必须重新测量。
- 程序信任本机日期时间，手动跨日期可能再次触发每日任务重置。
- 磁盘持续不可写时只能有限重试并记录诊断，无法保证最终状态落盘。
- 当前不提供 AI 对话、云同步、自动更新、安装器或 Microsoft Store 分发。

完整列表及证据边界见 [`docs/acceptance/known-limitations.md`](docs/acceptance/known-limitations.md) 和 [`docs/acceptance/matrix.md`](docs/acceptance/matrix.md)。

## 后续计划

建议按以下顺序继续：

1. 在 Figma 中重新制作动画关键帧，并优化逐帧观感与运行时流畅性。
2. 重新设计右键管理面板的视觉、信息层级、边缘弹出和动效。
3. 完成多显示器、高 DPI、Windows 10 和长时间运行回归。
4. 整理 GitHub `main` 分支、版本标签和发布说明。
5. 在明确代码与角色素材授权后决定是否公开发布。

更细的历史实施记录见 [`docs/implementation-status.md`](docs/implementation-status.md)。

## 参与开发

提交修改前请遵循以下原则：

- 不提交 `.tools/`、`artifacts/`、`bin/`、`obj/` 或个人运行数据。
- 功能修改应包含相应自动测试；修复缺陷时优先增加能够复现问题的回归测试。
- 动画素材修改必须遵守画布、Alpha、锚点和路径契约。
- 不要把用户任务正文或其他私人数据写入诊断日志。
- 不要绕过原子保存、单实例或安全发布流程。
- 提交前运行 `scripts/verify.ps1`；涉及真实窗口行为时运行对应实机探针。
- 将功能开发放在独立分支中，保持 `main` 可构建、可验证。

## 授权说明

当前仓库尚未包含 `LICENSE` 文件，因此暂未声明为开源项目。除非仓库所有者另行书面说明，请不要假定以下内容可以自由复制、修改、商用或重新分发：

- Charlotte Desktop Pet 的源代码。
- 夏洛特角色形象、立绘、逐帧动画和衍生美术。
- 文档中的产品设计、角色设定和素材规范。

后续可以为程序代码和角色美术分别制定授权：例如代码使用独立的软件许可证，角色与动画素材继续保留单独的版权及使用限制。
