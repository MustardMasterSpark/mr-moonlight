> # ⚠️ SUPERSEDED IN ONE PLACE — 2026-08-31
>
> **The "✅ Buy the FPS Animation Baker Toolkit" verdict in this document is REVERSED.** It was
> written before HQ FPS Weapons 2.0 was inventoried. The ~15 hand animations it is justified by
> (see the table below) are **already owned**, on one shared `FP_Arms` skeleton: the M1911 and the
> DoubleBarrelShotgun named in MRM-22/MRM-24 ship with 8 clips each, and every weapon ships
> Equip/Unequip/Aim/Idle. **Do not buy an FPS animation tool for the weapons.**
>
> The asset we *did* adopt is **Retarget Pro V5** — for enemies and Tracey's body, not weapons, and
> it lives in Playground and never enters Mr. Moonlight. **Read `Docs/retarget-pro-strategy.md`.**
>
> The rest of this document (shake, blur, the three rejected toolkits) still stands.

# Mr. Moonlight — Asset Toolkit

**What this is.** An evaluation of every asset in `Linear context/assets.txt`: what it does, whether it earns its place, which Linear issues it touches, and a buy / skip / defer call with the reasoning shown.

**The lens.** Every recommendation is filtered through three constraints, in this order:

1. **19 days.** Sept 1 for the playable loop, Sept 8 for the release.
2. **Unity WebGL, under 1 GB, in a browser.** This kills things that would be fine on desktop.
3. **Claude Code writes the code.** This changes the maths on "toolkit" assets more than anything else on the list — see the section on the two big kits.

> **Prices are not listed here.** Unity Asset Store pricing changes and there are frequent sales. Check each link before buying; several of these go 50% off regularly, and two of them are free.

---

## Verdict at a glance

| Asset | Verdict | Why, in one line |
|---|---|---|
| **Smooth Shake Free** | ✅ **Buy (free)** | Free, used by 4 issues, zero risk |
| **Vegetation Spawner Free** | ✅ **Buy (free)** | Free, and you cannot hand-place a forest in 19 days |
| **FPS Animation Baker Toolkit** | ❌ **REVERSED 2026-08-31 — skip** | ~~Weapon-hand animation is the one thing Claude genuinely cannot do for you~~ — the animations are already owned. See the banner |
| **Blur Shaders 2 for URP** | ✅ **Buy** | The opening scene depends on it and it is already named in your spec |
| **Flora** | 🟡 **Defer** | Overlaps Vegetation Spawner. Try the free one first, buy only if it fails |
| **Haze Volumetric Fog for URP** | 🟡 **Defer to M2** | Beautiful, and the most likely single thing to blow your WebGL frame budget |
| **Exploration Toolkit** | ❌ **Skip** | You would be inheriting someone else's architecture 12 days out |
| **Action Horror FPS Kit** | ❌ **Skip** | Same, and your enemy spec is far more specific than any kit's |
| **Cinematic Camera Controller** | ❌ **Skip** | **Your cutscenes are first-person. You do not need a cinematic camera at all** — see below |
| **DOTween Pro** | ✅ **Owned, use everywhere** | This is the answer to "smooth" across ~20 issues |
| **Text Animator for Unity** | ✅ **Owned, use in M2** | Free win for the punk-zine UI |
| **AllSky 220** | ✅ **Owned, use now** | The lighting issue needs 4+ skyboxes and you already have 220 |

**Net: buy 4 (two of them free), defer 2, skip 3.**

---

## ✅ Buy now

### Smooth Shake Free
`assetstore.unity.com/packages/tools/animation/smooth-shake-free-271263`

**Your note:** *"for shakes, in the earthquake"*

**Verdict: buy — it is free, and it does more than you thought.**

You listed it for the earthquake, but camera shake is called for in at least four issues:

| Issue | Use |
|---|---|
| **MRM-22 Pistol** | Recoil — you wrote *"we use a shake asset, maybe for this"* |
| **MRM-24 Shotgun** | Much heavier recoil than the pistol |
| **MRM-17 Death sequence** | Camera shake during the fall |
| **MRM-50 Punji trap** | Shake "as if in pain or in shock" |
| **MRM-57 Earthquake** | The escalating quake |
| **MRM-54 VFX fear** | High-frequency panic tremor |

Six systems, one free asset, and it keeps shake behaviour consistent across all of them instead of six hand-rolled implementations that feel different.

**One caution, and it matters:** several issues specify that camera shake **must not alter the player's real aiming direction** (fear tremor, drunk sway, weed float). Confirm the asset can shake a *visual* transform separately from the aim transform. If it can only shake the whole camera, you need a two-transform rig — camera parent holds aim, child holds shake. Worth checking on day one, not on day fifteen.

---

### Vegetation Spawner Free
`assetstore.unity.com/packages/tools/terrain/vegetation-spawner-free-automatic-tree-grass-placement-177192`

**Your note:** *"vegetation implementation"*

**Verdict: buy — free, and hand-placing an island of trees is not a thing you have time for.**

**Issue:** MRM-58 (terrain blockout + vegetation pass) — which blocks A* pathfinding, which blocks every enemy in the game. This asset is on the critical path.

**The real risk is not the asset, it is the tree count.** A forest is the easiest way to blow both your frame rate and your 1 GB ceiling. Before you detail anything:

- Agree a tree/prop budget in **MRM-6 (WebGL spike)**
- Use **GPU instancing** and aggressive **LOD** — WebGL will not forgive you otherwise
- Test in an actual browser build early, not the editor. The editor lies about this.

Your art direction helps you here: **PS1/N64 low-poly with pixelated textures**. Your trees should be cheap by design. Lean into that rather than fighting it.

---

### FPS Animation Baker Toolkit
`assetstore.unity.com/packages/tools/animation/fps-animation-baker-toolkit-370556`

**Your note:** *"to do weapon FPS hand animations"*

**Verdict: buy. This is the clearest buy on the list after the free ones.**

**Why:** this is precisely the category where an asset beats both you and Claude. Claude cannot author animation. You *can*, but first-person weapon-hand animation is fiddly, high-volume, and you have mocap work queued that matters more. Every hour this saves goes to Vernon's cabin.

**Volume it covers:**

| Issue | Animations needed |
|---|---|
| **MRM-22 Pistol** | Fire, reload, lower, raise, empty click |
| **MRM-24 Shotgun** | Fire ×2, reload, lower, raise, empty click |
| **MRM-23 Pickaxe** | 3 swings, lower, raise |
| **MRM-25 Weapon switching** | Lower/raise for every hold state |
| **MRM-43 Map and compass** | Handling animation |
| **MRM-52 Turret** | None — the turret is deliberately unanimated |

That is roughly **15 hand animations** across the demo. Your own spec already names this asset in **MRM-9**: *"we should already make use of the FPS Animation Baker Toolkit with a basic Player Model, and generic guns."*

**Do this early**, not when you get to the weapons. Placeholder hands that never get replaced are how a demo ends up looking unfinished.

---

### Blur Shaders 2 for Unity URP
`assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/blur-shaders-2-for-unity-urp-374982`

**Your note:** *"blur system"*

**Verdict: buy — the opening scene does not work without it, and you already wrote it into the spec.**

**Issues:**
- **MRM-56 Opening sequence VFX** — the cinematic blur where the whole world is blurry *except* the wristwatch, then the canteen. You named this exact asset in the source.
- **MRM-53 Damage feedback** — radial blur that intensifies as health drops
- **MRM-54 VFX fear** — peripheral radial blur
- **MRM-55 VFX substance profiles** — peripheral blur in all three

**Why this one matters more than its size suggests:** Scene 1 is the first thirty seconds of your game. Blurry world → one clear watch → nausea → vomit → find water → clarity. **If the focus effect does not read, a player just thinks the game is broken.** That is a terrible first impression and it is entirely carried by this asset.

**WebGL caution:** full-screen blur is a real per-pixel cost, and you are stacking it with fear vignette, chromatic aberration, bloom and possibly three substance profiles. **MRM-6 must establish how many post-process passes you can afford at once.** Expect to need a hard cap.

---

## 🟡 Defer

### Flora
`assetstore.unity.com/packages/tools/terrain/flora-323661`

**Your note:** *"To populate vegetation on the island, can it auto place trees"*

**Verdict: defer. It overlaps Vegetation Spawner, which is free.**

You have two vegetation assets on the list solving the same problem. Buying both is paying twice for one job.

**The sequence:** install **Vegetation Spawner Free** first (MRM-58). Populate the island. Build it for WebGL. Then ask a specific question: *did it fail at something?* Common answers are instancing performance, painting control, or LOD handling.

- **It worked** → you saved the money, spend it on the FPS Animation Baker instead.
- **It failed on performance specifically** → Flora is an instancer and that is its strength. Buy it then, with a known problem to solve.

**Do not buy both up front.** Twelve days from a milestone, an unused asset is not neutral — it is a tab you keep meaning to evaluate.

---

### Haze — Volumetric Fog & Lighting for URP
`assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/haze-volumetric-fog-lighting-for-urp-336656`

**Your note:** *"this is for lighting and volumetric fog"*

**Verdict: defer to M2. Beautiful, and the single most likely thing to sink your frame rate in a browser.**

**Where it would be used:** MRM-47 (world lighting) calls for fog in the final scene after the sky turns red. MRM-44 mentions a possible fog-based visible breath.

**The honest assessment.** Volumetric fog is one of the most expensive effects in real-time rendering. In WebGL, with the compilation and threading restrictions of a browser build, it is a serious cost. And you want it in the **most demanding scene in your game** — the finale, with the red sky, the earthquake shaking every tree in a radius, spotters patrolling, the Furman charging, the turret firing, and the blue fire ring burning.

**Would it look incredible in the forest at night?** Yes. **Is it a Sept 1 requirement?** No — M1's bar is a stranger finishing the game in a browser.

**Recommendation:**
1. Ship M1 with Unity's built-in fog. It is cheap and, with your cold palette and low-poly art, honestly quite close.
2. After MRM-10 gives you real frame numbers from an actual build, decide whether you have headroom.
3. If you do, buy it for M2 and use it **only in the finale**, not globally.

**Consider this as well:** for a PS1/N64-styled game, *authentic* fog is distance fog, not volumetrics. The reference era used fog to hide draw distance. There is a case that the cheap option is also the more stylistically correct one.

---

## ❌ Skip

### Exploration Toolkit
`assetstore.unity.com/packages/templates/systems/exploration-toolkit-336992`

**Your note:** *"check which systems would be helpful, levers, player controller"*

### Action Horror FPS Kit (URP)
`assetstore.unity.com/packages/templates/systems/action-horror-fps-kit-urp-383006`

**Your note:** *"check which systems would be helpful, enemies can be done as a base from the zombie"*

**Verdict on both: skip. And the reason is the same, and it is about time, not quality.**

These are both **template/system kits** — they give you a working game and you modify it. That trade is excellent at the start of a project and increasingly bad as you go. **You are 12 days from a milestone with a Unity project that already exists, 60 written issues, and a very specific design.**

**What you would actually be buying:**

| What you hope | What happens |
|---|---|
| "Enemies from the zombie base" | Their zombie has *their* state machine, *their* animator setup, *their* detection model. Yours needs vision cones with collider occlusion, a hearing sphere whose radius varies by state, a 3-run search routine, a flare that spawns 3–10 reinforcements, and a Zealot who checks whether he is inside the *player's* vision cone. **None of that is in a kit.** |
| "A player controller for free" | Yours needs crouch-toggle with smooth collider transition, a stamina curve, boots-vs-noise, a compass mode that turns the body instead of the camera, a stretcher mode and a turret mode. You would be deleting most of theirs. |
| "Levers and interactables" | MRM-16 is a 2-point issue. You do not need to buy it. |

**And here is the part that flips the maths entirely: you have Claude Code.**

The value of a kit is *code you do not write*. That value drops sharply when code is the cheap part of your project. Your expensive resources are **19 days, your own hands in Unity, and mocap time** — none of which a kit gives back. What a kit *costs* you is integration time, learning someone else's architecture, and fighting its assumptions when your spec disagrees. **That cost is paid in exactly the resource you are short of.**

**The one honest counter-argument:** if the Action Horror kit ships good **enemy animations** you could retarget, that is real value — animation is your genuine bottleneck. But buy it for the animations then, with your eyes open, and delete the systems. Do not buy it for the systems.

**Verdict: skip both. If you have already bought one, mine it for art and animation only, and do not let its architecture near your codebase.**

---

### Cinematic Camera Controller
`assetstore.unity.com/packages/tools/camera/cinematic-camera-controller-372344`

**Your note:** *"check if this would be helpful for camera control on cinematics? like dolly's and stuff? really what we move is Tracey on the cinematics... so IDK really how I'll do cinematics (worried sad face)"*

**Verdict: skip — and the worried face is the actually important part of that note, so let me answer it properly.**

**Why the asset is wrong for you.** Dolly-and-crane cinematic camera tools exist to move a camera *around* a scene — third-person, over-the-shoulder, sweeping establishing shots. But your Pitch document states, twice:

> *"Mr. Moonlight is a first-person shooter that never breaks perspective."*

**Your cutscenes are first person.** Scene 8 in Vernon's cabin is not a camera flying around a room — it is **Tracey standing in a room, looking at people**. The camera is her eyes. It does not dolly. It does not crane. It stays exactly where it always is.

**So what do you actually need for cinematics?** Three things, none of which is a camera asset:

1. **MRM-15 Cutscene framework** — letterboxing, control lockout, safeguards. Already specced.
2. **Animated characters to look at** — Holly, Vernon, Scott performing. **This is what QuickMagic mocap is for** (MRM-63). The performance is in the *characters*, not the camera.
3. **Scripted head-look for Tracey** — the only camera work you need. During a cutscene, her head turns to face whoever is talking, or to look at the thing she is meant to notice. That is a smooth `LookAt` on the camera transform driven from the event director. **Claude can write it in one session.**

**The reframe:** you were worried about cinematics because you were imagining film-making. You are not making film shots. You are **standing a player in a room and having people perform in front of them.** That is dramatically less work than what you feared, and every hour you were going to spend learning a camera tool goes to mocap instead, which is your real bottleneck.

**One genuine consideration:** if you ever want a shot Tracey cannot see — the red moon rising, the cultists surrounding the well from outside — you would break first person to get it. **The Pitch says never break perspective.** I would hold that line: the horror is stronger when the player only knows what Tracey knows.

**Concrete recommendation:** skip the asset. Add a `cutscene_look_at` verb to the event director (MRM-11) as part of that issue. It costs you one line in a spec you are already writing.

---

## ✅ Already owned — use these harder

### DOTween Pro
`assetstore.unity.com/packages/tools/visual-scripting/dotween-pro-32416`

**Your note:** *"this could be useful for some animations, I don't know how much, Claude surely will appreciate to have in mind as a general use tool"*

**You undersold this. It is the answer to a word you wrote about twenty times in your issue list: "smooth."**

Your own working agreement says: *"whenever I say smooth, you can decide how to do this, with DOTween, curves, etc."* **DOTween is the default answer**, and using one tool for all of it means "smooth" feels the same everywhere in the game instead of subtly different per system.

Where it lands:

| Use | Issues |
|---|---|
| Crouch height transition | MRM-9 |
| Weapon lower/raise | MRM-25 |
| All UI fades — menus, prompts, inventory, letterboxing | MRM-15, 16, 18, 19, 42 |
| Health and stamina bar animation | MRM-12, 46 |
| Damage overlay fade in/out on separate curves | MRM-53 |
| Stat decay curves (fear, drunk, weed, morphine) | MRM-48 |
| Quick sober | MRM-48 |
| Sound layer fades | MRM-38 |
| Skybox and lighting transitions | MRM-47 |
| Turret shell ejection arc | MRM-52 |
| Compass needle wobble and settle | MRM-43 |

**Tell Claude Code it is available in the kickstart doc.** Otherwise it will hand-roll coroutines for all of the above.

**WebGL note:** DOTween is lightweight and fine in browser builds. Use `SetLink` on tweens attached to GameObjects so they die with their target — orphaned tweens on destroyed objects are a classic WebGL crash.

---

### Text Animator for Unity
`assetstore.unity.com/packages/tools/gui/text-animator-for-unity-ui-toolkit-and-text-mesh-pro-341308`

**Your note:** *"for the text"*

**Verdict: owned, use it — but in M2, not M1, and be careful where.**

**Good fits:**
- **MRM-65 UI polish** — the punk/acid/*Death to the World* aesthetic wants text that behaves badly: jitter, wobble, distress. This is exactly that.
- **MRM-18 Credits** — the long scroll
- **MRM-20 Sparring dummy** — floating damage numbers, *Symphony of the Night* style
- **MRM-14 System messages** — the blue text could use a subtle entrance

**Where NOT to use it:** the **dialogue subtitles** (MRM-13). Your spec is explicit — *"plain white text, akin to the early Silent Hill games."* Silent Hill subtitles do not animate. They appear. Animated subtitles would fight both your reference and your readability, and readability matters because your VO is doing the emotional work.

**Rule of thumb: animate the interface, never the performance.**

---

### AllSky 220 Sky / Skybox Set
`assetstore.unity.com/packages/2d/textures-materials/sky/allsky-220-sky-skybox-set-10109`

**Your note:** *"skybox"*

**Verdict: owned, and MRM-47 needs it immediately.**

Your lighting progression needs at least four distinct skies:

| Beat | Sky |
|---|---|
| Campsite → Glade, 06:30–10:30 | Grey overcast morning |
| Cabin → mine, afternoon into dusk | Dimming, heavier |
| Post-mine, 01:00 | Near-total darkness, stars — **Scott's Big Dipper conversation happens here, so constellations must be visible** |
| Post-well finale | **Red** |

220 skyboxes is more than enough. **Two specific notes:**

1. **The night sky is a story beat, not a backdrop.** Scott identifies the Big Dipper and then the North Star is *red*. The player needs to be able to look up and see those stars. Pick that skybox for legibility of the constellation, not just for mood — or place the red "North Star" as a separate emissive object, which is probably the more controllable answer given MRM-57 already does exactly that for the red moon.
2. **WebGL budget:** skybox cubemaps are large textures. You need 4, not 220. **Strip the unused ones from the build** — this is an easy several-hundred-megabyte win and belongs in MRM-64 (optimization log) as your first entry.

---

## Assets you do NOT have and may need

Not in your list, worth a thought:

| Need | Issue | Note |
|---|---|---|
| **A\* pathfinding** | MRM-27 | You may not need to buy anything — Unity's built-in **NavMesh** is free, WebGL-safe and probably sufficient. MRM-27 makes this decision explicitly. Only buy a pathfinding package if NavMesh demonstrably fails on your sloped terrain. |
| **Localization** | MRM-65 | Unity's own **Localization package** is free and column-driven, which matches your spreadsheet plan. You mentioned you *might* have an asset — check before Claude builds one. |
| **Retro Realism props** | MRM-58 | Referenced in your issue list for vegetation but not in `assets.txt`. **Do you own this?** If not it needs adding to the buy list — the campsite, glade and mine staging all need props. |

---

## The single most important line in this document

**Buy the two free ones today. Buy the FPS Animation Baker and Blur Shaders this week. Skip the three toolkits.**

The three assets I am telling you to skip are the three most expensive on the list, and the reason is the same in all three cases: **they cost you time you do not have, in exchange for code that is no longer your bottleneck.** You have Claude Code writing systems. Your scarce resources are days, your hands in Unity, and mocap. Spend money only on things that give those back — and the FPS Animation Baker is the clearest example of one that does.
