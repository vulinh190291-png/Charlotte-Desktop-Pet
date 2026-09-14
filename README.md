# Charlotte Desktop Pet

Windows 10/11 x64 桌面宠物 MVP。当前版本使用满足透明画布、帧数、时序和脚锚点契约的粗加工占位帧；正式 Q 版日漫动画可按 `docs/asset-contract.md` 原位替换。

## 开发验证

```powershell
.\scripts\verify.ps1
```

本项目锁定项目内 `.tools/dotnet` 的 .NET SDK 10.0.401，不要求修改全局 SDK。

## 发布

```powershell
.\scripts\publish.ps1
```

输出目录为 `artifacts/publish/win-x64`。复制整个目录后直接运行 `Charlotte.Windows.exe`；不要只复制 exe，因为动画和配置保持外置以便替换。

发布脚本先在临时目录生成并校验完整产物，再替换正式发布目录，避免旧文件混入新版本。可额外运行下面的发布布局回归；它会刷新含空格路径 `artifacts/release smoke`，从不同工作目录校验资源，并在隔离副本中验证透明帧可替换：

```powershell
.\scripts\test-publish-layout.ps1
```

## 性能测量

先发布，再执行：

```powershell
.\scripts\measure-performance.ps1
```

脚本默认进行 5 次进程冷启动、30 秒预热、5 分钟 Idle 和 1 分钟隐藏采样，结果写入 `artifacts/performance`。测试资料通过受限的 `--data-dir` 参数隔离，不污染正式用户数据。

只核对 CPU、RAM、Windows build 和 DPI，不启动桌宠；DPI 优先读取鼠标所在显示器，非交互环境不可用时明确标记为系统 DPI fallback：

```powershell
.\scripts\measure-performance.ps1 -EnvironmentOnly
```

当前通过项、未测硬件和正式美术缺口分别记录在 `docs/acceptance/performance.md`、`docs/acceptance/matrix.md` 与 `docs/acceptance/known-limitations.md`。

## 操作

- 左键单击角色触发点击反馈，拖动可把角色停留在任意桌面位置。
- 右键角色打开统一管理面板；可预览动作、管理每日任务和日程、设置当前用户开机启动或退出。
- 每日任务最多 7 条；完成任务触发 Victory；删除后 5 秒内可撤销。
- 重启后在启动瞬间鼠标所在显示器恢复横向相对位置，纵向重新落到该显示器工作区底部基准线。
- 默认始终置顶；其他应用覆盖角色当前显示器完整显示区域时暂时隐藏。
- 无交互时按外置行为参数进行低干扰短距离散步；打开管理面板会暂停自动散步。

运行数据位于 `%LocalAppData%\CharlotteDesktopPet`，不写入发布目录。
