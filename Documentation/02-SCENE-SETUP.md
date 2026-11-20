# 02 · Scene Setup

The scene is called **`NumberRush3D`** and lives at `Assets/NumberRush3D/Scenes/NumberRush3D.unity`.
It is produced automatically by **`Tools ▸ NUMBER RUSH 3D ▸ 1. Build Materials, Prefabs & Scene`**; this chapter describes exactly what that produces, so you can rebuild it by hand or modify it confidently.

---

## 1. Final hierarchy

```
NumberRush3D
├── Main Camera                     Transform (0, 15, -17.66) rot (42, 0, 0)
│                                   Camera · AudioListener · CameraRig
├── Directional Light               rot (52, -34, 0)
│                                   Light (Directional, soft shadows, intensity 1.15)
├── Environment
│   ├── Ground                      (0, -0.5, 2.75)   scale (40, 1, 45.5)
│   ├── Lane_0 … Lane_4             (x = -5.6 … 5.6, 0.02, 2.75)  scale (2.41, 0.03, 45.5)
│   ├── Rail_Left                   (-7.45, 0.45, 2.75)  scale (0.7, 0.9, 45.5)
│   ├── Rail_Right                  ( 7.45, 0.45, 2.75)  scale (0.7, 0.9, 45.5)
│   ├── SpawnBackdrop               (0, 1.6, 25.5)       scale (40, 3.2, 0.8)
│   └── DefenseLine                 (0, 0.02, -2.5)      scale (14, 0.04, 0.3)
├── Army                            (0, 0, -8)           ArmyController
│   ├── Formation                   (0, 0, 0)            empty · soldiers live here
│   ├── Pool                        (0, 0, 0)            empty · sleeping soldiers
│   └── ArmyMarker                  (0, 0.02, 0.95)      scale (2.52, 0.03, 0.35)
├── EnemySpawner                    EnemySpawner
│   └── Enemies                     empty · sleeping enemies
├── BulletPool                      BulletPool
│   └── Bullets                     empty · sleeping bullets
├── VfxPool                         VfxPool
│   └── Sparks                      empty · sleeping sparks
├── Systems                         EnemyRegistry · SwipeInput · GameManager
├── UI Canvas                       Canvas (Screen Space - Overlay) · CanvasScaler · GraphicRaycaster
│   │                               GameOverPanel
│   └── SafeArea                    SafeArea
│       ├── HUD                     HudController
│       │   ├── ArmyGroup           ArmyCaption, ArmyLabel
│       │   ├── ScoreGroup          ScoreCaption, ScoreLabel
│       │   ├── RightGroup          BestLabel, WaveLabel
│       │   └── RecruitBar          BarBack ▸ BarFill, RecruitLabel
│       └── GameOverPanel           [inactive at start]
│           ├── Dim                 RawImage, full screen, black 78 %
│           └── Card                RawImage 1020 × 640
│               ├── Title           "ARMY WIPED OUT"
│               ├── ScoreCaption / ScoreValue / BestValue
│               ├── RetryButton     Button ▸ Label
│               ├── Hint
│               └── QuitButton      Button ▸ Label
└── EventSystem                     EventSystem + input module
```

> `GameOverPanel` (the **component**) sits on `UI Canvas` and stays active; the **GameObject** it shows/hides is `UI Canvas/SafeArea/GameOverPanel`, which starts inactive. That way the component can subscribe to `GameManager.RunEnded` before the panel is needed.

---

## 2. Manual scene construction (12 steps)

If you prefer not to run the generator, create an empty scene and follow these steps. The generator performs exactly this sequence.

### Step 1 — new scene + environment

1. `File ▸ New Scene ▸ Empty Scene`, save as `Assets/NumberRush3D/Scenes/NumberRush3D.unity`.
2. Create an empty `Environment` at the origin.
3. Add cubes (`GameObject ▸ 3D Object ▸ Cube`) with these transforms and **delete the colliders** from every one of them (they are decoration only, and a stray collider would eat bullets once the layer mask falls back to "everything"):

| Name | Position | Scale | Material |
|---|---|---|---|
| Ground | (0, -0.5, 2.75) | (40, 1, 45.5) | Ground |
| Lane_0 … Lane_4 | (x, 0.02, 2.75), x = -5.6 / -2.8 / 0 / 2.8 / 5.6 | (2.41, 0.03, 45.5) | Lane |
| Rail_Left | (-7.45, 0.45, 2.75) | (0.7, 0.9, 45.5) | Rail |
| Rail_Right | (7.45, 0.45, 2.75) | (0.7, 0.9, 45.5) | Rail |
| SpawnBackdrop | (0, 1.6, 25.5) | (40, 3.2, 0.8) | Rail |
| DefenseLine | (0, 0.02, -2.5) | (14, 0.04, 0.3) | Rail |

Shadow flags used by the generator: Ground / Lane = *cast off, receive on*; Rails = *cast on, receive on*; everything gameplay related keeps the defaults.

### Step 2 — camera

1. `GameObject ▸ Camera`, name it `Main Camera`, tag `MainCamera`.
2. Position **(0, 15, -17.66)**, rotation **(42, 0, 0)**.
3. Camera component: FOV **45**, Clear Flags **Solid Color**, Background `#090B14` (`GameSettings.skyColor`), Near 0.3, Far 220, HDR off, MSAA off (URP handles AA).
4. Add **`CameraRig`** and drag the Camera into its `targetCamera` field.

The exact Z is not critical: `CameraRig` re-solves it on Awake and whenever the aspect ratio changes (see §4).

### Step 3 — light

1. `GameObject ▸ Light ▸ Directional Light`, rotation **(52, -34, 0)**.
2. Intensity **1.15**, colour `#FFF7EB`, Mode Realtime, Shadows **Soft**, Shadow Strength 0.6, Bias 0.04.
3. `Window ▸ Rendering ▸ Lighting`: Ambient Source **Color**, colour `#4D5775` (linear ≈ (0.30, 0.34, 0.46)). Auto-generate lighting **off** (all objects are dynamic).

### Step 4 — Army

1. Empty `Army` at **(0, 0, -8)** → add **`ArmyController`**.
2. Two empty children: `Formation` and `Pool` (both at local 0).
3. Add a decorative `ArmyMarker` cube: local position (0, 0.02, 0.95), scale (2.52, 0.03, 0.35), material `Rail`, no collider.
4. In `ArmyController`: `Soldier Prefab` = `Assets/NumberRush3D/Prefabs/Soldier.prefab`, `Formation Root` = `Formation`, `Pool Root` = `Pool`.

### Step 5 — pools

1. `EnemySpawner` (empty) → **`EnemySpawner`** component, child `Enemies` assigned to `Pool Root`; `Enemy Prefab` = `Enemy.prefab`; `Enemy Types` = the three `EnemyType_*` assets; `Prewarm` = 14.
2. `BulletPool` (empty) → **`BulletPool`**, child `Bullets` assigned to `Pool Root`; `Bullet Prefab` = `Bullet.prefab`; `Prewarm` = 96.
3. `VfxPool` (empty) → **`VfxPool`**, child `Sparks` assigned to `Pool Root`; `Spark Prefab` = `Spark.prefab`; `Prewarm` = 64, `Max Size` = 220.

### Step 6 — systems

1. Empty `Systems` at the origin.
2. Add **`EnemyRegistry`**, **`SwipeInput`**, **`GameManager`** (all three on the same GameObject).
3. In `GameManager` assign:
   * `Settings` → `Assets/NumberRush3D/Settings/GameSettings.asset`
   * `Army` → the `Army` GameObject
   * `Spawner` → the `EnemySpawner`
   * `Bullet Pool` → the `BulletPool`
   * `Vfx Pool` → the `VfxPool`
   * `Enemies` → the `EnemyRegistry` on this object
   * `Camera Rig` → the `CameraRig` on Main Camera
   * `Input Source` → the `SwipeInput` on this object
   * `Hud` / `Game Over Panel` → the UI components created in step 7

   (All of them are auto-found if left empty, but explicit references are faster and safer.)

### Step 7 — UI

1. `GameObject ▸ UI ▸ Canvas`: Render Mode **Screen Space - Overlay**, Canvas Scaler **Scale With Screen Size**, reference **1920 × 1080**, Match **0.5**.
2. Add **`GameOverPanel`** to the Canvas GameObject itself.
3. Child `SafeArea` (stretched) + **`SafeArea`** component.
4. Child `HUD` (stretched) + **`HudController`**, containing:
   * `ArmyGroup` top-left at (56, -40): caption "ARMY" (30 px, white 55 %) and the number (104 px, `playerAccentColor`),
   * `ScoreGroup` top-centre at (0, -28): caption "SCORE", number 104 px white,
   * `RightGroup` top-right at (-56, -40): "BEST 0" (36 px) and "WAVE 1" (30 px, white 55 %),
   * `RecruitBar` bottom-centre at (0, 54): `BarBack` (620 × 16, colour `#141A29`) with a stretched `BarFill` child (`playerAccentColor`), and a 24 px label under it.
   Assign all five labels + the `BarFill` RectTransform + the label into `HudController`.
5. Child `GameOverPanel` (under SafeArea), **deactivate it**, then add:
   * `Dim` — RawImage stretched, colour `(0.01, 0.02, 0.05, 0.78)`,
   * `Card` — RawImage 1020 × 640, colour `#0F1426` 98 %,
   * title (62 px, `#FF6B5C`), "SCORE" caption, score number (120 px), best (38 px),
   * `RetryButton` — RawImage + Button, 480 × 112, `#2973F2`, label "RETRY" 44 px,
   * `Hint` — 24 px white 45 %,
   * `QuitButton` — RawImage + Button, 180 × 62 at the bottom-right, `#2E3347`, label "QUIT".

   Assign them into `GameOverPanel`: `Panel Root` = this inactive GameObject, plus the four labels and the two Buttons.
6. `GameObject ▸ UI ▸ Event System` (the generator adds it if missing and picks the input module that matches your input backend).

### Step 8 — layers and tags

Create a user layer **`Enemy`** (`Project Settings ▸ Tags and Layers`, first free slot from 8).
Assign it to the **Enemy prefab root** (the layer is inherited by children automatically).
No custom tags are required; `MainCamera` is the only tag used.

### Step 9 — build settings

`File ▸ Build Profiles` (Unity 6) → add the scene, **enabled**, and make sure it is the only scene: restart uses `SceneManager.LoadScene(buildIndex)`, which fails for a scene that is not in the list.

### Steps 10–12

10. Create the four prefabs — see **[03 · Prefabs](03-PREFABS.md)**.
11. Create `GameSettings` + the three `EnemyType` assets — see **[04 · Inspector Configuration](04-INSPECTOR-CONFIGURATION.md)**.
12. Press Play and check the first-run list in chapter 01 §7.

---

## 3. Why the world is laid out on the Z axis

* The army stands at `z = -8`, enemies spawn at `z = +15` and walk towards **-Z**.
* The defense line is at `z = -2.5`: an enemy that crosses it rams the army and dies.
* The camera sits behind the army (`z ≈ -17.7`) and looks towards **+Z** with a 42° pitch.

That means:

| Consequence | Value |
|---|---|
| Enemy travel distance | 15 − (−2.5) = **17.5 m** |
| Travel time at grunt speed 2.4 m/s | **≈ 7.3 s** — the player has time to react and dodge |
| Number of lanes | 5, centres at x = −5.6, −2.8, 0, 2.8, 5.6 |
| Playable half width | 7.0 m (+ rails at ±7.45) |

Changing `laneCount` or `laneWidth` automatically moves the lanes, the ground, the rails and the camera framing, because everything is derived from `GameSettings` (the generator reads it, and `GameSettings.LaneX()` is used at runtime). After changing them, re-run generator step 1 to rebuild the environment geometry.

---

## 4. Camera framing math (why it adapts to every phone)

`CameraFraming.SolvePosition()` computes the camera Z from two frustum constraints and takes the **closer** (more negative) result:

```
pitch        = 42°                 vertical FOV = 45°   → half = 22.5°
tanH         = tan(22.5°) · aspect                       (aspect 16:9 = 1.778)
halfWidth    = TrackHalfWidth · cameraTrackMargin = 7.0 · 1.12 = 7.84

1) horizontal fit at the spawn line
   depthNeeded = halfWidth / tanH = 7.84 / 0.7364 = 10.65
   fitZ        = farZ − (depthNeeded − height·sin(pitch)) / cos(pitch)
               = 17.5 − (10.65 − 10.04) / 0.7431 = 16.68

2) bottom edge must land behind the army
   bottomAngle = pitch + halfFOV = 64.5°
   nearFitZ    = armyZ − bottomPadding − height / tan(bottomAngle)
               = −8 − 2.5 − 15 / 2.0965 = −17.66

cameraZ = min(fitZ, nearFitZ) = −17.66      →  camera = (0, 15, −17.66)
```

Constraint 2 dominates on landscape phones, which is what you want: the army is always just above the bottom edge, and the spawn line lands around 90 % of the screen height. On a narrower window the horizontal constraint takes over and the camera simply pulls back, so the outer lanes never clip.

`CameraRig` re-runs this solve whenever `Screen.width/height` changes (rotation, split-screen, game view resize) and adds:

* **lateral follow** — `cameraX = armyX · cameraLateralFollow (0.32)`, SmoothDamp 0.22 s,
* **screen shake** — impulse-based, decays at `shakeDecay` (7) per second, capped at `maxShakeOffset` (0.35 m), rotation shake derived from the same offset,
* **background** — `backgroundColor = skyColor`, clear flags solid.

---

## 5. Environment values used by the generator

```csharp
nearZ    = armyZ − 12                    = −20
farZ     = spawnZ + spawnJitterZ + 8     = 25.5
length   = farZ − nearZ                   = 45.5
centerZ  = (nearZ + farZ) / 2             = 2.75

ground   = scale (TrackHalfWidth·2 + 26, 1, length)      at (0, −0.5, centerZ)
lane_i   = scale (laneWidth · 0.86, 0.03, length)        at (LaneX(i), 0.02, centerZ)
rails    = scale (0.7, 0.9, length)                      at (±(TrackHalfWidth + 0.45), 0.45, centerZ)
backdrop = scale (TrackHalfWidth·2 + 26, 3.2, 0.8)       at (0, 1.6, farZ)
```

The ground's top face is exactly `y = 0`, so every gameplay object can be placed with `y = 0` (feet on the floor).

---

## 6. Scene checklist

- [ ] Main Camera tagged `MainCamera` (the health bars billboard against `Camera.main`)
- [ ] `GameManager` on `Systems`, all eight references set
- [ ] `Army` has `ArmyController` with Soldier prefab + both roots
- [ ] `Enemy` layer exists and is applied to the Enemy prefab root
- [ ] Every decoration cube has **no** collider
- [ ] Scene is in Build Profiles and enabled
- [ ] `GameOverPanel` GameObject starts inactive, the component lives on the Canvas

---

Next: **[03 · Prefabs](03-PREFABS.md)**
