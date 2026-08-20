# Mr. Moonlight — Pitch Document, extracted

Source: `MrMoonlight Pitch Document.pdf`, Mustard 2026.
This describes the **full game**. The demo is Day 1 only. Everything below is marked **[DEMO]** or **[FULL GAME — out of scope]** so you never build the wrong thing.

---

## 1. Elevator pitch

| | |
|---|---|
| **Genre** | FPS with exploration and horror elements. Gritty, heavy combat against deadly enemies that makes you consider your options. Ludonarrative focus for exposition and tutorial elements. |
| **MVP launch target** | PC — itch.io release first, Steam page and demo to follow |
| **Later targets** | PC (itch.io, Steam, GOG), Xbox, PlayStation, Switch. Mobile kept in mind during design. |
| **Audience** | Young adults to adults (18+), niche focus |
| **Rating** | Mature (17+) / Adults Only (18+). Blood and gore, substance abuse, religious/ethical/blasphemous topics, mental health topics, foul language, depictions of nudity. |
| **Engine** | **Unity 6.3 LTS** |
| **Dev tools** | Blender, Photoshop, Substance Painter/Designer, FL Studio, Audacity |
| **MVP date** | itch.io, first week of September |
| **Full game** | TBD, ideally mid-2027 |

**Logline.** A group of friends, a weeklong camping trip, a remote Alaskan island. One last good time together before the big changes of adulthood… Something goes wrong.
You are **Tracey**, a grumpy drug addict trying to save her kidnapped friends from a cult that will sacrifice them at the end of the week.
There is something about the island: **each night its terrain goes through a deadly shape-shift and forces you to look for shelter before 3 a.m.**

**Reference games:** *Slender: The Eight Pages* (2012) · *MISERY* (2025) · *DUSK* (2018) · *The Forest* (2018).

---

## 2. Setting

**1979. AANNIARVIK ISLAND, ALASKA.**

Seven friends reach a point where they must make big life decisions. Fearing the end of the friendship, they take a break from hard Alaskan life for a final weeklong camping trip.

Aanniarvik was inhabited by natives long ago. **During WWII it was an American base**, ready for a Japanese invasion that never came. Now it is empty except for a single forest ranger.

> This is why there is a **Japanese Type 92 turret** at the chapel, **Japanese posters and signage** in the mine infirmary, a **flak tower**, and a **radio station**. The military layer is 30 years dead and still lying around.

Tracey is a twenty-something history-major college dropout with a grumpy potty mouth and bad habits. She didn't want to come; her friends insisted.

---

## 3. Core loop

Mr. Moonlight is a first-person shooter that **never breaks perspective**. First person always — including cutscenes, deaths and the turret.

- **[FULL GAME]** Exploration is key: every day the map layout changes **procedurally** (Perlin-noise terrain, parameterised biomes), which gives replay value.
- **[DEMO + FULL]** Combat is **gritty and slow**. Every bullet counts, every blow drains stamina. There are things you should not face head-on. **Item management is vital.**
- **[FULL GAME]** Seven days. Each day: complete an objective and get back to shelter **before 3 a.m.** Each day gets harder and weirder.
- **Losing conditions:** getting killed, failing the objectives, or failing to escape. Losing returns you to the start of the day. **[FULL GAME]** limited-use manual save system.

**[DEMO]** The demo is **Day 1, hand-authored, on a fixed map.** No procedural generation. No day cycle simulation — lighting is authored per story beat. No manual save; only checkpoints.

---

## 4. Art direction (see also `03-style-guide.md`)

- 3D **low-poly**, 5th-console-generation look (PS1/N64). Simple models, **well-defined silhouettes**.
- **Pixelated textures.** Intended pipeline: **Pixel8r + Substance Painter**.
- Colour palette: **cold green, gray and brown tones**, high contrast.
- UI and menus: **minimalist, minimum text**, simplistic but high-contrast, **punk/acid/grunge**.

**Keywords:** substance abuse · punk-acid style · foul language · crude combat · claustrophobic darkness · item management · hallucinations · addiction · exploration · procedural terrain · time limit · regret · monsters · traps · day and night cycle · hide and seek · objective centric · night sky.

---

## 5. Sound design

- **A good headset is recommended.** Stereophonic tricks, Doppler effect.
- **Sound is how the player detects threats and finds locations.** This is a design pillar, not flavour — it is why the audible-distance system and enemy audio detection exist.
- **Little music.** Mostly environmental atmosphere. Props (like trees) have their own sound sources.
- Scores exist for the **main menu** and the **ending**, plus **drum and percussion tracks** for hallucination states and special cinematics.

---

## 6. Themes

| Theme | How it lands |
|---|---|
| **Substance abuse** | The player is not told Tracey is an addict. It shows through the drug system: real buffs, hard drawbacks, a rock-paper-scissors balance you can abuse. Narratively load-bearing. |
| **Religion** | Depictions of Christianity based on the first Russian settlers of Alaska and on the characters' faiths. Some props and enemies have pagan and demonic traits. Initially left to interpretation and mystical symbolism — but there **is** a purpose and a meaning to it. |
| **Ethical topics** | 1979, post-Vietnam. A nation's morals under evaluation; society choosing new boogeymen. A secular world brings freedom and choices — at what cost? Each character carries a distinct moral question. |
| **Mental health** | Tracey is a rude, almost hostile junkie who prefers to be alone. **She wasn't always like this.** Strongest presence in the last day of the full game, outside the MVP. |

Narrative style: **"show, don't tell"** — punctual, sparse dialogue that hints at much deeper character development. Never over-expose.

---

## 7. The drug system

**[DEMO]** implements: alcohol (vodka bottle, beer can), marijuana blunt, morphine vial. Each has an *instant use* profile only — the **prolonged use** column below is **[FULL GAME]**.
**Shrooms are [FULL GAME] — do not build them.**

### Alcohol
Temporary melee-damage buff and reduced fear. Long use hits movement, aim and stamina, and risks blackouts or vomiting.

| Instant use | Prolonged use *(full game)* |
|---|---|
| + melee damage<br>+ damage reduction<br>− blurry effect<br>− reduced stamina | + heavy damage reduction<br>+ berserker mode<br>− prone to vomiting<br>− slow movement<br>− can induce blackouts |

### Marijuana
Reduces fear and anxiety, reduces hand shake when aiming. Constant use affects sound perception and invites hallucinations.

| Instant use | Prolonged use *(full game)* |
|---|---|
| + reduces fear<br>+ reduces shaking<br>+ healing boost<br>− hearing pitch affected<br>− can suddenly make you laugh (**enemies can spot you**) | + heals<br>+ stamina boost<br>− auditory hallucinations<br>− slows movement<br>− time skips<br>− increases fear after it wears off |

### Morphine
Improves health regeneration, grants damage reduction. Constant use hits stamina drain and damage taken.

| Instant use | Prolonged use *(full game)* |
|---|---|
| + fast healing<br>+ damage reduction<br>+ reduces shaking<br>− nausea<br>− increases fear factor | + immunity to certain attacks<br>+ large instant heal<br>− random heart attacks<br>− blackouts<br>− withdrawal symptoms |

### Shrooms — **[FULL GAME, DO NOT BUILD]**
Found in the wild. Induce hallucinations, heighten light perception, useful in low visibility. Long run: hallucinations and altered enemy behaviour, enemies gain buffs and extra attacks.

> **Note on the demo's drug numbers.** The Linear issues define the demo's actual stat model (0→1 floats, smooth decay curves, the relapse multiplier, the Munchies multiplier). Where the Pitch and the Linear issues disagree on *effects*, **the Linear issues win** — the Pitch is the design intent, the issues are the spec.

---

## 8. Other systems named in the pitch

- **FPS mechanics:** player controller, enemy behaviour, combat, inventory, jumpscare and event system manager, interaction system, flashlight.
- **Sound system:** an audio manager that can trigger sounds by fear and hallucination level, and that tells the player about nearby enemies and locations.
- **Dialog system:** triggers a sound line on events, adds narrative without over-exposition. *"Really, I just want subtitles whenever a character is talking, in plain white text, akin to the early Silent Hill games."*
- **Animations:** a lot of the language is physical. Special care over **arm and hand animation** when handling weapons or moving. For the MVP, heavy use of **video-to-mocap** (GoPro with POV tools, **DeepMotion** and **QuickMagic**).

---

## 9. Locations

**[DEMO]** Campsite · Glade (observation post with telescope) · Vernon's cabin · Mineshaft (+ warehouse + infirmary) · Well · Chapel. Flak tower is **visible in the distance only** — a soft boundary, not enterable.

**[FULL GAME]** Dock (ruined, only entry point to the island) · Radio station / workshop · additional observation posts · the rest of the forest.

| Location | Description |
|---|---|
| Camping site | Everything brought over on the ferry, including the RV: a couple of tents, the marital-sized tent with the waterbed, the fireplace, general mess |
| Vernon's cabin | Small hermit cabin. More of a box than a cabin. Still, warm inside. |
| Dock | The only entry point to the island. In ruins. |
| Radio station / workshop | Some G.I. hoped to call in reinforcements a few decades ago. Still a useful place. |
| Chapel | Vestige of a distant missionary era. No services. Well maintained. |
| Flak tower | Built strong enough to fend off a bomber formation. |
| Mineshaft | Was there ever anything of value here? |
| Observation post | Spots where the telescope can be mounted to watch the night sky. |
| The forest | All around us, ever changing. |

---

## 10. Enemies

### Environmental threats
- **Bear traps** — laid across the island. You can disarm them and set them yourself; they can also trap enemies. **[FULL GAME for the disarm/reuse mechanic]**
- **Punji traps** — agonizing, often fatal. Some pits are hidden. Their presence forces you to reconsider how you explore. **[DEMO — implemented as a scripted insta-kill]**

### Monsters

**WOLVES** — **[DEMO]**
Nimble and fast, usually in packs. **Afraid of fire.**
*Movement:* roam the island freely, usually in packs.
*Attack:* circle you a few times, then start to pick at you with bites.
*Defense:* if you hit them they stop attacking and return to circling. Enough damage and they flee and restart roaming.

**FURMAN** — **[DEMO — boss]**
Gnarly creature that attacks like a berserker. Avoid contact; eliminate as a priority if you can't.
*Movement:* plains or forest, usually sleeping or roaming.
*Attack:* charges on sight, heavy bites. **The charge also induces fear factor.**
*Defense:* no tactic but to charge, and a lot of health. **Heavy blows or attacks can stun them for some seconds.**

**CULTIST — ZEALOT** — **[DEMO]**
Ambush and melee specialist.
*Movement:* very quiet, black attire hides them in darkness, they can be anywhere. They usually start moving only after spotting you. **If you haven't seen them, they only move when your back is turned.** Once spotted they charge. If they lose you, they follow from a distance.
*Attack:* knives and scythes. Can throw Molotov cocktails **[not in demo]**. **Attacks from behind can be fatal.**
*Defense:* low health; their best defense is being sneaky.

**CULTIST — SPOTTER** — **[DEMO]**
Scouts of the cult. Can alert other cultists to your presence.
*Movement:* roam the map cautiously **with lamps**. Can also group at altars or in buildings.
*Attack:* shotgun or melee clubs. **Can alert and spawn more cultists with a whistle or flare gun.**
*Defense:* average Joe.

**CULTIST — PRIEST** — **[FULL GAME, DO NOT BUILD]**
High-ranking, ranged. Always near an altar or fireplace; can be summoned by spotters. Crossbows and poison blow darts that induce hallucinations; spells in later stages. Moves in zigzag to a new position if attacked while allies are nearby.

**STONE TOTEMS** — **[FULL GAME, DO NOT BUILD]**
Entities of the forest. **They won't move as long as you are watching them.** Placed near altars or water. The closer you are, the more your speed and stamina drop. No defense — a few hits break them.

---

## 11. Item catalogue

**[DEMO]** items are marked. Everything else is full-game.

| Item | Type | Notes | Demo? |
|---|---|---|---|
| Medkit | Consumable | Single use, medium healing | — |
| **Bandages** | Consumable | Small heal | **[DEMO]** |
| Walkie-talkie | Equipment | Detect enemies, communication. Call function to hear the other radio from a distance **or create a distraction**. Consumes batteries. | **[DEMO — story use only]** |
| Flashlight | Equipment | Consumes batteries **[battery drain not in demo]** | **[DEMO]** |
| D batteries | Consumable | Power some items | — |
| **Soda can** | Consumable | Slight healing + stamina boost | **[DEMO]** |
| **Crackers** | Consumable | Small healing + slight stamina boost | **[DEMO]** |
| **Canteen** | Consumable, refillable | Water helps some stats and stamina recovery. Refill at springs or wells. | **[DEMO]** |
| Ka-Bar knife | Melee | Default melee. Fast slashes, extremely close range. | — |
| **Inuit pickaxe** | Melee | Heavy slow blows, medium-range melee. **Will break after a while** *(breakage not in demo)* | **[DEMO]** |
| **Map and compass** | Equipment | Navigate the island. **Deploying it locks you in position and exposes you.** | **[DEMO]** |
| **Double barrel** | Firearm | Extreme damage at close range, **very loud** | **[DEMO]** |
| Zippo | Equipment | Requires gas. Small convenient light. | — |
| Gas can | Equipment | Starts some objectives, refills the Zippo, can make explosive traps | — |
| Matches | Consumable | Emergency light source | **[DEMO — found in red cooler]** |
| **.45 pistol** | Firearm | Reliable, high stopping power. *(M1911, 7-round magazine per the Linear issues)* | **[DEMO]** |
| Crossbow | Ranged | Silent, deadly | — |
| Arisaka rifle | Firearm | Long-range, high damage | — |
| **Ammo** | Consumable | Found across the island in certain spots. **Scarce.** | **[DEMO]** |
| **Vodka bottle / beer can** | Drug | Drunkenness stat | **[DEMO]** |
| **Marijuana blunt** | Drug | Weed-high stat | **[DEMO]** |
| **Morphine vial** | Drug | Morphine-high stat | **[DEMO]** |

---

## 12. Cast (as Tracey sees them)

| | |
|---|---|
| **Tracey** | "What can I confess about me? Addict, asshole, dull." |
| **Holly** | "Miss Perfect. She is a nice person, but HOLY FUCK, you can't be that nice all the time. Spoiled brat." |
| **Rylee** | "Who would have guessed? Lady Jester wants to be a nurse; stand-up comedy would suit her better." |
| **Scott** | "Come on Scott! Why did you dodge the draft and still want to join the air force!!? Fine, get lost in space." |
| **Shannon** | "*clears throat, dork voice* 'sha-sha-sha-shannooon!!' I said it right? Just kidding! I still don't like that drawing you made of me." |
| **Robert** | "Talk about bigger and dumber. Not that he is a bad person, he just hates the Man too." |
| **William** | "We are still friends with him? Well, I might bring my stereo so he can fix it." |
| **Vernon** | "Holly's uncle, I think he's a convicted felon or a hermit or something, I don't know." |

Full profiles in `04-character-profiles.md`.
