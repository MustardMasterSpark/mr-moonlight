// Generic per-vertex wind sway for RetroLit-shaded vegetation (MRM-18, main menu). This is
// deliberately NOT menu-specific - it lives in the shared RetroLit shader so it can be reused
// for the Island/full game later (trees, bushes, tall grass props, anything RetroLit-shaded).
// See Docs/retrolit-wind-sway.md for the full writeup and how to enable it on a new material.
//
// Design: one shared wind direction/speed, plus two independently-tunable intensity
// categories (Trees, Flowers) so a scene can have gently swaying flowers under aggressively
// thrashing trees, or vice versa, without touching individual materials. A
// RetroLitWindController component (Assets/_Project/Code/Runtime/World/) drives the four
// globals every frame from Inspector sliders. With no controller in the scene, the globals
// are simply zero and every material renders motionless, same as stock RetroLit.
//
// Per-material properties (RetroLit's own Properties block, under the "Wind" header):
//   _WindEnabled     - toggle. Off (default) skips the function entirely - zero cost, zero
//                      visual change for every material that hasn't opted in.
//   _WindCategory    - 0 = Trees, 1 = Flowers. Selects which global intensity this material
//                      reads.
//   _WindHeight      - local-space height (object units, measured from the mesh's own pivot)
//                      at which sway reaches full strength. Below this it fades to zero at the
//                      pivot, so trunks/stems stay planted while canopies/petals move.
//   _WindFlexibility - per-material multiplier on top of the category's global intensity, for
//                      one species swaying more than another at the same global setting.
#ifndef RETRO_WIND_INCLUDED
#define RETRO_WIND_INCLUDED

float4 _MoonlightWindDirection; // world-space XZ wind direction (need not be normalized); yw unused
float _MoonlightWindSpeed;
float _MoonlightWindTreeIntensity;
float _MoonlightWindFlowerIntensity;

// Displaces an object-space vertex to fake wind sway. Call before transforming to world/clip
// space, on every pass that outputs a position (forward, shadow caster, depth-only,
// depth-normals) so the mesh, its shadow and its depth all sway in lockstep.
void ApplyMoonlightWind(inout float3 positionOS, float3 objectWorldPos)
{
    if (_WindEnabled < 0.5) return;

    float categoryIntensity = lerp(_MoonlightWindTreeIntensity, _MoonlightWindFlowerIntensity, saturate(_WindCategory));
    float intensity = categoryIntensity * max(_WindFlexibility, 0.0);
    if (intensity <= 0.0001) return;

    // Eases in from 0 at the pivot to full strength at _WindHeight and beyond, so the lower
    // half of the mesh (trunk, stem) stays nearly still while the top sways.
    float heightFactor = saturate(positionOS.y / max(_WindHeight, 0.0001));
    heightFactor *= heightFactor;

    // Phase keyed off the OBJECT's world position (not per-vertex), so the whole crown swings
    // as one coherent motion and nearby instances don't sway in lockstep with each other.
    float phase = _MoonlightWindSpeed * _Time.y + dot(objectWorldPos.xz, float2(0.13, 0.17));
    float sway = sin(phase) + 0.5 * sin(phase * 2.37 + 1.7); // two waves = less mechanical

    float2 windDir = normalize(_MoonlightWindDirection.xz + 1e-5);
    positionOS.xz += windDir * sway * intensity * heightFactor;
}

#endif // RETRO_WIND_INCLUDED
