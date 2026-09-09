# Compile-time references

`interop/` contains only the seven generated assemblies required by `Utility.csproj` and its tests. They form a fixed compile-time contract, independent of an individual Mod's game-specific assemblies. Keep full Interop exports outside Git.

Do not distribute these DLLs with Utility. The active IL2CPP loader provides the runtime wrappers.
