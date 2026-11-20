# 06 · Android Build

Target: **ARM64 · IL2CPP · Vulkan + GLES3 · landscape · min API 26**

---

## 1. Install the Android modules

Unity Hub ▸ **Installs** ▸ ⚙ next to your 6000.3.x editor ▸ **Add modules**:

- ✅ **Android Build Support**
- ✅ **Android SDK & NDK Tools**
- ✅ **OpenJDK**

Verify: `Edit ▸ Preferences ▸ External Tools ▸ Android` — *SDK*, *NDK* and *JDK* paths must all be filled (the Hub-installed versions are the ones Unity tests against). If they are empty, click **Use embedded JDK / SDK / NDK** and re-check.

---

## 2. Apply the player-settings preset

**`Tools ▸ NUMBER RUSH 3D ▸ 2. Configure Android Player Settings`** (or `NumberRushBuildTools.ConfigureAndroid(true)` from your CI script).

It sets:

| Setting | Value |
|---|---|
| Product / company | `NUMBER RUSH 3D` / `Number Rush Studio` |
| Bundle identifier | `com.yourstudio.numberrush3d` ← **change before publishing** |
| Version | `0.1.0`, bundle version code `1` |
| Orientation | Auto Rotation, landscape left + right only, animated rotation off |
| Colour space | Linear |
| Graphics APIs | Vulkan, OpenGLES3 |
| Scripting backend | IL2CPP |
| Target architecture | ARM64 |
| API compatibility | .NET Standard 2.1 |
| Managed stripping | Medium + `stripEngineCode` |
| Min API | 26 (Android 8.0) |
| Target API | Automatic (highest installed) |
| Optimized frame pacing | On |
| Internet / SD permissions | Off |
| GPU skinning | On |

`Application.targetFrameRate = 60` comes from `GameSettings.targetFrameRate` at runtime.

---

## 3. Keystore (required for release, optional for testing)

1. `Edit ▸ Project Settings ▸ Player ▸ Publishing Settings`.
2. **Keystore Manager ▸ Create New ▸ Anywhere**.
   * Keystore password (write it down — you cannot publish updates without it),
   * Alias `numberrush`, alias password, validity 25+ years.
3. Assign the keystore, the alias and both passwords in **Project Settings ▸ Player ▸ Publishing Settings**.
4. **Back up the `.keystore` file and both passwords.** Losing them means you can never update the app on Google Play.

For internal testing you can leave the fields empty and Unity signs with the debug keystore (`~/.android/debug.keystore`) — but then the APK cannot be uploaded to Play.

---

## 4. Build

### 4.1 One-click builds

| Menu item | Output |
|---|---|
| `Tools ▸ NUMBER RUSH 3D ▸ 3. Build Android APK` | `Builds/Android/NumberRush3D.apk` (side-load / device testing) |
| `Tools ▸ NUMBER RUSH 3D ▸ 4. Build Android App Bundle (Play Store)` | `Builds/Android/NumberRush3D.aab` |

Both call `ConfigureAndroid(true)` first, save the open scenes, build, log the result and reveal the file in your file manager.

### 4.2 Build Profiles window (manual route)

`File ▸ Build Profiles ▸ Android`:

* **Scene list:** `Assets/NumberRush3D/Scenes/NumberRush3D.unity` — enabled, index 0 (restart relies on `SceneManager.LoadScene(buildIndex)`).
* **Build** = APK for testing, **Build App Bundle** = AAB for Play.
* **Development Build** for profiling (never for a store build).

### 4.3 Command line

```bash
# APK
"/path/to/Unity" -quit -batchmode -projectPath "/path/to/game" \
  -executeMethod NumberRush3D.EditorTools.NumberRushBuildTools.BuildApkMenu \
  -logFile build.log

# AAB
"/path/to/Unity" -quit -batchmode -projectPath "/path/to/game" \
  -executeMethod NumberRush3D.EditorTools.NumberRushBuildTools.BuildAabMenu \
  -logFile build.log
```

Add `-buildTarget Android` if the project is not already switched. Both methods exit with a non-zero code on failure, which is what a CI step needs.

---

## 5. Install and test on a device

```bash
adb devices                                  # phone must show as "device"
adb install -r Builds/Android/NumberRush3D.apk
adb logcat -s Unity ActivityManager          # filter Unity's log
```

Wireless debugging (Android 11+): `Developer options ▸ Wireless debugging ▸ Pair device with pairing code`, then

```bash
adb pair 192.168.1.42:37123
adb connect 192.168.1.42:39127
```

Uninstall: `adb uninstall com.yourstudio.numberrush3d`

---

## 6. Device profiling

1. Build with **Development Build** + **Autoconnect Profiler**, run `Window ▸ Analysis ▸ Profiler`.
2. Add the **`Android Logcat`** package (`Window ▸ Analysis ▸ Android Logcat`) to see native crashes and the `[NumberRush]` logs.
3. Expected budget at 60 fps on a mid-range device (e.g. Snapdragon 6-series):
   * CPU main thread < 8 ms, render thread < 6 ms,
   * ~150–250 draw calls submitted (SRP batcher + GPU instancing),
   * < 150 MB total memory, < 40 MB texture (there are no textures at all).
4. First things to turn down if you miss the budget:
   * `GameSettings.maxConcurrentEnemies` (26 → 18),
   * shadows off on units (Enemy/Soldier prefabs → MeshRenderer ▸ Cast Shadows = Off),
   * MSAA 4x → 2x in the URP asset.

`Window ▸ Analysis ▸ Rendering Debugger` (URP) shows overdraw instantly — the lane strips and ground are the only large surfaces, so overdraw should stay low.

---

## 7. Play Store checklist

- [ ] Bundle id changed from `com.yourstudio.numberrush3d`
- [ ] Version code incremented for every upload (`PlayerSettings.Android.bundleVersionCode`)
- [ ] **AAB** uploaded, signed with your release keystore
- [ ] 64-bit ARM64 target included (required since 2019)
- [ ] Target API level = the current Play requirement (set to Automatic and keep the editor updated)
- [ ] Icon set: `Player Settings ▸ Icon` (adaptive icon: foreground + background)
- [ ] Splash screen configured (`Player Settings ▸ Splash Image`)
- [ ] Store listing: landscape screenshots (min 2), feature graphic, short/full description
- [ ] Content rating questionnaire, data-safety form (this game collects nothing — no network, no analytics)
- [ ] Privacy policy only if you add analytics/ads later
- [ ] Test on a small screen (720×1280 landscape) and a tablet (4:3) — the camera solves the framing automatically, but the HUD is authored for 16:9

---

## 8. Common build errors

| Error | Cause / fix |
|---|---|
| `Android SDK not found` | Hub module missing → add *Android SDK & NDK Tools*, then re-check `Preferences ▸ External Tools`. |
| `IL2CPP requires NDK` | Same as above; IL2CPP cannot compile without the NDK. |
| `Gradle build failed ... minSdkVersion` | A plugin requires a higher API. Raise `PlayerSettings.Android.minSdkVersion` to 26+. |
| `Keystore was tampered with, or password was incorrect` | Wrong alias/password in Publishing Settings. Re-enter both passwords, not just the keystore one. |
| `CommandInvokationFailure: Unable to list target platforms` | Old Android SDK tools; reinstall the SDK through the Hub. |
| `Target architecture ARMv7 is deprecated` | Use ARM64 only (already the preset). |
| App builds but the screen is black | Camera not tagged `MainCamera`, or the scene is missing from Build Profiles. |
| App builds but is unresponsive to swipes | `Active Input Handling` is *Input System (New)* but the Input System package is not installed → set it to **Both** (see chapter 01 §5). |

---

Next: **[07 · Architecture](07-ARCHITECTURE.md)**
