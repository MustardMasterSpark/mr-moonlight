# Mr. Moonlight — Design Context Index

**Purpose.** These five files replace the four source PDFs (Screenplay, Pitch, Style, Character Profiles). Load these instead. Do not open the PDFs — they are image-heavy and expensive, and everything text-bearing in them is reproduced here.

**Status.** These are **background/context**, not the source of truth.
The source of truth for what gets built is **Linear** (project `MrMoonlightDemo`). If a Linear issue contradicts anything in these files, the Linear issue wins. If you find a contradiction, flag it to the developer instead of silently picking one.

| File | Contains | Read it when |
|---|---|---|
| `01-screenplay-demo.md` | Beat-by-beat flow of the Day 1 demo, scene by scene, with the gameplay/event-director extraction | Building the event director, cutscenes, dialogue, level scripting, staging any location |
| `02-pitch-document.md` | Genre, scope, art direction, systems, full enemy/item catalogue, MVP-vs-full-game boundary | Deciding whether a feature is in scope; designing systems; naming things |
| `03-style-guide.md` | Visual style, enemy aesthetic, 3D character rules, audio direction | Any art, shader, UI, VFX or audio decision |
| `04-character-profiles.md` | The 8 characters: physical, voice, personality, wardrobe, spoilers | Writing dialogue, directing VO, modelling/rigging characters, animation intent |

---

## Canonical spellings — use these everywhere in code and assets

The source documents disagree with each other. These are the rulings. **Use these exact strings for class names, prefabs, animation states, audio folders and Linear issue titles.**

| Canonical | Also appears in sources as | Note |
|---|---|---|
| **Tracey** | Tracy, Stacy, Staycy, Stacey | Player character. Screenplay and Character Profiles both use *Tracey*. `linear issues.txt` uses *Tracy* throughout — that file is wrong on this point. |
| **Rylee** | Riley | |
| **Furman** | Fureman, Furyman, furman | Boss creature of the demo |
| **Zealot** | seller (dictation error) | Melee cultist |
| **Spotter** | — | Ranged cultist with lamp |
| **Vernon** | — | |
| **Shannon** | — | |
| **Aanniarvik** | — | The island |
| **Claude** | Cloud, Klaus, CloudScroll | Dictation errors in `linear issues.txt` |

---

## Contradictions between sources — RESOLVED by the developer, 2026-08-20

1. **Melee weapon → `Pickaxe`.** ✅ Ruled. The Pitch and Screenplay call it the Inuit Pickaxe; `linear issues.txt` calls it "the axe". **Canonical name is `Pickaxe`. Mechanics are exactly as written in the Linear issue** (3-swing combo: horizontal L→R medium, horizontal R→L medium, vertical top-down highest; resets on unequip/switch; costs stamina; speed penalty).

2. **Trap on the road to the cabin → `punji trap`.** ✅ Ruled. Insta-kill with its own long death sequence, independent of current health. The bear trap in the Screenplay is not built for the demo.

3. **Normal death → one system.** ✅ Ruled. `linear issues.txt` describes it twice ("Player gets Killed" and "death/blackout"). Treat as **one** system; the "death/blackout" version is the complete spec.

4. **Shotgun origin → RESOLVED, and it added a new mechanic.** ✅ Ruled. Two sources in the demo:
   - **Scott's shotgun**, found near him in the mine.
   - **Dead Spotters.** A Spotter's shotgun **falls from his other hand exactly as the lamp falls from the first** — detaches, drops with acceleration, becomes an interactable pickup.

   **Pickup rules:** no shotgun yet → added to weapon selection. Already has one → **+2 shells** and the floor shotgun disappears.

   **Universal pickup rule:** *all* interacted pickable objects are removed from the world scene and added to the inventory. (More inventory detail to come from the developer.)

5. **Downed-but-alive enemies → NEW ISSUE, approved.** ✅ From `Style.pdf`: *"Some enemies, when you take them down, they won't be dead, they will be on the floor, yelling and moaning in pain."* A downed state with a looping pain vocalisation, for Spotters and Zealots only. Not in the original issue list; approved 2026-08-20.

6. **Name spellings → `Tracey`, `Rylee`, `Furman`.** ✅ Ruled. The developer confirms `linear issues.txt` is full of misspellings and the Screenplay/Character Profiles spellings win.

### Still open

- **Release scope.** The Pitch describes the *full* seven-day game with procedural terrain. **The demo is Day 1 only, hand-authored, on a fixed map.** Procedural generation, the day/night cycle system, the manual save system, shrooms, the crossbow, the Arisaka, the Ka-Bar, stone totems, priests, the dock, the flak tower and the radio station are **all out of scope**. Do not build them and do not prepare hooks for them unless a Linear issue says so.
- **Mine geometry** — carved into the island terrain, or a teleport to a separate space enabling a non-Euclidean layout? Flagged in the issue list as needing a Claude recommendation.
- **Munchies multiplier** — the source attaches the food-healing multiplier to the *morphine* stat but names it "Munchies", which reads like the weed stat. Needs one word from the developer.

## Two-milestone reality (read this before estimating anything)

- **September 1 — class delivery.** Basic playable loop only: main menu → gameplay → death/game over → back to main menu. Cutscenes, polish, final art and full VO may be absent. Placeholders everywhere are acceptable and expected.
- **September 8 — itch.io release.** The polished demo.

When a Linear issue could be cut, cut it toward Sept 1 and restore it for Sept 8.

## Hard technical constraint

Target is **Unity WebGL, playable in the browser on itch.io, total upload under 1 GB, 1920×1080 in fullscreen**, Xbox controller plus keyboard/mouse. Every art, audio and system decision is subordinate to this. Unity 6.3 LTS, URP.
