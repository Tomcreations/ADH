# ADH Aestik Modding Guide Part 5

## Common Failures, Checklists, and Best Practices

This section is the practical support layer.

It focuses on what usually goes wrong, what to check before release, and what habits make ADH mods more reliable over time.

## Common Failure Points

These are the most common reasons an ADH mod fails to work:

wrong manifest filename

wrong `entry` path

missing DLL

invalid JSON

reused or unstable `id`

wrong runtime references

heavy startup behavior that harms early game flow

scene-dependent logic that assumes too much too early

event listeners that are never cleaned up

UI elements that get created more than once

These failures often look different from each other on the surface, but most of them reduce to the same root problem.

Either the package is not describing the payload correctly, or the runtime code is making assumptions that do not survive real gameplay.

## What to Avoid

Do not depend on random manifest names.

Do not expect ADH to run shell commands from the manifest.

Do not hide the DLL under messy deep nesting unless the pathing is intentional and necessary.

Do not reuse someone else’s `id`.

Do not upload empty or placeholder packages.

Do not ship a version number that does not match the build users are actually getting.

Do not write startup logic that assumes every scene object already exists.

Do not treat a successful import as proof that the mod actually works.

## Good Habits

Keep the folder name close to the mod id.

Keep the DLL name obvious.

Keep the description short and honest.

Keep the manifest exact and valid JSON.

Test with a local zip before uploading online.

Keep version numbers updated.

Keep startup logic as light as practical.

Use an already working ADH mod as a structural reference when you are unsure how to shape your own.

## Fast Package Checklist

Before importing or uploading, check:

1. does the zip contain `adh-manifest.json` or `manifest.json`
2. does the manifest point to the correct DLL
3. does the DLL actually exist in the archive
4. is the JSON valid
5. is the `id` unique
6. does the version match the build
7. is the package actually a mod rather than just support files

## Fast Runtime Checklist

Before release, also check:

1. does the mod initialize safely at startup
2. does it still behave correctly after a scene change
3. does it assume objects exist too early
4. does it clean up event hooks or persistent objects properly
5. does it avoid unnecessary heavy startup work

## Example Release Layout

The most common expected release layout is:

```text
ExampleMod/
  adh-manifest.json
  ExampleMod.dll
```

That is the cleanest baseline for a standard DLL mod.

## Final Advice

If you want your mod to work well with both ADH and Aestik, think in this order:

1. is the package valid
2. is the manifest correct
3. is the DLL built properly
4. does the runtime behavior make sense during early startup
5. does the mod still behave correctly after the game enters normal play

That order matters because it mirrors the way real failures usually appear.

You cannot debug runtime behavior correctly if the package is fundamentally wrong.

You cannot judge package quality correctly if the runtime behavior is unstable after scene transitions.

Treat packaging and runtime behavior as one connected system.

That mindset will save you a lot of time.

## The Most Useful Standard

The best real standard is not whether the mod “works once.”

The best standard is whether the mod still behaves correctly in repeated real use.

That means:

it imports cleanly

it launches cleanly

it survives scene changes

it behaves predictably when enabled and disabled

it is packaged clearly enough that another person can inspect it and understand what it is

If you can meet that standard, your mod is in good shape.
