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

### Visual Studio

Visual Studio restores and builds the projects loaded in the solution. Include
the pinned Utility project (and Adapter when used) in each Mod's standard solution.
Standard Visual Studio solutions ignore machine-local shared source overrides.

To edit a sibling Utility checkout, configure the Mod's
`SharedDependencies.local.props`, then run from the Mod repository:

```powershell
pwsh -NoProfile -File shared/Utility/scripts/New-ModSolution.ps1 -Project GCMod/GCMod.csproj
```

Replace the project path with the Mod's actual project. Open the generated
`<repository>.local.slnx` in Visual Studio 2022 17.14 or newer. The generator adds
the actual shared projects selected by the local configuration. Keep this file
out of Git and regenerate it when changing shared project paths.

Visual Studio builds shared projects into their own `bin` and `obj` directories;
reference metadata must use those same paths. Command-line project builds retain
the per-Mod output isolation under `artifacts/shared`.

See [docs/API.md](docs/API.md) for usage and runtime limitations.
