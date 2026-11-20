# 07 · Architecture

The project is deliberately small but layered: **one composition root**, **interface-injected dependencies**, **pooled everything**, and **no singleton lookups inside gameplay code**.

---

## 1. Module map

```
                     ┌──────────────────────────────────────────┐
                     │              Core                        │
                     │  GameManager  (composition root)         │
                     │  GameSettings (ScriptableObject)         │
                     │  IBattleContext  IDamageable  GameState  │
                     │  ObjectPool<T>                           │
                     └───────┬───────────────┬──────────────────┘
                             │ injects       │ implements
             ┌───────────────┼───────────────┼──────────────────┐
             ▼               ▼               ▼                  ▼
      ┌────────────┐  ┌────────────┐  ┌────────────┐   ┌──────────────┐
      │   Army     │  │  Enemies   │  │  Combat    │   │   View / UI  │
      │ ArmyCtrl   │──│ EnemySpawn │─▶│ BulletPool │   │ CameraRig    │
      │ Soldier    │  │ Enemy      │  │ Bullet     │   │ CameraFraming│
      │ Formation  │  │ Registry   │  │ Health     │   │ HudController│
      │            │  │ EnemyType  │  │ VfxPool    │   │ GameOverPanel│
      │            │  │ HealthBar  │  │ Spark      │   │ SafeArea     │
      └─────┬──────┘  └─────┬──────┘  └─────┬──────┘   └──────┬───────┘
            │               │               │                 │
            └───────────────┴───────┬───────┴─────────────────┘
                                    ▼
                           ┌──────────────────┐
                           │    Controls      │
                           │ SwipeInput       │
                           │ ILaneInputSource │
                           │ PointerInputBridge│
                           └──────────────────┘
```

### Dependency rules

1. **Core knows nothing specific.** `GameSettings`, `GameState`, `IDamageable`, `ObjectPool<T>` are leaf types. `IBattleContext` is the only interface that mentions the concrete systems, and it exists purely so that gameplay classes can call back into the composition root.
2. **Gameplay → `IBattleContext`.** `SoldierUnit`, `Enemy`, `ArmyController` never touch `GameManager.Instance`, never call `FindObjectOfType`, never use `Camera.main` for gameplay.
3. **The composition root owns creation.** `GameManager.Awake` builds pools and injects; nothing else instantiates gameplay objects except through a pool.
4. **UI listens, never polls.** `HudController` and `GameOverPanel` subscribe to events on the object they are handed in `Bind()`.
5. **One file talks to Unity input, one file talks to Unity rendering pipeline selection (editor).** Swapping either is a single-file change.

---

## 2. Boot sequence (deterministic)

`GameManager` has `[DefaultExecutionOrder(-100)]`.

| Order | Call | What happens |
|---|---|---|
| 1 | `GameManager.Awake` | `Time.timeScale = 1`, singleton back-reference, validate `GameSettings`, `EnsureReferences()`, `targetFrameRate`, load best score, `VfxPool.Initialize()`, `BulletPool.Initialize()`, `ArmyController.Initialize(settings, this, input)`, `EnemySpawner.Initialize(...)`, `CameraRig.Initialize(settings, army.transform)`, `State = Playing` |
| 2 | other `Awake`s | `Health` auto-collects tint renderers, `Enemy` finds its `Health`/`Body`, `WorldHealthBar` resolves `Camera.main`, `EnemyRegistry` clears its list |
| 3 | `GameManager.Start` | `army.Begin()` (creates the soldier pool + starting soldiers), `spawner.Begin()`, `hud.Bind(this)`, `gameOverPanel.Bind(this)`, first events |
| 4 | `Update`/`LateUpdate` | see §3 |

Because injection happens in step 1 and *usage* only starts in step 3, no system has to worry about "is my settings reference ready yet".

---

## 3. Per-frame flow

```
Update
 ├── GameManager        RunTime += dt  (state == Playing)
 ├── SwipeInput         samples pointer → enqueues ±1 lane steps (unscaled time)
 ├── ArmyController     consumes a step → target lane, SmoothDamp X, lerps soldiers to slots
 ├── SoldierUnit (×N)   sticky target → fire timer → BulletPool.Spawn(origin, dir, speed, dmg)
 │       └── BulletPool.Get → Bullet.Launch
 ├── Bullet (×N)        SphereCastNonAlloc sweep → IDamageable.TakeDamage → impact spark → pool
 ├── Enemy (×N)         move −Z, bob; z ≤ defenseLineZ → Army.ApplyCasualties + die
 ├── Health (×N)        hit-flash timer only
 └── EnemySpawner       spawn timer → weighted type pick → lane pick → pool.Get → Enemy.Spawn

LateUpdate
 ├── CameraRig          re-solve framing if the aspect changed, lateral follow, shake
 └── WorldHealthBar(×N) billboard to the camera
```

Each system is independent within a frame; interleaving order does not matter because nothing reads a value that must be updated *earlier in the same frame*. One frame of extra latency is invisible at 60 fps.

---

## 4. Pooling strategy

| Pool | Factory | Prewarm | Cap | Release trigger |
|---|---|---|---|---|
| Soldiers | `Instantiate(soldierPrefab)` | `startingSoldiers` | `maxSoldiers + 8` | casualty |
| Enemies | `Instantiate(enemyPrefab)` | 14 | `maxConcurrentEnemies + 8` | death / ram |
| Bullets | `Instantiate(bulletPrefab)` | 96 | `maxBulletsInFlight` (220) | hit / lifetime |
| Sparks | `Instantiate(sparkPrefab)` | 64 | 220 | lifetime |

`ObjectPool<T>`:

* idle items live under a dedicated `poolRoot` transform and are `SetActive(false)`,
* `Get()` pops, repositions, activates; when the pool is empty **and** at capacity it returns `null` and the caller skips the spawn (that is the bullet budget guard, not an error),
* `Release()` is idempotent (a `HashSet` tracks idle items), so double-release bugs cannot duplicate objects,
* everything is `O(1)`; a five-minute run performs **zero** allocations after warm-up (no LINQ, no `new` in `Update`, no per-frame arrays, `MaterialPropertyBlock` instead of material instances).

---

## 5. Why bullets are physics-free

```csharp
// Bullet.Update, simplified
Vector3 step = _velocity * Time.deltaTime;
int hits = Physics.SphereCastNonAlloc(transform.position, _radius, step.normalized, HitBuffer, step.magnitude, _enemyMask, QueryTriggerInteraction.Ignore);
```

* **No tunnelling.** At 42 m/s and 30 fps a bullet travels 1.4 m per frame — far more than an enemy's 0.92 m width. A swept sphere cannot miss.
* **No Rigidbody, no trigger matrix, no `OnTriggerEnter` dispatch.** The bullet only needs a collider on the *other* side, which is why only enemies carry one.
* **One shared `RaycastHit[16]`** for every bullet → no GC.
* **Filtering happens twice:** the layer mask keeps the physics query cheap, and `hit.collider.GetComponentInParent<IDamageable>()` guarantees only damageable objects are hit (so a stray decoration collider can never eat a bullet).
* The nearest damageable hit wins, so a bullet that clips two overlapping enemies damages the closer one.

The same idea is used for target acquisition: `EnemyRegistry` is a flat `List<Enemy>` scanned linearly. With ≤ 26 enemies a linear scan (and a swap-remove `Unregister`) beats any spatial structure, and soldiers only search every 0.22 s with a random per-soldier phase.

---

## 6. Damage pipeline

```
Bullet  ──TakeDamage(amount, hitPoint)──▶  Health  ──Died──▶  Enemy
                                             │                  ├─ context.ReportKill(type)  → score + recruits
                                             └─Damaged──▶       ├─ context.Vfx.PlayBurst
                                                WorldHealthBar  ├─ context.ShakeCamera
                                                                └─ owner.Release(this)  → back to the pool
```

`Health` is the only place that mutates HP, and it emits both events. Adding armour, shields or damage types means changing exactly one class.

---

## 7. Extension recipes

### Add an enemy archetype (2 minutes)

1. `Assets ▸ Create ▸ NUMBER RUSH 3D ▸ Enemy Type`, name it `EnemyType_Shielder`.
2. Set `maxHealth 22`, `moveSpeed 1.2`, `scale 1.3`, `armyDamage 3`, `spawnWeight 0.25`, `minDifficulty 0.6`.
3. Drag it into `EnemySpawner.enemyTypes`.

No code, no prefab, no scene change.

### Add a system (e.g. a score multiplier)

```csharp
public sealed class ComboSystem : MonoBehaviour
{
    private GameManager _game;
    private float _streakTimer;

    public void Initialize(GameManager game) => _game = game;

    private void Update()
    {
        if (_game == null || !_game.IsPlaying) return;
        _streakTimer = Mathf.Max(0f, _streakTimer - Time.deltaTime);
        // ...combo logic...
    }
}
```

Add a field to `GameManager`, call `combo.Initialize(this)` in `Awake`, and (optionally) add `int ScoreMultiplier` to `IBattleContext` if gameplay needs to read it.

### Replace input

```csharp
public sealed class DragFollowInput : MonoBehaviour, ILaneInputSource
{
    public bool TryConsumeLaneStep(out int direction) { direction = ComputeFromDrag(); return direction != 0; }
}
```

Pass it to `ArmyController.Initialize(settings, context, dragInput)` instead of `SwipeInput`. Nothing else changes.

---

## 8. Assembly definitions (optional, for larger teams)

The project compiles into `Assembly-CSharp` / `Assembly-CSharp-Editor` on purpose: zero setup, and the editor tool can reference every runtime type. If you want faster iteration and enforced dependencies, add:

```
Assets/NumberRush3D/Scripts/NumberRush3D.Runtime.asmdef
  name: NumberRush3D.Runtime, references: [] (UnityEngine.UI only)

Assets/NumberRush3D/Editor/NumberRush3D.Editor.asmdef
  name: NumberRush3D.Editor, references: [NumberRush3D.Runtime], includePlatforms: [Editor]
```

Nothing in the code needs to change — there are no `UnityEditor` calls outside `Editor/`, and no editor-only types leak into runtime scripts.

---

## 9. Known simplifications (and what production would change)

| Simplification | Production note |
|---|---|
| Restart = scene reload | Fine and fast here (single scene). A soft reset would need `ArmyController.ResetRun()`, pool draining, HUD rebind. |
| Legacy UI `Text` | Swap to TextMeshPro (`TMP_Text`), a 3-line change per field. |
| No audio | Add a pooled `AudioDirector`; the hooks are `SoldierUnit.Fire`, `Enemy.HandleDied`, `ArmyController.ApplyCasualties`, `GameManager.TriggerGameOver`. |
| No enemy projectiles | `EnemyRegistry.FindNearest` already gives line-of-sight-free targeting for the other side. |
| Single `Enemy` prefab for all archetypes | Add `EnemyType.prefabOverride` and one pool per archetype. |
| Best score only in `PlayerPrefs` | Add coins/upgrades with a `SaveSystem` writing JSON to `Application.persistentDataPath`. |
| No object-count debug UI | `BulletPool.ActiveCount`, `EnemyRegistry.Count`, `EnemySpawner.SpawnedCount` are all public for a debug overlay. |

---

Next: **[08 · Troubleshooting](08-TROUBLESHOOTING.md)**
