# Mr. Moonlight — Style Guide, extracted

Source: `Style.pdf` — "A little Bit of Style", Mustard 2026.
The source PDF is **mostly reference imagery**. The images cannot be reproduced here; what follows is every directive the document actually states, plus notes on what the images are for. **If you need to see a reference board, ask the developer to describe or attach it.**

---

## 1. Visual style

Set in **Alaska, 1979**. The style is a mix of **grunge, acid and punk**, blended with **punk Christian elements**.

First-person shooter, **always in first-person perspective** — the camera never leaves Tracey's eyes.

Character design, the few UI elements, the tone of the game and the characters themselves all serve this style. **Tracey is a punk herself**, and she is the lens.

The PDF's reference boards are grouped as:

| Board | What it establishes |
|---|---|
| **Grunge** | Texture, wear, dirt, washed-out colour, 90s photocopy grain |
| **Acid** | Saturated psychedelic accents, distortion, used for drug/hallucination states |
| **Punk** | DIY typography, torn paper, safety pins, hand-scrawled marks, high contrast black/white |
| **Punk Christianity** | The distinctive fusion: crosses, icons and religious imagery rendered in DIY punk-zine treatment |

**Practical translation for UI/HUD/menus:** minimal text, high contrast, hand-made rather than clean-corporate. Torn edges, photocopy artefacts and stencil/zine typography over polished vector work. See the Pitch: *"minimalist, with minimum text: simplistic, but high-contrast and in a punk/acid/grunge style."*

---

## 2. "Death to the World"

> A lot of this aesthetic is heavily influenced by the 90s Orthodox magazine **"Death to the World"**.

This is the single most useful reference for the game's look. It is a punk zine made by Orthodox Christian monks: high-contrast black-and-white photocopied saints, skulls, monastic photography, and hardcore-flyer typography. **When in doubt about a UI, title card, poster or menu treatment, that is the target.**

Video references cited in the doc:
- `https://youtu.be/yC9VKhygjsA`
- `https://youtu.be/Sc27-dmJ_4wxc` — 30 mins; *"the first 4 mins are enough to catch the feeling"*

The developer has a Drive folder of scanned issues for visual reference.

---

## 3. The enemy

> Though they appear monstrous, these enemies are **ordinary men and women consumed by unchecked passions**. Their masks are not disguises but **symbols of the identities they choose to embody**.

This is the core idea of the cult design and it should drive every modelling and animation decision: they are people, and the horror is that they chose this.

The aesthetic is **gritty and realistic**, drawing from underground subcultures and extreme groups active in the late 20th century. The reference points named are the *"snuff BDSM"* atmosphere of films like **8MM** and **A Serbian Film**, and Rockstar's **Manhunt**.

**Most enemies are human. A few supernatural figures exist, belonging to a separate, otherworldly realm.** (In the demo: the Zealots and Spotters are people; the **Furman** is not.)

---

## 4. 3D character style

Low poly, **highly stylized**, simple enough to animate but defined and human enough to read.
**Facial expressions are done with 2D texture swaps, not with a rig.**

| WANT | AVOID |
|---|---|
| Low-poly, highly stylized 3D models | Low-poly models that try to be realistic |
| Stylized facial expressions via **texture swap, not rigged** | Full face rig or blend shapes |
| Clever use of **body language** to convey emotion and personality | Rigid postures, no natural spine |
| Really expressive facial expressions that match the character's personality | Dull, boring, "kdrama" limited facial expressions |
| **Pixelated textures**, matching the rest of the game | Plain photos used as textures |
| **Big blocky shapes** — like the boots. Each character has a distinctive shape and silhouette | Conventional human figure and proportions (this is not a realistic game) |
| Highlight **specific details** of the character | Generic anime body (MikuMikuDance) |

**Implications for the character pipeline:**
- Budget for a **face texture atlas per character** with swappable expression states, driven from animation events or code. No blend shapes, no facial bones.
- Silhouette is the priority. Exaggerate the identifying element (Tracey's beanie and boots, Rylee's guitar and coat, Robert's mass, Vernon's belly).
- Textures go through the pixelation step (Pixel8r + Substance Painter) — do not ship clean PBR.

---

## 5. Audio

| Context | Direction | Reference |
|---|---|---|
| **Main menu and UI** | A low psychedelic theme — a version of **Mozart's Requiem** in 60s–70s psychedelic rock style | `https://youtu.be/Ksbf0A4iuoE` |
| **Heavy combat / persecution** | Heavy metal with sound effects of yelling and machinery | `https://youtu.be/GNO0kArln1E` |
| **Very few important cutscenes** | Instrumental — **Dies Irae** from Mozart's Requiem | `https://youtu.be/FGqoU9NIjZw` |
| **Outside combat, moving through the forest** | Mainly **ambient**. Many props generate their own sound effects (trees, certain spots playing natural effects). Tricks and distant yelling used to create tension. | `https://youtu.be/30zC6VR5CdI` |
| **Human sounds — yelling and pain** | *"Without sugarcoating it, I want brutal and realistic sound effects... I want to make the players feel uncomfortable."* | `https://youtu.be/poz3SE0pzdc` · `https://youtu.be/NDaRiyx9fYQ` |

**A specific gameplay directive from this section:**

> Some enemies, when you take them down, **they won't be dead** — they will be on the floor, **yelling and moaning in pain**.

This is a downed-but-alive enemy state with a persistent pain-vocalisation loop. It is not currently written as its own Linear issue. **Flag it** — it is cheap (an extra animation state plus a sound loop on the death path) and it does more for this game's tone than most of the VFX work.

Note also from the Pitch: sound is **the primary threat-detection channel for the player**, tuned with stereo tricks and Doppler. A headset is assumed.

---

## 6. Consolidated art/audio rules for implementation

1. First person, always. No third-person camera, ever.
2. Low poly, PS1/N64 silhouette-first modelling.
3. Pixelated textures on everything, via Pixel8r + Substance Painter.
4. Cold palette: green, gray, brown. High contrast. Warm colour is reserved for lamps, fire and flares, and reads as important.
5. Faces are texture swaps. No facial rigs.
6. UI is zine-punk: high contrast, minimum text, hand-made.
7. Emissive materials are used sparingly and deliberately — **wolf eyes, Furman eyes**, the spotter's lamp, bullet tracers, the blue fire, the red moon. Check each against the lighting system.
8. Audio is a gameplay system before it is decoration.
9. Brutal, uncomfortable human sound. Do not sanitise the pain.
