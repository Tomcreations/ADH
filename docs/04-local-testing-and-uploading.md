# ADH Aestik Modding Guide Part 4

## Local Testing, Packaging Checks, and Uploading

This section explains how to move from a local build to a real ADH package, how to test before release, and how to use the developer flow without treating upload as a substitute for proper packaging.

## Recommended Local Workflow

The clean local workflow is:

1. write the mod code
2. build the DLL
3. create a mod folder with the DLL and manifest
4. zip the package if needed
5. import it into ADH locally
6. launch modded Aestik from ADH
7. test in real gameplay
8. fix issues before uploading

That order matters.

Uploading too early usually just moves packaging mistakes into the catalog instead of fixing them.

## What to Test Locally

Before you think about publishing, check:

does ADH detect the package

does the manifest parse correctly

does the mod appear with the expected name and version

does the DLL path resolve correctly

does the mod initialize without obvious failures

does the mod still behave correctly after a scene change

does the enable and disable state behave normally

does deleting and re-importing the package still work

## Zip Import Expectations

For a standard DLL mod, ADH expects a zip like this:

```text
MyCoolMod.zip
  adh-manifest.json
  MyCoolMod.dll
```

or this:

```text
MyCoolMod.zip
  MyCoolMod/
    adh-manifest.json
    MyCoolMod.dll
```

That is enough for a normal package.

If your zip contains several possible manifests, ADH may ask which one should be used.

That can be useful in some deliberate bundle cases, but most of the time it means the archive was not kept clean enough.

## What Not to Upload

Do not upload:

placeholder packages

empty shells

notes-only zips

support files with no real payload

builds with version numbers that do not match the actual package

archives where the manifest points to files that are not present

## Uploading Through ADH

The normal developer flow is:

1. open ADH
2. go to the developer section
3. create or open a project
4. enter the project title
5. enter the description
6. add an optional website URL if needed
7. upload the package

The upload still needs to be a valid ADH package.

Uploading is not magic.

It does not fix:

bad pathing

bad DLL builds

invalid JSON

wrong package shape

missing payload files

If the package is bad locally, it is still bad after upload.

## Official and Unofficial Content

User projects should be treated as unofficial by default unless the ADH side explicitly marks them as official.

That matters for user trust.

If you are not publishing first-party ADH content, assume:

`trust_level = "Unofficial"`

Do not mark unofficial content as official because it “seems fine.”

Trust labels should stay deliberate.

## Packs

Packs are grouped installs.

Use a pack when the package is meant to deliver multiple related pieces together, not when you are just shipping one normal DLL mod.

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

If you are shipping one normal runtime mod, use `kind = "code"`.

## Common Packaging Mistakes Found Late

A lot of upload failures are not really “upload” failures.

They are packaging mistakes discovered late.

Typical examples:

manifest file name is wrong

DLL path does not match `entry`

the correct DLL was not included in the zip

the JSON is malformed

the mod `id` changed accidentally between releases

the version did not match the actual build

the archive contains random junk from the build tree

## Strong Release Habit

Before every release, rebuild the package from a clean staging folder.

Do not zip directly from a messy working directory if you can avoid it.

A clean staging folder helps prevent:

old DLLs being included accidentally

wrong config files being shipped

screenshots or notes being bundled by mistake

multiple manifest candidates appearing unintentionally

## Practical Release Standard

A package is ready to upload when:

it imports cleanly

it launches cleanly

it behaves correctly in a real scene

its version is correct

its metadata is honest

its archive only contains the files users actually need

That is the standard to aim for.
