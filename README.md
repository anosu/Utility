# Utility

Shared IL2CPP runtime utilities for BepInEx and MelonLoader: Toast, assets/fonts, diagnostics, and pure .NET translation caching.

```sh
git submodule update --init --recursive
python shared/ModEngineering/scripts/mod.py test
python shared/ModEngineering/scripts/mod.py build
```

Source: `src/Utility`; tests: `tests/Utility.Tests` and `tests/Utility.Lifecycle.Tests`. The library targets .NET 6, while tests target .NET 8. The engineering SDK is pinned in `global.json`. Seven Unity compile-time references are tracked under `dependencies/interop`; they are not deployment files. Consumers must not replace these contracts with arbitrary game exports.

`Utility.Translation.JsonResourceCache` contains no Unity calls. It merges requests, verifies caller-supplied protocol hashes, writes atomically, handles corrupt caches, propagates cancellation, and falls back to readable stale files. Games retain their own URL construction, manifests, loading policy and Toast messages. A cache instance represents one CDN/language session; callers own HttpClient.

Mods reference `shared/Utility/src/Utility/Utility.csproj` at a fixed Git commit. `SharedDependencies.local.props` can select a sibling checkout. CI uses pinned dependencies. Standard solutions include pinned projects; `scripts/solution.ps1 --local` generates an ignored solution containing the actual local projects and tests. Shared outputs are isolated for command-line builds; VS uses each loaded project's own output paths.

Engineering implementation now lives in [ModEngineering](shared/ModEngineering/README.md). `scripts/New-ModSolution.ps1` remains a forwarding entry point for existing users.

See [API](docs/API.md) for Unity runtime capabilities and limitations.
