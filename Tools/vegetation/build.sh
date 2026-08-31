#!/bin/sh
# Regenerate Docs/mrm70-biome-distribution-measured.md from the measured prefab data.
# Run from Tools/vegetation/ after re-running measure_prefabs.cs (see README.md).
set -e
cd "$(dirname "$0")"

OUT=../../Docs/mrm70-biome-distribution-measured.md

python biomes.py   > biome_tables.md
python appendix.py > appendix.md

{
  cat head.md
  cat sec_boundaries.md
  printf '\n---\n\n## 7. Per-biome distribution\n\n'
  cat biome_tables.md
  cat tail.md
  cat sec_gaia.md
  printf '\n---\n\n'
  cat appendix.md
} > "$OUT"

echo "wrote $OUT ($(grep -c '' "$OUT") lines)"
grep -q 'Every row is density-limited' "$OUT" \
  && echo "OK: no spacing/density conflicts" \
  || echo "WARNING: spacing-limited rows present - see the table at the end of section 7"
