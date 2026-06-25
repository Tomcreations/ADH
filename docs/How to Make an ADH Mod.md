
# ADH Aestik Modding Guide

This guide is for people making mods that are meant to work with ADH and Aestik.

It covers the practical side of the format, how ADH loads mods, how to package them, how to upload them, and what to watch out for if you want your mod to behave properly in the game.

## What ADH Is Expecting

ADH is a loader for Aestik mods.

It does two separate jobs:

1. It manages mod packages on disk.
2. It boots the runtime loader inside Aestik so enabled mods are loaded when the game starts.

From a mod author's point of view, that means your mod needs to satisfy two layers:

1. The package layer
2. The runtime layer

The package layer is the folder structure and manifest.

The runtime layer is the DLL that ADH loads into the game.

If either part is wrong, the mod may appear in the loader but not function correctly in the game.

## Where Mods Live

ADH expects mods inside Aestik's mod directory.

Typical path:

`Aestik_Data\ModLoader\Mods`

Each mod should be inside its own folder.

Good example:

```text
Mods/
  MyCoolMod/
    adh-manifest.json
    MyCoolMod.dll
```

Do not drop many unrelated DLLs and manifests loosely into the root mod folder.

Keep one mod per folder unless you are intentionally building a bundle or pack.

## When Mods Load

ADH loads enabled mods during game startup.

The loader starts after assemblies are loaded, scans the mod folder, reads manifests, resolves basic ordering, loads assemblies, and calls each mod's `Initialize` method.

That means:

1. Your mod is loaded early.
2. Your mod should not assume the player is already in active gameplay.
3. Your mod should be prepared for scene changes after startup.
4. Heavy work should not all happen in one giant blocking burst if you can avoid it.

If your mod needs scene-specific behavior, subscribe to scene load events or create a manager object that persists across scenes.

## The Required Package Format

For zip imports and normal packaged mods, ADH prefers:

`adh-manifest.json`

It also accepts:

`manifest.json`

`adh-manifest.json` should be treated as the real standard.

Do not rely on older text manifest names if you want your mod to be future-safe with the current ADH package flow.

## Basic Package Layout

```text
MyCoolMod/
  adh-manifest.json
  MyCoolMod.dll
```

If your DLL is inside a subfolder, the manifest `entry` must point to it correctly.

Example:

```text
MyCoolMod/
  adh-manifest.json
  bin/
    MyCoolMod.dll
```

Then:

```json
{
  "entry": "bin/MyCoolMod.dll"
}
```

Use clean paths.

Do not bury the DLL under deep random nesting unless there is a real reason for it.

## The Manifest

This is the main file ADH reads to understand your package.

Example:

```json
{
  "id": "my-cool-mod",
  "name": "My Cool Mod",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Short explanation of what the mod does.",
  "entry": "MyCoolMod.dll",
  "enabled": "true",
  "kind": "code",
  "priority": "0",
  "loader_version": "2.0.0",
  "source_url": "",
  "download_url": "",
  "category": "General",
  "trust_level": "Unofficial",
  "testing_status": ""
}
```

## Manifest Fields

### `id`

Your internal mod id.

Rules:

1. Keep it stable.
2. Keep it unique.
3. Keep it simple.
4. Prefer lowercase words separated by dashes.

Good:

`my-cool-mod`

Bad:

`Cool Mod FINAL TEST 4`

If you change the `id` after release, ADH may treat the new package as a different mod.

### `name`

This is the visible mod name shown in ADH.

It should be readable and human-friendly.

Good:

`My Cool Mod`

### `version`

This is your release version.

Keep it updated when you ship changes.

Good:

`1.0.0`

`1.1.0`

`2.0.0`

### `author`

Your name, handle, or team name.

This is what users see in the loader.

### `description`

A short explanation of what the mod does.

Keep it short enough to scan easily in the UI, but detailed enough that users know what they are installing.

### `entry`

The DLL ADH should load.

This is one of the most important fields.

If this points to the wrong file, the mod will package successfully but fail at runtime.

### `enabled`

Usually:

`"true"`

or

`"false"`

For a normal release package, set it to `"true"`.

### `kind`

Use:

`"code"`

for a normal DLL-based mod.

Use:

`"pack"`

for a bundle-style package.

### `priority`

A load-order hint.

Higher numbers go earlier in the UI ordering and may matter if multiple mods depend on initialization order.

If you do not need anything special, use:

`"0"`

### `loader_version`

The ADH loader version your mod targets.

Current packages in this project use:

`"2.0.0"`

If this is different, ADH may still load the mod, but version mismatches can become a support issue later.

### `source_url`

Optional website link for the mod.

Leave it blank if you do not need it.

### `download_url`

Usually not something you fill manually for portal-driven releases.

Leave it blank for local packages.

### `category`

A simple label used in the loader.

Examples:

`UI`

`Gameplay`

`Tools`

`General`

`Pack`

### `trust_level`

Use:

`Unofficial`

unless the package is an official ADH-managed release.

### `testing_status`

Normally blank.

Do not invent your own warning text here unless the ADH team has a reason to surface something specific.

## The Runtime Side

A package alone is not enough.

Your DLL also has to be shaped like a real ADH mod.

ADH loads assemblies and looks for an implementation of the runtime mod interface.

That means your mod should follow the ADH runtime pattern.

The best working example in your current materials is:

`Aestik health bar source\src\AestikEnemyHpBar.cs`

That file shows the general structure of a real mod that works with the current loader.

## What a Real ADH Mod Usually Does

A typical ADH mod:

1. Implements the runtime mod interface
2. Receives a `ModContext`
3. Uses `Initialize` for startup
4. Uses `Shutdown` for cleanup
5. Hooks scenes, game objects, or other runtime systems after initialization

Your mod should keep startup work organized and avoid one giant uncontrolled block of code inside `Initialize`.

## Good Runtime Habits

### Keep Initialization Safe

Do not assume every object already exists when your mod initializes.

If your mod depends on scene objects, wait for the correct scene or poll carefully.

### Clean Up Properly

If your mod subscribes to events or creates persistent objects, make sure `Shutdown` can clean them up.

### Log Useful Things

Use the provided logging context where it makes sense.

Do not spam logs every frame unless you are debugging something specific.

### Avoid Fragile Reflection If You Can

If there is a cleaner direct way to interact with the game, prefer that over messy reflection paths.

Reflection can work, but it becomes harder to maintain.

## Example Local Workflow

1. Write your C# mod.
2. Build the DLL.
3. Create a folder with the DLL and manifest.
4. Zip it.
5. Test the zip by importing it into ADH.
6. Launch modded Aestik.
7. Confirm the mod behaves correctly in a real scene.

## Example Zip Layout

Good:

```text
MyCoolMod.zip
  adh-manifest.json
  MyCoolMod.dll
```

Also good:

```text
MyCoolMod.zip
  MyCoolMod/
    adh-manifest.json
    MyCoolMod.dll
```

Bad:

```text
MyCoolMod.zip
  notes.txt
  screenshot.png
```

Bad:

```text
MyCoolMod.zip
  random/
    nested/
      maybe/
        MyCoolMod.dll
```

unless the manifest pathing is intentional and correct.

## Packs

Packs are for grouped installs.

Use a pack when the package is meant to deliver multiple related pieces together, not just one DLL mod.

Example pack manifest:

```json
{
  "id": "my-pack",
  "name": "My Pack",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Bundle of mods.",
  "entry": "",
  "enabled": "true",
  "kind": "pack",
  "priority": "0",
  "loader_version": "2.0.0",
  "source_url": "",
  "download_url": "",
  "category": "Pack",
  "trust_level": "Unofficial",
  "testing_status": ""
}
```

If you are just shipping one DLL mod, use `code`, not `pack`.

## Uploading Through ADH

ADH includes a developer upload flow for projects.

The normal upload flow is:

1. Open ADH
2. Open the developer section
3. Create a project or open an existing one
4. Enter the project title
5. Enter the description
6. Add an optional website URL if you want users to see a site button
7. Upload your package

The upload package still needs to be a valid ADH package.

That means:

1. It needs the manifest
2. It needs the actual payload
3. The manifest must match what is inside the archive

Uploading does not replace packaging discipline.

## What to Upload

For a standard DLL mod, upload a zip like this:

```text
MyCoolMod.zip
  adh-manifest.json
  MyCoolMod.dll
```

For a pack:

```text
MyPack.zip
  adh-manifest.json
  files...
```

Do not upload a project that contains only notes or placeholder files.

## Official and Unofficial Content

By default, user projects should be treated as unofficial unless the ADH side explicitly marks them as official.

That matters for how users interpret trust.

If you are not shipping first-party ADH content, assume:

`trust_level = "Unofficial"`

## Common Failure Points

These are the most common reasons a mod fails to work:

1. Wrong manifest filename
2. Wrong `entry` path
3. Missing DLL
4. Invalid JSON
5. Reused or unstable `id`
6. Wrong runtime references
7. Heavy startup behavior that breaks early game flow
8. Scene-dependent logic that assumes too much too early

## Things to Avoid

Do not depend on random manifest names.

Do not expect ADH to run shell commands from your manifest.

Do not hide your DLL deep inside messy nested folders unless the manifest points to it properly.

Do not reuse someone else's `id`.

Do not upload empty shells with no real mod payload.

Do not ship a version number that does not match the actual build users are getting.

## Good Habits

Keep the folder name close to the mod id.

Keep the DLL name obvious.

Keep the description short and honest.

Keep the manifest exact and valid JSON.

Test with a local zip before uploading it online.

Keep your version number updated.

Keep your startup path light unless there is a strong reason not to.

Use the existing health bar source as a working reference when you are not sure how to structure a real mod.

## Fast Checklist

Before importing or uploading, check:

1. Does the zip contain `adh-manifest.json` or `manifest.json`?
2. Does the manifest point to the right DLL?
3. Does the DLL actually exist in the package?
4. Is the JSON valid?
5. Is the `id` unique?
6. Does the version match the build you are uploading?
7. Is the package actually a mod and not just support files?
8. Does the mod initialize safely at game startup?
9. Does the mod still behave correctly after a scene change?

## Example Release Layout

```text
ExampleMod/
  adh-manifest.json
  ExampleMod.dll
```

That is the format ADH expects most often.

## Final Advice

If you want your mod to work well with both ADH and Aestik, think in this order:

1. Is the package valid
2. Is the manifest correct
3. Is the DLL built properly
4. Does the runtime behavior make sense during early startup
5. Does it still behave correctly after the game moves into normal play

If those five things are in good shape, your mod is much more likely to behave properly in real use.
