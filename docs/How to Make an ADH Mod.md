# How to Make an ADH Mod

ADH loads mods for Aestik from folders inside the game's mod directory.

The usual location is:

`Aestik_Data\ModLoader\Mods`

Each mod should be inside its own folder.

## Basic layout

Example:

```text
MyCoolMod/
  adh-manifest.json
  MyCoolMod.dll
```

`adh-manifest.json` is the preferred manifest name.

If `adh-manifest.json` is not present, ADH will also accept:

`manifest.json`

Nothing else should be relied on.

## Simple manifest

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

## What the fields mean

`id`

Internal mod id. Keep it lowercase and stable.

`name`

The visible mod name in ADH.

`version`

Your mod version.

`author`

Your name or team name.

`description`

Short description shown in the loader.

`entry`

The DLL ADH should load.

`enabled`

Usually `"true"` or `"false"`.

`kind`

Use `"code"` for a normal mod.

Use `"pack"` only for bundle-style installs.

`priority`

Load order hint. Higher numbers go earlier in the UI order.

`loader_version`

ADH loader version target.

`source_url`

Optional site link.

`download_url`

Usually handled by the portal. Leave blank for local mods.

`category`

Simple label like `UI`, `Gameplay`, `Tools`, `General`.

`trust_level`

Use `Unofficial` unless it is a first-party ADH release.

`testing_status`

Leave blank unless the ADH team sets something specific.

## DLL mods

Normal mods are DLLs.

ADH loads assemblies that implement the runtime mod interface used by the loader runtime.

The Aestik health bar source is the best example in this folder set.

Look at:

`Aestik health bar source\src\AestikEnemyHpBar.cs`

That shows the general shape of a real ADH mod.

## Minimal process

1. Write your mod code in C#.
2. Build a DLL against the same game/runtime references your mod needs.
3. Put the DLL in its own folder.
4. Add `adh-manifest.json`.
5. Import the folder as a zip or place it directly in the mods folder.

## Zip imports

If you import a zip through ADH, the zip should contain the mod files directly or inside one mod folder.

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

Avoid zips with multiple unrelated manifests unless you actually want ADH to ask which one to use.

## Packs

Packs are for bundled installs.

Example:

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

## Things to avoid

Do not depend on random manifest names.

Do not expect ADH to run shell commands from your manifest.

Do not hide your DLL deep inside messy nested folders unless the manifest `entry` points to it properly.

Do not reuse someone else's `id`.

## Good habits

Keep the folder name close to the mod id.

Keep the DLL name obvious.

Keep the description short.

Keep the manifest exact and valid JSON.

Test with a local zip before uploading it online.

## Fast checklist

Before importing, check:

1. Does the zip contain `adh-manifest.json` or `manifest.json`?
2. Does the manifest point to the right DLL?
3. Does the DLL actually exist in the package?
4. Is the JSON valid?
5. Is the `id` unique?

## Example release layout

```text
ExampleMod/
  adh-manifest.json
  ExampleMod.dll
```

That is the format ADH expects most often.
