# 01 · Project Setup

Target: **Unity 6.3 LTS (6000.3.x)** · **Universal Render Pipeline (URP) 17.3.0** · **Android / landscape**

---

## 1. Requirements

| Item | Version | Notes |
|---|---|---|
| Unity Editor | **6000.3.x** (6.3 LTS) | 6.0–6.2 also work; 6.3 is what this project was written for |
| Universal Render Pipeline | **17.3.0** | matches Unity 6.3; auto-resolved from `Packages/manifest.json` |
| uGUI (`com.unity.ugui`) | 2.0.0 | ships with Unity 6, used by the HUD |
| Build support | Android Build Support **+** OpenJDK **+** Android SDK & NDK Tools | install via Unity Hub ▸ Add Modules |
| Input | Input System **or** legacy Input Manager **or** Both | the code adapts (see §5) |

Optional packages that make profiling nicer: *Android Logcat* (Package Manager), *Memory Profiler*.

> **Upgrading a project that already has a NUMBER RUSH 3D scene?** Do not rebuild it.
> Copy the updated `Assets/NumberRush3D` folder over the old one, open your scene and run
> **`Tools ▸ NUMBER RUSH 3D ▸ 7. Upgrade Existing Scene`**. It adds the runner systems
> (endless track, number gates, campaign levels, campaign UI) and fills only empty references —
> nothing is deleted or replaced. Full details: **[09 · Runner Upgrade](09-RUNNER-UPGRADE.md)**.

---

## 2. Path A — recommended: create the project from the Universal 3D template

This is the path that requires no URP configuration at all, because the template assigns the pipeline asset for you.

1. **Unity Hub ▸ New project ▸ Universal 3D**.
2. Editor version: **Unity 6000.3.x**. Project name: `NumberRush3D`.
3. Create, then wait for the first import to finish.
4. Copy the folder `Assets/NumberRush3D` from this repository into `Assets/` of the new project.
   (Drag & drop in your file manager, or `cp -r` on the command line.)
5. Back in Unity, wait for the script compile to finish (no errors expected), then run
   **`Tools ▸ NUMBER RUSH 3D ▸ 1. Build Materials, Prefabs and Scene`**.
6. Press **Play**.

## 3. Path B — open this repository directly

This repository is a valid (minimal) Unity project: it contains `Assets/`, `Packages/manifest.json` and `ProjectSettings/ProjectVersion.txt`. Unity generates the remaining `ProjectSettings` assets with defaults on first open.

1. Unity Hub ▸ **Add project from disk** ▸ select this repository folder.
2. Open it with **6000.3.x**. If the Hub asks to upgrade the project, accept.
3. One package resolution step is required, because a bare project has no pipeline asset assigned:

   **a.** `Window ▸ Package Manager` → confirm **Universal RP 17.3.0** is installed (it comes from the manifest).

   **b.** `Assets ▸ Create ▸ Rendering ▸ URP Asset (with Universal Renderer)` → name it `URP_Mobile` → put it in `Assets/Settings/`.

   **c.** `Edit ▸ Project Settings ▸ Graphics` → **Default Render Pipeline** = `URP_Mobile`.
   Also set it for every quality level (`Project Settings ▸ Quality ▸ Render Pipeline Asset`), otherwise the quality level silently falls back to built-in rendering.

   **d.** `Tools ▸ NUMBER RUSH 3D ▸ 1. Build Materials, Prefabs and Scene`.

> **Why this matters:** URP shaders render **magenta** when no pipeline asset is assigned. The generator detects the situation and falls back to the built-in `Standard` shader so you never see pink boxes, but you only get the intended URP look once a pipeline asset is assigned.

---

## 4. Verify the setup

Run **`Tools ▸ NUMBER RUSH 3D ▸ 6. Project Setup Checklist`**. It prints and shows:

```
Render pipeline : UniversalRenderPipelineAsset      <- or "BUILT-IN (no SRP asset assigned)"
Input backend   : Input System package (new)        <- or legacy / Both
Enemy layer     : OK                                <- created by step 1
Scene           : OK
Build target    : Android
Bundle id       : com.yourstudio.numberrush3d
Graphics APIs   : Vulkan, OpenGLES3
Scripting       : IL2CPP / ARM64
Min SDK         : AndroidApiLevel26
```

Anything that says `MISSING` is fixed by running step 1 of the generator.

---

## 5. Input backend

Unity 6 can be set to *Input Manager (Old)*, *Input System Package (New)* or *Both* (`Edit ▸ Project Settings ▸ Player ▸ Active Input Handling`).

This project never assumes which one is active. `Assets/NumberRush3D/Scripts/Controls/PointerInputBridge.cs` is the **only** file that touches an input API, and it compiles the right implementation from Unity's generated defines:

| Active Input Handling | Define Unity sets | What runs |
|---|---|---|
| Input Manager (Old) | `ENABLE_LEGACY_INPUT_MANAGER` | `Input.touches` / `Input.mousePosition` |
| Input System Package (New) | `ENABLE_INPUT_SYSTEM` | `Touchscreen.current` / `Mouse.current` / `Keyboard.current` |
| Both | both | legacy branch (used first), Input System branch stays compiled |

The same file also supplies the keyboard fallback (A/D, ←/→, R/Space), used by the generator to pick the correct `EventSystem` input module (`StandaloneInputModule` vs `InputSystemUIInputModule`).

Nothing else in the project reads input, so switching backends never requires a code change.

---

## 6. Recommended project settings

### 6.1 Color space

`Project Settings ▸ Player ▸ Other Settings ▸ Color Space` = **Linear** (the Android preset applies this).

### 6.2 URP asset (mobile profile)

Select your URP asset and use these values. They are conservative and keep a mid-range phone above 60 fps:

| Section | Setting | Value |
|---|---|---|
| Rendering | Renderer | Universal Renderer |
| Rendering | Depth Texture | Off |
| Rendering | Opaque Texture | Off |
| Rendering | MSAA | 4x (or 2x on low-end) |
| Rendering | Render Scale | 1.0 |
| Quality | HDR | Off |
| Quality | Anti Aliasing (SMAA) | Off (MSAA already covers it) |
| Quality | Shadow Distance | 30 |
| Quality | Shadow Cascades | 1 (No Cascades) |
| Quality | Soft Shadows | Off for the lowest tier, on for higher |
| Quality | Shadow Resolution | 1024 |
| Quality | Additionallights | Disabled (the game uses one directional light) |
| Quality | SRP Batcher | On |

### 6.3 Universal Renderer asset

| Setting | Value |
|---|---|
| Rendering Path | Forward (Forward+ is fine too) |
| Intermediate Texture | Auto |
| Depth Priming Mode | Disabled |
| Accurate G-Buffer Normals | N/A for Forward |

### 6.4 Quality levels

Keep 2 levels (`Mobile` / `Mobile High`). Put the low-end values on level 0 and make the default **Mobile**; the game is GPU-light (≈150 draw-call candidates, no post processing, no realtime GI) so both levels look nearly identical.

### 6.5 Player settings (all applied by `Tools ▸ 2`)

| Setting | Value |
|---|---|
| Default orientation | Auto Rotation, **landscape left + right only** |
| Scripting backend | IL2CPP |
| Target architectures | ARM64 only |
| API compatibility | .NET Standard 2.1 |
| Managed stripping | Medium |
| Minimum API level | 26 (Android 8.0) |
| Target API level | Automatic (highest installed) |
| Graphics APIs | Vulkan, OpenGLES3 |
| Optimized frame pacing | On |
| Bundle identifier | `com.yourstudio.numberrush3d` — **change before publishing** |

`Application.targetFrameRate` is set to **60** at runtime by `GameManager` from `GameSettings.targetFrameRate`, and `vSyncCount` is forced to 0 (mobile needs the explicit frame rate).

---

## 7. First run checklist

1. Console shows no compile errors.
2. The scene `Assets/NumberRush3D/Scenes/NumberRush3D.unity` is open (it is opened by the generator).
3. Press **Play**: the **LEVEL SELECT** screen appears over the frozen scene (level 1 unlocked, the
   rest dimmed). Pick level 1, or **FREE RUN** for the endless mode.
4. In a run:
   * 10 blue soldiers appear near the bottom of the view,
   * red enemies walk in from the horizon and die in yellow sparks,
   * the HUD counts the army (top left), score (top centre), best/wave (top right),
   * a swipe (or A/D) slides the army one lane with a soft ease,
   * the recruit bar fills; every 8 kills adds a soldier.
5. The army runs forward on its own while the track keeps streaming (no gap, no void), green/red
   number gates arrive with 3D labels, and passing one changes the army immediately.
6. Let enemies through on purpose until the army reaches 0 → slow motion → failed panel
   (**RETRY** restarts) — in Free Run you get the original game over panel instead.

If any of that does not happen, jump to **[08 · Troubleshooting](08-TROUBLESHOOTING.md)**.

---

Next: **[02 · Scene Setup](02-SCENE-SETUP.md)**
