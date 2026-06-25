# ADH Manifest Specification

Version: `1.0`

This document defines the required JSON manifest format for ADH mods.

## Goals

- Make every mod declare what it is and what it installs.
- Make unofficial mods usable without making them opaque.
- Prevent arbitrary code execution during installation.
- Keep dependency declarations visible to the user.
- Allow the format to grow over time without breaking older mods.

## File name

Every mod package must contain a root manifest file named:

`adh-manifest.json`

If this file is missing, ADH must refuse installation.

## Safety model

ADH must treat the manifest as declarative data only.

The manifest may describe:

- metadata
- compatibility
- files to copy
- folders to create
- optional external dependencies that the user may install manually

The manifest must not be able to:

- run shell commands
- run PowerShell, batch, Python, Node, or other scripts
- modify the registry
- download and execute binaries
- patch arbitrary paths outside approved game and mod-loader roots

## Top-level format

```json
{
  "manifestVersion": "1.0",
  "id": "example.mod",
  "name": "Example Mod",
  "version": "1.0.0",
  "author": "Example Author",
  "description": "Short mod summary.",
  "homepage": "https://example.com/mod",
  "support": {
    "game": "Aestik",
    "gameVersions": ["1.0.0", "1.0.1"],
    "loaderVersion": ">=1.0.0"
  },
  "classification": {
    "channel": "unofficial",
    "reviewedByAdh": false
  },
  "dependencies": [],
  "install": {
    "createFolders": [],
    "copyFiles": []
  },
  "entrypoints": [],
  "capabilities": [],
  "custom": {}
}
```

## Required fields

### `manifestVersion`

- Type: string
- Required: yes
- Current value: `1.0`
- Purpose: allows ADH to parse the document according to a specific contract.

ADH rules:

- Reject manifests with no `manifestVersion`.
- Reject manifests with a newer major version that ADH does not understand.
- Ignore unknown fields from the same major version.

### `id`

- Type: string
- Required: yes
- Pattern: reverse-domain-like or dotted identifier, such as `com.author.modname` or `author.modname`
- Length: `3-128`

ADH rules:

- Must be unique per installed mod.
- Must use only lowercase letters, digits, dots, dashes, and underscores.
- If a new install conflicts with an existing `id`, ADH must ask whether to replace, cancel, or install as a separate disabled staging copy.

### `name`

- Type: string
- Required: yes
- Length: `1-120`

### `version`

- Type: string
- Required: yes
- Recommended: semantic versioning, for example `1.4.2`

ADH rules:

- Store as text.
- Compare semver when possible.
- Fall back to string comparison only for display, not upgrade logic.

### `author`

- Type: string
- Required: yes
- Length: `1-120`

### `description`

- Type: string
- Required: yes
- Length: `1-2000`

## Optional metadata fields

### `homepage`

- Type: string
- Optional
- Must be `https://` if present

### `support`

Required object fields:

- `game`
- `gameVersions`

Optional object fields:

- `loaderVersion`

Rules:

- `game` must be `Aestik`
- `gameVersions` is a non-empty array of supported versions or version ranges
- `loaderVersion` is a version range string for ADH compatibility

ADH processing:

- If the current game version is unsupported, show a compatibility warning and require explicit confirmation before install.
- If the loader version is unsupported, block install.

### `classification`

Optional object for trust presentation.

Fields:

- `channel`: `official` or `unofficial`
- `reviewedByAdh`: boolean

ADH processing:

- Official status must not be trusted solely from the manifest.
- ADH server-side catalog or local trusted metadata must be the source of truth.
- If a manifest says `official` but ADH does not verify it, display it as unofficial.

## Dependencies

Dependencies are declarations only.

ADH must never install them automatically.

### Format

```json
"dependencies": [
  {
    "id": "python.requests",
    "name": "requests",
    "source": "pip",
    "version": ">=2.32.0",
    "required": true,
    "reviewedByAdh": false,
    "notes": "Needed for optional web export features."
  }
]
```

### Supported `source` values

- `pip`
- `npm`
- `executable`
- `runtime`

### Dependency fields

#### `id`

- Type: string
- Required: yes
- Stable identifier for ADH tracking

#### `name`

- Type: string
- Required: yes
- Human-readable dependency name

#### `source`

- Type: string enum
- Required: yes

#### `version`

- Type: string
- Optional

#### `required`

- Type: boolean
- Optional
- Default: `true`

#### `reviewedByAdh`

- Type: boolean
- Optional
- Default: `false`

Important:

- This field is only advisory inside the manifest.
- ADH may only skip warnings if the mod itself is verified and the dependency is separately reviewed in ADH’s trust data.

#### `notes`

- Type: string
- Optional
- Length: `0-500`

### Required ADH dialog behavior

For each unreviewed dependency, ADH must show a separate confirmation step before installation continues.

Example dialog:

```text
This mod requires the following dependency:
Package: requests
Source: pip

This dependency has not been verified by the ADH team.
Only continue if you trust the mod author.
```

Rules:

- One confirmation per dependency.
- Bulk “accept all forever” must not exist.
- ADH may remember approval by dependency id, version range, and mod id, but only if the user explicitly opts in.
- If the user declines a required dependency, install must stop.
- If the user declines an optional dependency, install may continue only if the mod declares it optional.

## Install actions

The install section is the only place where package file operations are described.

Only predefined actions are allowed.

### Format

```json
"install": {
  "createFolders": [
    {
      "path": "Mods/ExampleMod/Config"
    }
  ],
  "copyFiles": [
    {
      "from": "files/ExampleMod.dll",
      "to": "Mods/ExampleMod/ExampleMod.dll",
      "overwrite": true,
      "sha256": "optional expected hash"
    }
  ]
}
```

### Allowed action types

- `createFolders`
- `copyFiles`

No other install actions are allowed in manifest version `1.0`.

### Path model

All manifest paths are relative virtual paths.

ADH must map them into approved roots only.

Approved destination roots:

- `Mods/`
- `Packs/`
- `Aestik_Data/ModLoader/`
- other explicit safe roots added by future ADH versions

Blocked destinations:

- absolute paths
- paths containing `..`
- Windows drive prefixes
- UNC paths
- user profile paths
- registry-like locations
- executable startup folders

### `createFolders`

Fields:

- `path`: required relative destination path

ADH processing:

- Normalize path separators.
- Reject if escaped outside approved roots.
- Create the folder if it does not exist.

### `copyFiles`

Fields:

- `from`: required package-relative source file path
- `to`: required destination relative path
- `overwrite`: optional boolean, default `true`
- `sha256`: optional expected hash of packaged source file

ADH processing:

- Ensure `from` exists inside the package.
- Ensure `to` resolves inside an approved root.
- Validate optional `sha256` before copy.
- Create parent directories as needed.
- Copy atomically where possible.
- If `overwrite` is `false` and target exists, fail the install or prompt the user.

## Entrypoints

Entrypoints describe which installed files are intended to be loaded by ADH after installation.

Example:

```json
"entrypoints": [
  {
    "type": "dotnet-assembly",
    "path": "Mods/ExampleMod/ExampleMod.dll"
  }
]
```

Supported `type` values in `1.0`:

- `dotnet-assembly`
- `content-pack`

Rules:

- Entrypoints are metadata, not execution instructions.
- ADH may validate the target file exists after install.
- ADH runtime decides whether and how to load it.

## Capabilities

Capabilities are declared intents used for review and user transparency.

Examples:

- `ui-overlay`
- `new-rooms`
- `asset-replacement`
- `save-data`
- `network-play`

ADH processing:

- Display them in the mod details.
- Do not treat them as permissions yet.
- Future ADH versions may add capability warnings or approval gates.

## `custom`

`custom` is a free-form object reserved for future-compatible vendor or tool metadata.

Rules:

- ADH must ignore unknown keys inside `custom`.
- ADH must not execute or interpret script text from `custom`.

## Validation rules

ADH must validate the manifest in this order:

1. Parse JSON.
2. Validate against schema.
3. Validate safe path rules.
4. Validate package contents referenced by `install` and `entrypoints`.
5. Validate compatibility with current game and ADH version.
6. Show dependency approvals.
7. Perform file operations.
8. Re-scan installed mod and ensure installed state matches the manifest.

## Safe processing rules by field

### Metadata fields

- Safe to display.
- Must be length-limited.
- Must be escaped in UI.

### URL fields

- Must be `https://` only.
- Display as links.
- Never auto-open during install.

### Dependency fields

- Display only.
- Never auto-install.
- Never run package managers automatically.

### File operations

- Must use normalized paths.
- Must remain inside ADH-approved destinations.
- Must not copy hidden package files unless explicitly declared.
- Must fail closed on ambiguity.

### Unknown fields

- Ignore when safe and when schema version is supported.
- Do not treat unknown fields as actions.

## Failure behavior

ADH must reject the install if:

- the manifest is missing
- JSON is malformed
- schema validation fails
- a required file is missing
- a destination escapes approved roots
- the mod targets an unsupported loader version
- a required dependency is declined

## Forward compatibility

To extend the format safely:

- bump `manifestVersion` only for breaking changes
- add new optional fields within the same major version
- add new install action types only when ADH explicitly supports them
- keep unsupported actions blocked, not ignored silently

## Example user flow

1. User chooses a mod package.
2. ADH finds `adh-manifest.json`.
3. ADH validates schema and paths.
4. ADH checks compatibility.
5. ADH shows dependency confirmations if needed.
6. ADH previews file destinations.
7. ADH installs files.
8. ADH registers the mod and enables it only if installation succeeded.

## Recommended package layout

```text
MyMod.zip
  adh-manifest.json
  files/
    MyMod.dll
    config/
      defaults.json
```

## Notes for unofficial mods

Unofficial mods are allowed, but they must still:

- include a valid manifest
- declare exact installed files
- declare dependencies
- stay inside approved install roots

This keeps the developer workflow simple while making installs inspectable and much safer for users.
