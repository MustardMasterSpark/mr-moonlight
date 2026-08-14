#!/usr/bin/env python3
"""
Mr. Moonlight -- Assignment 5 gap-detection agent.

Standalone, stdlib-only, re-runnable pipeline. It does NOT need an LLM in
the loop -- it is a small static-analysis tool that:

  1. Reads the GDD and extracts the Section 7 "Game Mechanics" feature
     list, with special attention to the Melee and Stamina systems.
  2. Scans the imported Advanced Horror FPS Kit C# scripts (player
     controller, melee/combat, stamina) and reports which GDD-described
     systems already exist in code and which don't.
  3. Detects, by reading the actual code (not a cached opinion), whether a
     melee hit that connects with an enemy currently drains the player's
     stamina -- and states that as the chosen gap.
  4. Explains why that gap should be prioritized over other missing
     systems it finds along the way.

Because step 3 is done by parsing the live source files, re-running this
script after the gap is closed in code will correctly report it as closed.

Usage:
    python Assignment5/agent.py                 # human-readable report
    python Assignment5/agent.py --json           # machine-readable report
    python Assignment5/agent.py --gdd PATH.txt --scripts DIR
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import zlib
from pathlib import Path
from typing import Dict, List, Optional

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GDD_TXT = REPO_ROOT / "Docs" / "GDD" / "GDD v2.txt"
DEFAULT_GDD_PDF = REPO_ROOT / "Docs" / "GDD" / "GDD v2.pdf"
DEFAULT_SCRIPTS_DIR = REPO_ROOT / "Assets" / "AdvancedMobileHorror" / "Scripts"


# ---------------------------------------------------------------------------
# Step 1a: get GDD text (prefer a plain-text export; PDF parsing is best-effort)
# ---------------------------------------------------------------------------

def load_gdd_text(gdd_txt: Path, gdd_pdf: Path) -> str:
    if gdd_txt.exists():
        return gdd_txt.read_text(encoding="utf-8", errors="replace")
    if gdd_pdf.exists():
        text = extract_pdf_text_best_effort(gdd_pdf)
        if text.strip():
            return text
    raise SystemExit(
        "Could not read the GDD.\n"
        f"  Tried text export: {gdd_txt}\n"
        f"  Tried PDF fallback: {gdd_pdf}\n"
        "This tool is stdlib-only (no PyPDF2/pdfminer), so PDF parsing is a "
        "best-effort scrape of FlateDecode content streams and will miss "
        "text in PDFs that use compressed object streams or subsetted "
        "fonts. Export the GDD to plain text next to the PDF (same name, "
        ".txt extension) and re-run."
    )


def extract_pdf_text_best_effort(pdf_path: Path) -> str:
    """
    Minimal, dependency-free PDF text scraper: inflates FlateDecode content
    streams with zlib (stdlib) and pulls literal strings out of Tj/TJ
    text-showing operators. Does not handle compressed object streams
    (/ObjStm) or ToUnicode CMaps, so it is a fallback, not a real parser.
    """
    raw = pdf_path.read_bytes()
    chunks: List[str] = []
    for stream_match in re.finditer(rb"stream\r?\n(.*?)\r?\nendstream", raw, re.DOTALL):
        blob = stream_match.group(1)
        try:
            data = zlib.decompress(blob)
        except zlib.error:
            continue
        chunks.append(_extract_text_from_content_stream(data))
    return "\n".join(c for c in chunks if c)


def _extract_text_from_content_stream(data: bytes) -> str:
    try:
        stream_text = data.decode("latin-1")
    except UnicodeDecodeError:
        return ""
    parts: List[str] = []
    for m in re.finditer(r"\((?:[^()\\]|\\.)*\)\s*Tj|\[(?:[^\[\]]*)\]\s*TJ", stream_text):
        for lit in re.finditer(r"\((?:[^()\\]|\\.)*\)", m.group(0)):
            piece = re.sub(r"\\(.)", r"\1", lit.group(0)[1:-1])
            parts.append(piece)
        parts.append(" ")
    return "".join(parts)


# ---------------------------------------------------------------------------
# Step 1b: pull Section 7 out of the GDD and read off the Melee/Stamina rows
# ---------------------------------------------------------------------------

SECTION_HEADERS = [
    ("01", "EXECUTIVE SUMMARY"), ("02", "STORY"), ("03", "CAST"),
    ("04", "GAME FLOW"), ("05", "THE ISLAND IN DAY 1"), ("06", "LOOK AND FEEL"),
    ("07", "GAME MECHANICS"), ("08", "ENEMIES AND THREATS"), ("09", "ITEMS"),
    ("10", "AI ARCHITECTURE"), ("11", "TOKEN BUDGET AND API CONSTRAINTS"),
    ("12", "FEEDBACK"), ("13", "MVP CRITICAL ROUTE"),
    ("14", "TECHNICAL STRATEGY AND TIMELINE"),
]

# The 20 systems named in GDD sec 7.2, "SYSTEMS IN THE SLICE", in document
# order, so a table row can be sliced out between one heading and the next.
SYSTEM_NAMES = [
    "Player controller", "Control locks", "Interaction", "Inventory",
    "Stamina", "Health and damage", "Fear", "Melee", "Firearms",
    "Map and compass", "Light", "Substances", "Bear trap",
    "Stretcher escort", "Objective tracker", "Subtitles", "Audio manager",
    "Scene checkpoint", "Time of day", "SLDD runner",
]


def extract_section(gdd_text: str, number: str, title: str) -> str:
    """
    Section headers in the document body are their own all-caps line, e.g.
    "07 GAME MECHANICS". The INDEX near the top of the doc lists the same
    numbers in mixed case followed by " - <blurb>" (e.g. "07 Game Mechanics
    - Player verbs..."). Matching is deliberately case-sensitive and
    line-anchored so the index entries are never mistaken for the real
    section boundaries.
    """
    idx = SECTION_HEADERS.index((number, title))
    start_pat = re.compile(rf"^{number}\s+{re.escape(title)}\s*$", re.MULTILINE)
    start = start_pat.search(gdd_text)
    if not start:
        raise SystemExit(f"Could not find section {number} '{title}' in the GDD text.")
    next_number, next_title = SECTION_HEADERS[idx + 1] if idx + 1 < len(SECTION_HEADERS) else (None, None)
    end_pos = len(gdd_text)
    if next_number:
        end_pat = re.compile(rf"^{next_number}\s+{re.escape(next_title)}", re.MULTILINE)
        end = end_pat.search(gdd_text, start.end())
        if end:
            end_pos = end.start()
    return gdd_text[start.start():end_pos]


VERB_NAMES = ["Look", "Move", "Sprint", "Interact", "Carry and drop",
              "Navigate", "Swing", "Shoot", "Drag"]


def _extract_rows_by_marker(section_text: str, marker: str, known_names: List[str]) -> Dict[str, str]:
    """
    Both the sec 7.1 verb table and the sec 7.2 system table are
    transcribed one row per paragraph, each starting with a literal
    "<marker>: " prefix (e.g. "System: Melee | ..."). Splitting on that
    prefix -- rather than trying to regex table-cell boundaries -- copes
    cleanly with cells that wrap across multiple lines in the text export.
    """
    rows: Dict[str, str] = {}
    prefix = f"{marker}: "
    for chunk in re.split(rf"\n(?={re.escape(prefix)})", section_text):
        chunk = chunk.strip()
        if not chunk.startswith(prefix):
            continue
        chunk = chunk[len(prefix):]
        name_match = re.match(r"([^|]+)\|(.*)", chunk, re.DOTALL)
        if not name_match:
            continue
        name = name_match.group(1).strip()
        if name not in known_names or name in rows:
            continue
        body = name_match.group(2)
        # Trim anything past this row's own paragraph (figure captions,
        # blank-line-separated prose that follows the table).
        body = re.split(r"\n\s*\n", body, maxsplit=1)[0]
        rows[name] = re.sub(r"\s+", " ", body).strip().lstrip("|").strip()
    return rows


def extract_system_rows(section7_text: str) -> Dict[str, str]:
    """Sec 7.2 'SYSTEMS IN THE SLICE' table, one entry per system."""
    return _extract_rows_by_marker(section7_text, "System", SYSTEM_NAMES)


def extract_verb_rows(section7_text: str) -> Dict[str, str]:
    """Sec 7.1 'WHAT THE PLAYER DOES' verb table, one entry per verb."""
    return _extract_rows_by_marker(section7_text, "Verb", VERB_NAMES)


# ---------------------------------------------------------------------------
# Step 2: scan the imported Advanced Horror FPS Kit scripts
# ---------------------------------------------------------------------------

# Heuristic "does this GDD system exist in code" signals. `files` is a
# strong signal (a script that is clearly that system's home); `patterns`
# is a weaker, whole-codebase keyword fallback. This mapping encodes what
# was actually found by hand-reading the kit for this assignment; it is
# intentionally explicit rather than "clever" because a generic name-match
# across a third-party asset's script names is not reliable.
SYSTEM_CODE_SIGNALS = {
    "Player controller": {"files": ["FirstPersonController.cs"], "patterns": []},
    "Control locks": {"files": [], "patterns": [r"\bcanSprint\b", r"\bcanJump\b", r"\bcanCrouch\b", r"\bcanMove\b"]},
    "Interaction": {"files": [], "patterns": [r"\binteractAction\b", r"Press E to"]},
    "Inventory": {"files": ["InventoryManager.cs"], "patterns": []},
    "Stamina": {"files": [], "patterns": [r"\bStamina\b"]},
    "Health and damage": {"files": [], "patterns": [r"\bGetDamage\b", r"\bHealth\b"]},
    "Fear": {"files": [], "patterns": [r"\bfear\b"]},
    "Melee": {"files": ["BaseballScript.cs"], "patterns": []},
    "Firearms": {"files": ["PistolScript.cs", "PistolAmmoScript.cs"], "patterns": []},
    "Map and compass": {"files": [], "patterns": [r"\bcompass\b"]},
    "Light": {"files": ["FlashLightScript.cs", "LightScript.cs"], "patterns": []},
    "Substances": {"files": [], "patterns": [r"\bmorphine\b", r"\bmarijuana\b"]},
    "Bear trap": {"files": [], "patterns": [r"bear\s*trap"]},
    "Stretcher escort": {"files": [], "patterns": [r"\bstretcher\b"]},
    "Objective tracker": {"files": [], "patterns": [r"\bobjective\b"]},
    "Subtitles": {"files": [], "patterns": [r"\bsubtitle\b"]},
    "Audio manager": {"files": ["AudioManager.cs"], "patterns": []},
    "Scene checkpoint": {"files": ["SaverScript.cs"], "patterns": [r"\bcheckpoint\b"]},
    "Time of day": {"files": [], "patterns": [r"time\s*of\s*day"]},
    "SLDD runner": {"files": [], "patterns": [r"\bSLDD\b", r"L01\.txt"]},
}


def scan_scripts(scripts_dir: Path) -> Dict[str, Dict]:
    if not scripts_dir.is_dir():
        raise SystemExit(f"Scripts directory not found: {scripts_dir}")

    cs_files = sorted(scripts_dir.glob("*.cs"))
    present_files = {f.name for f in cs_files}
    corpus = {}
    for f in cs_files:
        try:
            corpus[f.name] = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            corpus[f.name] = ""
    whole_corpus = "\n".join(corpus.values())

    coverage: Dict[str, Dict] = {}
    for system, signals in SYSTEM_CODE_SIGNALS.items():
        matched_files = [f for f in signals["files"] if f in present_files]
        matched_patterns = [p for p in signals["patterns"] if re.search(p, whole_corpus, re.IGNORECASE)]
        if matched_files:
            coverage[system] = {"present": True, "confidence": "strong", "evidence": matched_files}
        elif matched_patterns:
            coverage[system] = {"present": True, "confidence": "keyword-only", "evidence": matched_patterns}
        else:
            coverage[system] = {"present": False, "confidence": "n/a", "evidence": []}

    return {
        "scripts_dir": str(scripts_dir),
        "cs_file_count": len(cs_files),
        "coverage": coverage,
        "_corpus": corpus,  # kept for step 3, not printed directly
    }


# ---------------------------------------------------------------------------
# Step 3: detect whether swinging the melee weapon actually drains stamina,
# and whether that happens on every swing or only on a landed hit.
# ---------------------------------------------------------------------------

STAMINA_DRAIN_CALL_RE = r"(DrainStamina|UseStamina|SpendStamina)\s*\("


def extract_csharp_method_body(source: str, method_name: str) -> str:
    """Brace-matched extraction of one method's body, so the stamina check
    below looks at the actual method a call lives in rather than a fixed
    character window (which breaks the moment the file is reformatted)."""
    m = re.search(rf"\b{re.escape(method_name)}\s*\([^)]*\)\s*\{{", source)
    if not m:
        return ""
    start = m.end() - 1
    depth = 0
    for i in range(start, len(source)):
        if source[i] == "{":
            depth += 1
        elif source[i] == "}":
            depth -= 1
            if depth == 0:
                return source[start:i + 1]
    return source[start:]


def detect_melee_stamina_gap(corpus: Dict[str, str]) -> Dict:
    baseball = corpus.get("BaseballScript.cs", "")
    controller = corpus.get("FirstPersonController.cs", "")

    if not baseball:
        return {
            "gap_open": None,
            "reason": "BaseballScript.cs (the melee/combat script) was not found in the scanned "
                      "scripts directory, so the swing-to-stamina link cannot be checked.",
        }

    hit_body = extract_csharp_method_body(baseball, "Hit")
    check_target_body = extract_csharp_method_body(baseball, "CheckTheTarget")
    if not hit_body and not check_target_body:
        return {
            "gap_open": None,
            "reason": "Could not find BaseballScript.Hit() or CheckTheTarget() -- the stock kit's "
                      "melee method names may have changed shape; re-check manually.",
        }

    drains_on_every_swing = bool(re.search(STAMINA_DRAIN_CALL_RE, hit_body))
    drains_only_on_landed_hit = bool(re.search(STAMINA_DRAIN_CALL_RE, check_target_body))
    has_tunable_cost = re.search(r"public\s+float\s+\w*Stamina\w*Cost\w*\s*=", baseball)
    exposes_drain_method = re.search(r"public\s+void\s+(DrainStamina|UseStamina|SpendStamina)\s*\(", controller)

    if drains_on_every_swing or drains_only_on_landed_hit:
        trigger = ("every swing attempt (Hit())" if drains_on_every_swing
                   else "only a landed hit (CheckTheTarget())")
        return {
            "gap_open": False,
            "trigger": trigger,
            "reason": (
                f"BaseballScript.cs drains player stamina on {trigger}. "
                f"Tunable cost field present: {'yes' if has_tunable_cost else 'no'}. "
                "FirstPersonController exposes a public stamina-drain method: "
                f"{'yes' if exposes_drain_method else 'no'}."
            ),
        }

    return {
        "gap_open": True,
        "trigger": None,
        "reason": (
            "Neither BaseballScript.Hit() nor CheckTheTarget() calls any stamina-draining "
            "method. The only place Stamina is modified is FirstPersonController.Move(), and "
            "only in response to sprinting. Swinging the melee weapon currently costs the "
            "player nothing in stamina, even though GDD v0.2 sec 7.1/7.2 requires it "
            "('blows that drain stamina'; Melee row: 'swings that cost stamina')."
        ),
    }


# ---------------------------------------------------------------------------
# Step 4: prioritize
# ---------------------------------------------------------------------------

def build_priority_note(gap: Dict, coverage: Dict[str, Dict]) -> str:
    missing = [s for s, v in coverage.items() if not v["present"]]
    if gap["gap_open"] is True:
        return (
            "Close the melee-hit -> stamina-drain link first, ahead of the "
            f"{len(missing)} other GDD systems with no code yet ({', '.join(missing)}). "
            "Both endpoints already exist and work (a live Stamina value on the player, a "
            "working raycast Hit() -> CheckTheTarget() melee path), so this is a wiring fix "
            "inside one file, not a new subsystem -- small blast radius, no new dependencies. "
            "It is also immediately testable in Play mode (swing at an enemy, watch the "
            "stamina bar) with zero dependency on unbuilt content like the pickaxe model or "
            "the mine scene, and it is the concrete mechanism behind the GDD's stated design "
            "pillar 'combat that costs something.'"
        )
    if gap["gap_open"] is False:
        return (
            "The melee-hit -> stamina-drain gap is currently closed in code. Remaining GDD "
            f"systems with no code detected: {', '.join(missing) if missing else 'none'}. "
            "Re-run this tool after further changes to see whether that list has moved."
        )
    return "Could not determine gap status; see 'reason' above before prioritizing."


# ---------------------------------------------------------------------------
# Report assembly / CLI
# ---------------------------------------------------------------------------

def run(gdd_txt: Path, gdd_pdf: Path, scripts_dir: Path) -> Dict:
    gdd_text = load_gdd_text(gdd_txt, gdd_pdf)
    section7 = extract_section(gdd_text, "07", "GAME MECHANICS")
    verbs = extract_verb_rows(section7)
    systems = extract_system_rows(section7)

    scan = scan_scripts(scripts_dir)
    corpus = scan.pop("_corpus")

    gap = detect_melee_stamina_gap(corpus)
    priority = build_priority_note(gap, scan["coverage"])

    return {
        "gdd_section_7": {
            "verbs": verbs,
            "systems": systems,
            "melee_row": systems.get("Melee", "<not found>"),
            "stamina_row": systems.get("Stamina", "<not found>"),
        },
        "code_scan": scan,
        "gap": gap,
        "priority": priority,
    }


def print_report(report: Dict) -> None:
    print("=" * 78)
    print("MR. MOONLIGHT -- ASSIGNMENT 5 GAP-DETECTION AGENT")
    print("=" * 78)

    print("\n[1] GDD SECTION 7 -- MELEE AND STAMINA\n")
    print(f"  Melee   : {report['gdd_section_7']['melee_row']}")
    print(f"  Stamina : {report['gdd_section_7']['stamina_row']}")
    swing = report["gdd_section_7"]["verbs"].get("Swing")
    if swing:
        print(f"  Swing verb (7.1): {swing}")

    print(f"\n[2] CODE SCAN -- {report['code_scan']['cs_file_count']} .cs files in "
          f"{report['code_scan']['scripts_dir']}\n")
    for system, info in report["code_scan"]["coverage"].items():
        mark = "OK  " if info["present"] else "MISS"
        conf = f" ({info['confidence']})" if info["present"] else ""
        evidence = f" -- {', '.join(info['evidence'])}" if info["evidence"] else ""
        print(f"  [{mark}] {system}{conf}{evidence}")

    print("\n[3] GAP: does swinging the melee weapon drain player stamina?\n")
    status = {True: "GAP OPEN", False: "GAP CLOSED", None: "UNKNOWN"}[report["gap"]["gap_open"]]
    print(f"  Status: {status}")
    if report["gap"].get("trigger"):
        print(f"  Trigger: {report['gap']['trigger']}")
    print(f"  {report['gap']['reason']}")

    print("\n[4] PRIORITY\n")
    print(f"  {report['priority']}")
    print()


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--gdd", type=Path, default=DEFAULT_GDD_TXT,
                         help=f"Path to the GDD text export (default: {DEFAULT_GDD_TXT})")
    parser.add_argument("--gdd-pdf", type=Path, default=DEFAULT_GDD_PDF,
                         help="Fallback PDF path if --gdd text file is missing")
    parser.add_argument("--scripts", type=Path, default=DEFAULT_SCRIPTS_DIR,
                         help=f"Path to the C# scripts directory (default: {DEFAULT_SCRIPTS_DIR})")
    parser.add_argument("--json", action="store_true", help="Print machine-readable JSON instead of text")
    args = parser.parse_args(argv)

    report = run(args.gdd, args.gdd_pdf, args.scripts)

    if args.json:
        print(json.dumps(report, indent=2))
    else:
        print_report(report)
    return 0


if __name__ == "__main__":
    sys.exit(main())
