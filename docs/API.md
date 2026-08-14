# Utility API

`Utility.dll` provides a loader-neutral IL2CPP toast renderer and an AssetBundle helper. It targets modern BepInEx 6 and MelonLoader releases that use Il2CppInterop.

The library does not reference either loader. Both environments use the same initialization path and the same binary.

## Installation

Copy `Utility.dll` next to the consuming mod. Do not copy `Il2CppInterop.Runtime.dll` from the build output; the active loader supplies its compatible runtime.

The current build targets `net6.0` and requires these Unity modules at runtime:

- `UnityEngine.CoreModule`
- `UnityEngine.IMGUIModule`
- `UnityEngine.TextRenderingModule`
- `UnityEngine.AssetBundleModule` when `FontHelper` is used

The checked-in proxies are the default compile-time contract. To validate against another game's generated surface, build with `-p:UnityProxyDir=/path/to/generated/proxies`; optional Unity calls are isolated behind renderer or asset-loading fallback paths, but no runtime-loaded mod can call a native method that the target game removed entirely.

## Toast lifecycle

Call `Toast.Initialize()` once from the loader's main-thread initialization callback. Notifications submitted before initialization remain queued.

```csharp
using Utility.Toast;

Toast.Initialize();
Toast.Success("Utility", "Loaded successfully");
```

Call `Toast.Shutdown()` from the loader's main-thread unload callback.

```csharp
Toast.Shutdown();
```

`Initialize` registers `ToastUI` through `ClassInjector` and creates a dedicated `DontDestroyOnLoad` GameObject. It does not attach the component to the loader's plugin object, so duplicate initialization cannot destroy the plugin host.

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

```csharp
Toast.Info("Info", "Configuration loaded");
Toast.Success("Done", "Operation completed");
Toast.Warn("Warning", "Connection is unstable", 4f);
Toast.Error("Error", exception.Message, 6f);

bool accepted = Toast.Show("Custom", "Message", ToastUI.TYPE_INFO, 3f);
int count = Toast.Count;
Toast.Clear();
```

The queue accepts at most 50 notifications. `Show` returns `false` when it is full.
If the styled renderer degrades or is disabled, `Toast.LastRenderError` exposes the exception without allowing it to escape through the IL2CPP `OnGUI` trampoline.

## Configuration

Configuration is applied on the Unity main thread and invalid values are clamped.

```csharp
Toast.Configure(
    width: 360f,
    maxHeight: 140f,
    max: 4,
    titleSize: 17,
    textSize: 14,
    anchor: ToastUI.ANCHOR_BR
);
```

For compatibility with the original renderer, `maxHeight` retains its historical behavior as the minimum card height; longer wrapped messages can make a card taller.

Setting `max` to `0` pauses display without expiring waiting notifications. Set it to a positive value to resume.

`ToastUI.Instance` and its instance methods remain available for source compatibility, but new code should use the static `Toast` interface.

## Prefix-neutral assets

`Utility.dll` contains no reference to `TMPro.TMP_FontAsset` or `Il2CppTMPro.TMP_FontAsset`. The consuming mod supplies the generated type as a generic argument.

BepInEx-generated namespace:

```csharp
using TMPro;
using Utility.Fonts;

var font = new FontHelper<TMP_FontAsset>(bundlePath, "assets/fonts/my-font.asset");
StartCoroutine(font.LoadAsync()); // From the mod's existing MonoBehaviour coroutine host.
```

MelonLoader-generated namespace with an `Il2Cpp` prefix:

```csharp
using Il2CppTMPro;
using Utility.Fonts;

var font = new FontHelper<TMP_FontAsset>(bundlePath, "assets/fonts/my-font.asset");
MelonCoroutines.Start(font.LoadAsync());
```

Use the exact AssetBundle asset name. The helper intentionally uses `LoadAsset(name)` instead of enumerating all assets, reducing reliance on methods that may be absent in heavily stripped games.

For assets that do not need a concrete generated type, use the non-generic `FontHelper`, whose `Asset` property is a `UnityEngine.Object`.

The original single-argument constructor remains available for source compatibility:

```csharp
var font = new FontHelper<TMP_FontAsset>(bundlePath);
```

It uses `LoadAllAssets(Il2CppType.Of<UnityEngine.Object>())` and then falls back to the matching explicit-type `LoadAllAssetsAsync` path. This avoids the parameterless wrappers and keeps both paths consistent. Each attempt is isolated because stripped players may retain only one of them. It never calls `GetAllAssetNames()`. The explicit asset-name constructor remains the recommended path for maximum compatibility.

`LoadAsync` always resets `Loading` and unloads an acquired bundle from its iterator `finally` block, including when the coroutine is disposed early or a Unity wrapper throws. `Valid` reports whether a managed asset wrapper was loaded; it deliberately does not invoke Unity's native object truthiness operators.

## IL2CPP stripping

The injected `ToastUI` contains only the native-pointer constructor and Unity lifecycle messages. Queues, nullable configuration, and rendering state stay in ordinary managed classes and are not exposed during class injection.

The renderer uses `Texture2D.whiteTexture` rather than creating and populating a texture at runtime. This removes image conversion and per-pixel texture methods from the required surface.

It also avoids `GUIStyleState.background`, custom runtime textures, and `DrawTextureWithTexCoords`. Some loader-generated Unity proxy assemblies omit the style setter, while custom texture wrappers can become invalid on affected Il2CppInterop runtimes. If another optional styled-IMGUI method is missing, the renderer switches to a colored fallback that retains the dark background, accent bar, title color, and body color. If the basic GUI methods are also unavailable, rendering is disabled instead of throwing on every `OnGUI` invocation.

A mod cannot restore a Unity method that the game developer already removed from `GameAssembly`. If the game omits the entire IMGUI or TextRendering module, the toast renderer cannot operate; `link.xml` shipped with a runtime-loaded mod cannot change that. Such games require a separate renderer backed by a module they include.

## Verification

```shell
dotnet build Utility.sln -c Release
dotnet test Utility.sln -c Release --no-build
```

The tests inspect `Utility.dll` metadata and reject known-dangerous references, including Unity object truthiness operators, `GUIStyleState.set_background`, `DrawTextureWithTexCoords`, and `GetAllAssetNames`. They also verify the explicit-type synchronous and asynchronous enumeration paths, bundle-file fallback, and named-asset fallback.
