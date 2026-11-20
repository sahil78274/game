# 08 · Troubleshooting

## 0. Debug in 60 seconds

1. **Console** — the project logs with the `[NumberRush]` prefix at every important failure (`missing settings`, `missing prefab`, `missing layer`, `build complete`, `pool exhausted`).
2. **`Tools ▸ NUMBER RUSH 3D ▸ 6. Project Setup Checklist`** — render pipeline, input backend, layer, scene, Android settings in one dialog.
3. **Hierarchy** — during Play you should see children appearing under `Army/Formation`, `EnemySpawner/Enemies` and `BulletPool/Bullets`.
4. **Inspector** — select `Systems` and confirm all eight references on `GameManager` are filled.

---

## 1. Editor / import problems

| Symptom | Cause | Fix |
|---|---|---|
| **Materials are magenta/pink** | URP shaders with no pipeline asset assigned (the project renders with the built-in pipeline) | Assign a URP asset in `Project Settings ▸ Graphics` **and** in `Project Settings ▸ Quality ▸ Render Pipeline Asset` (chapter 01 §3). Then re-run generator step 1. |
| **`Tools ▸ NUMBER RUSH 3D` menu missing** | Scripts did not compile | Fix console errors first; the menu is defined in `Assets/NumberRush3D/Editor/NumberRushBuildTools.cs` |
| **Package resolution error on open** | `Packages/manifest.json` pins URP **17.3.0** (Unity 6.3) | Either open with 6000.3.x, or delete `Packages/manifest.json` + `Packages/packages-lock.json` and use the Hub's **Universal 3D** template instead (chapter 01 §2) |
| **`The type or namespace name 'FindFirstObjectByType' ...`** | Unity older than 2023.1 | The project targets Unity 6.3. On an older editor replace `FindFirstObjectByType<T>(...)` with `FindObjectOfType<T>()` in `GameManager.EnsureReferences()` |
| **`The name 'InputSystemUIInputModule' ...`** | Active input handling is *Input System* but the package is not installed | Install `com.unity.inputsystem` or set Active Input Handling to **Both** |
| **Generator stops with "Failed to save prefab"** | A prefab with the same path is open/read-only, or the folder was deleted | Delete `Assets/NumberRush3D/Prefabs`, close inspectors on those assets, re-run step 1 |
| **Generator dialog about saving the scene** | It replaces the open scene with a fresh one | It is the same prompt as `File ▸ Open Scene`; save your work first |

---

## 2. Nothing happens when I press Play

| Symptom | Cause | Fix |
|---|---|---|
| **No soldiers** | `ArmyController.soldierPrefab` empty, or `GameSettings.startingSoldiers` = 0 | Assign `Prefabs/Soldier.prefab`; check console for `[NumberRush] ArmyController has no soldier prefab` |
| **No enemies** | `EnemySpawner.enemyPrefab` / `enemyTypes` empty | Assign `Prefabs/Enemy.prefab` and the three `EnemyType_*` assets |
| **No bullets** | `BulletPool.bulletPrefab` empty, or the soldiers have no target | Check the console warning; confirm enemies are in front (`z > armyZ`) |
| **Nothing at all + error in Awake** | `GameManager.settings` empty | Assign `Settings/GameSettings.asset` — the manager disables itself otherwise |
| **Enemies never die** | The `Enemy` layer does not exist → mask fell back to everything, but the enemy collider is missing | Check for the `Layer 'Enemy' does not exist` warning and re-run step 1; confirm the Enemy prefab root has a **non-trigger** BoxCollider and is on the `Enemy` layer |
| **Enemies walk through the army and the game never ends** | `defenseLineZ` is behind the army, or `armyDamage` = 0 | Keep `defenseLineZ > armyZ` (e.g. −2.5 vs −8) |
| **Enemies spawn on top of the camera** | `spawnZ` too small or the camera is mis-framed | Restore `spawnZ 15`, and let `CameraRig` solve the framing (chapter 02 §4) |

---

## 3. Input

| Symptom | Cause | Fix |
|---|---|---|
| **Swipes do nothing on the phone** | Active Input Handling = *Input System (New)* without the package | `Project Settings ▸ Player ▸ Active Input Handling` = **Both** |
| **Swipes work but feel unresponsive** | `stepCooldown` too high or the swipe threshold too large | `SwipeInput.stepCooldown` 0.09 → 0.05, `minSwipeScreenFraction` 0.05 → 0.03 |
| **One swipe moves several lanes** | `continuousDrag` is on, or fast repeated swipes are queued | Turn `continuousDrag` off (default); the queue is capped at ±2 by design |
| **Keyboard does nothing in the editor** | `keyboardFallback` off on `SwipeInput` | Enable it |
| **Swipe sends the army the wrong way** | Left/right mapping | `SwipeInput` maps positive screen-X to `+1` lane (right). If your build is mirrored, flip the sign in `EnqueueStep` calls |

---

## 4. UI

| Symptom | Cause | Fix |
|---|---|---|
| **No HUD at all** | `HudController` not on an active object, or the Canvas has no `CanvasScaler` | The `HUD` GameObject must be active; re-run step 1 to regenerate |
| **Text is invisible** | `Text.font` is null (built-in font lookup failed) | Select the Text components and assign `LegacyRuntime.ttf` (Project window ▸ search `LegacyRuntime`) |
| **Game over panel never appears** | `GameOverPanel` component is on the inactive panel instead of the Canvas | The **component** must live on `UI Canvas`; the **GameObject** it controls is `SafeArea/GameOverPanel` |
| **Panel appears but buttons do nothing** | No `EventSystem`, or the wrong input module | Re-run step 1 (it creates the EventSystem), or add `InputSystemUIInputModule` / `StandaloneInputModule` to match your input backend |
| **Panel is frozen mid-animation** | `Time.timeScale = 0` on game over — that is intended | Use `Time.unscaledDeltaTime` in your own UI animations |
| **Retry restarts in slow motion** | `Time.timeScale` was left at 0 | `GameManager.Awake` and `Restart()` both reset it to 1 |
| **HUD overlaps the notch** | `SafeArea` missing | Add the `SafeArea` component to `UI Canvas/SafeArea` |
| **HUD scales oddly on a tablet** | Reference resolution is 1920×1080 with match 0.5 | Tune `CanvasScaler.matchWidthOrHeight` (0 = match width, 1 = match height) |

---

## 5. Gameplay feel

| Symptom | Cause | Fix |
|---|---|---|
| **Army is always at max soldiers** | Recruits too generous | `killsPerRecruit` 8 → 14, and keep `EnemyType.recruitValue` at 0 except for brutes |
| **Everything dies instantly** | Fire rate/DPS too high, or `bulletDamage` raised | See the balance equation in chapter 05 §3 |
| **The army never hits anything** | `aimSpreadDegrees` too high, or `bulletRadius` too small | 1.4 / 0.12 are the sane values |
| **The whole army shoots one enemy** | Sticky targeting working as intended | It spreads naturally as targets die; if you want more spread, lower `targetSearchRange` so soldiers pick different nearest targets from different positions |
| **Soldiers jitter in formation** | `formationLerpSpeed` too high, or slots rebuilt every frame | Only `AddSoldiers`/`RemoveLastSoldier` rebuild slots; report a bug if it happens otherwise |
| **Rear soldiers are cut off at the bottom** | Very large army + `cameraBottomPadding` too low | Raise `cameraBottomPadding` (2.5 → 4) or reduce `soldierRowSpacing` |
| **Screen shake never stops** | `shakeDecay` = 0 | Keep it ≥ 1 |

---

## 6. Performance

| Symptom | Cause | Fix |
|---|---|---|
| **Frame rate capped at 30** | Battery saver / thermal throttling on device | Check `Application.targetFrameRate` (60) and the device's game mode; profile with the Unity Profiler |
| **Hitches every few seconds** | GC from your own additions (LINQ, string concat in `Update`, `new` arrays) | The shipped code allocates nothing per frame — profile `GC Alloc` per frame, target 0 B |
| **Slow with 60 soldiers** | Draw calls: 2 renderers per soldier | Enable GPU instancing on the soldier materials, or disable shadow casting on soldiers |
| **Enemies pile up** | `maxConcurrentEnemies` too high for the device | 26 → 18, and shorten `bulletMaxLifetime` |
| **Too many sparks** | `VfxPool` bursts on every death | Lower the burst counts in `Enemy.HandleDied` (7) / `Ram` (8) / `ArmyController.RemoveLastSoldier` (4) |
| **First bullet causes a spike** | `prewarm` too low | Raise `BulletPool.prewarm` (96) |

---

## 7. Android

| Symptom | Cause | Fix |
|---|---|---|
| **Build fails: no SDK/NDK** | Hub modules missing | Chapter 06 §1 |
| **Black screen after launch** | Scene not in Build Profiles | Chapter 02 step 9 |
| **Game runs in portrait** | Orientation overridden in the manifest | Re-run step 2 (landscape-only preset) |
| **Touch does not work but the editor does** | Input backend | Chapter 01 §5 |
| **App crashes on start** | IL2CPP stripping removed a reflected type | Set Managed Stripping Level to `Minimal` while debugging (`Tools ▸ 2` sets `Medium`) |
| **Device gets hot** | 60 fps + shadows + MSAA 4x | Lower MSAA to 2x, disable unit shadows, cap the frame rate at 45 |

---

## 8. Verifying the source itself

```bash
# 1. C# syntax of every file (tree-sitter, no Unity needed)
python3 Verification/check_syntax.py

# 2. Cross-file consistency:
#    - every SerializedObject field name used by the editor generator exists
#    - every member accessed through the injected references exists
python3 Verification/check_consistency.py
```

Both must print `0 problems`. The syntax checker is also useful after you edit the scripts: it reports the exact line and token of any broken file.

---

## 9. Getting help

When reporting an issue, include:

* Unity version (`Help ▸ About Unity`),
* the render pipeline shown by the setup checklist,
* Active Input Handling mode,
* the full `[NumberRush]` console output,
* whether the problem reproduces in a freshly generated scene (run step 1 again).

---

Back to the **[README](../README.md)**
