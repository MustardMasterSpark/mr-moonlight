# Draw Debug Tools

Stop guessing what your gameplay is doing. Draw shapes, labels and live graphs from 
your code and understand the behavior at a glance. No setup, and stripped from your 
release build.

## Requirements

Unity 2022.3 LTS or newer. Works with the built-in render pipeline, URP and HDRP.

## Install

Import the package from the Asset Store. No further setup, and nothing to place in
a scene; the first draw call initialises the system.

## Settings

The debug camera keys, float graph size, draw layer and agent path colours live on
`Resources/Settings/DDTSettings`, which the system loads on its own.

To give one scene its own settings, add the `DrawDebugTools` component to a GameObject
in that scene and assign a `DDTSettings` asset to it. A component already in the scene
takes precedence over the one created on demand.

## Hello world

```csharp
using UnityEngine;
using DDT;

public class Example : MonoBehaviour
{
    void Update()
    {
        DrawDebugTools.DrawSphere(transform.position, 0.5f, 16, Color.green);
    }
}
```

Everything the package exposes lives in the `DDT` namespace. For a shorter call site,
`using static DDT.DrawDebugTools;` lets you write `DrawSphere(...)` with no prefix.

## Upgrading from 2.x

Add `using DDT;` to each file that uses the package. No other change is needed. Type and
method names are unchanged.

Call any draw function from any script, in `Update`, `LateUpdate`, a coroutine or
an event handler.

## Release builds

Draw calls are compiled out of release player builds. The compiler removes the whole call
site, arguments included, so this line costs nothing in a shipped game:

```csharp
DrawDebugTools.Log("health: " + health);
```

Calls are kept in the editor, in development builds, and anywhere `DDT_ENABLED` appears in
`Player Settings > Scripting Define Symbols`. Add that define to keep debug drawing in a
release build.

Because the calls disappear rather than being disabled at runtime, you can leave debug
drawing in your code permanently instead of deleting it.

## Samples

The `Example` folder contains two demo scenes:

- **DrawingFunctionsExamples**: every draw function in one scene, each with its own example.
- **AgentPathVisualizationExample**: a NavMesh agent whose path is drawn live. Click anywhere
  on the ground to send the agent there.

You can delete the `Example` folder without affecting the package.

## Third-party assets

The package bundles two fonts, each under its own licence, included alongside it
in `Fonts/`:

| Font | Licence | Copyright |
|---|---|---|
| Russo One | SIL Open Font License 1.1 | Jovanny Lemonad |
| Droid Sans Mono | Apache License 2.0 | Google |

"Russo" and "Russo One" are Reserved Font Names under the OFL, so a modified copy of
that font has to be renamed.

## Documentation

https://ddt.qinteract.com
