# Formal Animation Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import 77 supplied formal animation frames and update the runtime for Sleep 5/8/5, Drag 4/4/4, Battle 13, and baked effects.

**Architecture:** A deterministic WPF build tool converts read-only source PNGs into the existing 480x600 runtime contract and records an auditable source map. The runtime keeps `Sleep` as its public loop state, adds `SleepEnter` and `SleepExit`, and carries a per-clip overlay-effects flag so formal frames do not receive duplicate effects.

**Tech Stack:** C# 14, .NET 10, WPF imaging, System.Text.Json, xUnit, JSON manifest.

**Spec:** `docs/superpowers/specs/2026-09-15-formal-animation-integration-design.md`

## Global Constraints

- Write only under `C:/Users/Administrator/Desktop/Charlotte_Desktop_Pet/Charlotte`.
- Treat `C:/Users/Administrator/Desktop/Charlotte_Desktop_Pet/Character Reference Sheet` as read-only input.
- Runtime frames are 480x600 32-bit Alpha PNGs with a fixed ground anchor at Y=560.
- Do not crop or scale frames independently from other frames in the same motion family.
- New animation rules supersede old conflicting frame-count and transition rules.
- ClickSoft, ClickAnnoyed, and ClickWarning remain placeholders until formal assets exist.

---

### Task 1: Deterministic formal-asset preparation

**Files:**
- Create: `tools/Charlotte.AssetPrep/Charlotte.AssetPrep.csproj`
- Create: `tools/Charlotte.AssetPrep/Program.cs`
- Modify: `Charlotte.sln`
- Create: `assets/character/formal-v1/source-map.json` and 77 generated PNGs

**Interfaces:**
- Consumes: command arguments `<reference-root> <output-root>`.
- Produces: normalized action directories and a source-map JSON report; process exit code 0 only when all 77 mapped inputs were generated.

- [x] Add an executable WPF tool whose explicit mapping contains Idle 8, Walk 8, Rest 8, SleepEnter 5, Sleep 8, SleepExit 5, DragStart 4, DragHold 4, DragRelease 4, Battle 13, and Victory 10.
- [x] Load every source with `BitmapCacheOption.OnLoad`, reject non-alpha input, and hash its original bytes with SHA-256.
- [x] Render a fixed 480x600 transparent output with one family transform, center X=240, and ground Y=560; normalize the Sleep loop export anomaly without content-bound cropping.
- [x] Write outputs atomically into `assets/character/formal-v1` and serialize `source-map.json` containing 77 ordered entries.
- [x] Run `scripts/dotnet.ps1 run --project tools/Charlotte.AssetPrep -- <reference-root> assets/character/formal-v1` and verify the reported count is 77.
- [x] Run the asset checker after the manifest task and commit the tool and generated assets with `feat: prepare formal animation assets`.

### Task 2: Animation contract and baked-effect policy

**Files:**
- Modify: `src/Charlotte.Core/Animation/AnimationModels.cs`
- Modify: `src/Charlotte.Windows/Assets/AnimationManifest.cs`
- Modify: `src/Charlotte.Windows/Assets/AssetValidator.cs`
- Modify: `src/Charlotte.Windows/Services/AnimationPresenter.cs`
- Modify: `tests/Charlotte.Tests/AssetValidatorTests.cs`
- Modify: `tests/Charlotte.Tests/ManifestLoaderTests.cs`

**Interfaces:**
- Produces: `AnimationId.SleepEnter`, `AnimationId.SleepExit`, and `AnimationClip.OverlayEffects`.
- Manifest clip field: `overlayEffects: boolean`; asset stage accepts `placeholder`, `formal`, or `hybrid`.

- [x] Add failing validator tests that require SleepEnter 5, Sleep 8, SleepExit 5, DragStart/Hold/Release 4 each, and Battle 13.
- [x] Run the focused tests and confirm failures reflect the old frame-count contract.
- [x] Add the two animation IDs and update `AssetValidator.CheckClip` to the confirmed counts and Battle duration range.
- [x] Run the focused tests and confirm they pass.
- [x] Add a failing manifest test showing the project manifest is hybrid, formal Battle has 13 frames, and formal clips disable overlay effects while placeholder Click stays enabled.
- [x] Run the focused manifest test and confirm it fails against the old manifest/model.
- [x] Add `OverlayEffects` to `AnimationClip`, parse `overlayEffects`, accept `hybrid`, and have the presenter sample/render effects only when the current clip enables them.
- [x] Update the manifest and run focused manifest/effect tests to green.
- [x] Commit with `feat: support formal animation contracts`.

### Task 3: Sleep transition state machine

**Files:**
- Modify: `src/Charlotte.Core/Animation/AnimationScheduler.cs`
- Modify: `tests/Charlotte.Tests/AnimationSchedulerTests.cs`
- Modify: `src/Charlotte.Windows/Windows/ControlPanelWindow.xaml.cs` only if public Sleep routing requires it.

**Interfaces:**
- Existing `AnimationRequest.Panel(AnimationId.Sleep)` remains the public sleep request.
- `SleepEnter.ReturnTo == Sleep`; `SleepExit.ReturnTo == Idle`.

- [x] Add a failing scheduler test proving automatic sleep follows SleepEnter then Sleep.
- [x] Add a failing scheduler test proving interaction during Sleep or SleepEnter enters SleepExit, and SleepExit finishes at Idle.
- [x] Add a failing scheduler test proving a queued panel Sleep request after Battle starts SleepEnter rather than jumping to Sleep.
- [x] Run focused scheduler tests and confirm each fails for the missing transition behavior.
- [x] Implement `StartSleep` and `WakeFromSleep` helpers with graceful direct-loop/direct-Idle fallback when transition clips are absent.
- [x] Route ambient sleep, panel Sleep, and interaction wake-up through those helpers without changing Drag priority.
- [x] Run scheduler tests and then all Core/Windows tests to green.
- [x] Commit with `feat: add sleep enter and exit transitions`.

### Task 4: Manifest deployment, documentation, and verification

**Files:**
- Modify: `config/animations.json`
- Modify: `docs/asset-contract.md`
- Modify: `docs/implementation-status.md`
- Modify: `docs/acceptance/matrix.md`

**Interfaces:**
- The manifest points formal actions at `character/formal-v1/*` and retains generated Click paths.
- Frame durations remain manifest-configurable and are not encoded in filenames.

- [x] Configure Idle 8, Walk 8, Rest 8, SleepEnter 5, Sleep 8, SleepExit 5, Drag 4/4/4, Battle 13, and Victory 10 using normalized formal assets.
- [x] Set `overlayEffects: false` for baked formal clips and `true` for placeholder Click clips; set `assetStage` to `hybrid`.
- [x] Update the asset contract and acceptance matrix with the new source-map, state transitions, and remaining Click placeholder limitation.
- [x] Run `scripts/dotnet.ps1 run --project tools/Charlotte.AssetCheck -- assets config/animations.json` and require exit code 0.
- [x] Run `scripts/dotnet.ps1 test Charlotte.sln --no-restore --verbosity minimal` and require zero failed tests.
- [x] Run `scripts/dotnet.ps1 build Charlotte.sln -c Release --no-restore --verbosity minimal` and require exit code 0.
- [x] Inspect `git diff --check`, `git status --short`, and generated frame counts before reporting completion.
- [x] Commit with `docs: record formal animation deployment`.
