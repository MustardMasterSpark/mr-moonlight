# WebGL Constraints — read this before writing any code

**Target:** Unity 6.3 LTS · URP · **WebGL** · itch.io · **under 1 GB** · **960×540, embedded in the itch.io page (not fullscreen)** · Xbox controller + keyboard/mouse.

**Resolution decision (2026-08-21, supersedes MRM-6's original 1920×1080 fullscreen target):** itch.io's `Default` WebGL template shows a persistent branding/fullscreen-button bar that only fully disappears in true browser fullscreen, and true fullscreen has its own letterboxing quirks across monitor aspect ratios. Rather than fight that, the game is embedded at a fixed 960×540 (exactly half of 1920×1080 — a clean 2× divisor, so nothing built at 1080p reference scales blurrily) directly in the itch.io page, not launched fullscreen. This also cuts every full-screen post-processing pass (fear vignette, chromatic aberration, etc. — see the per-pixel cost note below) to a quarter of its 1080p fill-rate cost, and the smaller, slightly softer canvas suits the game's period setting better than a crisp full-HD window. See `Docs/changelog.md` and MRM-10.

Every rule below is something that **works in the Unity editor and breaks, or silently degrades, in a browser**. That gap is where the schedule dies. A Windows build would forgive most of this. WebGL will not.

---

## 1. There is no filesystem

`Application.persistentDataPath` exists but is backed by IndexedDB, and `System.IO` on arbitrary paths does not work the way you expect.

**Consequences for this project:**

- **All spreadsheet data must be baked into ScriptableObjects at build time.** Dialogue, system messages, objectives and the event script are authored as CSV and **converted by an editor script**, not parsed at runtime. This is not an optimization — runtime CSV loading will fail or stall.
- **Checkpoint saves go to `PlayerPrefs` or IndexedDB**, and must be **flushed immediately on write**. In a browser tab, "later" often never arrives. Call `PlayerPrefs.Save()` explicitly.
- **Never** `File.ReadAllText` a config at runtime.

## 2. Threading is restricted

WebGL runs single-threaded unless you have explicitly enabled and tested threading support, which brings its own compatibility problems.

- **`System.Threading` is unreliable.** No `Thread`, no `Task.Run` for real parallelism.
- **Coroutines and `async/await` on the main thread are fine.**
- **A\* pathfinding cannot be offloaded to a worker thread.** Budget for it on the main thread, and cap concurrent agents. MRM-27 must measure the cost with **10 active agents** — the Spotter flare can spawn that many at once, and that is the game's worst case.

## 3. Audio is the biggest threat to the 1 GB ceiling

Count what this game wants: ~250 voice lines from 4 actors · ambient beds · per-prop sound pools on a forest of trees · per-terrain footsteps × barefoot/boots · enemy vocalisations · pain loops · weapon sounds · UI sounds.

**Rules:**

| Category | Load type | Compression |
|---|---|---|
| Voice lines | Streaming, or Compressed In Memory | Vorbis, low quality — speech survives it |
| Ambient loops | Streaming | Vorbis |
| One-shots (footsteps, weapons, UI) | Decompress On Load | Vorbis or ADPCM for very short clips |
| Music | Streaming | Vorbis |

- **Mono for anything that will be spatialised.** Stereo doubles the size for no benefit on a 3D-positioned source.
- **Set project-wide import presets once** (MRM-6) rather than fixing 400 clips later.
- **The audible-distance system (MRM-38) is a memory and voice-count strategy, not just a gameplay one.** Empty pools cost nothing — keep that early-out.
- **Cap simultaneous voices.** Browsers have a lower practical limit than desktop.

## 4. Post-processing is not free

The VFX issues stack a lot: health red tint + radial blur + fear vignette + heartbeat pulse + chromatic aberration + double vision + bloom + colour grading + three substance profiles.

- **MRM-6 must establish how many full-screen passes can be live at once**, and the answer will be a small number.
- **Design a priority system:** which effects survive when several are active. Suggested order — damage feedback > fear > substances.
- **One volume, many weighted overrides** beats several stacked volumes.
- Full-screen blur is per-pixel and it is on at 1920×1080. Test it there, not in a small editor game view.

## 5. Lighting

- **Bake everything you can.** Real-time lights are expensive; the demo's lighting is authored per story beat, so most of it can be baked and swapped rather than computed.
- **The mine is the exception** — it is flashlight-only, total darkness, dynamic by necessity. **Cap the number of real-time lights there.** Every Spotter carries a lamp; a group of them is a group of real-time point lights.
- **Shadows:** consider disabling them on the lamps and using a fake blob or none at all. Shadow casting per lamp will hurt.
- **Skyboxes are large cubemaps.** You have AllSky 220. **Ship 4.** Strip the rest from the build — this is the single easiest hundreds-of-megabytes win available, and it belongs in `optimization.md` as entry number one.

## 6. First load matters more than frame rate

The Assignment #10 gate is *"playing within 2 minutes."* A 900 MB build on a slow connection fails that gate even at 60 fps.

- **Enable compression** (Brotli where itch.io supports it, gzip otherwise) and verify the served headers.
- **Consider a small loading scene** so the player sees something immediately.
- **Measure cold-cache load time** from a machine that has never seen the build. This is not optional testing — it is the graded criterion.

## 7. Input

- **Gamepad support in WebGL depends on the browser's Gamepad API** and behaves differently from the editor. **Test it in an actual browser build** (MRM-8, MRM-10).
- Some browsers require a **user gesture** before the gamepad appears at all — the player may need to press a key or click once first. Plan the main menu accordingly.
- `Cursor.lockState` works but needs a user gesture and behaves differently across browsers. Test the pause/unpause cursor flow specifically.

## 8. Things that just do not work

- **`Application.Quit()` does nothing.** The main menu's Quit option needs a different behaviour in browser builds — hide it, or return to a splash. Decide in MRM-18.
- **`System.Diagnostics.Process`**, most reflection-heavy serialization, and anything expecting a console.
- **`Debug.Log` in a shipped build** still costs — strip logging for release.

## 9. Text encoding

- **UTF-8 without BOM.** Tracey's dialogue is full of apostrophes and em dashes, and the localization plan adds Spanish and Russian.
- **TextMeshPro font atlases must include every glyph used.** A missing glyph is an invisible line, and you will find it in the build, not the editor. If Russian ever ships, that atlas gets large — plan for a separate atlas per language rather than one giant one.

---

## The testing rule

**Nothing is verified until it has run in a browser, from itch.io, on a machine that is not the dev machine.**

The editor is not a preview of WebGL. It is a different platform that happens to share your code. Build early (MRM-10), build often, and treat every editor-only verification as provisional.
