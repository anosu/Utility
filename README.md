# Utility

Loader-neutral IL2CPP utilities for BepInEx and MelonLoader: toast notifications, diagnostics, and AssetBundle loading.

## Build

Requires .NET SDK 8 or newer.

```sh
dotnet test Utility.sln -c Release
```

The library targets .NET 6. Its seven compile-time Unity/Interop assemblies are tracked in `dependencies/interop`, so a fresh checkout builds without a game installation. These references are not deployment files; the game's loader supplies the runtime assemblies. `UnityProxyDir` can override the compile-time contract for compatibility testing.

## Use from a Mod

Add this repository as a submodule and reference `Utility/Utility.csproj` with `ProjectReference`. Building the Mod builds Utility and copies `Utility.dll` to the Mod's output directory automatically.

Consumers can pass `SharedDependencyBuildRoot` through ProjectReference metadata to isolate the intermediate and output directories. Use a different absolute directory for each consuming repository when sharing a local Utility checkout.

Each Mod pins a tested submodule commit. Local development may opt into a sibling checkout through an ignored configuration file; CI must build the pinned commit.

See [docs/API.md](docs/API.md) for usage and runtime limitations.
