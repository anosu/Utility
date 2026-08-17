# Utility API

`Utility.dll` provides loader-neutral IL2CPP toast notifications, structured diagnostics, and AssetBundle loading. It targets modern BepInEx 6 and MelonLoader releases that use Il2CppInterop.

The library does not reference either loader. Both environments use the same initialization path and the same binary.

## Installation

Copy `Utility.dll` next to the consuming mod. To enable the optional uGUI fallback, also copy `Utility.Notifications.Ugui.dll` beside it. Do not copy `Il2CppInterop.Runtime.dll` from the build output; the active loader supplies its compatible runtime.

The current build targets `net6.0` and requires these Unity modules at runtime:

- `UnityEngine.CoreModule`
- `UnityEngine.IMGUIModule`
- `UnityEngine.TextRenderingModule`
- `UnityEngine.AssetBundleModule` when `AssetBundleLoader` is used

`Utility.Notifications.Ugui.dll` additionally requires `UnityEngine.UIModule` and `UnityEngine.UI`. The core assembly does not reference those modules. Toast prefers IMGUI; it loads the optional assembly only after both styled and compatibility IMGUI rendering fail.

The proxies under `Utility/lib/Proxies` are the default local compile-time contract. To validate against another game's generated surface, build with `-p:UnityProxyDir=/path/to/generated/proxies`; optional Unity calls are isolated behind renderer or asset-loading fallback paths, but no runtime-loaded mod can call a native method that the target game removed entirely.

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
| `Utility.Ugui.dll` | `Utility.Notifications.Ugui.dll` |

## Project layout

- `Utility/Assets` contains the loader-neutral AssetBundle module.
- `Utility/Diagnostics` contains structured host logging.
- `Utility/Notifications` exposes the toast interface; renderer state and the injected behaviour live under `Internal`.
- `Utility.Notifications.Ugui` is an optional adapter and is not referenced by the core assembly.
- `Utility.Tests` mirrors those modules and keeps IL2CPP metadata checks under `Compatibility`.
- `Directory.Build.props` owns shared compiler, proxy-path, and Il2CppInterop settings.

## Toast lifecycle

Call `Toast.Initialize()` once from the loader's main-thread initialization callback. Notifications submitted before initialization remain queued.

```csharp
using Utility.Notifications;

Toast.Initialize();
Toast.Success("Utility", "Loaded successfully");
```

Call `Toast.Shutdown()` from the loader's main-thread unload callback.

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

All notification and configuration calls are thread-safe. Unity objects are only mutated from `Update` or `OnGUI` on the main thread. Toast respects `Screen.safeArea` and scales its configured dimensions from a 768-pixel-high reference viewport, up to 1.6x on larger render targets. The card-width limit transitions smoothly between square and 4:3 viewports, reaching 34% of the safe-area width at 4:3 and wider, so desktop 1080p receives a readable size increase without allowing a high-DPI mobile card to approach half the screen. Physical DPI can provide a modest text-only increase of up to 1.25x, but never controls card geometry. The title-to-message gap follows 25% of the final title size and is constrained to 5-8 pixels, keeping mobile and desktop proportions consistent. Cards that no longer fit after an orientation or resolution change return to the waiting queue. Each card keeps its natural height while the complete stack fits; taller cards are reduced only when the stack actually exceeds the safe area. The uGUI and compatibility renderers use a managed width estimate that distinguishes narrow text from CJK, full-width, and surrogate-pair characters. If `Screen.safeArea` or `Screen.dpi` is stripped, the layout falls back to the full screen and viewport-based sizing. If IMGUI fails and `Utility.Notifications.Ugui.dll` is available, Toast creates a high-order overlay `Canvas` with non-interactive `Image` and legacy `Text` components. No `EventSystem` or raycaster is added, so notifications do not intercept game input.

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

`minimumHeight` is the card's lower height bound; longer wrapped messages can make a card taller.

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

`Load` always resets `IsLoading` and unloads an acquired bundle from its iterator `finally` block, including when the coroutine is disposed early or a Unity wrapper throws. `IsLoaded` reports whether a managed asset wrapper was loaded; it deliberately does not invoke Unity's native object truthiness operators.

## IL2CPP stripping

The injected `ToastBehaviour` contains only the native-pointer constructor and Unity lifecycle messages. Queues, nullable configuration, and rendering state stay in ordinary managed classes and are not exposed during class injection.

The optional uGUI renderer is loaded through a small internal contract only after IMGUI becomes unusable, keeping `UnityEngine.UI` out of `Utility.dll` metadata. It uses `LegacyRuntime.ttf` with an `Arial.ttf` fallback, lazily creates and reuses card objects, and uses Unity's built-in sliced `UISprite` for rounded card corners. A missing cosmetic sprite only produces square cards. If this final fallback also fails, rendering is disabled without retrying either backend every frame.

The IMGUI renderer uses `Texture2D.whiteTexture` rather than creating and populating a texture at runtime. This removes image conversion and per-pixel texture methods from the required surface.

It also avoids `GUIStyleState.background`, custom runtime textures, and `DrawTextureWithTexCoords`. Some loader-generated Unity proxy assemblies omit the style setter, while custom texture wrappers can become invalid on affected Il2CppInterop runtimes. If another optional styled-IMGUI method is missing, the renderer switches to a colored fallback that retains the dark background, accent bar, title color, and body color. Compatibility IMGUI probes `fontSize` separately from `fontStyle`: when the size setter survives stripping it keeps the responsive typography without requiring bold text; otherwise it uses Unity's default font size with compact title spacing. If the basic GUI methods are also unavailable, rendering is disabled instead of throwing on every `OnGUI` invocation.

A mod cannot restore a Unity method that the game developer already removed from `GameAssembly`. `link.xml` shipped with a runtime-loaded mod cannot change that. Toast can operate when at least one complete backend remains: uGUI requires the Unity UI modules and a built-in legacy font, while IMGUI requires its GUI and text-rendering surface.

## Verification

```shell
dotnet build Utility.sln -c Release
dotnet test Utility.sln -c Release --no-build
```

The tests inspect `Utility.dll` metadata and reject known-dangerous references, including Unity object truthiness operators, `GUIStyleState.set_background`, `DrawTextureWithTexCoords`, and `GetAllAssetNames`. They verify that the core has no hard Unity UI dependency, that IMGUI failure retains the optional uGUI fallback, and that all AssetBundle fallback paths remain present. Pure managed layout tests cover safe-area constraints, smooth orientation transitions, mixed natural card heights, shared renderer metrics, CJK text width, CRLF, and surrogate pairs.
