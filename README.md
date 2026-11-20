# NUMBER RUSH 3D

A complete, playable **Unity 6.3 LTS** prototype of a hyper-casual **3D army shooter** for mobile landscape, written in clean modular C#.

Blue soldiers stand at the bottom of the track, red enemies march in from the horizon, the army shoots automatically and you swipe left/right to dodge the incoming wave. Every 8 kills recruits one more soldier; every enemy that reaches the line costs you men. When the last soldier falls, the run is over.

```
   swipe  <  [ ARMY 42 ]  >      auto-fire  ->  ->  ->      [ red wave ]
```

---

## What is in the box

| Area | Status |
|---|---|
| Unity 6.3 LTS compatible project (URP, Universal 3D) | ✅ |
| Landscape mobile gameplay, 5 lanes, ~20 m of track | ✅ |
| Swipe controller (touch + mouse + keyboard fallback) | ✅ |
| Automatic shooting, sticky target acquisition, spread | ✅ |
| Bullet pooling (zero allocation, hard frame cap) | ✅ |
| Enemy spawning with difficulty ramp, 3 enemy archetypes | ✅ |
| Enemy health, damage, hit flash, world health bars | ✅ |
| Game over, slow-motion beat, best score, instant restart | ✅ |
| Clean modular architecture (composition root + interface injection) | ✅ |
| **One-click** scene / prefab / material generator (editor menu) | ✅ |
| Android build preset + APK/AAB build menu | ✅ |
| Full written setup: hierarchy, prefabs, inspector, scene, build | ✅ |

**31 C# files** — 27 runtime scripts + 4 editor tools. No third-party assets, no art dependencies (everything is built from primitives and generated materials).

---

## 60-second quick start

1. **Create the project** — Unity Hub → New Project → **Universal 3D** template → Unity **6000.3.x (6.3 LTS)** → name it `NumberRush3D`.
2. **Copy** the `Assets/NumberRush3D` folder from this repository into your new project's `Assets/` folder.
3. In Unity, run **`Tools ▸ NUMBER RUSH 3D ▸ 1. Build Materials, Prefabs and Scene`**.
4. Press **Play**. Swipe / drag with the mouse, or press **A / D**.
5. Run **`Tools ▸ NUMBER RUSH 3D ▸ 2. Configure Android Player Settings`**, then **`3. Build Android APK`**.

That is the whole setup — step 3 generates the scene, prefabs, materials, settings assets and wires every inspector reference for you.

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
    │   ├── View/        CameraRig, CameraFraming  (aspect-aware framing + shake)
    │   └── UI/          HudController, GameOverPanel, SafeArea
    ├── Editor/          NumberRushBuilder (+.Scene, +.Ui), NumberRushBuildTools
    ├── Materials/       generated .mat assets
    ├── Prefabs/         generated Bullet / Spark / Soldier / Enemy prefabs
    ├── Settings/        GameSettings + EnemyType_Grunt / _Runner / _Brute assets
    └── Scenes/          generated NumberRush3D.unity
Documentation/           the manual (8 chapters, see below)
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
| Threat | An enemy that reaches `defenseLineZ` kills `armyDamage` soldiers and destroys itself |
| Death | 0 soldiers → state `GameOver` → 0.25× slow motion for 0.55 s → freeze → retry panel |
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

Both pass on all 31 files. Scene and prefab assets are generated by the editor tool at import time (Unity writes the `.meta` GUIDs), which is why no fragile hand-written YAML ships in this repository.
