# Formal Animation Integration Design

## Authority

The user confirmed on 2026-09-15 that `Charlotte_Animation_Implementation_Notes.docx` supersedes earlier animation rules wherever they conflict. In particular, Battle is 13 frames, Drag is 4/4/4, and Sleep is 5/8/5 with explicit enter and exit transitions.

The user confirmed on 2026-09-20 that runtime playback should use the calmer timing profile below. This timing decision supersedes the early placeholder defaults but does not change frame counts, clip priority, or interruptibility.

The user subsequently confirmed on 2026-09-20 that Walk and all three Click animations must be retired, together with the legacy `generated/` directory. Click input remains an interaction signal, while automatic walking and the panel Walk command are disabled.

## Goal

Deploy the retained Charlotte PNG sequences without modifying the source reference folder, while preserving the existing 480x600 logical canvas, 240x300 DIP window, scheduler priority rules, and non-animation click behavior.

## Asset pipeline

- Treat `Character Reference Sheet` as read-only input.
- Import only transparent action frames; exclude RGB overview sheets such as `Battle.png` and `Walk.png`.
- Generate versioned runtime assets under `assets/character/formal-v1`.
- Use a fixed transform for every frame in one motion family. Never crop individual visible bounds or resize the runtime canvas to a frame's content.
- Render every runtime frame to 480x600 Pbgra32 and horizontally center it. The deployed formal manifest uses visible-foot anchor Y=540 so the Idle feet meet the taskbar boundary; the source-family rectangle keeps its fixed preprocessing transform.
- Repair the exported `Sleep_Loop (7).png` canvas through the same deterministic family normalization so it cannot change the runtime canvas or display aspect ratio.
- Record source-relative names, hashes, output names, dimensions, and transform settings in `source-map.json`.

## Runtime model

- Keep `AnimationId.Sleep` as the looping sleep state and public panel command.
- Add `SleepEnter` and `SleepExit` clips.
- Automatic or panel-requested sleep starts at `SleepEnter`, continues to `Sleep`, and wakes through `SleepExit` before Idle.
- Drag remains higher priority than other actions. DragStart, DragHold, and DragRelease each contain four frames; DragRelease remains uninterruptible and completes before pending work.
- Battle contains 13 frames, remains uninterruptible, and returns to Idle.
- Each clip declares whether programmatic overlay effects are enabled. Formal Sleep, Drag, Battle, and Victory frames use baked effects and therefore disable overlays.
- The manifest reports a `formal` asset stage and contains no Walk or Click clips.
- Idle uses per-frame holds of 280/320/360/480/360/320/280/480 ms so the pose has deliberate pauses instead of mechanical constant-rate playback.
- Rest uses 320 ms per frame; Sleep uses 340 ms per frame; SleepEnter and SleepExit use 220 ms per frame.
- Battle uses 130 ms per frame and Victory uses 140 ms per frame. Drag timing remains unchanged to preserve direct manipulation feedback.
- Validation accepts Battle totals from 1500–1900 ms and Victory totals from 1200–1600 ms, retaining protection against clearly mistimed assets while allowing this profile.

## Failure behavior

- A missing or invalid formal clip is disabled by the resilient manifest loader and falls back to Idle without preventing startup.
- If SleepEnter is unavailable, sleep starts directly in the Sleep loop. If SleepExit is unavailable, wake-up returns directly to Idle.
- Asset validation rejects wrong frame counts, non-480x600 frames, missing alpha content, unsafe paths, and invalid durations.

## Verification

- Unit tests cover 5/8/5 sleep transitions, wake behavior, 4/4/4 drag counts, 13-frame Battle, the formal manifest, retired Walk/Click behavior, and baked-effect suppression.
- The asset preparation command must create exactly 69 formal runtime frames plus `source-map.json` and must not emit Walk.
- `Charlotte.AssetCheck` must pass on the complete manifest.
- The full solution test suite and Release build must pass before completion is reported.
