# Agent Note: Android native toast View

Status: implemented

## Problem

Games on the same Android device can expose different Unity render sizes, blurring toast text in lower-resolution Unity UI buffers. The reported IL2CPP player also lacks `GUIStyle.set_font_Injected` and `Canvas.set_renderMode_Injected`. Its new Canvas starts in World Space, so checking its mode after the failed setter does not recover uGUI.

## Decision

Android prefers an Activity View renderer. The existing managed toast queue owns content, duration, and fade timing; `AndroidToastRenderer` serializes active cards and theme values into an immutable UTF-8 snapshot. A small JNI library obtains the Java VM and current Unity Activity, loads an embedded DEX with `InMemoryDexClassLoader`, and calls a Java helper. The helper attaches a non-interactive View above the Unity host and draws the cards in the View's physical pixels. This path calls neither Unity IMGUI nor uGUI. If native startup or presentation fails, Toast selects the existing IMGUI and uGUI paths.

The DEX and ARM64 native library are embedded in `Utility.dll`, matching the existing single-DLL Android Mod package and the 64-bit loader requirement. The DEX is loaded from memory to avoid Android 14's writable-DEX restriction. An optional reflective `AndroidJNI.GetJavaVM` retry only runs if the JNI library cannot locate the Java VM itself. The current managed `ToastBehaviour` still provides frame timing, so this backend addresses stripped drawing ICalls, not stripping of all Unity lifecycle APIs. Startup and layout corrections are recorded in [the follow-up bug fix](../bug-fix/2026-09-30-android-toast-startup-and-layout.md).

## Interface and lifecycle

`Toast.Show`, `Configure`, `Clear`, `Count`, and `Shutdown` remain unchanged. `Toast.Renderer` adds `AndroidView`. The Java helper acknowledges startup after the View is attached; later UI-thread errors are reported on the next frame, triggering the managed fallback. Presentation is coalesced to the latest snapshot on the Android UI thread. Activity replacement reattaches the View, and shutdown removes it. The View returns false for touch input.

Layout uses the actual host View bounds and window insets, with the same 480-pixel Android reference short side and theme values as Unity renderers. The native renderer uses Android sans-serif typography and a single antialiased rounded background shape, eliminating the seven-band seam. Text metrics can differ slightly from Unity's font renderer.

## Alternatives considered

Reusing a game's existing screen-space Canvas avoids its missing setter but depends on a suitable Canvas, hierarchy ordering, and scene lifetime. A serialized Overlay prefab bypasses the setter but adds Unity-version-specific assets and still renders into a potentially low-resolution Unity target. Raising the game's render resolution changes its performance. `Adapter-Android` demonstrates in-memory DEX loading through `MelonLoader.Java`; Utility uses a JNI library so the backend does not acquire a compile-time dependency on one mod loader.

## Consequences

The managed build and tests pass, and the Android DEX and ARM64 JNI library compile and are embedded in `Utility.dll`. Android View startup has been verified on a connected MelonLoader game; touch pass-through, Activity recreation, BepInEx loading, and visual parity remain unverified. On unsupported ABI or Android below API 26, native startup fails and the existing Unity backends remain available. The Java helper and JNI library are rebuilt with `build_android.py` when their source changes; normal Mod builds use the embedded binaries.
