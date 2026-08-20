# Mr. Moonlight — Demo Screenplay (Day 1), extracted for development

Source: `MrMoonlight screenplay demo.pdf` — "MR. MOONLIGHT · DAY ONE VOICE SCRIPT", Mustard 2026.
This file is the **flow of the demo**: what happens, where, in what order, with what triggers. It is the primary input for the **event director**, the **dialogue system**, the **objective list**, and every **staging** issue.

Characters with speaking parts in the demo: **Tracey** (player), **Rylee** (radio only), **Holly**, **Vernon**, **Scott**, **Shannon** (one line, radio). Four voice actors cover these.

**Line ID scheme used below:** `D-<scene>-<nnn>`. Use these IDs as the primary key in the dialogue spreadsheet and as the audio filename stem, so VO files, subtitles and event-director rows all key off one identifier.

---

## Demo at a glance

| # | Scene | In-fiction time | Location | Ends when |
|---|---|---|---|---|
| 1 | Waking at the campsite | 06:30 | Campsite | Tracey drinks from the canteen |
| 2 | The missing boots | 07:00 | Campsite | Tracey opens the red cooler, takes the walkie-talkie |
| 3 | Radio conversation with Rylee | 07:30 | Campsite | Rylee signs off; line goes dead |
| 4 | Will's tent and the compass | 08:00 | Campsite | Tracey takes the map-and-compass pouch |
| 5 | The forest road to the Glade | 08:30 | Forest | Distant screams stop all at once |
| 6 | The empty Glade | 10:30 | Glade | Wolf pack attacks; Tracey breaks out |
| 7 | The road to Vernon's cabin | 12:30 | Forest (trapped) | Tracey knocks on the cabin door |
| 8 | Vernon's cabin | 15:00 | Cabin | Holly sends Tracey off; Vernon collapses |
| 9 | The mine | 19:00 | Mine → infirmary | Tracey loads Scott on the stretcher and exits |
| 10 | The night escape | 01:00 | Forest → well → chapel hill | Chapel door opens; hard cut to black |

Time of day runs **06:30 → past 03:00**. The lighting progression is driven off this and is authored, not simulated.

---

## SCENE 1 — WAKING AT THE CAMPSITE (06:30)

**Opening state.** Black screen. A walkie-talkie is calling somewhere out in the trees, unanswered. A cheap digital wristwatch starts chirping.

**Beats**
1. Screen comes up **blurred and tilting**. Everything is blurry *except the watch face* — the cinematic blur effect with a single clear focus point. This is the tutorial for "look at the highlighted thing".
2. Tracey's hand rises into view, finds the watch, shuts the alarm off. Forest goes quiet; the distant radio keeps calling.
3. She stands. The world **swings hard to one side**. Nausea.
4. **Vomit sequence** — she drops to her knees, retches. Control is removed.
5. Vision clears a little. Focus point moves to a **camp table with a canteen** on it.
6. She takes the canteen, drinks standing, head tipped back to the treetops. Scene ends.

**Dialogue**
| ID | Speaker | Line | Direction |
|---|---|---|---|
| D-01-001 | Tracey | *Uhhhh...* | Barely surfacing. Low, thick, no consonants. |
| D-01-002 | Tracey | *Auhhhhh...* | Longer, put-upon. Negotiating with the alarm and losing. |
| D-01-003 | Tracey | All right, all right already! | Snapping awake into irritation |
| D-01-004 | Tracey | I hear you! I hear you... | Muttered downward. Second "I hear you" at half volume. |
| D-01-005 | Tracey | *Ufffffffff...* | Long exhale with a voice behind it. Relief plus the start of a headache. |
| D-01-006 | Tracey | It's way too early... | Flat, grudging |
| D-01-007 | Tracey | Why the fuck didn't I turn it off? | Sharper. Self-directed — blaming last night's version of herself. |
| D-01-008 | Tracey | Uhhhg... shit... | Voice drops away mid-word. Small, sick, resigned. |
| D-01-009 | Tracey | *Puaaaghhhhhhhhhhhhhhhhhhh!* | Full-bodied retch. Ugly, no dignity. |
| D-01-010 | Tracey | Ah... ah... fuck... | Wrecked, breathless, spitting between words. |
| D-01-011 | Tracey | Getting too old for this shit. | Dry, deadpan, almost amused. |
| D-01-012 | Tracey | Uhhhh... water... | Weak, cracked, hardly voiced. |
| D-01-013 | Tracey | *Ahhhhhhh...* | Deep, satisfied, ragged. First good thing all day. |

**Systems this scene requires**
- `cinematic blur` with maintained focus points (watch → canteen)
- `vomit animation + particle`
- `stamina stat` + **`stamina stat block`** — stamina is scripted down during the vomit and **locked low** until she drinks from the canteen, then released. This whole scene exists to teach the player that stamina is a thing.
- `dialog system`, `event system`, `objectives`
- Player controller with control lockout
- Interaction/highlight system (canteen)
- Campsite staging

**Teaching function:** look, interact, stamina exists.

---

## SCENE 2 — THE MISSING BOOTS (07:00)

**Beats**
1. Head clears; the radio call resolves to somewhere inside the camp.
2. She hooks the canteen on her belt. Eyes travel down — she is **barefoot in cold mud, in socks**.
3. She searches the camp: an **RV**, scattered **tents**, a **dead bonfire**. In front of the RV a **blue cooler** with beer, soda and crackers — *more than she can carry* (this is the pocket-storage limit tutorial: 4 item types).
4. Behind the RV, a **red cooler** where no cooler should be.
5. She opens it: a box of **matches**, and the **walkie-talkie**, calling. She picks it up.

**Dialogue**
| ID | Speaker | Line | Direction |
|---|---|---|---|
| D-02-001 | Tracey | What in the world? | Genuine surprise, quiet, turning her head to find the sound. |
| D-02-002 | Tracey | Wha... what!? | Disbelief cracking into anger. First word a stumble, second an accusation. |
| D-02-003 | Tracey | My boots! My fucking boots! | Full volume. Furious, slightly ridiculous, does not care. |
| D-02-004 | Tracey | Fucking assholes. | Muttered at her own feet. Venomous but low. |
| D-02-005 | Tracey | Huh... This isn't where you belong. | Puzzled, slowing. Spoken *to the cooler*. |

**Systems**
- Boots mechanic **in its barefoot state**: reduced speed, quieter footsteps, sock model on feet
- Item pickup + `pocket storage` cap of 4 types
- Interaction highlight; audible-distance sound layers (the calling radio is a diegetic navigation beacon)
- Campsite staging: RV, 3 tents, bonfire, blue cooler, red cooler, table, canteen, trash/beers

**Teaching function:** inventory is limited; sound leads you places; you are handicapped without boots.

---

## SCENE 3 — RADIO CONVERSATION WITH RYLEE (07:30)

Tracey roams the empty campsite freely during this. **Rylee is heard only through the radio** — apply a radio filter to every `D-03-*` Rylee line. This is the longest pure-dialogue stretch in the demo and the main characterisation delivery for Rylee.

**Dialogue**
| ID | Speaker | Line | Direction |
|---|---|---|---|
| D-03-001 | Tracey | Uhhh... hello? | Cautious, slightly embarrassed |
| D-03-002 | Tracey | Hellooo? | Impatient, stretched, annoyed at the silence |
| D-03-003 | Tracey | You better start talking. You took my boots, bitch! | Flat, hard, fed up. Delivered like a threat she intends to carry out. |
| D-03-004 | Rylee | Calm down girl! Good morning to you too! Huh... so you had the third radio. | Amused, unbothered. Sing-song greeting, small delighted realization at the end. Voice furred with sleep throughout. |
| D-03-005 | Tracey | It was in a fucking cooler. I bet you were the airhead who left it there. Where are my boots? | Rapid, irritated, biting the ends off words. Only the question matters to her. |
| D-03-006 | Rylee | I love you too, Tracey. Anyway, thanks for finding it. William would've killed me if it got lost. | Warm, sincere, completely ignoring the question. |
| D-03-007 | Tracey | Rylee. | One word. Deadpan. |
| D-03-008 | Rylee | I barely slept. Wild night. I was looking for the radio, but I guess I took a little nap. What time is it, anyway? | Rambling happily, losing the thread, then genuinely curious. |
| D-03-009 | Tracey | RYLEE! | Shouted flat into the microphone. All patience gone. |
| D-03-010 | Rylee | Shhhh! Everyone is still sleeping. What do you want? | Sharp scandalized whisper. Startled, then indignant. |
| D-03-011 | Tracey | My motherfucking boots! Where are they? I'm walking around in my fucking socks on the fucking wet forest floor. | Every "fucking" is load-bearing. Builds to real outrage. |
| D-03-012 | Rylee | Oh, Tracey... don't kill me. I have them with me. | Caught. Slow, small, wheedling. One guilty breath. |
| D-03-013 | Tracey | You fat bitch. | Pure flat deadpan fury. Three words, no music in them. |
| D-03-014 | Rylee | Hey! I'm sorry. Last night, you, Robert, and I went back to camp for more booze and the sleeping bags. Then you started puking and passed out. We left you all cozy near the bonfire, and I took off your boots so you could sleep comfortably... I guess I forgot I still had them. I'm sorry! It was an accident, I swear! | Fast tumbling apology, no breaths where there should be. Enjoying the story even while begging — until the last words, which are almost sincere. |
| D-03-015 | Tracey | Mmhmm. It better have been—and not one of your jokes. | Unconvinced. Hum first, then slow and sarcastic, like a verdict. |
| D-03-016 | Rylee | You should see yourself when you sleep. You look like a puppy, even after puking a gallon of shit. I'll take a photo while you're sleeping so you can see what I mean. | Teasing and affectionate. She means the puppy line kindly, which makes it worse. |
| D-03-017 | Tracey | Gross. | One word, dropped like a stone. Genuinely repulsed. |
| D-03-018 | Rylee | Relax. I'll ask you first—and only when you're naked. | Silky and filthy, thoroughly pleased with herself. Winding Tracey up on purpose. |
| D-03-019 | Tracey | Fuck you, creep. Anyway, where the hell are you guys? You left me all alone. | Disgust, then a hard turn into the real question. The last sentence carries something she would not admit to. |
| D-03-020 | Rylee | You don't remember? I guess not. You made a pretty big mess of yourself early on. Well, we're still in the Glade... Ah, I feel sorry for you, Tracey! The meteor shower was amazing! Then we lit another bonfire, had some grass with hot dogs and vodka, and I made out with Holly and that Greek body of hers. Then I blew Scott while giving a double handjob to Will and the redskin. Shannon was there too, watching everything from her good-girl chair. | Gleeful, cruel-but-not-really. Boasting through a fog of exhaustion, listing the night like a shopping receipt, savoring every item because Tracey missed it. Speed and delight — **never leering**. |
| D-03-021 | Tracey | Shut the fuck up, you filthy whore. Ugh, disgusting. Just tell me where you are! | Explosive first half, collapsing into ordinary grumpiness. The insult is habitual, not heartfelt. |
| D-03-022 | Rylee | Ah, don't hate me just because I'm pretty! | Mock-wounded, playing to an audience of one. |
| D-03-023 | Tracey | Rylee, nobody thinks you're pretty, you cow. | Slow, level, surgically unkind. Takes her time with it. |
| D-03-024 | Rylee | So rude! Well... uhh... I think we're not that far from camp? What did Will say? Uh... east! Yes, east! Just head east from the campsite. Straight ahead! | Genuinely stung for half a second, then lost trying to remember, then suddenly certain and bright. **Three different Rylees in one line.** |
| D-03-025 | Tracey | What kind of directions are those? | Incredulous |
| D-03-026 | Rylee | Sorry! Do you want me to wake Scott so he can come rescue you? | A trap, set sweetly. She knows exactly what she is doing with the name. |
| D-03-027 | Tracey | Nah. A barefoot walk through the forest is exactly what I need to connect with my inner self. | Sarcasm as armor. Too casual, a beat too late to be convincing. |
| D-03-028 | Rylee | Your pride wins again. You missed your chance to be alone with Scott, dumbass. | Smug, quick, affectionate. She scored and she knows it. |
| D-03-029 | Tracey | Shut up! | Too loud, too fast. Embarrassment coming out as anger. |
| D-03-030 | Rylee | Yeah, sorry. I will. I'm really tired. Mmmh... go inside Will's tent. There was an extra compass in there, so you can head east. That British weirdo keeps the key to his tent's lock under the RV rug. | Backing off, honestly tired. Energy drains across the line; the end is practical, helpful, almost sisterly. |
| D-03-031 | Tracey | Uhhhh... why do you- wait. What were you doing inside his tent? | Starts as a complaint, stops dead, turns into suspicion. The "wait" is the hinge of the line. |
| D-03-032 | Rylee | Relax. I was just having a peek. I wanted to find underwear with cum stains, but I guess he's pretty clean. | Blithe, unembarrassed, faintly disappointed in the result. |
| D-03-033 | Tracey | Ugh... you're the worst. | Worn out rather than shocked. She has known this person a long time. |
| D-03-034 | Rylee | Yours truly. Sorry, I'm falling asleep. We'll wait for you, barefoot princess... | Proud of the title, then genuinely fading. The last three words are almost a yawn — and the last Tracey hears from her. |

**Beat after:** the line goes dead. Tracey lowers the radio, pulls a face at it.

**Systems**
- Dialogue system with radio filter bus, non-blocking (player walks during it)
- Objective set: *Find the compass in Will's tent*
- Long dialogue chains — the event director must sequence these with wait-for-line-complete

---

## SCENE 4 — WILL'S TENT AND THE COMPASS (08:00)

**Beats**
1. William's tent is **padlocked**.
2. Under the **rug beneath the RV** there is a key. She picks it up.
3. The lock drops, the tent opens. Inside, clothes thrown across the floor and a **neck pouch: a map and a compass**. She takes it.
4. She steps back into the cold and turns to face the trees.

| ID | Speaker | Line | Direction |
|---|---|---|---|
| D-04-001 | Tracey | Now, let's get that compass. | Businesslike and sour. Ticking an item off a list she resents. |
| D-04-002 | Tracey | Right. East. Let's go east. | Grumbling herself into motion. Repeating the direction to make it real, and to have someone to blame if it is wrong. |

**Systems**
- Key → lock interaction gate
- **Map and compass** equip mechanic acquired here (camera locks down to the compass, look-input rotates Tracey's body instead of the camera)

**Teaching function:** the compass *is* the navigation UI. There is no minimap.

---

## SCENE 5 — THE FOREST ROAD TO THE GLADE (08:30)

**Beats**
1. East into the trees. Wet, gray, enormous forest. She is in socks.
2. Ahead, **a single wolf crosses the path at a distance**, unhurried, and does not look at her. → This is the **Running Line** wolf behaviour: waypoint path, no detection, no attack, destroyed at the last waypoint.
3. Her breathing changes after that and does not change back.
4. Further on: **people, screams and violence** ahead. Grows into a gruesome cacophony.
5. The sounds **thin out, fade, and stop — all at once**, before she can see anything. The silence that replaces them is not a natural silence.

| ID | Speaker | Line | Direction |
|---|---|---|---|
| D-05-001 | Tracey | Ah—! | A gasp caught behind the teeth. Not a scream — trying hard to make no noise. |
| D-05-002 | Tracey | What's happening? | Small and unsure. Asking the forest, not wanting an answer. |
| D-05-003 | Tracey | Jesus! | Sharp, involuntary, frightened. |

**Systems**
- Wolf `Running Line` behaviour
- Positional ambient audio set-piece (screams) with a hard synchronized cut-off
- Fear stat first bump; player breathing loop change

---

## SCENE 6 — THE EMPTY GLADE (10:30)

**Staging.** The Glade is a wide circular clearing of grass, free of trees. In the centre: a **mounted telescope** with a **pickaxe buried in its frame**. Strewn around: sleeping bags tangled where people should be, a dead bonfire, **Tracey's boots**, **Rylee's broken guitar**, empty beer cans and bottles. On a table: a **beer**, a **soda**, and a **Polaroid photo of all seven friends**.

**Beats**
1. She calls out for her friends once. Does not dare call twice.
2. **She finds her boots and puts them on.** → Boots equipped: speed up, footstep noise up, sock model swaps out. *This is the payoff of Scene 2.*
3. She picks up the Polaroid, looks at it a long moment, folds it into the pouch.
4. She works the **pickaxe** free of the telescope. → **Melee weapon acquired, auto-equipped.**
5. Out in the far trees, **something flashes** — a small, deliberate light.
6. **Telescope sequence:** she puts her eye to the telescope. Camera transitions with a blink. Telescope view = black screen with a soft-edged circular aperture at centre, long zoom. Player has turret-style look control only. Starting aim is **up at the sky**. The objective is to find the flash; it resolves into a **cabin, far off, with a shape standing near it**. On acquisition the telescope **locks**.
7. Behind her, very close, **wolves start howling and snarling**. Wolf pack encounter begins. She has the pickaxe and nothing else.

| ID | Speaker | Line | Direction |
|---|---|---|---|
| D-06-001 | Tracey | Friends...? | Low, hopeful, already breaking. Calls once, does not dare call twice. |
| D-06-002 | Tracey | Rylee... | The name of the person who took the boots, said as an apology to her. Shocked, quiet, the voice coming apart. |
| D-06-003 | Tracey | Guys... | Broken open. Barely audible, addressed to seven people in a photograph. |
| D-06-004 | Tracey | I... | One syllable, then nothing. Starts a sentence she cannot finish and does not try again. |
| D-06-005 | Tracey | Huh?! | Curiosity and alertness cutting cleanly through the grief. |
| D-06-006 | Tracey | Who... are... | Trailing off mid-thought, still peering. Two half-words, no full stop. |
| D-06-007 | Tracey | Ahhhh! | Full scream of fright, torn out of her. Still bent over the eyepiece when it starts. |
| D-06-008 | Tracey | I need to get out of here!! | Shouted through effort and adrenaline, mid-fight. A decision, not a plea. |

**Systems**
- Glade staging
- Boots equip/unequip mechanic (first equip)
- Melee weapon (pickaxe) acquisition + 3-swing combo + stamina cost
- **Telescope mechanic** (blink transition, aperture view, turret-style control, target acquisition, lock)
- Wolf **Group behaviour** (howl by the spotter wolf, circle formation, 2 attackers at a time)
- Fear stat, combat music switch

**Teaching function:** melee combat, stamina drain, wolves.

---

## SCENE 7 — THE ROAD TO VERNON'S CABIN (12:30)

**Beats**
1. The road is **trapped**. A trap waits in the leaves (see Index note #2 — build the **punji trap**).
2. There are **people in the trees — in black outfits, silent, patient**. Whether they engage depends on whether the player triggered the trap. → **Stealth is real and it works.** This is the Zealot introduction.
3. If they come, they come with **knives**, and she has a pickaxe. It is not quick and it is not clean.
4. When it is over she is standing among bodies she does not understand, hyperventilating.
5. The cabin: a small wooden box in the forest, weak candlelight inside, **every inch of the door covered in crosses**. She knocks.

| ID | Speaker | Line | Direction |
|---|---|---|---|
| D-07-001 | Tracey | Oh God... | Whispered between ragged breaths, shaking. **Not devotion** — the sound of a mind refusing what it has just done. |

**Systems**
- Punji trap (insta-kill, its own long death sequence)
- Zealot enemy: sneak/backstab behaviour, player visual cone check
- Enemy audio detection (noise from the trap triggering pulls them in)
- Fear stat spike; hyperventilating breath loop below 20% stamina

**Teaching function:** noise gets you killed; some fights are optional.

---

## SCENE 8 — VERNON'S CABIN (15:00)

The longest cutscene in the demo. Three characters, heavy blocking, most of the mocap work. **Note for direction: Holly is the traitor — she planned the trip so the cult could take her friends. Every warm thing she says here is a lie she half-believes. Vernon's fury at her is the only honest reading of the room, and the player is not meant to understand it yet.**

**Staging.** Narrow hallway, candles, boarded windows, a smell of sickness. A **shrine** with an **icon of Christ** on it. A **bed** against the wall. A **table** with a second walkie-talkie, a **morphine pack** and a **backpack**.

**Beats**
1. Coughing from the far end — wet, tearing, getting louder — stops the moment she enters the room.
2. **Holly is standing in the middle of the room with a pistol pointed at Tracey's face.** Mutual terror, then recognition. Gun down. They embrace.
3. Tracey tries to explain the Glade and falls apart doing it.
4. From the bed, **Vernon** drags himself upright. He does not look at Tracey at all — he looks at Holly, and shouts **"WHAT HAVE YOU DONE?!"** twice, the second at full volume, and it costs him. Blood comes with the coughing.
5. Holly explains he is her uncle, the ranger. Vernon cuts across her: *"They are back..."*
6. Holly: *"He has tuberculosis."* Tracey takes a step back.
7. **The second walkie-talkie bursts into life.** All three freeze; Tracey gets to it first. **Scott** is on the line, with **Shannon**. They ran; Robert, Will and Rylee were taken. Scott reports he is in a **mineshaft**. Then Shannon screams at something arriving, unfriendly voices flood the channel, and the line dies.
8. Tracey bolts. Holly catches her hand.
9. Vernon, from the bed: **"northwest... this time the mine is northwest from here"** — "*this time*" is the first hint that the island moves.
10. **Vernon orders Holly to give Tracey the pistol.** → **.45 pistol acquired.** (Not auto-equipped; the player equips it with RB.)
11. Vernon takes a **morphine vial** from the pack and puts it into his own arm. Tracey watches and recognises exactly what she is looking at.
12. Vernon hands her a **backpack**. → **Storage capacity 4 → 10.**
13. He warns her about a **tower near the mine entrance** — do not go near it.
14. He collapses mid-sentence, hard, onto the floorboards. The **icon of Christ falls face down** with him.
15. Holly refuses to let Tracey stay, presses **two marijuana blunts** into her palm, and sends her out.

| ID | Speaker | Line | Direction |
|---|---|---|---|
| D-08-001 | Holly | Ahh! | Short terrified cry — more frightened of the gun than Tracey is |
| D-08-002 | Tracey | NO!! | Shouted at the top of her lungs, hands flying up. Shock, not defiance. |
| D-08-003 | Holly | Trey??! | The nickname, cracked and disbelieving |
| D-08-004 | Tracey | Holly! | Pure relief, all in one breath |
| D-08-005 | Tracey | Holly! Thank God you are okay | Muffled against her shoulder, sobbing. Thanking a God she does not believe in. |
| D-08-006 | Holly | Tracey, what's going on?! | Frightened, pleading for an explanation |
| D-08-007 | Tracey | I don't know. I- I... The Glade! something happened! I don't know where everybody is! | Falling apart mid-sentence. Stammering, restarting, words out of order. |
| D-08-008 | Tracey | There are... people here. They... they took them | Slower and much worse. She hears what she is saying as she says it. |
| D-08-009 | Vernon | What have you done?! | A sick man finding enough air to be furious |
| D-08-010 | Tracey | Who's... | Bewildered, starting a question nobody will answer |
| D-08-011 | Vernon | WHAT HAVE YOU DONE?! | Everything he has, full volume, and it costs him. **The shout should audibly tear something.** |
| D-08-012 | Tracey | Holly? | Wary and quiet. Asking who this man is without asking it. |
| D-08-013 | Holly | He's my uncle. Vernon, he's the ranger of... | Low, tired, still holding him up. An introduction she does not get to finish. |
| D-08-014 | Vernon | They are back... | Cutting across her. Low, distant, listening to something outside the walls. |
| D-08-015 | Tracey | Who's back? | Confused, drawn in despite herself |
| D-08-016 | Vernon | The people who took your friends, they are back, that... thing... | Real fear from a man not easily frightened. Stumbles on the last two words, cannot finish. |
| D-08-017 | Vernon | You stupid kid, what are you doing here? | Contemptuous and hard |
| D-08-018 | Vernon | I told you to never come here *cough* and you *cough* bring *cough* your friends *coughing* | Building in anger until the coughs cut through. **Each starred cough breaks the line mid-word** — the rage is real and the body will not allow it. |
| D-08-019 | Tracey | Is he okay? | Softened despite everything. Concern from someone bad at showing it. |
| D-08-020 | Holly | He has tuberculosis. | Quiet, matter-of-fact, sad. She has said this sentence before to other people. |
| D-08-021 | Holly | Don't worry. He's strong, he survived Korea... | Waving it off with practiced, cheerful denial. Reassuring herself as much as Tracey. |
| D-08-022 | Tracey | Guys!!!? | Desperate hope, far too loud into the handset |
| D-08-023 | Scott | Tracey!!! are you okay?!! | *(radio)* Shouting with relief and alarm at once — he did not expect anyone to answer. |
| D-08-024 | Tracey | Scott!!! yes! I'm okay, I'm safe with Holly, where are you? what's going on? are you okay? | Everything at once, far too fast, questions piling up without waiting for answers |
| D-08-025 | Scott | Shannon is with me, she's fine but she's in shock. | *(radio)* Still keyed up, deliberately levelling out for her benefit. |
| D-08-026 | Scott | we managed to run away. I- Robert, Will, Rylee. They took them! | *(radio)* The guilt lands hard on "They took them" — he ran, and they did not. |
| D-08-027 | Tracey | Jesus... I'm scared Scott... *sobbing* | The armor comes off completely. An admission she would never make to anyone else. |
| D-08-028 | Scott | Tracey, you are alright. I am alright, Shannon is alright. I need you to be strong right now | *(radio)* Slow, warm, absolutely steady — a person deliberately being a floor for someone else to stand on. |
| D-08-029 | Tracey | ... Yes, sorry... I'm sorry Scott... what do we do now? | Steadying herself with effort. Still wet, coming back under control, embarrassed about it. |
| D-08-030 | Scott | We need to group up. I'm not sure where we are, we didn't go too far from the Glade. | *(radio)* Thinking aloud, building a map out of nothing |
| D-08-031 | Scott | This... is a tunnel. We are in a mineshaft | *(radio)* Puzzled first half, then completely flat on the last four words as he realizes what he is standing in. |
| D-08-032 | Tracey | A mineshaft? | Repeating the word back, uncomprehending |
| D-08-033 | Scott | Yes... But don't move from where you are! I'll go with you. | *(radio)* Decisive and reassuring. He has a plan now, and the plan makes him sound braver. |
| D-08-034 | Tracey | What about Shannon? | Immediate concern for someone she has never been kind to |
| D-08-035 | Scott | Shannon is okay, she's just scared, we'll be careful, right Shannon? | *(radio)* Confident, turning his head away from the handset for the last two words |
| D-08-036 | Scott | Shannon? Shannon are you okay? | *(radio)* The confidence drains out between the two questions |
| D-08-037 | Shannon | SCOTT! WHAT'S THAT?!! | *(radio)* Raw terror, shouted away from the handset at something arriving |
| D-08-038 | Tracey | SCOTT?! SCOTT!! | Screamed into a handset that has stopped working. Both names, the second louder and more useless. |
| D-08-039 | Tracey | Holly! I— Scott! I need to go! | Panicking and coming apart. Already turning away before finishing. |
| D-08-040 | Holly | Tracey, wait! where are you going?!! | Alarmed, holding on. Trying to slow her with her voice as much as her grip. |
| D-08-041 | Tracey | I need to find Scott! in... the mineshaft! | Barely coherent, straining against the grip. "Mineshaft" arrives as though it explains everything. |
| D-08-042 | Holly | But you don't even know where to go! | Louder, frightened, reasoning with someone past reason |
| D-08-043 | Vernon | northwest... this time the mine is northwest from here | From the bed. Slow, painful, every word costing him. **"this time" should sound like an ordinary thing to say, which it is not.** |
| D-08-044 | Vernon | The gun, give her the gun | An order, flat and immediate. No room for discussion. |
| D-08-045 | Holly | What? | Thrown completely. Small and confused. |
| D-08-046 | Vernon | She's gonna need it. I have other weapons, and I don't remember giving you my gun | Unarguable. The last clause is a dry, pointed correction — she took it without asking. |
| D-08-047 | Tracey | Mister? are you okay? | Careful, and more knowing than she lets on. **She recognises what she is looking at.** |
| D-08-048 | Vernon | yeah... I'll be fine | Brushing it aside, warmer and looser than a second ago as the drug lands |
| D-08-049 | Vernon | We need to hurry kid, I see you have a compass, we'll go northwest from here. | Brisk and commanding. Back in charge of the room. |
| D-08-050 | Vernon | I'll follow you in a bit, but you need to go now, we need to be back here before darkness | An instruction wrapped around a real worry. "Before darkness" carries more weight than she can hear yet. |
| D-08-051 | Vernon | You know how to use that? | A genuine question, asked plainly. He needs a true answer. |
| D-08-052 | Tracey | ...yeah | Quiet, after a pause, with a small unexpected steadiness underneath |
| D-08-053 | Vernon | Good, take this too, you are naked.. | Relieved. The last two words are almost affectionate, and gruffly amused. |
| D-08-054 | Tracey | Thanks | One word, awkward. She is not good at this. |
| D-08-055 | Vernon | There is a tower near the mine entrance, don't go near it, it must be swarming with those heathens... | Practical instruction, then a sudden drop into **open loathing** on the last word |
| D-08-056 | Vernon | Hurry now, you need to be fast, and you need to be quiet. | Pushing her out of the door with his voice. Urgent and absolute. |
| D-08-057 | Vernon | I'll get my gear and follow you soon. I'll use the radio to *coughing hard again* | Firm and organised, until the sentence is destroyed by a coughing fit |
| D-08-058 | Holly | Uncle!! | A cry of pure alarm, already dropping to her knees |
| D-08-059 | Holly | No! you go and get Scott, I'll stay here. | Hard and certain. Not a request. She has chosen who she is staying for. |
| D-08-060 | Holly | Trey... you look terrible. you are the strongest of us. Here... take it easy | Everything softens. Gentle, warm, using the old nickname deliberately. Talking someone down. |
| D-08-061 | Tracey | What? | Baffled. A laugh almost gets in at the edge of it. |
| D-08-062 | Holly | You've heard Scott, I also need you to be strong. whatever the fuck is going on, we need to be positive | Bright and immovable. Determined optimism used as a weapon against the situation; the swearing does not break it. |
| D-08-063 | Tracey | Positive? They have our friends!! you—!! | Anger surging up — then she hears herself and stops dead on the last word. **The sentence should die in her mouth.** |
| D-08-064 | Tracey | You are right... I'm sorry Holly. I'll be back soon. | Apologetic, grateful, quiet. The closest she comes to kindness all day. |
| D-08-065 | Holly | You can't be scared now Tracey. I trust in you. Go now! | Sending her away with total, unearned confidence. She wants Tracey to believe it more than she believes it herself. |

**Systems**
- Cabin staging + interior lighting (candles, boarded windows)
- Full cutscene system: letterboxing, control lockout, scripted animation sequencing, camera safety inside a small interior
- Radio dialogue with filter
- **Pistol** acquisition; **backpack** → storage 4→10; **2× marijuana blunt** granted
- Skybox/global-illumination swap while the player is indoors and cannot see it
- Mocap: the heaviest animation load in the demo

**Teaching function:** ranged combat exists; the island moves; there is a deadline.

---

## SCENE 9 — THE MINE (19:00)

**Beats**
1. Northwest with the compass, in falling light. Wolves and men in black are out here; **both are avoidable with care**. A **tower** stands above the treeline in the distance — she does not go near it. *(The tower is set dressing / a soft boundary, not a location you enter.)*
2. Mine entrance: a **burning torch** and a **totem with a skull** set on it. Among litter on the ground, a **flashlight lying face down, switched on**. She picks it up. **Scott's name is written on the side in Sharpie.** → **Flashlight acquired; visible-breath particle enabled from here on.**
3. Inside: the tunnel runs mostly straight and mostly downhill. **Total darkness — no global illumination, no skybox, no bounce light.** Only the flashlight and enemy lamps. Blood on the floor leads ahead.
4. The far end opens into a **room lined with bunk beds**, with a **makeshift infirmary** through a doorway to one side. **Scott is on the floor. Shannon is nowhere.**
5. Tracey finds the gut wound and presses down on it.
6. **Vernon on the radio** talks her through it. The infirmary has **bandages and morphine** — the walls still carry **Japanese posters and signage thirty years out of date**. She does the work herself.
7. Vernon tells her to get out: it is getting dark. **Deadline stated: be safe before 3 a.m.** She asks what that means; he starts to explain something enormous, catches himself, and ends up shouting at her to move.
8. **Stretcher** found propped among the bunk rows next to old stains on the concrete. She loads Scott onto it. → **Stretcher mode acquired.**
9. Exit is a door at the back of the infirmary with a **pin-up poster nailed to it**.

| ID | Speaker | Line | Direction |
|---|---|---|---|
| D-09-001 | Tracey | Scott!... | Recognition landing badly. Quiet, winded, a name said as bad news. |
| D-09-002 | Tracey | oh no! | Small, sinking. Two words she does not want to be true. |
| D-09-003 | Tracey | Please be okay, please be okay | A whispered chant to nobody, to keep her legs moving. Faster the second time. |
| D-09-004 | Tracey | SCOTT!! | A full scream of his name, all panic, no control |
| D-09-005 | Scott | Trey... hello... | Thin and slow, shallow breaths between words. Genuinely pleased to see her. |
| D-09-006 | Tracey | oh God, you are bleeding! | Horrified and moving fast |
| D-09-007 | Scott | just a scratch... Shannon, she escaped... | Breathless and dismissive. Trying to be funny for her sake and not managing it. |
| D-09-008 | Scott | I'm sorry Tracey... I'm sorry for all..... | Fading. An apology far too large for the sentence carrying it, and he trails off before saying what it is for. |
| D-09-009 | Tracey | shut up Scott, you can't die now | Angry because the alternative is worse. An order, not comfort. |
| D-09-010 | Vernon | Kid? are you okay? I've been trying to call you for a while | *(radio)* Worried, covering it with brusqueness |
| D-09-011 | Tracey | Yes, I'm okay, I found Scott, he's bleeding bad | Relief for half a second, then straight back into fear |
| D-09-012 | Tracey | please mister! help me! | Openly begging. All the hardness gone out of her voice. |
| D-09-013 | Vernon | Where are you? | *(radio)* Three words, hard and fast. He needs information, not feelings. |
| D-09-014 | Tracey | I'm... inside the mine, there are a lot of bunk beds | Lost. Describing what she can see because she does not know what else to give him. |
| D-09-015 | Vernon | Bunk beds... You are near an exit. That's why the radio works now | *(radio)* Working it out aloud on the pause, then explaining. He knows this place. |
| D-09-016 | Vernon | Listen kid, I'm far from where you are. You'll have to save him yourself. | *(radio)* Level and regretful. Telling her no, and he knows what it costs her. |
| D-09-017 | Tracey | Mister! I don-t... I-! | Stammering, coming apart, unable to finish either sentence |
| D-09-018 | Vernon | Calm yourself, God has not abandoned you. | *(radio)* Steady and completely sincere. **He is not being poetic — he means it literally.** |
| D-09-019 | Vernon | There is an infirmary next to you. Bandages. Morphine. Use them. Hurry | *(radio)* Clipped and instructional. Each item its own short sentence, like a man handing over tools. |
| D-09-020 | Scott | huh... that's the spot... I'm happy you came along on the trip after all... | Sagging with relief as the morphine goes in. Slurred, warm, honest in a way he would not risk sober. |
| D-09-021 | Tracey | shut up Scott, don't talk | Sharp, refusing the moment. She cannot hear this right now. |
| D-09-022 | Scott | yes ma'am... | Amused and obedient. Almost asleep. |
| D-09-023 | Vernon | What's up kid? | *(radio)* Impatient, but checking |
| D-09-024 | Tracey | I treated Scott, the bleeding stopped. | Exhausted and quietly proud. She did it herself. |
| D-09-025 | Vernon | Good, good... | *(radio)* Low and warm. Approval from a man who does not give much of it. |
| D-09-026 | Vernon | listen, you need to get outta there. It's getting real dark outside | *(radio)* Tone changes completely. Urgent, and afraid of the dark in a way that is not rhetorical. |
| D-09-027 | Tracey | but Scott! he can't move! | Protesting, panicking at the idea of leaving him |
| D-09-028 | Vernon | you'll have to drag him, there should be a stretcher around | *(radio)* Blunt and practical. No sympathy, because sympathy would waste time. |
| D-09-029 | Vernon | also listen kid... sorry, Tracey right? | *(radio)* Starts as a command, then stops and softens. He is realizing he has not once used her name. |
| D-09-030 | Tracey | yeah... | Quiet and slow. Something in her settles at being asked. |
| D-09-031 | Vernon | Tracey, you gotta get somewhere safe, you can't come back, the enemy is all around here | *(radio)* Deliberate and heavy. Every clause is a rule she has to follow. |
| D-09-032 | Tracey | But! what about the others? Shannon? | Insistent. She is not letting this go. |
| D-09-033 | Vernon | save yourself first, then we'll see | *(radio)* Cold, and honest about being cold |
| D-09-034 | Vernon | in the infirmary there is an exit. Head north, there is a chapel where you can hide | *(radio)* Total clarity. This is the part he is sure of. |
| D-09-035 | Vernon | time is running out, I'll try to meet you there | *(radio)* Hurried. "I'll try" is doing a lot of work. |
| D-09-036 | Vernon | whatever you do, don't let them catch you, get to safety before 3 a.m. | *(radio)* The deadline stated as though it were common knowledge, which for him it is. |
| D-09-037 | Tracey | 3 a.m.? what do you mean | Baffled and newly frightened. The question is genuine — this means nothing to her. |
| D-09-038 | Vernon | You'll die, anything dies... I... we'll talk about that later, you need to get the hell outta there NOW! | *(radio)* Starts to explain something enormous, catches himself, shuts it down, ends shouting. He is not having this conversation now. |
| D-09-039 | Tracey | hey... Vernon right? | Softly, slowly, changing the subject. The first thing she has said to him that is not about survival. |
| D-09-040 | Vernon | yes. | *(radio)* One flat syllable |
| D-09-041 | Tracey | thank you... be careful | Awkward and real. Two things she almost never says, said carefully. |
| D-09-042 | Vernon | heh, you be careful kid. you can do this, I'll meet you at the chapel | *(radio)* A short laugh first. Gruff, kind, and certain about her in a way nobody else has been today. |
| D-09-043 | Scott | who's that? | Idle curiosity. Drifting. |
| D-09-044 | Tracey | Vernon, he's Holly's uncle | Explaining. Half her mind still on the radio. |
| D-09-045 | Scott | ahhh, yes, the 'boogeyman' | Recognition, mild amusement. "Boogeyman" in affectionate quotation marks. |
| D-09-046 | Tracey | Scott, I think he is our best chance of getting outta here alive | Irritated on Vernon's behalf. Defensive of someone she met four hours ago. |
| D-09-047 | Scott | yeah. I'm joking, Holly can be weird sometimes too | Conceding easily. Fond, unbothered, three-quarters asleep. |
| D-09-048 | Tracey | Now wait here | Bossy and brisk, covering for the fact that she does not want to leave him |
| D-09-049 | Scott | not going anywhere | Dry. The joke of a man who cannot stand. |
| D-09-050 | Scott | haha, careful with the goods, still hurts a bit you know? haha | Laughing through pain and regretting the laugh. Flirting badly and enjoying it. |
| D-09-051 | Tracey | *panting and straining* Do you have any idea how heavy you are? | Delivered through real physical effort — **the panting is the performance**, and the sentence has to fight its way out between breaths. |
| D-09-052 | Scott | hahahaha, fat, slim, I don't care as long as I'm your type... | Delighted with himself. A line he has clearly been saving. |
| D-09-053 | Scott | ah, so you can still blush, cute... | Soft, surprised, admiring. **He is not joking on this one.** |
| D-09-054 | Tracey | shut up Scott, look around you, you are about to die | Flustered and furious about being flustered. All the heat is embarrassment, none of it is anger. |
| D-09-055 | Tracey | let's get out of here | Impatient, and back to work |
| D-09-056 | Scott | aye aye, you have the wheel captain | Cheerfully handing over command. Weak, but game. |

**Systems**
- Mine geometry (tunnels, bunk room, infirmary) — see the non-Euclidean question in the staging issue
- Total-darkness lighting mode: no skybox, no global illumination
- **Flashlight** mechanic (sway on move, more on sprint)
- **Visible breath** particle, gated on low stamina, enabled from here on
- Audible-distance **sound layer: mine**
- Bandages + morphine items; **morphine high stat** demonstrated on Scott, not Tracey
- **Stretcher mode**: speed penalty, no weapons/inventory/map, collider that must be navigated
- Checkpoint candidates: mine entrance, after treating Scott

**Teaching function:** flashlight, darkness, stealth in a corridor, the stretcher constraint.

---

## SCENE 10 — THE NIGHT ESCAPE (01:00)

**Beats**
1. Outside: **fully dark, colder**. Wolves calling a long way off.
2. She switches the lamp on — **Scott immediately tells her to turn it off; they'll be spotted.** Light dies instantly. → *Explicit mechanical instruction to the player: the flashlight makes you visible.*
3. She can't read the compass in the dark. Scott, flat on his back, talks her into using the **North Star** — locate the Big Dipper, draw the line. He is delighted; this is the happiest he has sounded since the mine.
4. **The North Star is red.** Scott confirms it should not be. This is the first supernatural fact the player is asked to accept.
5. **Men in the dark with torches, sweeping.** Avoidable with patience. → Spotter enemies, patrol behaviour, lamps.
6. The ground rises. A **church on a hill** appears against the sky. Nearer: a **stone fountain well with a statue of Our Lady of Guadalupe** over it.
7. Scott is thirsty. She sets the stretcher down and **fills the canteen at the well**. She crouches over him and tips the canteen to his mouth, head down, eyes on his face.
8. She looks up. **They are surrounded** — figures in black in a silent ring around the well, close, and she has no idea how long they have been there.
9. One begins to **hum**, then another, until the ring hums together.
10. One throws his head back, screams and charges — and a **ring of blue fire snaps up around the well and burns him where he stands.**
11. The humming grows. The circle begins to move. **Vernon on the radio: he is creating a distraction.**
12. **Vernon's distraction:** an explosion out in the trees with a heavy emissive flash, **three flares** rising, gunshots, figures in the ring falling, the rest turning and running toward the fire.
13. **"RUN TRACEY RUN!"** She runs. Two more cultists on the road up the hill.
14. **The world changes:** sky turning red above the trees, earthquake, trees cracking, whistles answering each other in the dark.
15. She hauls Scott up to the **chapel door** and drops the stretcher. Beside the doorway, sandbagged into the earth, a **Japanese gun emplacement thirty years abandoned** (Type 92 turret).
16. She has her hand on the chapel door when **the radio starts calling behind her** — she dropped it at the bottom of the steps, far below. She goes back down for it.
17. She crouches, picks it up, and when she stands **the Furman is on the road with her** — huge, staring, howling, gathering itself to charge. **Boss fight.** (The turret is the intended tool. The Furman collides with the **breakable statue** on the stairs at a scripted moment and is stunned.)
18. The beast falls howling. The shaking gets worse.
19. She gets back to the door and **cannot open it.** She hammers on it with both fists. It does not give.
20. Her **wristwatch begins to chirp.** She turns to the sky: a **red moon** comes up over the trees, far too fast, wrong in every way.
21. **Behind her, the chapel door opens.**
22. Gasp, then a long scream of pure horror at whatever has just opened it.
23. **Screen fades to black. All sound is met with silence.** END OF DAY ONE.

| ID | Speaker | Line | Direction |
|---|---|---|---|
| D-10-001 | Tracey | now... to the north... can't see shit | Remembering the instruction, then immediately frustrated by it. Muttered. |
| D-10-002 | Scott | No! turn it off! They'll spot us! | Urgent whisper from the stretcher, sharp with alarm |
| D-10-003 | Tracey | But! I can't even see the compass, we need to go north! | Whispering back and arguing at the same time. Frustrated, reasonable, stuck. |
| D-10-004 | Scott | The north star! | A sudden bright idea from flat on his back. Genuinely pleased. |
| D-10-005 | Tracey | what? | Blank |
| D-10-006 | Scott | The north star! well, look up at the sky and find the Big Dipper | Warming to a favorite subject. Whispered, but enthusiastic. |
| D-10-007 | Tracey | What's the Big Dipper? | Genuinely does not know. Impatient about not knowing. |
| D-10-008 | Scott | Well, it's a constellation, it looks like a kite, yeah, at this time of year the tail is a bit downwards and the kite is upwards, hmm, look at the 2 stars at the top and draw a line to the right and-- | Off and running, delighted, over-explaining to a woman dragging him through a forest — **and then cut off dead mid-word** |
| D-10-009 | Tracey | Hey Scott? What's that red star? | Interrupting. Not curious — wary. Something up there is wrong. |
| D-10-010 | Scott | Red star? | Repeating it back. The enthusiasm drains out between the two words. |
| D-10-011 | Scott | that's... that's the north star... | Slow, quiet, appalled. He knows exactly what he is looking at, and it is not what it should be. |
| D-10-012 | Scott | I- I don't get it, it's not red. | Trying to reason it away and failing. Small. |
| D-10-013 | Tracey | but that's the north? | Practical, pressing him. She wants a direction, not an astronomy problem. |
| D-10-014 | Scott | It should be... that's scary | Honest, and frightened by his own honesty |
| D-10-015 | Tracey | don't mention it... we need to move quiet now... | Shutting the conversation down, then dropping to a whisper for the last three words |
| D-10-016 | Scott | you know... *smacking his lips* I'm a bit thirsty... | Tired and mildly apologetic about asking |
| D-10-017 | Tracey | yeah? you want anything else? | Sardonic. The put-upon tone of a woman who has been dragging a man uphill for an hour. |
| D-10-018 | Scott | hah, always grumpy huh? | Charmed. He likes her exactly as she is. |
| D-10-019 | Tracey | well, it's your lucky day, I have a canteen and there seems to be water | Grudging generosity, dressed up as condescension |
| D-10-020 | Scott | ahhh... thank you Tracey. I hate being thirsty | Deep gratitude for a small thing. Almost tender. |
| D-10-021 | Tracey | oh yeah? well, I hate... - | Starting a joke back — and stopping mid-word, absolutely cold. **The break must be instant.** |
| D-10-022 | Tracey | AHHHHHHHHHHHH! | A raw, unbroken scream of terror. The loudest sound she has made so far. |
| D-10-023 | Scott | FUCK | One word from the ground, flat with fear |
| D-10-024 | Tracey | What the fuck! | Frightened past comprehension. **Not an exclamation — a genuine question with nobody to answer it.** |
| D-10-025 | Vernon | Kid, you gotta make a run! you are nearly there. I'll get them out of your way | *(radio)* Gasping, moving, alarmed — he is somewhere doing something, and it is costing him |
| D-10-026 | Vernon | RUN TRACEY RUN! | *(radio)* Screamed, everything he has left. **Second time he uses her name in the level.** |
| D-10-027 | Scott | What's going on?!!! | Shouted from the stretcher over the noise. Alarmed and helpless. |
| D-10-028 | Scott | We need to get inside!! | Urgent, insistent, close to panic |
| D-10-029 | Scott | A turret?! | Confused and unsettled by it. It does not belong here. |
| D-10-030 | Tracey | the fucking radio! wait Scott! | Alarmed |
| D-10-031 | Tracey | JESUS! | Torn out of her at the top of her range. Terror and relief in the same breath, after the thing has gone down. |
| D-10-032 | Tracey | *gasp* AHHHHHHHHHHHH! | A short hopeless gasp, then a long scream of pure horror at whatever has just opened the door |

**Systems**
- Full-darkness night lighting; flashlight-as-liability
- Spotter patrols with lamps
- Well staging (well + Virgin of Guadalupe statue, clearing free of trees)
- **Blue circle of fire** VFX (ignition spreading both ways around the ring, then extinguish)
- **Vernon's distraction** set-piece: explosion, emissive flash, 3 flares, tracer fire, scripted enemy deaths, shake
- **Skybox swap to red** + apocalyptic global red illumination + fog
- **Earthquake effect**: tree shake radius, escalating rumble, cracking sounds
- **Red Moon**: 3D celestial body, cutscene-controlled rise
- Chapel staging: hill, stairs, wooden crosses, **breakable statue**, **Type 92 turret emplacement**
- **Turret mechanic**: 30-round belt, -10°/+45° traverse limits, shell ejection, heavy acceleration
- **Furman boss fight**
- Stretcher mode throughout
- Hard cut to black + total silence = end of demo

---

## Item / capability acquisition order (drives gating)

| Order | Thing | Where | Effect |
|---|---|---|---|
| 1 | Canteen | Scene 1, camp table | Releases the stamina lock |
| 2 | Walkie-talkie | Scene 2, red cooler | Radio dialogue channel |
| 3 | Matches | Scene 2, red cooler | (item, unused in demo flow) |
| 4 | Beer / soda / crackers | Scene 2, blue cooler | Consumables; **more than 4 types available on purpose** |
| 5 | Tent key | Scene 4, rug under RV | Unlocks Will's tent |
| 6 | Map + compass pouch | Scene 4, Will's tent | Navigation |
| 7 | **Boots** | Scene 6, Glade | Speed up, noise up |
| 8 | Polaroid photo | Scene 6, Glade table | Narrative item |
| 9 | **Pickaxe** ("axe") | Scene 6, telescope | Melee, auto-equipped |
| 10 | **.45 Pistol** | Scene 8, from Holly | Ranged; not auto-equipped |
| 11 | **Backpack** | Scene 8, from Vernon | Storage 4 → 10 |
| 12 | 2× marijuana blunt | Scene 8, from Holly | Weed-high stat |
| 13 | **Flashlight** | Scene 9, mine entrance | Light + visibility liability |
| 14 | Bandages, morphine | Scene 9, infirmary | Heal Scott (scripted) |
| 15 | **Stretcher** | Scene 9, bunk room | Movement mode |
| 16 | **Turret** | Scene 10, chapel | Boss weapon |

**The double-barrel shotgun has no pickup point in the screenplay.** It is specified in the Linear issues as a real demo weapon with its own pickup. The developer must choose where it appears — the mine or the road to the chapel are the natural candidates. **Flag this when the shotgun issue comes up.**

## Objective list (drives the objective + pause-menu display)

1. Turn off the alarm
2. Find water
3. Find your boots
4. Answer the radio
5. Get the compass from Will's tent
6. Head east to the Glade
7. Look through the telescope
8. Survive the wolves
9. Reach the cabin
10. Go northwest to the mine
11. Find Scott
12. Treat Scott's wound
13. Get Scott out of the mine
14. Head north to the chapel
15. Get to the chapel before 3 a.m.
16. Kill the Furman
17. Open the chapel door

## Enemies used, in order of first appearance

1. **Wolf** — Running Line (Scene 5), Group behaviour (Scene 6)
2. **Zealot** — Scene 7 (ambush, backstab), recurring
3. **Spotter** — Scene 9 exterior and Scene 10 (patrols with lamps)
4. **Furman** — Scene 10, boss

The **Priest** and **Stone Totem** from the Pitch do **not** appear in the demo.
