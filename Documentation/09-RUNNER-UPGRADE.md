# 09 · Runner Upgrade (existing scenes)

This chapter covers the runner conversion and, most importantly, **how to bring an already built
scene up to date without rebuilding it**: menu item **`Tools ▸ NUMBER RUSH 3D ▸ 7. Upgrade Existing Scene`**.

If you are starting from scratch, step 1 already runs the exact same upgrade routine at the end of
the build, so both routes end with identical wiring.

---

## 1. What the upgrade adds

Menu item 7 only **adds** objects and only **fills empty references**. Nothing is deleted, renamed,
re-parented or replaced, and no existing serialized value is overwritten. Running it twice is a
no-op (every step is guarded).

| Where | Added when missing | What it does |
|---|---|---|
| `Systems` | `TrackManager` | Endless pooled track slices that follow the army |
| `Systems` | `GateManager` | Streams number gate rows ahead of the army |
| `Systems/Gates` | child `Gates` | Parent for the pooled gate objects |
| `Systems` | `LevelManager` | Campaign flow: level resolve, completion, stars, coins, screens |
| `UI Canvas` | `LevelSelectScreen` | Full screen level grid (built at runtime from the catalog) |
| `UI Canvas` | `LevelCompletePanel` | Stars, score, coins, Next / Replay / Levels |
| `UI Canvas` | `LevelFailedPanel` | Progress reached, Retry / Levels |
| `UI Canvas` | `PausePanel` | Resume / Restart / Levels, driven by `GameState.Paused` |
| `SafeArea/HUD` | `CoinsLabel` | Wallet counter (top right, inside the safe area) |
| `SafeArea/HUD` | `LevelProgress` + `Fill` | Level progress bar under the score |
| `SafeArea/HUD` | `ToastLabel` + `CanvasGroup` | Gate result / level feedback |
| `SafeArea/HUD` | `PauseButton` | Calls `GameManager.TogglePause()` |
| `Settings/` | `LevelCatalog.asset` | Level list used by the level select |
| `Settings/Levels/` | `Level_001 … Level_100.asset` | Data driven campaign levels |

References are assigned with `AssignIfEmpty`: a field that already points at something keeps its
value, which is why a designer's manual rewiring survives a re-run.

### References the upgrade writes

```
TrackManager.legacyTrack   <- scene root "Environment"   (hidden at runtime, endless slices replace it)
GateManager.gateRoot       <- Systems/Gates
LevelManager.game          <- GameManager
LevelManager.catalog       <- Settings/LevelCatalog.asset
LevelManager.track         <- TrackManager
LevelManager.gates         <- GateManager
LevelManager.spawner       <- EnemySpawner
LevelManager.hud           <- HudController
LevelManager.legacyGameOverPanel <- GameOverPanel
LevelManager.levelSelect / completePanel / failedPanel / pausePanel <- the four screens
GameManager.army / spawner / cameraRig / hud / gameOverPanel        <- only if empty
HudController.coinsLabel / progressFill / toastLabel+toastGroup / pauseButton
```

`CameraRig` and `EnemySpawner` need no new reference: the camera already follows `army.transform`,
and the spawner now places enemies at `armyZ + enemySpawnAhead` at runtime. The only camera tweak is
raising `farClipPlane` to at least 260 so the far track slices are never clipped.

---

## 2. Why there is no black void behind the start section

Three things happen when the scene starts:

1. `LevelManager` initialises `TrackManager` and calls `Begin(armyZ)` **before the first frame is
   rendered** (its start-up runs from the first `Update`, after `GameManager.Start`).
2. `TrackManager.Begin` hides the legacy `Environment` object (ground slab, lane strips, rails,
   backdrop) and immediately places `trackSegmentCount` (8) × 20 m slices — from 20 m behind the
   army to 140 m in front of it. Every slice carries its own floor, lane dividers, kerbs and
   decoration, so the world is complete on frame 0.
3. Every frame, `TrackManager.Update` extends the track while the front edge is closer than
   `lookAhead` (70 m) and teleports the oldest slice to the front once it is `recycleBehind` (30 m)
   behind the army. Slices are recycled — never instantiated — so the count stays at 8.

Gate rows do the same thing: `GateManager` keeps rows alive between 26 m ahead and 26 m behind.

---

## 3. What changed in the existing systems

Nothing was rewritten for the sake of it; these are the edits a runner genuinely needs.

| Component | Change |
|---|---|
| `ArmyController` | Owns forward motion (`ForwardSpeed`, `DistanceTravelled`), lane clamp, `Recruit`/`SetSoldierCount`/`ApplyCasualties`, gate math (`ComputeGateResult`), `OverrideStartSpeed` for levels |
| `SoldierUnit` | Targets filtered against the army's **live** Z, procedural run cycle, spawn pop |
| `SwipeInput` | Ignores pointers over UI, one-step request queue, `SetInteractionEnabled` for menus |
| `CameraRig` | Exact forward follow (army never drifts on screen), damped lateral follow, boss rumble |
| `Enemy` | Ram line is `armyZ + ramDistanceAhead` instead of a fixed world Z |
| `EnemySpawner` | Spawns ahead of the army, schedules bosses, level concurrency + type filter |
| `Health` | `DamageMultiplier` for armoured enemies |
| `GameManager` | Level run profile, coins, progress, boss flags, `CompleteLevel()`, pause/resume, `SuppressResultPanel` |
| `HudController` | Coins, level progress, toast, pause button (auto-created if the scene has none) |
| `GameOverPanel` | Stays hidden during campaign runs (the level screens take over) |

Unchanged on purpose: the bullet pool and shooting pipeline, the enemy pool, the VFX pool, the input
backend, the render pipeline, `NumberRushBuildTools`, the Android preset and every prefab.

---

## 4. Campaign levels

A level is a `LevelDefinition` asset — nothing about a level is hardcoded:

| Field | Meaning |
|---|---|
| `levelNumber`, `title` | Identity + label on the level select button |
| `trackLength` | Metres the army must survive to complete the level |
| `coinReward` | Coins on completion (plus `coinsPerKill` from kills, plus `coinBonusPerStar`) |
| `startingSoldiers`, `runSpeed` | Army size at the start; `runSpeed` 0 = use `GameSettings.runnerStartSpeed` |
| `enemyHealthMultiplier`, `enemySpeedMultiplier`, `spawnIntervalScale` | Per-level difficulty scaling |
| `maxConcurrentEnemies` | Concurrency cap for this level |
| `enemyTypes` | Which kinds may spawn (empty = the spawner's inspector list) |
| `gateRows` | Authored gate patterns, cycled; empty = procedural rows |
| `bossType`, `bossProgress` | Boss asset and the progress (0..1) at which it appears |
| `threeStarArmyFraction`, `twoStarArmyFraction` | Star thresholds against the **peak** army of the run |

**Generated set (100 levels).** `EnsureLevelCatalog` writes level 1…100 with: track 220 m + 8 m per
level, 8…24 starting soldiers, enemy HP ×1…×3.2, speed ×1…×1.7, spawn interval ×1…×0.45, 20…44
concurrent enemies, coins 20…218, three cycling gate patterns and a **boss every 5th level from level
5**. Enemy kinds unlock at level 4 (Runner), 8 (Brute) and 12 (Armored). Defaults are written **only
when an asset is created**, so hand tuning is never lost.

**Flow.** `LevelLaunch.StartLevel(n)` stores the request and reloads the scene (the same reset
mechanism `Restart` already used). `LevelManager` (execution order −110) resolves the request in
`Awake` and publishes `LevelLaunch.ActiveLevel`; `GameManager` (−100) reads it in its own `Awake` and
installs the run profile before the army is spawned.

**Completion.** `LevelProgress01` reaches 1 **and** (on boss levels) the boss is dead →
`GameManager.CompleteLevel()` → army stops, 0.35× slow motion for `levelEndSlowMotionDuration`,
freeze, then `LevelCompletePanel`. Stars are computed from the final vs peak army size and stored per
level in `PlayerPrefs`; the next level is unlocked at the same time.

**Failure.** Army wiped out → `GameOver` → the legacy game over panel is suppressed → after the
hit-stop `LevelFailedPanel` shows the progress reached, with Retry / Levels.

**Persistence.** `NumberRush3D.UnlockedLevel`, `NumberRush3D.Stars.<n>` and the wallet
`NumberRush3D.Coins`. Progress survives app restarts.

**Free Run** is still available from the level select and behaves like the original endless mode.

---

## 5. Verification checklist

After running step 7:

1. **Console** — no errors. `[NumberRush] Scene upgrade complete: N component(s) added, M reference(s) assigned.`
2. **Hierarchy** — `Systems` now shows `TrackManager`, `GateManager`, `LevelManager`; `Systems/Gates` exists;
   the canvas shows the four new screens; the HUD has `CoinsLabel`, `LevelProgress`, `ToastLabel`,
   `PauseButton`.
3. **No duplicates** — run step 7 again: it reports `0 component(s) added`.
4. **Assets** — `Assets/NumberRush3D/Settings/LevelCatalog.asset` + `Settings/Levels/Level_001…100.asset`.
5. **Play** — the level select appears (the scene is paused behind it), level 1 is unlocked and
   levels 2…100 are dimmed.
6. **Pick level 1** — the army runs forward on its own, the track keeps coming with no gap or void,
   green/red gates arrive with 3D labels, enemies spawn ahead.
7. **Pass a gate** — the army count changes immediately, a toast appears and the value is never 0.
8. **Finish a level** — stars + coins are awarded, the next level unlocks, **NEXT** continues to
   level 2, **REPLAY** restarts the same level, **LEVELS** returns to the grid.
9. **Fail a level** — the failed panel appears with the progress reached.
10. **Checklist** — `Tools ▸ NUMBER RUSH 3D ▸ 6. Project Setup Checklist` now reports
    `Runner systems : OK`, the level count, the unlocked level and the legacy-track state.

---

## 6. Manual wiring (if you prefer to add it by hand)

1. Select `Systems` → **Add Component** → `Track Manager`, `Gate Manager`, `Level Manager`.
   (Order does not matter; execution order is set by attribute.)
2. `Track Manager` → drag the scene root `Environment` into **Legacy Track**.
3. `Gate Manager` → drag a child object named `Gates` (create it) into **Gate Root**.
4. `Level Manager` → assign Game Manager, Level Catalog, Track Manager, Gate Manager, Enemy Spawner,
   Hud Controller, Game Over Panel and the four screens.
5. Create the four screens as **empty children of the UI Canvas** with a stretched `RectTransform`
   and add the matching component. They build their own contents at runtime, so an empty object is
   enough.
6. HUD widgets are optional: `HudController` creates coins, progress, toast and pause itself when the
   fields are empty.

---

## 7. Known limits

* The upgrade is verified by static analysis only in this repository (no Unity editor is available
  here). Always run the checklist in section 5 in the editor after running step 7.
* The generated 100 levels are a **starting point** — tune `LevelDefinition` assets freely.
* `Legacy Track` is hidden for the whole session. If you keep bespoke decoration under
  `Environment`, either move it out of that object or leave `Legacy Track` empty in `TrackManager`.
