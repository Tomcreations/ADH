# ADH Aestik Modding Guide Part 2

## Package Layout and Manifest Rules

This section covers the package side of ADH modding.

It explains what files ADH expects, how the folder layout should look, what the manifest is for, and how each important field should be used.

If you get this layer wrong, the mod can fail before runtime ever becomes relevant.

## Manifest File Names

ADH prefers:

`adh-manifest.json`

It also accepts:

`manifest.json`

If both exist, treat `adh-manifest.json` as the real standard.

If you want your package to be future-safe, use `adh-manifest.json`.

Do not rely on older loose manifest conventions.

Do not depend on custom manifest file names.

## Basic Package Layout

The standard layout is:

```text
MyCoolMod/
  adh-manifest.json
  MyCoolMod.dll
```

That is the simplest and most reliable form.

If your DLL is stored in a subfolder, the manifest must point to it precisely.

Example:

```text
MyCoolMod/
  adh-manifest.json
  bin/
    MyCoolMod.dll
```

Then the manifest should use:

```json
{
  "entry": "bin/MyCoolMod.dll"
}
```

Use direct, readable paths.

Avoid deep random nesting unless there is a real organizational reason.

The more unnecessary path complexity you add, the easier it becomes to break installs or confuse users.

## Recommended Packaging Rules

Use one mod per folder.

Keep the manifest at the mod root whenever possible.

Keep the DLL name obvious.

Keep support files close to the mod that uses them.

If you include assets, keep their folder names stable so future updates do not move them around without reason.

## Example Manifest

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

## Field by Field

### `id`

This is the internal identity of the mod.

It needs to be stable.

It needs to be unique.

It should be simple and predictable.

Preferred style:

`my-cool-mod`

Bad examples:

`Cool Mod FINAL TEST 4`

`My Cool Mod New New Fixed`

Changing the `id` after release can cause ADH to interpret the mod as a different package entirely.

That makes updates, support, and migrations harder.

### `name`

This is the visible name shown to users.

It should be human-readable and clear.

Example:

`My Cool Mod`

This field is for presentation, not identity.

The `id` should stay technical and stable while `name` stays readable.

### `version`

This is the package release version.

Keep it aligned with the build users are actually receiving.

Good examples:

`1.0.0`

`1.1.0`

`2.0.0`

Do not upload a new DLL while forgetting to change the version if the change matters to users or support.

### `author`

This is the visible author field shown in ADH.

Use your name, handle, or team name.

Keep it stable across releases unless you intentionally rebrand the project.

### `description`

This is the short summary users see in the loader.

It should quickly answer:

what the mod changes

what category it belongs to

why a user might want it

Do not write vague filler here.

Be short, but useful.

### `entry`

This is one of the most important fields in the package.

It tells ADH which DLL should be loaded.

If the path is wrong:

the package may import successfully

the mod may appear in the loader

the runtime may still fail because the actual DLL cannot be found

Always verify that the path in `entry` exactly matches the final package structure.

### `enabled`

Typical values:

`"true"`

`"false"`

For most releases, using `"true"` is reasonable.

That means the mod becomes active after installation unless the user disables it.

### `kind`

Use:

`"code"`

for normal DLL-based mods.

Use:

`"pack"`

for grouped or bundled installs.

If you are shipping one normal gameplay or UI mod, use `code`.

### `priority`

This is a load-order hint.

Higher numbers appear earlier in UI ordering and may matter when several mods have startup order expectations.

If you do not have a reason to change it, use:

`"0"`

Do not treat priority as a substitute for actual compatibility design.

### `loader_version`

This is the ADH loader version the package targets.

The current project materials use:

`"2.0.0"`

If your package targets a different loader version, that may not always hard-fail immediately, but it can become a compatibility problem later.

### `source_url`

Optional website link.

Use it if you want users to have a clear source page.

If you do not need it, leave it blank.

### `download_url`

For local packages, leave it blank.

For portal-driven catalog items, this is usually handled by the platform rather than handwritten manually each time.

### `category`

Simple label used by the loader.

Common examples:

`UI`

`Gameplay`

`Tools`

`General`

`Pack`

Use a category that helps users understand what type of mod they are looking at.

### `trust_level`

Use:

`Unofficial`

unless the package is truly first-party or ADH-managed official content.

Do not mark a package as official just because you think it is safe.

Official status is a trust classification, not just a personal opinion.

### `testing_status`

Normally leave this blank.

It should not become a dumping ground for random notes or personal warnings.

If the ADH side wants to expose a specific testing label later, it can do so deliberately.

## Zip Layouts

ADH works best with zip files that are direct and readable.

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

## What Makes a Package Healthy

A healthy ADH package has these traits:

clear manifest name

clear folder structure

correct `entry` path

one obvious payload

clean metadata

stable `id`

correct version

no confusing junk files that look like placeholders

The easier the package is to inspect, the easier it is to support.
