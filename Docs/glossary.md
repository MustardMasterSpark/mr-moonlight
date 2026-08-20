# Glossary — canonical names and terms

**Use these exact strings** in class names, prefabs, animation states, audio folders, Linear titles and dialogue. The source documents disagree with each other; these are the rulings, confirmed with Carlos on 2026-08-20.

---

## Characters

| Canonical | Also seen as | Notes |
|---|---|---|
| **Tracey** | ~~Tracy~~ ~~Stacy~~ ~~Staycy~~ | The player. Theresa Gallagher, 25. `linear issues.txt` misspells it throughout — Carlos: *"I surely made tons of misspells in that file."* |
| **Rylee** | ~~Riley~~ | Radio only, Scene 3 |
| **Holly** | | Cabin scene. **She is the traitor** — never on screen in the demo |
| **Vernon** | | Radio + cabin. Holly's real father |
| **Scott** | | Radio + Scenes 9–10. Carried on the stretcher |
| **Shannon** | | One radio line, Scene 8. Never found |
| **Robert** | | Non-speaking. Polaroid only |
| **William** | | Non-speaking. His padlocked tent holds the compass |

**Nicknames — fixed, do not invent new ones:**
Tracey = *Trey* (Holly, Scott) · *Punk*, *Princess* (Rylee) · *Potato Digger* (William)
Holly = *Miss Perfect* (Tracey) · Rylee = *Lady Jester* (Tracey) · Scott = *Rocketman* (Tracey), *Scotsman* (Rylee)
Robert = *Chief* (Rylee) · William = *Will*, *Tinman* (Robert) · Vernon = *the boogeyman* (Holly)

---

## Enemies

| Canonical | Also seen as | Type |
|---|---|---|
| **Spotter** | | Ranged cultist. Lamp + double-barrel shotgun. Flare summons reinforcements |
| **Zealot** | ~~seller~~ *(dictation error)* | Melee cultist. Sneak + backstab |
| **Wolf** | | Pack behaviour. First enemy the player meets |
| **Furman** | ~~Fureman~~ ~~Furyman~~ | The boss. Berserker charge |

**Not in the demo — do not build:** Priest · Stone Totem.

**Tracey does not know their names.** In her dialogue she says *"them"*, *"those things"*, *"people in black"* — never "Zealot". The names are for code, not for her mouth.

---

## Places

| Canonical | Notes |
|---|---|
| **Aanniarvik** | The island. Alaska, 1979 |
| **Campsite** | Scenes 1–4. RV, tents, coolers |
| **The Glade** | Scene 6. Observation post with the telescope |
| **Vernon's cabin** | Scene 8. The long cutscene |
| **The mine** | Scene 9. Tunnels + bunk room + infirmary |
| **The well** | Scene 10. Virgin of Guadalupe statue, blue fire ring |
| **The chapel** | Scene 10. Hill, stairs, crosses, turret, Furman fight |

**Visible but not enterable:** the flak tower (a soft boundary in the distance).
**Not in the demo:** the dock, the radio station, other observation posts.

---

## Items and equipment

| Canonical | Also seen as | Notes |
|---|---|---|
| **Pickaxe** | ~~Axe~~ | The Inuit Pickaxe. `linear issues.txt` calls it "the axe" throughout. **Ruled: Pickaxe.** Mechanics unchanged |
| **Pistol** | | M1911, 7 rounds |
| **Shotgun** | | Double barrel, 2 shells, 7 pellets each |
| **Turret** | | Japanese Type 92, 30-round belt |
| **Bandages** | ~~vandages~~ | |
| **Crackers** | ~~crakers~~ | |
| **Marijuana blunt** | ~~marihuana~~ | |
| **Stretcher** | ~~streccher~~ | |
| **Canteen · Walkie-talkie · Matches · Map and compass · Flashlight · Boots · Backpack · Polaroid · Vodka bottle · Beer can · Morphine vial · Soda** | | |

---

## Systems

| Canonical | Notes |
|---|---|
| **Event Director** | The level's director. Runs the whole demo from a spreadsheet |
| **MoonlightTunables** | The single ScriptableObject holding every tunable value |
| **Sound pool** | A component holding clips + pitch range + volume, firing one at random |
| **Sound layer** | `island` · `cavern` · `mine` · `chapel`. Default for anything placed is `island` |
| **Audible distance** | The sphere around Tracey inside which pooled sounds are heard |
| **Vision cone** | Enemy detection primitive. Also exists on the player, solely for the Zealot |
| **Hearing sphere** | Enemy audio detection radius. Varies by behaviour state |
| **Engagement distance** | Where a chasing enemy stops and starts attacking |
| **Detection probability** | A value on each player sound, read by the enemy hearing system |
| **Munchies stat** | The food-healing multiplier. **⚠ Open: attached to morphine or weed?** See MRM-48 |
| **Conformist / Punk** | Easy / normal difficulty. Punk is default |
| **Sober protection** | The 40-second window before the relapse multiplier applies |

---

## Terms from Carlos's dictation

| He writes | He means |
|---|---|
| **cloud**, **Klaus**, **CloudScroll** | **Claude** |
| **I** | **the developer** (his own instruction: change all instances of "I" to "developer") |
| **smooth** | Claude's choice of DOTween, curve, or whatever is cleanest |
| **radious** | radius |
| **Dijsktra** | Dijkstra |
| **chappel** | chapel |

---

## Line IDs

**`D-<scene>-<nnn>`** — e.g. `D-08-043` is Scene 8, line 43.

This is the primary key across the dialogue spreadsheet, the VO filenames (`VO_D-08-043`), the event script and the localization columns. **All ~250 demo lines already carry an ID** in `Outputs/DesignContext MDs/01-screenplay-demo.md`.

Other ID prefixes: `SYS-###` system messages · `OBJ-##` objectives · `S<scene>-<nnn>` event script steps.

---

## Two spellings worth extra care

1. **Tracey.** It appears in class names, prefabs, animation states, audio folders and every dialogue row. Getting it wrong means a rename across the whole project. The screenplay and the character profiles both say **Tracey**.

2. **Pickaxe.** The Linear issues and the design docs disagreed; Carlos ruled **Pickaxe**. Any code, prefab or animation named `Axe` should be renamed on sight.
