# Agent Note: Android native toast View

Status: proposed

## Problem

On the same Android device, games can expose different Unity render sizes while occupying the same physical screen area. The existing IMGUI and uGUI backends can keep the toast's relative size consistent, but a low-resolution Unity UI buffer can blur text after display upscaling. A reported player could not resolve native ICalls needed by both Unity backends, including `GUIStyle.set_font_Injected` and `Canvas.set_renderMode_Injected`; engine stripping or a Unity/proxy version mismatch may cause this.

## Proposal

Add an Android-only native View renderer as an optional preferred backend for physical-pixel clarity and as a final fallback when Unity renderers fail. Keep the current Unity renderers for other platforms and for Android installations where the native bridge cannot start. The renderer uses the app Activity's view hierarchy, draws above the Unity surface without intercepting touch, and owns its UI-thread lifecycle. Pass immutable toast snapshots from the managed queue to the Android UI thread; convert viewport anchors, safe insets, sizes, colors, rounded corners, typography, and animation alpha into physical-screen coordinates. Share the existing theme values and behavioral rules so backend selection does not alter toast content or intended layout.

Start with a small integration spike: confirm that a distributable Java bridge can be loaded by both supported Android mod loaders, attached above the Unity surface, and detached on shutdown without relying on Unity IMGUI, uGUI, or AndroidJava ICalls that the target player may have stripped. If that distribution path fails, revise the packaging design before adding the full renderer.

## Alternatives considered

Increasing the game's render resolution could sharpen both game and toast, but changes game performance and may be reset by the player. Enlarging Unity font sizes or rendering into a high-resolution texture that is later composited into a low-resolution Unity target cannot restore physical pixel detail. A [guarded Canvas render-mode fallback](../../implemented/bug-fix/2026-09-30-optional-canvas-render-mode-setter.md) can rescue some uGUI initializations, but it does not address other stripped ICalls or low-resolution output.

## Acceptance criteria

- On one device, toast screenshots from two games with different `Screen.width/height` but the same display shape have comparable physical size and text sharpness.
- Toast colors, corners, accent, text hierarchy, stacking, fade timing, safe-area placement, and title truncation match the existing renderers within measured pixel tolerances.
- The native View neither consumes game touch input nor survives `Toast.Shutdown()`; rotation and Activity recreation do not orphan it.
- A game missing both reported Unity ICalls can show a toast through the native backend. If the bridge is unavailable, the existing renderer failure is reported without a crash.
- The bridge works in a documented package with both BepInEx and MelonLoader Android targets, or the supported loader scope is explicitly narrowed after the integration spike.

## Risks

Android view ordering varies with Unity's SurfaceView/TextureView setup, and an Activity may be replaced during the game lifecycle. Java helper packaging and JNI access are loader-dependent. Native Android text rasterization can differ from Unity, so visual parity requires device screenshots. The current [band geometry note](../../implemented/bug-fix/2026-09-29-toast-band-seams-and-stripped-background-tint.md) covers the Unity paths and remains relevant when this backend is unavailable.
