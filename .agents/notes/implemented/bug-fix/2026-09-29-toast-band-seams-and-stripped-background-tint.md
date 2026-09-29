# Agent Note: Toast band seams and stripped background tint

Status: implemented

## Problem

The toast background consists of seven adjacent translucent rectangles. Their floating-point edges can fall between screen pixels after Android scaling or card-height fitting. Native renderers can blend the edges separately, exposing a thin line. IMGUI also read `GUI.backgroundColor` before choosing a background backend, so a stripped accessor prevented its `GUI.DrawTexture` fallback from running.

## Decision

The shared band geometry rounds every horizontal boundary and inset to pixels, and both renderers round card bounds before drawing. The bands remain adjacent and do not overlap, preserving the configured background alpha. IMGUI treats `GUI.backgroundColor` as a capability of the styled-box backend: if its getter or setter fails, it selects `GUI.DrawTexture` while preserving the other GUI state. Desktop font lookup tries an OS font after both built-in resource names fail. Both renderers continue to use the same font provider and band geometry.

## Alternatives considered

A single generated rounded texture or uGUI sprite would remove band boundaries entirely. It would require additional texture creation, pixel upload, or sprite APIs that may also be stripped in the target game. Overlapping adjacent bands would hide gaps, but their translucent alpha would darken the overlap. Pixel-aligned boundaries avoid both requirements.

## Consequences

Scaled corners change by at most one pixel per step. The fallback can display a matching card when `GUI.backgroundColor` or built-in font resources are unavailable, provided the selected rendering and font APIs still exist. No runtime-loaded mod can restore native Unity methods removed from the game. Unit tests verify rounded boundaries and the missing-tint path; visual verification in a target Unity player remains necessary for driver-specific rasterization behavior.
