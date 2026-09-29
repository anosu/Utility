# Agent Note: Optional Canvas render-mode setter

Status: implemented

## Problem

An Android player failed both toast backends: `GUIStyle.set_font_Injected` was unresolved in IMGUI, then `Canvas.set_renderMode_Injected` was unresolved while initializing uGUI. Its IL2CPP dump shows getter-only `GUIStyle.font` and `Canvas.renderMode`, while the uGUI `Text.font`, Canvas sorting, and RectTransform setters used by the fallback are retained. A required call to set an already correct Canvas mode prevented the remaining uGUI path from being tried.

## Decision

uGUI attempts to set a new Canvas to `ScreenSpaceOverlay`. If the setter throws, it reads the Canvas mode and proceeds only when the mode is already `ScreenSpaceOverlay`. The setter and getter calls are isolated in non-inlined methods so either missing proxy member can fail within the guarded path. Sorting setup remains required. The current IMGUI explicit-font contract remains unchanged; silently using the game's font would change the toast typography.

## Alternatives considered

Removing the setter unconditionally would require trusting the Canvas default in every Unity build. Continuing after any setter failure without reading the mode could report uGUI as active while drawing an invisible Canvas. Keeping the setter required would lose a potentially usable backend in this player. The guarded read uses a retained getter shown in the supplied dump and preserves a visible failure if the mode is unsuitable.

## Consequences

The reported `Canvas.set_renderMode_Injected` failure is no longer terminal if the new Canvas is already an overlay. Other Canvas, Image, Text, or RectTransform calls may still fail at runtime; the dump establishes managed metadata presence, not successful native ICall resolution or a device-rendered toast. Controlled tests cover overlay and non-overlay modes, and the normal build passes. The planned [Android native View backend](../../proposed/feature/2026-09-30-android-native-toast-view.md) remains a separate route for physical-pixel clarity and games where Unity UI is unusable.
