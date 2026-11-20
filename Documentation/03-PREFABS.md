# 03 · Prefab Setup

Four prefabs live in `Assets/NumberRush3D/Prefabs/`. All of them are built from Unity primitives and generated materials, so the project has **zero art dependencies** — you can run it before you own a single model.

| Prefab | Pooled by | Cap | Contains |
|---|---|---|---|
| `Bullet.prefab` | `BulletPool` | 220 | `Bullet` |
| `Spark.prefab` | `VfxPool` | 220 | `Spark` |
| `Soldier.prefab` | `ArmyController` | 68 | `SoldierUnit` |
| `Enemy.prefab` | `EnemySpawner` | 34 | `Health`, `Enemy`, `WorldHealthBar`, `Rigidbody`, `BoxCollider` |

**Universal rule for all four:** the pivot sits on the ground (`y = 0`), the object faces **+Z** (the enemy faces **-Z**, towards the camera), and no prefab contains a `Canvas`, a `Camera` or a light.

---

## 1. Materials

`Assets/NumberRush3D/Materials/` — URP/Lit (or Standard, see chapter 01 §3). Generated with `material.color = <below>`, smoothness, metallic 0, **GPU instancing on**.

| Material | Colour | Smoothness | Notes |
|---|---|---|---|
| Ground | `#171C2B` | 0.12 | receive shadows, cast off |
| Lane | `#262F47` | 0.20 | lane strips, cast off |
| Rail | `#38476B` | 0.30 | rails, backdrop, army marker |
| SoldierBody | `#2973F2` | 0.35 | *player color* |
| SoldierHead | `#9ED1FF` | 0.45 | *player accent color* |
| Bullet | `#FFDE4D` | 0.50 | emission ×1.8 |
| Spark | `#FFF2BF` | 0.60 | emission ×1.8 |
| EnemyBody | `#E8303B` | 0.30 | tinted per `EnemyType` at spawn |
| EnemyVisor | `#140D12` | 0.60 | deliberately **not** tinted |
| HealthBarBack | `#0D0F17` | 0.10 | |
| HealthBarFill | `#59F26B` | 0.15 | tinted green→red by `WorldHealthBar` |

Colours are driven by `GameSettings` (`playerColor`, `enemyColor`, …) — change them there and re-run generator step 1 to rebuild the materials.

---

## 2. `Bullet.prefab`

```
Bullet                      Bullet
└── Visual                  MeshRenderer (Cube 0.16 × 0.16 × 0.75), material Bullet
                            no collider · cast shadows off
```

| Step | Detail |
|---|---|
| Create | `GameObject ▸ Create Empty`, name `Bullet`, add **`Bullet`** |
| Visual | `3D Object ▸ Cube` child, name `Visual`, **delete its BoxCollider**, scale **(0.16, 0.16, 0.75)** |
| Field | `Bullet.bodyRenderer` → the `Visual` MeshRenderer |

*The long axis is Z* because `Bullet.Launch()` does `Quaternion.LookRotation(direction)`, so the stretched cube always points down the flight path.

The prefab has **no collider and no Rigidbody on purpose** — hit detection is done by the bullet itself with a sphere sweep against the `Enemy` layer (chapter 07 §5).

---

## 3. `Spark.prefab`

```
Spark                       Spark
└── Visual                  MeshRenderer (Cube 0.2³), material Spark
                            no collider · cast shadows off
```

* Field: `Spark.bodyRenderer` → `Visual` MeshRenderer.

One cube per spark; `Spark.Play()` sets colour (via `MaterialPropertyBlock`), velocity, spin and lifetime, and returns the object to the pool when it expires.

---

## 4. `Soldier.prefab`

```
Soldier                     SoldierUnit
└── Visual                  <- bodyRoot (idle bob + spawn pop are applied here)
    ├── Body                MeshRenderer (Cube 0.44 × 0.66 × 0.44) at (0, 0.33, 0)   material SoldierBody
    ├── Head                MeshRenderer (Cube 0.32 × 0.30 × 0.32) at (0, 0.79, 0)   material SoldierHead
    ├── Muzzle              empty transform at (0, 0.62, 0.36)
    └── Flash               MeshRenderer (Cube 0.17³) at (0, 0.62, 0.40)            material Bullet
                            MeshRenderer DISABLED · cast shadows off
```

| Step | Detail |
|---|---|
| Root | Empty `Soldier` + **`SoldierUnit`** |
| Visual | Empty child `Visual` — this is the animated root, keep the meshes under it |
| Body / Head | Cubes, **delete every BoxCollider**, apply materials, use the local positions above |
| Muzzle | `Create Empty` child of `Visual` at (0, 0.62, 0.36) — bullets spawn here, so it must sit in front of the body |
| Flash | Cube child of `Visual`, then untick **MeshRenderer ▸ enabled** in the inspector |
| Fields | `muzzle` = Muzzle · `bodyRoot` = Visual · `muzzleFlash` = Flash's MeshRenderer |

**Why no collider?** Soldiers are never hit by bullets (only by enemies that reach the line, which is a Z comparison) and they never push anything. Removing the colliders keeps 60 soldiers out of the physics broadphase entirely. All three fields are also auto-found at runtime (`Visual/Muzzle`, first child, `Visual/Flash`) if you leave them empty.

---

## 5. `Enemy.prefab`

```
Enemy                       layer = Enemy
│                           Health · Enemy · Rigidbody · BoxCollider
├── Visual                  <- bodyRoot
│   ├── Body                MeshRenderer (Cube 0.9 × 1.1 × 0.9) at (0, 0.62, 0)      material EnemyBody
│   └── Visor               MeshRenderer (Cube 0.66 × 0.20 × 0.06) at (0, 0.8, -0.47) material EnemyVisor
└── HealthBar               WorldHealthBar at (0, 1.42, 0)
    ├── Back                MeshRenderer (Cube 1.02 × 0.16 × 0.03)                   material HealthBarBack
    └── FillPivot           empty at (-0.5, 0, -0.02)
        └── Fill            MeshRenderer (Cube 1.0 × 0.10 × 0.04) at (0.5, 0, 0)     material HealthBarFill
```

### 5.1 Components on the root

| Component | Settings | Why |
|---|---|---|
| **Health** | Max Health 3 (replaced by the `EnemyType` at spawn) · Tint Renderers = `[Body]` · Hit Flash `#FFFFFF` · Flash Duration 0.07 | one damage receiver for every enemy kind |
| **Enemy** | `health` = root Health · `bodyRoot` = `Visual` · `healthBar` = `HealthBar` | movement, ramming, death flow |
| **Rigidbody** | Is Kinematic **on** · `Use Gravity` **off** · Interpolate **None** · Collision Detection Discrete · Constraints **Freeze All** | a moving collider without a Rigidbody is treated as *static* by PhysX and re-bakes the broadphase every frame; the kinematic body removes that cost |
| **BoxCollider** | Center **(0, 0.62, 0)** · Size **(0.92, 1.24, 0.92)** · **Is Trigger off** | the only collider in the whole game — it is what the bullet sphere-sweep hits |

### 5.2 Layer

`layer = Enemy` on the **root** (children inherit). The layer is what `GameSettings.EnemyMask` resolves, and it is the only mask a bullet tests. If the layer is missing, `GameSettings` falls back to "everything" and logs a warning — bullets still work because they filter by `IDamageable`, they are just less cheap.

### 5.3 Health bar construction (the pivot trick)

The fill is **not** resized by changing its `localScale.x` around its own centre (that would grow both ways). Instead:

* `FillPivot` sits at the **left edge** of the bar (`x = -0.5`),
* `Fill` sits at `x = +0.5` inside that pivot, so at pivot scale `(1,1,1)` it exactly covers the bar,
* `WorldHealthBar.Refresh()` sets `fillPivot.localScale = (normalizedHealth, 1, 1)`.

Scaling the pivot around its left edge shrinks the fill towards the left, which is exactly the behaviour of a health bar, without touching any mesh pivot or using an Image/Sprite.

`FillPivot.z = -0.02` and `Fill` depth 0.04 put the fill in front of `Back` (depth 0.03) so there is no z-fighting.

`WorldHealthBar.LateUpdate()` copies the main camera's rotation → the bar always faces the player, even though the enemy never rotates.

### 5.4 Enemy archetypes are data, not prefabs

There is **one** enemy prefab. `EnemyType` ScriptableObjects describe Grunt / Runner / Brute (health, speed, scale, colour, score, recruit value, army damage, spawn weight, unlock difficulty). `Enemy.Spawn()` applies `localScale`, health, speed and colour. Adding a fourth enemy kind is therefore a *Create ▸ NUMBER RUSH 3D ▸ Enemy Type* + one entry in the spawner's array — no prefab work.

If you later want a different **mesh** per archetype, make prefab variants and give the spawner an array of prefabs indexed by type — see chapter 07 §7.

---

## 6. Prefab audit (before you press Play)

- [ ] Bullet / Spark / Soldier contain **no** colliders
- [ ] Enemy root is on the `Enemy` layer and has its BoxCollider **non-trigger**
- [ ] Enemy Rigidbody is **kinematic**
- [ ] `Health.tintRenderers` contains the Body renderer only (not the visor, not the bar)
- [ ] `WorldHealthBar.fillPivot` = FillPivot, `fillRenderer` = Fill
- [ ] Muzzle is in front of the soldier body (`z > 0`)
- [ ] Every prefab root is at position `(0,0,0)` before saving

---

Next: **[04 · Inspector Configuration](04-INSPECTOR-CONFIGURATION.md)**
