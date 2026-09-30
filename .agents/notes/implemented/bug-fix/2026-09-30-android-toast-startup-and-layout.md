# Agent Note: Android toast startup and layout corrections

Status: implemented

## Problem

On an Android 15 MelonLoader game, the first native View implementation loaded its ARM64 library but `toast_start` returned failure. The Unity IMGUI and uGUI fallbacks could not draw because the player stripped their required ICalls. The bridge compressed every startup failure into zero, making the failing JNI stage invisible in mod logs. The same implementation embedded an unused 32-bit library, submitted snapshots every game frame even with no toast, rebuilt Android text layouts during fades, and treated root-window insets as if they were relative to the toast View.

## Decision

The managed VM fallback resolves `UnityEngine.AndroidJNI` from `UnityEngine.AndroidJNIModule` or another loaded assembly. The native bridge reports distinct startup stages. VM, JNI environment, Activity availability, and View attachment failures are retried every 0.25 seconds for up to five seconds before selecting the Unity fallbacks; other failures fall back immediately. Pending notifications wait during this startup window.

The Android backend embeds only ARM64, matching the supported Android mod loaders. It skips empty and unchanged snapshots, while the View reuses `StaticLayout` objects when message text, text size, and content width remain unchanged. Root-window insets are transformed into the overlay View's coordinates before calculating safe bounds.

When no cards or commands need processing, including paused display with waiting cards, the runtime skips layout and viewport JNI work. With active cards, the renderer compares layout, theme version, card identity, and alpha before allocating a JSON snapshot; unchanged frames use a half-second JNI maintenance call to detect Activity replacement and asynchronous View errors without copying or parsing JSON. JNI viewport storage is reused. Java coalesces pending frames under one lock before parsing the latest JSON on the UI thread. Start and stop invalidate queued callbacks by generation so an old callback cannot affect a newly attached View. The View also reuses measured titles and heights and avoids an offscreen alpha layer for fully opaque cards.

This corrects the startup and performance limitations of the [native View decision](../feature/2026-09-30-android-native-toast-view.md) without changing the public Toast API.

## Alternatives considered

`Adapter-Android` obtains JNI through MelonLoader, but Utility also serves BepInEx, so a loader-specific API is not a shared solution. Retrying every failure indefinitely would repeatedly load a broken bridge on unsupported Android versions; the bounded retry is limited to stages that can become ready after mod initialization. Submitting identical JSON every frame reliably observes UI failures and Activity changes but wastes JNI and Java allocations; the active-only heartbeat bounds detection delay while idle processing remains dormant.

## Consequences

The connected game's startup log now selects `AndroidView` where the original build reported `Android toast bridge could not start`. The resource build and managed tests pass. View appearance, touch behavior, Activity replacement, and BepInEx startup still require their own validation. The retry window can delay the Unity fallback by up to five seconds when a transient startup stage never becomes available.
