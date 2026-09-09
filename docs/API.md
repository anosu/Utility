# Utility API

`Utility.dll` provides loader-neutral IL2CPP toast notifications, structured diagnostics, and AssetBundle loading. It targets modern BepInEx 6 and MelonLoader releases that use Il2CppInterop.

The library does not reference either loader. Both environments use the same initialization path and the same binary.

## Installation

Copy `Utility.dll` next to the consuming mod. Do not copy `Il2CppInterop.Runtime.dll` from the build output; the active loader supplies its compatible runtime.

The current build targets `net6.0` and requires these Unity modules at runtime:

- `UnityEngine.CoreModule`
- `UnityEngine.IMGUIModule`
- `UnityEngine.TextRenderingModule`
- `UnityEngine.UIModule`
- `UnityEngine.UI`
- `UnityEngine.AssetBundleModule` when `AssetBundleLoader` is used

Toast prefers IMGUI and switches to its built-in uGUI renderer if IMGUI drawing or explicit font sizing fails. Both renderers use the same font selection.

The proxies under `dependencies/interop` are the default local compile-time contract. To validate against another game's generated surface, build with `-p:UnityProxyDir=/path/to/generated/proxies`; optional Unity calls are isolated behind renderer or asset-loading fallback paths, but no runtime-loaded mod can call a native method that the target game removed entirely.

## Breaking changes

The reorganized interface intentionally removes the legacy compatibility surface:

| Previous | Current |
|---|---|
| `Utility.Toast.Toast` | `Utility.Notifications.Toast` |
| Public `ToastUI`/`ToastBehaviour` instance methods and integer constants | Internal lifecycle behaviour plus `ToastKind` and `ToastAnchor` |
| `Toast.Warn` | `Toast.Warning` |
| `maxHeight`, `max`, integer `anchor` | `minimumHeight`, `maximumVisible`, typed `ToastAnchor` |
| `Utility.Fonts.FontHelper<T>` | `Utility.Assets.AssetBundleLoader<T>` |
| `LoadAsync`, `Loading`, `Valid` | `Load`, `IsLoading`, `IsLoaded` |
| Non-generic asset helper | Explicit generic asset wrapper type |
| Root logging types | `Utility.Diagnostics` |
| `Utility.Ugui.dll` / `Utility.Notifications.Ugui.dll` | Merged into `Utility.dll` |

## Project layout

- `Utility/Assets` contains the loader-neutral AssetBundle module.
- `Utility/Diagnostics` contains structured host logging.
- `Utility/Notifications` exposes the toast interface; renderer state and the injected behaviour live under `Internal`.
- `Utility.Tests` mirrors those modules and keeps IL2CPP metadata checks under `Compatibility`.
- `Utility.Lifecycle.Tests` runs the production loading coroutines and toast lifecycle callbacks against controlled Unity substitutes.
- `Directory.Build.props` owns shared compiler, proxy-path, and Il2CppInterop settings.

## Toast lifecycle

Call `Toast.Initialize()` once from the loader's main-thread initialization callback. Notifications submitted before initialization remain queued.

```csharp
using Utility.Notifications;

Toast.Initialize();
Toast.Success("Utility", "Loaded successfully");
```

Call `Toast.Shutdown()` from the loader's main-thread unload callback.

Shutdown immediately detaches the active behaviour. Calling `Initialize` again in the same frame is supported; callbacks from the old behaviour are ignored while Unity finishes its deferred destruction.

```csharp
Toast.Shutdown();
```

`Initialize` registers `ToastBehaviour` through `ClassInjector` and creates a dedicated `DontDestroyOnLoad` GameObject. It does not attach the component to the loader's plugin object, so duplicate initialization cannot destroy the plugin host.

## BepInEx 6

```csharp
public override void Load()
{
    Toast.Initialize();
    Toast.Success(NAME, $"v{VERSION} loaded");
}

public override bool Unload()
{
    Toast.Shutdown();
    return true;
}
```

## MelonLoader

```csharp
public override void OnInitializeMelon()
{
    Toast.Initialize();
    Toast.Success("MyMod", "Loaded");
}

public override void OnDeinitializeMelon()
{
    Toast.Shutdown();
}
```

## Notifications

All notification and configuration calls are thread-safe. Unity objects are only mutated from `Update` or `OnGUI` on the main thread.

On Android, configured sizes use a reference viewport with a 480-pixel short side, with a 0.85 multiplier for text. Text scales with the render target's short side, independently of the card-width limit, reported DPI, and screen orientation. For example, the default 16-unit body text renders at 20, 31, and 41 pixels on 720-, 1080-, and 1440-pixel short sides. This keeps its screen proportion consistent when games on the same phone use different render resolutions. Desktop retains the 768-pixel reference height, 1.6x layout ceiling, and optional DPI text increase of up to 1.25x.

Both renderers use symmetric text insets: 12 reference units from each side of the card, and 8 above and below the content. Android scales these insets and the title spacing with the short side as well, so text does not crowd the card edge at higher resolutions. At a 1080-pixel short side, horizontal insets are 27 pixels and vertical insets are 18 pixels. Titles and messages share the same horizontal bounds, and wrapping and card-height calculations use these same insets. Corner radius (8 reference units) and accent width (4.8 reference units) also scale with the Android short side, using the same non-overlapping background bands in both renderers.

IMGUI and uGUI share an explicit font. Android first requests system sans-serif families in a fixed order (Noto Sans CJK SC, Noto Sans CJK JP, Noto Sans SC, Noto Sans, Roboto, sans-serif). If dynamic font creation is unavailable, both renderers try `LegacyRuntime.ttf`, then `Arial.ttf`. Font availability and glyph coverage still depend on the device and Unity build; the library does not bundle a font. Font selection is logged once when loaded, and owned dynamic fonts are released on shutdown. IMGUI isolates and restores the drawing matrix, GUI colors, background tint, and enabled state. All backends use background alpha `0.94 × animationAlpha`, without inheriting a game's IMGUI background tint. If it cannot set the font or size, Toast switches to uGUI instead of displaying unstyled labels with the game's default small font. uGUI disables automatic text shrinking and rich-text interpretation.

Toast respects `Screen.safeArea`; card width transitions smoothly between square and 4:3 viewports, reaching 34% of the safe-area width at 4:3 and wider. The title-to-message gap follows 12.5% of the final title size, constrained to 2-4 reference units (scaled on Android). Title height follows the font size without an additional fixed minimum. Title bounds and estimated body line heights reserve 1.5 times the font size for taller CJK glyphs. Cards that no longer fit after an orientation or resolution change return to the waiting queue. Each card keeps its natural height while the complete stack fits; taller cards are reduced only when the stack exceeds the safe area. IMGUI uses `GUIStyle.CalcHeight`, and uGUI uses `Text.preferredHeight` after setting the content width and font size. If a measurement API is unavailable, that renderer caches the failure and uses the shared managed line estimate. All title paths use bold text and the same single-line ellipsis rule, preserving Unicode text elements. Title widths use native measurement where available, with a managed estimate as fallback. uGUI body measurements and formatted titles are cached until their inputs change. A stripped `Screen.safeArea` falls back to the full screen; a stripped desktop `Screen.dpi` falls back to viewport-based sizing.

The uGUI fallback creates a high-order overlay `Canvas` with non-interactive `Image` and legacy `Text` components. No `EventSystem` or raycaster is added, so notifications do not intercept game input.

```csharp
Toast.Info("Info", "Configuration loaded");
Toast.Success("Done", "Operation completed");
Toast.Warning("Warning", "Connection is unstable", 4f);
Toast.Error("Error", exception.Message, 6f);

bool accepted = Toast.Show("Custom", "Message", ToastKind.Info, 3f);
int count = Toast.Count;
Toast.Clear();
```

The queue accepts at most 50 notifications. `Show` returns `false` when it is full.
`Clear` discards obsolete pending notifications while retaining configuration. Pending configuration updates are merged, preserving the last valid value for each setting, so repeated configuration or clearing before the next frame does not grow the command queue indefinitely.
If the styled renderer degrades or is disabled, `Toast.LastRenderError` exposes the exception without allowing it to escape through the IL2CPP `OnGUI` trampoline.

`Toast.Renderer` reports the active backend as `ToastRendererKind.Ugui`, `ToastRendererKind.Imgui`, or `ToastRendererKind.None`. It starts as `Imgui` and changes to `Ugui` after a successful fallback. While a fallback is pending, or after every backend fails, it reports `None`.

```csharp
if (Toast.Renderer == ToastRendererKind.Ugui)
{
    // The optional Canvas renderer is active.
}
```

## Configuration

Configuration is applied on the Unity main thread and invalid values are clamped.

```csharp
Toast.Configure(
    width: 360f,
    minimumHeight: 140f,
    maximumVisible: 4,
    titleSize: 17,
    textSize: 14,
    anchor: ToastAnchor.BottomRight
);
```

`minimumHeight` is the card's lower height bound; longer wrapped messages can make a card taller. Android also reserves room for a title and one body line when determining how many cards fit, so larger configured fonts reduce the visible count before single-line messages get clipped.

Setting `maximumVisible` to `0` pauses display without expiring waiting notifications. Set it to a positive value to resume. The default is `4`; the effective value can be lower when the current safe area cannot fit that many minimum-height cards. The injected `ToastBehaviour` is internal; callers only use the static `Toast` interface.

## Loader logging

`Utility.Diagnostics.Logging` forwards structured diagnostic events without referencing either loader. Install the sink before calling `Toast.Initialize`. Replacing or removing a sink is atomic, and an exception thrown by the sink is retained in `Logging.LastSinkError` rather than crossing a Unity callback. Recoverable failures that lead to another fallback carry only a one-line exception type and message in `LogEntry.Message`; `LogEntry.Exception` is reserved for terminal failures after every available path has failed. Loader adapters can therefore print complete stacks only when the requested feature could not be completed.

BepInEx 6:

```csharp
using Utility.Diagnostics;

Logging.SetSink(entry =>
{
    string text = entry.Exception == null
        ? $"[{entry.Category}] {entry.Message}"
        : $"[{entry.Category}] {entry.Message}\n{entry.Exception}";

    switch (entry.Level)
    {
        case LogLevel.Debug:
            Log.LogDebug(text);
            break;
        case LogLevel.Warning:
            Log.LogWarning(text);
            break;
        case LogLevel.Error:
            Log.LogError(text);
            break;
        default:
            Log.LogInfo(text);
            break;
    }
});
```

MelonLoader:

```csharp
using Utility.Diagnostics;

Logging.SetSink(entry =>
{
    string text = entry.Exception == null
        ? $"[{entry.Category}] {entry.Message}"
        : $"[{entry.Category}] {entry.Message}\n{entry.Exception}";

    switch (entry.Level)
    {
        case LogLevel.Warning:
            MelonLogger.Warning(text);
            break;
        case LogLevel.Error:
            MelonLogger.Error(text);
            break;
        default:
            MelonLogger.Msg(text);
            break;
    }
});
```

Call `Logging.SetSink(null)` when the consuming mod unloads. Utility emits only lifecycle, backend selection, fallback, and terminal asset-loading events; it does not log per frame or repeatedly probe a stripped safe-area/DPI method.

## Prefix-neutral assets

`Utility.dll` contains no reference to `TMPro.TMP_FontAsset` or `Il2CppTMPro.TMP_FontAsset`. The consuming mod supplies the generated type as a generic argument.

BepInEx-generated namespace:

```csharp
using TMPro;
using Utility.Assets;

var font = new AssetBundleLoader<TMP_FontAsset>(bundlePath, "assets/fonts/my-font.asset");
StartCoroutine(font.Load()); // From the mod's existing MonoBehaviour coroutine host.
```

`Load` accepts optional success and error callbacks. Supplying `onError` handles terminal load
failures without letting them escape through the coroutine; the same exception remains available
through `LastError`.

```csharp
StartCoroutine(font.Load(
    onComplete: () => Toast.Success("Assets", "Font loaded"),
    onError: exception => Toast.Error("Assets", exception.Message)
));
```

MelonLoader-generated namespace with an `Il2Cpp` prefix:

```csharp
using Il2CppTMPro;
using Utility.Assets;

var font = new AssetBundleLoader<TMP_FontAsset>(bundlePath, "assets/fonts/my-font.asset");
MelonCoroutines.Start(font.Load());
```

Use the exact AssetBundle asset name. The loader intentionally uses `LoadAsset(name)` instead of enumerating all assets, reducing reliance on methods that may be absent in heavily stripped games. The generic type must be the wrapper namespace generated by the active loader.

The single-argument constructor performs automatic discovery:

```csharp
var font = new AssetBundleLoader<TMP_FontAsset>(bundlePath);
```

It uses `LoadAllAssets(Il2CppType.Of<UnityEngine.Object>())` and then falls back to the matching explicit-type `LoadAllAssetsAsync` path. This avoids the parameterless wrappers and keeps both paths consistent. Each attempt is isolated because stripped players may retain only one of them. It never calls `GetAllAssetNames()`. The explicit asset-name constructor remains the recommended path for maximum compatibility.

Run and dispose the `Load` iterator on Unity's main thread. Its `finally` block resets `IsLoading` and unloads an acquired bundle, including when the iterator is disposed early or a Unity wrapper throws. Disposing while `LoadFromFileAsync` is pending retrieves its result synchronously before unloading it; this can wait for native loading to finish. Stopping a coroutine must also dispose its iterator for this cleanup to run. An asset is published only after persistence setup succeeds, so a setup failure leaves `IsLoaded` false and permits a later retry. `IsLoaded` deliberately avoids Unity's native object truthiness operators.

## IL2CPP stripping

Only the native-pointer constructor and Unity lifecycle messages of `ToastBehaviour` are exposed during class injection. Its managed detach helper is marked `HideFromIl2Cpp`. Queues, nullable configuration, and rendering state stay in ordinary managed classes.

The uGUI fallback is part of `Utility.dll` and requires the Unity UI modules. It shares the font provider with IMGUI, lazily creates and reuses card objects, and builds rounded card backgrounds from `Image` bands. Titles and bodies wrap inside their text rectangles and truncate vertically, preventing long titles from extending beyond the card. If this final fallback also fails, rendering is disabled without retrying either backend every frame.

The IMGUI renderer uses `Texture2D.whiteTexture` rather than creating and populating a texture at runtime. This removes image conversion and per-pixel texture methods from the required surface.

IMGUI backgrounds first use `GUI.Box` with `GUIStyleState.background`, then try `GUI.DrawTexture` if the first path fails. Failure of both disables IMGUI and queues uGUI. Styled typography can fall back to simpler GUIStyle-based labels, which still require an explicit font, font size, and clipping. It does not fall back to unstyled labels or allow text to escape the card. Failed capabilities are cached for the renderer lifetime.

A mod cannot restore a Unity method that the game developer already removed from `GameAssembly`. `link.xml` shipped with a runtime-loaded mod cannot change that. Toast requires at least one complete backend and a usable dynamic or built-in font: uGUI needs the Unity UI modules, while IMGUI needs its GUI and text-rendering surface.

## Verification

```shell
dotnet build Utility.sln -c Release
dotnet test Utility.sln -c Release --no-build
```

The metadata tests reject known-dangerous references, including Unity object truthiness operators, `DrawTextureWithTexCoords`, and `GetAllAssetNames`. They check IMGUI drawing references, absence of a concrete TextMeshPro dependency, and retention of uGUI and AssetBundle fallback paths. Managed tests cover layout, text estimates, and bounded command processing. Lifecycle tests link the production sources and simulate delayed destruction, cancellation during bundle/asset requests, and persistence failures followed by retries. These substitutes verify managed control flow; Android rendering, glyph coverage, and native loading behavior still require device validation.
