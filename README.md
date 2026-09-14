# Utility

General-purpose utilities for Unity IL2CPP mods: Toast, assets/fonts, diagnostics, and pure .NET JSON resource caching and string-table fingerprints.

```sh
git submodule update --init --recursive
python shared/ModEngineering/scripts/mod.py test
python shared/ModEngineering/scripts/mod.py build
```

Source: `src/Utility`; tests: `tests/Utility.Tests` and `tests/Utility.Lifecycle.Tests`. The library targets .NET 6, while tests target .NET 8. The engineering SDK is pinned in `global.json`. Seven Unity compile-time references are tracked under `dependencies/interop`; they are not deployment files. Consumers must not replace these contracts with arbitrary game exports.

`Utility.Caching.JsonResourceCache` contains no Unity calls. `LoadAsync` memoizes a resource and applies retry cooldown; `RefreshAsync` rereads disk on every request with refresh, local-first, or local-only selection. Both support caller-supplied validation, atomic writes, corrupt-cache recovery, cancellation, and stale-file fallback. The caller owns HttpClient, resource identities, paths, schemas, validation rules and notifications. Utility contains no game-specific manifest or language policy.

`Utility.Cryptography.StringTableHash` computes deterministic fingerprints of caller-ordered string pairs or nested JSON string tables. It defines byte encoding and separators, not application-specific dictionary categories.

Cache retention is bounded by LRU (256 resource keys by default); idle resource gates are reclaimed. `InvalidateAsync` waits for current work on a key before clearing its retained values and cooldown. `JsonResourceCacheOptions` configures retention, the streamed download limit (64 MiB by default), and explicit serializer settings/converters shared by local and remote reads. Existing callers retain their JSON conventions when no serializer settings are supplied.

Mods reference `shared/Utility/src/Utility/Utility.csproj` at a fixed Git commit. `SharedDependencies.local.props` can select a sibling checkout. CI uses pinned dependencies. Standard solutions include pinned projects; `scripts/solution.ps1 --local` generates an ignored solution containing the actual local projects and tests. Shared outputs are isolated for command-line builds; VS uses each loaded project's own output paths.

Engineering implementation now lives in [ModEngineering](shared/ModEngineering/README.md). `scripts/New-ModSolution.ps1` remains a forwarding entry point for existing users.

See [API](docs/API.md) for Unity runtime capabilities and limitations.
