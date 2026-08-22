# Mr. Moonlight

First-person horror shooter. Aanniarvik Island, Alaska, 1979.
Unity 6.3 LTS, URP, **WebGL target, under 1 GB**, itch.io.

**Display target: 960×540, embedded in the itch.io page — not fullscreen, not 1920×1080.**
Decided 2026-08-21 (see `Docs/webgl-constraints.md` and `Docs/changelog.md`). All UI —
Canvas Scaler reference resolution, HUD layout, menus — targets **960×540**. This reverses
an earlier 1920×1080-fullscreen assumption; if you see that number anywhere, it's stale.

## Read first
1. `Claude Code Context MDs/kickstart.md` — how this project works
2. `Docs/webgl-constraints.md` — read before writing code
3. `Docs/unity-conventions.md` and `Docs/csharp-conventions.md`
4. `Docs/glossary.md` — canonical names (Tracey, Pickaxe, Furman)

## Source of truth
**Linear**, project `MrMoonlightDemo`, team `MRM`.
Design docs in `Docs/Design/` are background only.
If an issue and a document disagree, the issue wins.

## Hard rules
- No hardcoded values. Everything in `MoonlightTunables`. See `Docs/unity-conventions.md`.
- Scene-view and inspector work (placement, staging, wiring, saved scenes) is not an automatic
  handoff. Carlos is the only one who touches Unity, so when you can see a way to do it yourself
  via the UnityMCP bridge, **ask him for permission first** rather than silently stopping and
  handing off instructions. If he says yes, do it, verify by reading the actual
  component/scene state back, and document what changed. If he'd rather do it himself or doesn't
  answer, wait. See `Claude Code Context MDs/kickstart.md` §B.3.
- One issue, one branch, one PR.
- Never commit or push. The developer uses GitHub Desktop.

## Deadlines
Sept 1 — playable loop, graded class gate.
Sept 8 — polished itch.io release.
