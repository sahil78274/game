# NUMBER RUSH 3D

A complete, playable **Unity 6.3 LTS** prototype of a hyper-casual **3D runner army shooter** for mobile landscape, written in clean modular C#.

The blue army runs forward on its own along an endless track, shoots red enemies automatically, and you swipe left/right to steer it through green/red **number gates** (+10, ×2, −5, ÷2 …) that grow or shrink the crowd. Every 8 kills recruits another soldier; every enemy that reaches the army costs men. Campaign levels end at a finish distance (with a boss from level 5 onward); Free Run is still the endless mode.

```
   swipe  <  [ ARMY 42 ]  >   ->  ->  ->   [ +10 | ÷2 ]   ->  ->  ->   [ red wave ]
```

---

## What is in the box

| Area | Status |
|---|---|
| Unity 6.3 LTS compatible project (URP, Universal 3D) | ✅ |
| Landscape mobile gameplay, 5 lanes, auto-run ≈7 m/s | ✅ |
| Endless pooled track (8 slices, recycled — no gaps, no unbounded instantiation) | ✅ |
| Swipe controller (touch + mouse + keyboard fallback) | ✅ |
| Automatic shooting, sticky target acquisition, spread | ✅ |
| Bullet pooling (zero allocation, hard frame cap) | ✅ |
| Number gates: +5/+10/+20, ×2/×3, −5/−10, ÷2 — 3D green/red labels, fire once per crossing | ✅ |
| Enemy spawning ahead of the player, 5 archetypes (grunt, fast, heavy, armoured, boss) | ✅ |
| Campaign: 100 data-driven levels, level select, stars, coins, pause, complete/failed screens | ✅ |
| Non-destructive **7. Upgrade Existing Scene** menu for scenes built before the runner upgrade | ✅ |
| Enemy health, damage, hit flash, world health bars | ✅ |
| Game over, slow-motion beat, best score, instant restart | ✅ |
| Clean modular architecture (composition root + interface injection) | ✅ |
| **One-click** scene / prefab / material generator (editor menu) | ✅ |
| Android build preset + APK/AAB build menu | ✅ |
| Full written setup: hierarchy, prefabs, inspector, scene, build | ✅ |

**50 C# files** — 45 runtime scripts + 5 editor tools. No third-party assets, no art dependencies (everything is built from primitives and generated materials).

---

## 60-second quick start

1. **Create the project** — Unity Hub → New Project → **Universal 3D** template → Unity **6000.3.x (6.3 LTS)** → name it `NumberRush3D`.
2. **Copy** the `Assets/NumberRush3D` folder from this repository into your new project's `Assets/` folder.
3. In Unity, run **`Tools ▸ NUMBER RUSH 3D ▸ 1. Build Materials, Prefabs and Scene`**.
4. Press **Play**. Swipe / drag with the mouse, or press **A / D**.
5. Run **`Tools ▸ NUMBER RUSH 3D ▸ 2. Configure Android Player Settings`**, then **`3. Build Android APK`**.

That is the whole setup — step 3 generates the scene, prefabs, materials, settings assets, 100 level assets and wires every inspector reference for you.

> **Already have a scene from an older version?** Do **not** rebuild it. Open it and run
> **`Tools ▸ NUMBER RUSH 3D ▸ 7. Upgrade Existing Scene`** — it adds the runner systems
> (track, gates, levels, campaign UI) and fills only the empty references.
> See **[Documentation/09-RUNNER-UPGRADE.md](Documentation/09-RUNNER-UPGRADE.md)**.

> Prefer to build it by hand, or need to understand what the generator does?
> See **[Documentation/02-SCENE-SETUP.md](Documentation/02-SCENE-SETUP.md)** (manual hierarchy) and
> **[Documentation/03-PREFABS.md](Documentation/03-PREFABS.md)** (manual prefabs).

---

## Project layout

```
Assets/
└── NumberRush3D/
    ├── Scripts/
    │   ├── Core/        GameManager, GameSettings, GameState, IBattleContext,
    │   │                IDamageable, ObjectPool
    │   ├── Controls/    SwipeInput, ILaneInputSource, PointerInputBridge
    │   ├── Army/        ArmyController, ArmyFormation, SoldierUnit
    │   ├── Combat/      Bullet, BulletPool, Health, VfxPool, Spark
    │   ├── Enemies/     Enemy, EnemySpawner, EnemyRegistry, EnemyType, WorldHealthBar
    │   ├── Track/       TrackManager, TrackSegment   (pooled endless track)
    │   ├── Gates/       GateManager, NumberGate, GateRowDefinition
    │   ├── Levels/      LevelManager, LevelDefinition, LevelCatalog,
    │   │                LevelProgressStore, LevelLaunch
    │   ├── View/        CameraRig, CameraFraming  (aspect-aware framing + shake)
    │   └── UI/          HudController, GameOverPanel, SafeArea, UiFactory,
    │                    LevelSelectScreen, LevelCompletePanel,
    │                    LevelFailedPanel, PausePanel
    ├── Editor/          NumberRushBuilder (+.Scene, +.Ui, +.Levels, +.Upgrade),
    │                    NumberRushBuildTools
    ├── Materials/       generated .mat assets
    ├── Prefabs/         generated Bullet / Spark / Soldier / Enemy prefabs
    ├── Settings/        GameSettings, EnemyType_* assets, LevelCatalog + Levels/Level_001..100
    └── Scenes/          generated NumberRush3D.unity
Documentation/           the manual (9 chapters, see below)
Verification/            python tooling used to syntax-check the C# sources
Packages/manifest.json   URP 17.3.0 + uGUI (Unity 6.3)
ProjectSettings/         ProjectVersion.txt (6000.3.0f1)
```

`Materials/`, `Prefabs/`, `Settings/` and `Scenes/` are **generated** the first time you run the build menu, which is why they are not committed with hand-written GUIDs.

---

## How a run plays

| Beat | What happens |
|---|---|
| Start | 10 soldiers spawn in a centred block, army starts in the middle lane |
| Loop | Every `spawnStartInterval → spawnMinInterval` (1.35 s → 0.34 s over 95 s) a burst of 1-3 enemies spawns on random lanes |
| Shooting | Each soldier acquires the closest living enemy in front (sticky targeting) and fires every `1/shotsPerSecond` |
| Kills | Score + type score, every 8th kill recruits one extra soldier (max 60) |
| Running | The army advances at `runnerStartSpeed` (7 m/s) with no input; the track and gates stream ahead |
| Gates | Green/red gate halves apply +/× or −/÷ to the **actual** count (never below 1, capped at `maxSoldiers`) |
| Threat | An enemy that reaches `armyZ + ramDistanceAhead` kills `armyDamage` soldiers and destroys itself |
| Finish | Campaign levels complete at `trackLength` (boss levels need the boss dead) → stars, coins, next level |
| Death | 0 soldiers → state `GameOver` → 0.25× slow motion for 0.55 s → freeze → retry / failed panel |
| Retry | Scene reload (no state to reset), best score persisted in `PlayerPrefs` |

Controls: **swipe / drag horizontally**, **A / D or ← →** in the editor, **R / Space** to retry.

---

## Documentation

| Chapter | Contents |
|---|---|
| [01 · Project Setup](Documentation/01-PROJECT-SETUP.md) | Unity 6.3 LTS install, URP configuration, packages, input backend, player settings, first run |
| [02 · Scene Setup](Documentation/02-SCENE-SETUP.md) | Generated + manual hierarchy, every object's transform/components, camera math, lighting, build settings |
| [03 · Prefabs](Documentation/03-PREFABS.md) | Full setup of the Bullet, Spark, Soldier and Enemy prefabs (hierarchy, components, layer, physics) |
| [04 · Inspector Configuration](Documentation/04-INSPECTOR-CONFIGURATION.md) | Every serialized field of every script, with defaults, ranges and effect |
| [05 · Gameplay & Tuning](Documentation/05-GAMEPLAY-AND-TUNING.md) | Difficulty math, DPS budget, ready-made presets, balancing recipe, extension points |
| [06 · Android Build](Documentation/06-ANDROID-BUILD.md) | SDK/NDK, keystore, APK vs AAB, adb install, device profiling, store checklist |
| [07 · Architecture](Documentation/07-ARCHITECTURE.md) | Module map, dependency rules, per-frame data flow, pooling strategy, how to extend |
| [08 · Troubleshooting](Documentation/08-TROUBLESHOOTING.md) | Magenta materials, no bullets, no swipes, missing layer, silent UI, performance, build errors |
| [09 · Runner Upgrade](Documentation/09-RUNNER-UPGRADE.md) | **Upgrading an existing scene (menu step 7)**, endless track, number gates, campaign levels, levels/stars persistence, verification checklist |

---

## Design decisions worth knowing

* **One composition root.** `GameManager` creates nothing at runtime except pools; it injects `IBattleContext` into the army, the spawner and VFX. Gameplay classes never call `FindObjectOfType` or use singletons.
* **Everything is pooled.** Bullets (cap 220), enemies (cap 34), sparks (cap 220) and soldiers (cap 68). A five minute run allocates nothing after warm-up, so no GC spikes on a mid-range phone.
* **Bullets are physics-free.** A `SphereCastNonAlloc` sweep along each frame's travel distance replaces Rigidbody/trigger bullets: no tunnelling, no trigger matrix, one shared hit buffer.
* **Only one side of a collision needs a collider.** Only enemies have colliders (kinematic Rigidbody + BoxCollider on the `Enemy` layer). Bullets, soldiers and VFX have none.
* **Aspect-aware camera.** `CameraFraming` solves the camera Z from the horizontal and vertical frustum constraints, so 16:9, 19.5:9 and 4:3 all frame the track correctly with no manual tuning.
* **Legacy UI Text on purpose.** The generator needs no "Import TMP Essentials" step, so the project builds and runs immediately. Swapping to TextMeshPro is a 3-line change (see chapter 04).
* **Data-driven enemies.** `EnemyType` ScriptableObjects hold health, speed, scale, colour, score, recruit value, spawn weight and the difficulty at which the type unlocks.

---

## Verified

The C# sources in this repository are checked with two scripts in `Verification/`:

```bash
python3 Verification/check_syntax.py       # tree-sitter C# parse of every file (0 errors)
python3 Verification/check_consistency.py  # serialized field names exist / injected members resolve
```

Both pass on all 50 files. Scene and prefab assets are generated by the editor tool at import time (Unity writes the `.meta` GUIDs), which is why no fragile hand-written YAML ships in this repository.
