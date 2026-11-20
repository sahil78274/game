# 05 · Gameplay & Tuning

## 1. The loop in numbers

```
START                 ARMY 10 soldiers, lane 2 (centre), score 0
  │
  ├─ enemies spawn every lerp(1.35 s → 0.34 s) over 95 s, 1 → 3 per burst
  │
  ├─ each soldier: closest live enemy in front (sticky) → fire every 1/0.9 s
  │
  ├─ kill  → +score (10/15/30) , +1 soldier every 8th kill (brutes also give +1)
  │
  ├─ enemy reaches z = -2.5 → army loses 1 (grunt/runner) or 2 (brute) soldiers
  │
  └─ 0 soldiers → 0.25× slow-mo (0.55 s) → freeze → panel → RETRY (scene reload)
```

Score is the only progression metric; the best score is stored in `PlayerPrefs["NumberRush3D.BestScore"]`.

---

## 2. Difficulty model

```csharp
difficulty      d(t) = Clamp01(t / difficultyRampSeconds)          // 0 → 1 over 95 s
spawn interval  I(t) = Lerp(spawnStartInterval, spawnMinInterval, d)
burst           B(t) = Clamp(Round(Lerp(spawnBurstMin, spawnBurstMax, d)), min, max)
enemy speed     v    = type.moveSpeed * enemySpeedMultiplier
```

`EnemyType.minDifficulty` gates archetypes:

| Type | Unlocks at | Weight | HP | Speed | Damage |
|---|---|---|---|---|---|
| Grunt | d = 0.00 (t = 0 s) | 1.00 | 5 | 2.4 m/s | 1 |
| Runner | d = 0.18 (t ≈ 17 s) | 0.70 | 3 | 4.1 m/s | 1 |
| Brute | d = 0.40 (t ≈ 38 s) | 0.45 | 16 | 1.7 m/s | 2 |

Weighted average HP once everything is unlocked:
`(1·5 + 0.7·3 + 0.45·16) / 2.15 = 6.65 HP`

---

## 3. The one balance equation

```
army DPS        = soldiers × shotsPerSecond × bulletDamage
required DPS(t) = (1 / I(t)) × averageEnemyHP
hold the line   ⇔  armyDPS ≥ requiredDPS
```

With the shipped defaults:

| Time | d | Spawn rate | Avg HP | Required DPS | Soldiers needed | Typical army |
|---|---|---|---|---|---|---|
| 0 s | 0.00 | 0.74 /s | 5.0 | **3.7** | 5 | 10 → 9 DPS ✅ |
| 30 s | 0.32 | 0.97 /s | 4.2 | **4.1** | 5 | ~13 → 12 DPS ✅ |
| 60 s | 0.63 | 1.40 /s | 6.7 | **9.3** | 11 | ~17 → 15 DPS ✅ |
| 95 s | 1.00 | 2.94 /s | 6.7 | **19.6** | 22 | ~22-26 → 20-23 DPS ⚠️ |

So the ramp and the growth curve cross at roughly the 90–100 second mark: that is where runs end for an average player, and where a skilled player has to stop losing soldiers.

**Second constraint — travel time.** An enemy needs `(spawnZ − defenseLineZ) / speed = 17.5 / 2.4 ≈ 7.3 s` to reach the line. If the army kills slower than the spawn rate, enemies queue up until `maxConcurrentEnemies` (26) is hit and then rams start happening every second. That is the death spiral — and it is intentional.

**Third constraint — the pool caps.** `maxBulletsInFlight` (220) and `maxConcurrentEnemies` (26) are the frame-time governors, not difficulty dials. With 60 soldiers at 0.9 shots/s and 1.6 s of flight time, at most ~86 bullets are alive, so the cap is never hit in normal play; it only protects you if you crank `shotsPerSecond` up to 5.

---

## 4. Ready-made presets

Paste these into `GameSettings` (all fields exist, see chapter 04 §1).

### Chill / onboarding

```
startingSoldiers      12      shotsPerSecond     1.10
killsPerRecruit        6      bulletDamage       1
spawnStartInterval   1.80      spawnMinInterval   0.55
spawnBurstMax          2      difficultyRamp     120
maxConcurrentEnemies  18      enemySpeedMultiplier 0.9
```

### Default (as shipped)

```
startingSoldiers      10      shotsPerSecond     0.90
killsPerRecruit        8      spawnStartInterval 1.35
spawnMinInterval    0.34      difficultyRamp       95
spawnBurstMax          3      maxConcurrentEnemies 26
```

### Brutal

```
startingSoldiers       8      shotsPerSecond     0.70
killsPerRecruit       12      spawnStartInterval 1.00
spawnMinInterval    0.22      difficultyRamp       70
spawnBurstMax          4      maxConcurrentEnemies 34
enemySpeedMultiplier 1.15
```

### Feel presets (independent of difficulty)

| Feel | Fields |
|---|---|
| Snappy / arcade | `laneMoveSmoothTime 0.08`, `cameraFollowSmoothTime 0.10`, `aimSpreadDegrees 2.2`, `shakeDecay 12` |
| Floaty / cinematic | `laneMoveSmoothTime 0.26`, `cameraFollowSmoothTime 0.40`, `cameraLateralFollow 0.5`, `shakeDecay 4` |
| Top-down readability | `cameraPitch 58`, `cameraHeight 18`, `cameraFieldOfView 40` |
| Cinematic low angle | `cameraPitch 28`, `cameraHeight 9`, `cameraFieldOfView 50` |

---

## 5. Recipes

**The game is too hard**
1. `spawnMinInterval` ↑ (0.34 → 0.5) — biggest lever.
2. `difficultyRampSeconds` ↑ (95 → 150) — slows the whole curve.
3. `startingSoldiers` ↑, or `killsPerRecruit` ↓ (8 → 5).
4. `enemySpeedMultiplier` ↓ (1 → 0.85) — buys reaction time.

**The game is too easy**
1. `maxConcurrentEnemies` ↑ (26 → 36) — more simultaneous pressure.
2. Add a second brute-like archetype with `minDifficulty 0.7` and high weight (see §7).
3. `armyDamage` on the grunt 1 → 2 — one mistake costs more.
4. `killsPerRecruit` ↑ (8 → 15) — slows the recovery.

**The army feels weak**
`shotsPerSecond` 0.9 → 1.2, or `bulletDamage` 1 → 1.5. Prefer fire rate: it looks better (more tracers) and costs less per point of DPS.

**The enemies die too fast / no tension at long range**
Raise `targetSearchRange` ↓ (45 → 26) so the army only starts shooting once enemies are closer, and raise `aimSpreadDegrees` (1.4 → 2.5).

**Runs end in the first 20 seconds**
Your `startingSoldiers` × `shotsPerSecond` is below 2× the initial required DPS. Either start with more soldiers or slow `spawnStartInterval`.

---

## 6. Reading the run in the profiler

* `EnemyRegistry.Count` should hover below `maxConcurrentEnemies`; if it sits at the cap, the player is losing.
* `BulletPool.ActiveCount` should stay well below `maxBulletsInFlight`.
* If the frame time spikes when the army grows, check the soldier count (60 soldiers = 120 renderers). Enable GPU instancing on the soldier materials (already on by default) or switch soldiers to a single mesh.

---

## 7. Extension points (clean seams, no refactor needed)

| Idea | Where to plug in |
|---|---|
| **Enemies that shoot back** | Add `EnemyShooter` with a timer that calls `context.Army.ApplyCasualties(1)` at range — the army already exposes it. |
| **Boss enemy** | New `EnemyType` with 120 HP, `scale 2.4`, `minDifficulty 0.85`; give the spawner a 2 % boss roll (a small branch in `PickType`) plus `armyDamage 6`. |
| **Power-ups (x2 fire rate, shield)** | Add a `PowerUp` component that multiplies `GameSettings.shotsPerSecond` temporarily — but prefer a `float damageMultiplier` on `IBattleContext` so tuning stays data-driven. |
| **Different enemy mesh per type** | Add `public GameObject prefabOverride` to `EnemyType` and use it in `EnemySpawner` (`Instantiate` per type instead of one pool) — pool per type, 3 small pools. |
| **Sound** | Add an `AudioDirector` with a pool of `AudioSource`s and call it from `SoldierUnit.Fire`, `Enemy.HandleDied`, `ArmyController.ApplyCasualties` and `GameOverPanel`. The seams are one-line calls. |
| **Combo / multiplier** | `GameManager.ReportKill` already centralises every kill; add a kill-streak timer there. |
| **Alternative input** | Implement `ILaneInputSource` (one method) and pass it to `ArmyController.Initialize` — drag-to-follow, tilt, on-screen buttons. |
| **Wave/boss cadence instead of the pure ramp** | Replace `EnemySpawner.Update`'s timer with a `WaveDefinition` list (`ScriptableObject[]`), keeping the existing pool and `SpawnOne`. |
| **Analytics** | Subscribe to `GameManager.RunEnded` (score, best) and `ArmyChanged`; no coupling required. |

---

Next: **[06 · Android Build](06-ANDROID-BUILD.md)**
