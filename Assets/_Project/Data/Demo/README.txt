ELVTR DEMO ONLY — NOT FINAL GAME CONTENT

The two files in this folder are source text for a one-off feature built for the ELVTR demo
build (Carlos's ask, 2026-09-10): the opening letterbox subtitle crawl and the red "edgy line"
that flashes on screen after a kill.

- DemoIntroLines.txt   — read one line at a time by DemoIntroSubtitles.cs
- DemoKillLines.txt    — drawn randomly (shuffled-bag, no repeats until exhausted) by
                         KillLineDisplay.cs, on every EnemyHealth.AnyDied

Neither file is meant to survive past the ELVTR submission. If the full game later needs real
intro narration or kill-quip lines, write new copy for that — do not just relabel these.

See Assets/_Project/Code/Runtime/UI/DemoIntroSubtitles.cs, LetterboxController.cs, and
KillLineDisplay.cs.
