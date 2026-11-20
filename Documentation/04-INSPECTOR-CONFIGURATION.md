# 04 · Inspector Configuration

Every serialized field in the project, with its default, its valid range and what changing it does. Defaults shown are the ones in code (so a freshly generated `GameSettings` asset matches this table).

Legend: **R** = the value is only read once at start-up (requires Play/re-import to take effect), **L** = live, changeable while the game runs.

---

## 1. `GameSettings` (ScriptableObject)

`Assets/NumberRush3D/Settings/GameSettings.asset` — the single source of truth for tuning. Every gameplay system receives it through `IBattleContext`, so nothing else hard-codes numbers. **L** for most values, but a few are cached at start-up (`maxSoldiers` pool size, `maxBulletsInFlight`).

### Lanes & Track

| Field | Default | Range | Effect |
|---|---|---|---|
| `laneCount` | 5 | 3–9 | Number of lanes the army can swipe between. Re-run generator step 1 to rebuild the lane strips. |
| `laneWidth` | 2.8 | 1–6 | Metres between lane centres. Together with `laneCount` it defines the playable width (7.0 m half width by default). |
| `armyZ` | −8 | any | Z of the army root. **R** (the camera framing and formation read it at start). |
| `defenseLineZ` | −2.5 | any | Enemies that cross this Z ram the army. Raise it (e.g. −5) to make the game harder, lower it to give more reaction time. |
| `spawnZ` | 15 | any | Spawn line. Distance to the defense line = `spawnZ − defenseLineZ` = 17.5 m. |

### Army

| Field | Default | Range | Effect |
|---|---|---|---|
| `startingSoldiers` | 10 | 1–200 | Soldiers at run start. |
| `maxSoldiers` | 60 | 1–300 | Recruit cap; also sizes the soldier pool (cap + 8). |
| `soldierSpacing` | 0.46 | 0.2–1.5 | Horizontal spacing inside a formation row. |
| `soldierRowSpacing` | 0.52 | 0.2–1.5 | Front-to-back spacing. The block is centred on the army root. |
| `maxFormationColumns` | 7 | 2–12 | Columns before the crowd grows backwards instead of sideways. |
| `laneMoveSmoothTime` | 0.13 | 0.02–0.6 | SmoothDamp time for the lane slide. 0.08 = snappy, 0.3 = floaty. |
| `formationLerpSpeed` | 14 | 1–40 | How fast soldiers snap to their slot after a recruit/casualty. |

### Shooting

| Field | Default | Range | Effect |
|---|---|---|---|
| `shotsPerSecond` | 0.9 | 0.2–5 | Per-soldier fire rate (≈ one bullet every 1.1 s). Primary DPS dial. |
| `bulletDamage` | 1 | 0.1–50 | Damage per hit. With `shotsPerSecond` it defines army DPS = `soldiers × shotsPerSecond × bulletDamage`. |
| `bulletSpeed` | 42 | 10–120 | m/s. At 42 a bullet covers the 17.5 m track in ~0.42 s. |
| `bulletRadius` | 0.12 | 0.02–0.5 | Sphere-sweep radius → effectively "how forgiving" hit detection is. |
| `bulletMaxLifetime` | 1.6 | 0.2–5 | Seconds before a missed bullet despawns (42 × 1.6 = 67 m). |
| `targetSearchRange` | 45 | 5–80 | Max distance a soldier looks for a target. Keep it ≥ `spawnZ − armyZ + 10`. |
| `targetRefreshInterval` | 0.22 | 0.05–1 | Delay before a soldier without a target searches again (staggered per soldier). |
| `aimSpreadDegrees` | 1.4 | 0–6 | Random cone per shot. Values above ~3 make long-range shots visibly miss. |

### Enemies

| Field | Default | Range | Effect |
|---|---|---|---|
| `enemyLayerName` | `Enemy` | string | Layer resolved into the bullet hit mask. Keep in sync with the layer you created. |
| `enemySpeedMultiplier` | 1 | 0.2–3 | Global multiplier over every `EnemyType.moveSpeed`. |
| `spawnStartInterval` | 1.35 | 0.1–5 | Seconds between waves at t = 0. |
| `spawnMinInterval` | 0.34 | 0.05–2 | Seconds between waves at full difficulty. |
| `difficultyRampSeconds` | 95 | 10–600 | Time to go from 0 → 1 difficulty. |
| `spawnBurstMin` | 1 | 1–6 | Enemies per wave at t = 0. |
| `spawnBurstMax` | 3 | 1–6 | Enemies per wave at full difficulty. |
| `maxConcurrentEnemies` | 26 | 1–80 | Hard cap on living enemies (also sizes the enemy pool). |
| `spawnJitterZ` | 2.5 | 0–6 | ± random Z at spawn so waves do not look aligned. |
| `spawnJitterX` | 0.35 | 0–1.2 | ± random X at spawn so enemies are not pixel-perfect in the lane. |

### Rewards

| Field | Default | Range | Effect |
|---|---|---|---|
| `killsPerRecruit` | 8 | 1–100 | Every N kills → one extra soldier (0 disables the automatic recruit). |

### Camera Framing

| Field | Default | Range | Effect |
|---|---|---|---|
| `cameraPitch` | 42 | 10–80 | Degrees down. Higher = more top-down. |
| `cameraFieldOfView` | 45 | 20–80 | Vertical FOV. Higher = more world, smaller units. |
| `cameraHeight` | 15 | 4–40 | Camera Y. Height and pitch together decide how much ground is visible. |
| `cameraBottomPadding` | 2.5 | 0–10 | Extra ground kept visible behind the army before the bottom edge. |
| `cameraTrackMargin` | 1.12 | 1–1.5 | Extra width around the outer lanes at the spawn line. |
| `cameraLateralFollow` | 0.32 | 0–1 | 0 = static camera, 1 = camera X locked to the army X. |
| `cameraFollowSmoothTime` | 0.22 | 0.02–1 | Damping of the lateral follow. |
| `shakeDecay` | 7 | 1–30 | Shake units lost per second. |
| `maxShakeOffset` | 0.35 | 0–1 | Max world-space shake offset. |

### Performance

| Field | Default | Range | Effect |
|---|---|---|---|
| `targetFrameRate` | 60 | 30–120 | Applied with `Application.targetFrameRate`; vSync is forced off. **R** |
| `maxBulletsInFlight` | 220 | 20–600 | Hard pool cap. When reached, soldiers simply skip the shot. **R** |

### Colors

`playerColor #2973F2` · `playerAccentColor #9ED1FF` · `enemyColor #E8303B` · `bulletColor #FFDE4D` · `groundColor #171C2B` · `laneColor #262F47` · `skyColor #090B14` · `railColor #38476B`

Used by `GameSettings` → materials (generator), soldier tints, HUD accents, camera background. Change them here and re-run generator step 1 to rebuild materials.

> `GameSettings.OnValidate()` clamps everything into range and clears the cached enemy layer mask, so editing in the inspector can never produce an invalid state.

---

## 2. `GameManager` (on `Systems`)

| Field | Default | Effect |
|---|---|---|
| `settings` | *required* | The `GameSettings` asset. If empty the manager logs an error and disables itself. |
| `army` | auto | `ArmyController` to inject. |
| `spawner` | auto | `EnemySpawner` to inject. |
| `bulletPool` | auto | `BulletPool` (initialised first). |
| `vfxPool` | auto | `VfxPool` (initialised before the bullet pool). |
| `enemies` | auto | `EnemyRegistry` (created on the fly if missing). |
| `cameraRig` | auto | Receives `Shake(intensity)` calls. |
| `inputSource` | auto | `SwipeInput`, handed to the army as `ILaneInputSource`. |
| `hud` | auto | `HudController`, bound in `Start()`. |
| `gameOverPanel` | auto | `GameOverPanel`, bound in `Start()`. |
| `gameOverSlowMotion` | 0.25 | `Time.timeScale` during the game-over beat. |
| `gameOverSlowMotionDuration` | 0.55 | Real-time seconds before the freeze + panel (`WaitForSecondsRealtime`, so it works at any timeScale). |

Execution order is `-100`, so `Awake()` (injection) runs before every other system's `Awake()`/`Start()`.
All "auto" fields are resolved with `FindFirstObjectByType<T>(FindObjectsInactive.Include)` when left empty — explicit references always win.

---

## 3. `ArmyController` (on `Army`)

| Field | Default | Effect |
|---|---|---|
| `soldierPrefab` | `Soldier.prefab` | **Required.** Instantiated `maxSoldiers + 8` times at most. |
| `formationRoot` | `Army/Formation` | Parent of living soldiers (local positions from `ArmyFormation`). |
| `poolRoot` | `Army/Pool` | Parent of sleeping soldiers. |

Runtime API (useful from your own scripts):
`SoldierCount`, `TargetLane`, `WorldX`, `Recruit(int)`, `ApplyCasualties(int)`, `event ArmyCountChanged(int, int)`.

---

## 4. `SoldierUnit` (on `Soldier` prefab)

| Field | Default | Effect |
|---|---|---|
| `muzzle` | auto `Visual/Muzzle` | Bullet spawn point. |
| `bodyRoot` | auto first child | Receives the spawn pop and the idle bob (its `localScale`/`localPosition` are animated, so keep the meshes under it). |
| `muzzleFlash` | auto `Visual/Flash` | Renderer enabled for 45 ms per shot. |

Behaviour constants (edit in code if you want to change the feel): pop duration 0.22 s, muzzle flash 0.045 s, bob amplitude 0.02 m at 7 rad/s, aim lean clamped to ±22°.

---

## 5. `BulletPool` / `Bullet`

| Component | Field | Default | Effect |
|---|---|---|---|
| `BulletPool` | `bulletPrefab` | `Bullet.prefab` | **Required**, otherwise soldiers cannot shoot (error logged). |
| | `poolRoot` | `BulletPool/Bullets` | Parent of pooled bullets. |
| | `prewarm` | 96 | Bullets created before the first shot (avoids a hitch at start). |
| `Bullet` | `bodyRenderer` | auto child | Renderer tinted with `bulletColor`. |

The pool's hard cap is `GameSettings.maxBulletsInFlight`. `ActiveCount` / `CreatedCount` are exposed for a debug HUD.

---

## 6. `VfxPool` / `Spark`

| Component | Field | Default | Effect |
|---|---|---|---|
| `VfxPool` | `sparkPrefab` | `Spark.prefab` | If empty, VFX are disabled with a warning (game still runs). |
| | `poolRoot` | `VfxPool/Sparks` | |
| | `prewarm` | 64 | |
| | `maxSize` | 220 | Hard cap; effects beyond it are silently skipped. |
| `Spark` | `bodyRenderer` | auto child | Tinted per effect. |

API: `PlayImpact(position, color, scale)` (1 spark) and `PlayBurst(position, color, count, scale)` (radial).

---

## 7. `Health` (on `Enemy` prefab)

| Field | Default | Effect |
|---|---|---|
| `maxHealth` | 3 | Overwritten at spawn by `EnemyType.maxHealth`. |
| `tintRenderers` | `[Body]` | Renderers recoloured via a shared `MaterialPropertyBlock` (no material instances). |
| `hitFlashColor` | white | Colour applied for `flashDuration` on hit. |
| `flashDuration` | 0.07 | Seconds. |

Events: `Damaged(Health, float)`, `Died(Health)`. Interface: `IDamageable` (`IsAlive`, `TakeDamage(amount, hitPoint)`).
`SetBaseColor(Color)` sets the resting colour (the enemy calls it with `EnemyType.color`); `Kill()` forces death.

---

## 8. `Enemy` (on `Enemy` prefab)

| Field | Default | Effect |
|---|---|---|
| `health` | auto `Health` on this object | Damage receiver. |
| `bodyRoot` | auto `Visual` | Idle bob + charge lean. |
| `healthBar` | `HealthBar` component | Bound to `health` at spawn. |

Runtime API: `IsAlive`, `AimPoint` (bullets aim here), `Type`, `Spawn(type, context, owner, position)`.

---

## 9. `EnemySpawner`

| Field | Default | Effect |
|---|---|---|
| `enemyPrefab` | `Enemy.prefab` | **Required.** |
| `enemyTypes` | Grunt, Runner, Brute | Weighted pool of archetypes. Empty entries are ignored. |
| `poolRoot` | `EnemySpawner/Enemies` | |
| `prewarm` | 14 | Enemies instantiated up front. |

Exposed: `SpawnedCount`, `Difficulty` (0–1).

---

## 10. `EnemyType` (ScriptableObject)

| Field | Grunt | Runner | Brute | Range | Effect |
|---|---|---|---|---|---|
| `displayName` | Grunt | Runner | Brute | string | Debug/editor label. |
| `color` | `#E8303B` | `#FF8F29` | `#B81F80` | colour | Body tint at spawn. |
| `maxHealth` | 5 | 3 | 16 | 1–60 | Bullets needed (at 1 damage). |
| `moveSpeed` | 2.4 | 4.1 | 1.7 | 0.5–12 | m/s before `enemySpeedMultiplier`. |
| `scale` | 1 | 0.78 | 1.45 | 0.4–3 | Spawn scale multiplier (collider scales with it). |
| `armyDamage` | 1 | 1 | 2 | 1–20 | Soldiers lost when it reaches the line. |
| `scoreValue` | 10 | 15 | 30 | 0–500 | Score per kill. |
| `recruitValue` | 1 | 1 | 1 | 0–10 | Extra soldiers granted on kill. |
| `spawnWeight` | 1 | 0.7 | 0.45 | 0.01–20 | Relative spawn chance. |
| `minDifficulty` | 0 | 0.18 | 0.4 | 0–1 | Difficulty gate (0 = from the start, 1 = after the full ramp). |

Adding an archetype: `Assets ▸ Create ▸ NUMBER RUSH 3D ▸ Enemy Type` → add it to `EnemySpawner.enemyTypes`.

---

## 11. `WorldHealthBar`

| Field | Default | Effect |
|---|---|---|
| `fillPivot` | `FillPivot` | Left-edge pivot scaled on X by health %. |
| `fillRenderer` | `Fill` | Tinted from `lowHealthColor` to `highHealthColor`. |
| `lowHealthColor` | `(0.95, 0.25, 0.20)` | Colour at 0 %. |
| `highHealthColor` | `(0.35, 0.95, 0.42)` | Colour at 100 %. |
| `billboardToCamera` | on | Copies `Camera.main` rotation every frame. |

---

## 12. `SwipeInput`

| Field | Default | Effect |
|---|---|---|
| `minSwipeScreenFraction` | 0.05 | Min horizontal travel as a fraction of screen width (5 % of 1080p ≈ 96 px, floor 24 px). |
| `maxSwipeSeconds` | 0.45 | A swipe must complete within this time. |
| `stepCooldown` | 0.09 | Minimum seconds between two accepted lane changes. |
| `continuousDrag` | off | Off = only a completed swipe counts (recommended). On = holding and dragging emits a step every time the threshold is crossed. |
| `keyboardFallback` | on | A/D and ←/→ in the editor/desktop. |

---

## 13. `CameraRig`

| Field | Default | Effect |
|---|---|---|
| `targetCamera` | auto (own Camera) | Camera driven by the rig. |

Everything else comes from `GameSettings` (pitch, FOV, height, margins, follow, shake).

---

## 14. `HudController`

| Field | Default | Effect |
|---|---|---|
| `armyLabel` | ArmyLabel | Big soldier count (0–60). |
| `scoreLabel` | ScoreLabel | Current score, `N0` formatted. |
| `bestLabel` | BestLabel | `BEST 1234`. |
| `waveLabel` | WaveLabel | `WAVE n` derived from `Difficulty01(RunTime)`. |
| `recruitFill` | BarFill | RectTransform whose `anchorMax.x` shows progress to the next recruit. |
| `recruitLabel` | RecruitLabel | `n KILLS TO +1`. |

The HUD only subscribes to `GameManager` events (`ScoreChanged`, `ArmyChanged`, `StateChanged`) — it never polls gameplay objects.

**Switching to TextMeshPro:** change the field types from `Text` to `TMPro.TMP_Text` and replace the generator's `CreateText` body with `AddComponent<TextMeshProUGUI>()`. Nothing else in the HUD changes.

---

## 15. `GameOverPanel` (component on `UI Canvas`)

| Field | Default | Effect |
|---|---|---|
| `panelRoot` | `SafeArea/GameOverPanel` | GameObject shown/hidden (starts inactive). |
| `titleLabel` | Title | `ARMY WIPED OUT` or `NEW BEST!`. |
| `scoreLabel` | ScoreValue | Final score. |
| `bestLabel` | BestValue | Best score. |
| `hintLabel` | Hint | `TAP RETRY TO PLAY AGAIN`. |
| `retryButton` | RetryButton | Calls `GameManager.Restart()`. |
| `quitButton` | QuitButton | Calls `GameManager.QuitApplication()`. |
| `gameOverTitle` | `ARMY WIPED OUT` | Copy. |
| `newBestTitle` | `NEW BEST!` | Copy shown when `score >= best`. |

---

## 16. Wiring matrix (who references whom)

| Component | References |
|---|---|
| `GameManager` | `GameSettings`, `ArmyController`, `EnemySpawner`, `BulletPool`, `VfxPool`, `EnemyRegistry`, `CameraRig`, `SwipeInput`, `HudController`, `GameOverPanel` |
| `ArmyController` | `SoldierUnit` prefab, `Formation`, `Pool` *(settings + context injected)* |
| `SoldierUnit` | `muzzle`, `bodyRoot`, `muzzleFlash` |
| `BulletPool` | `Bullet` prefab, `Bullets` root |
| `Bullet` | `bodyRenderer` |
| `VfxPool` | `Spark` prefab, `Sparks` root |
| `EnemySpawner` | `Enemy` prefab, `EnemyType[]`, `Enemies` root |
| `Enemy` | `Health`, `bodyRoot`, `WorldHealthBar` |
| `WorldHealthBar` | `fillPivot`, `fillRenderer` |
| `HudController` | 4 × `Text` (army/score/best/wave), `recruitFill`, `recruitLabel`, plus the runner widgets `coinsLabel`, `progressFill`, `toastLabel` + `toastGroup`, `pauseButton` (auto-created when empty) |
| `GameOverPanel` | 1 × `GameObject`, 4 × `Text`, 2 × `Button` |
| `CameraRig` | `Camera` |
| `TrackManager` | `legacyTrack` (scene root `Environment`), `segmentRoot`, streaming distances |
| `GateManager` | `gateRoot`, `rowSpacing`, `clusterSpacing`, `recycleDistance`, `prewarm` |
| `LevelManager` | `game`, `catalog`, `track`, `gates`, `spawner`, `hud`, `legacyGameOverPanel`, `levelSelect`, `completePanel`, `failedPanel`, `pausePanel` |
| `LevelSelectScreen` | `catalog`, optional `root` |
| `LevelCompletePanel` / `LevelFailedPanel` / `PausePanel` | optional `root` (contents are built at runtime) |

`CameraFraming`, `ArmyFormation`, `ObjectPool<T>`, `PointerInputBridge` and the interfaces have no inspector surface — they are pure code.

---

---

## 17. Runner fields added after the shooter prototype

Everything below was **appended** to the existing components; no older field changed meaning.
Defaults are shown, and all of them live in `GameSettings` unless stated otherwise.

### GameSettings — Runner

| Field | Default | Effect |
|---|---|---|
| `runnerStartSpeed` | 7 | Forward speed in m/s when a run starts |
| `runnerEndSpeed` | 0 | Speed reached over the level (0 = keep the start speed) |

### GameSettings — Infinite track

| Field | Default | Effect |
|---|---|---|
| `trackSegmentLength` | 20 | Length of one pooled slice |
| `trackSegmentCount` | 8 | Slices alive at once (× 20 m = 160 m of track) |
| `trackRecycleBehind` | 30 | Slice recycled once this far behind the army (metres) |
| `trackDecorJitter` | 0.4 | Random offset on side decoration |

### GameSettings — Runner enemies / number gates

| Field | Default | Effect |
|---|---|---|
| `enemySpawnAhead` / `enemyDespawnBehind` | 30 / 12 | Spawn point and cleanup distance relative to the army |
| `ramDistanceAhead` | 4 | Enemy rams the army at `armyZ + 4` |
| `gateLookAhead` / `gateSpacing` / `gateFirstDistance` | 26 / 22 / 18 | Gate streaming distances (metres) |
| `gateExtraGates` | 1 | Extra gate rows per cluster |
| `gateNegativeChance` | 0.55 | Chance a row offers a red gate |
| `gateToastDuration` | 1.4 | Seconds the gate result toast stays on screen |

### GameSettings — Levels & economy

| Field | Default | Effect |
|---|---|---|
| `coinsPerKill` | 1 | Wallet coins per kill |
| `firstLevel` | 1 | Level the campaign starts on |
| `levelEndSlowMotionDuration` | 0.55 | Slow motion beat at the finish |

### TrackManager / GateManager (inspector)

| Field | Default | Effect |
|---|---|---|
| `legacyTrack` | — | Scene root hidden at runtime (the old ground / lanes / rails) |
| `lookAhead` / `recycleBehind` | 70 / 30 | Track streaming window |
| `gateRoot` | `Systems/Gates` | Parent of pooled gates |
| `rowSpacing` / `clusterSpacing` / `recycleDistance` | 22 / 8 / 26 | Gate streaming window |
| `prewarm` | 8 | Gates created up front |

### LevelManager

| Field | Default | Effect |
|---|---|---|
| `showLevelSelectOnBoot` | on | Open the level grid when the scene starts without a level request |
| `buildMissingScreens` | on | Create the campaign screens at runtime if the scene has none |
| `coinBonusPerStar` | 10 | Extra coins per earned star |

### EnemyType (appended)

| Field | Default | Effect |
|---|---|---|
| `damageResistance` | 1 | Incoming damage multiplier (0.45 = armoured) |
| `formationDepth` | 0 | Extra metres in front of the ram line where this enemy stops |
| `isBoss` | off | Boss flag (camera rumble, boss levels, never randomly spawned) |

Next: **[05 · Gameplay & Tuning](05-GAMEPLAY-AND-TUNING.md)**
