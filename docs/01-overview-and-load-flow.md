# ADH Aestik Modding Guide Part 1

## Overview and Load Flow

This section explains what ADH is doing, what parts of the modding process it is responsible for, and what actually happens when Aestik starts with ADH-managed mods.

If you are building for ADH, it helps to think about the system in two layers.

The first layer is the package layer.

The second layer is the runtime layer.

The package layer is everything ADH sees before the game starts.

That includes:

`adh-manifest.json`

folder structure

zip layout

the DLL file location

basic metadata such as the name, version, category, and author

The runtime layer is what happens after ADH has already accepted the package and Aestik begins loading the mod inside the game.

That includes:

the DLL actually loading

the mod being initialized

the mod reacting to scenes, enemies, UI, events, and gameplay objects

A mod can be valid at one layer and broken at the other.

For example:

You can have a package that imports correctly into ADH but fails in the game because the `entry` path points to a DLL that does not behave correctly at runtime.

You can also have a perfectly good DLL that never loads because the package format is wrong or the manifest is malformed.

That is why both layers matter.

## What ADH Is Responsible For

ADH is the loader and manager for Aestik mods.

It does not just display a list of files.

It is responsible for:

discovering installed mods

reading mod manifests

tracking enabled and disabled state

importing packages

installing catalog mods

setting up the runtime bootstrap inside Aestik

launching the game in modded or vanilla mode from inside ADH

From a mod author point of view, ADH expects a mod to be something that can be understood by the package system and then successfully loaded by the runtime system.

## Where Mods Live

ADH expects mods to live inside the Aestik mod directory.

Typical location:

`Aestik_Data\ModLoader\Mods`

Each mod should live inside its own folder.

Good example:

```text
Mods/
  MyCoolMod/
    adh-manifest.json
    MyCoolMod.dll
```

That layout is predictable and easy for both ADH and the mod author to reason about.

Avoid dumping many unrelated DLL files directly into the `Mods` root.

Avoid mixing several unrelated mods inside one folder unless you are intentionally building a bundled package.

Keeping one mod per folder makes support and updates much simpler.

## When Mods Load

ADH loads enabled mods during game startup.

The runtime bootstrap is inserted into Aestik so that ADH can begin loading enabled packages when the game starts.

At a practical level, the load sequence is roughly this:

1. Aestik starts
2. ADH runtime bootstrap becomes active
3. ADH scans the mod folders
4. ADH reads manifests
5. ADH resolves basic ordering information
6. ADH loads assemblies
7. ADH looks for supported runtime mod implementations
8. ADH initializes those mods

This means a few important things for mod authors.

Your mod loads early.

Your mod should not assume the player is already in active gameplay.

Your mod should not assume the final gameplay scene has already loaded.

Your mod should not do all heavy work in one massive startup burst if that work can be deferred or staged.

## What Early Startup Means in Practice

A common mistake is writing `Initialize` code as if the game world is already fully alive.

That often causes problems because startup time and active gameplay time are not the same thing.

At startup:

some scene objects may not exist yet

UI roots may not be ready

game objects may be replaced during scene changes

managers may exist before the player enters normal play

If your mod needs to operate on live scene content, treat initialization as the place where you set your systems up, not always the place where you do every single scene-specific action immediately.

Good patterns include:

registering scene listeners

creating a manager object that persists across scenes

waiting until required objects exist before touching them

performing light discovery first and heavy work later

## Runtime Expectations

ADH expects a runtime mod to follow the loader runtime contract used by the current project.

That means the mod assembly should be structured like a real ADH runtime mod rather than just being any arbitrary DLL.

In the materials already in this project, the best practical example is the Aestik health bar source.

That example shows the shape of a real working ADH-targeted mod.

Use it as a reference when you are unsure how to structure initialization, cleanup, scene awareness, or object discovery.

## The Main Failure Pattern

The most common high-level mistake is thinking that “the mod imported successfully” means “the mod is correct.”

It does not.

An imported package only proves that ADH could read enough of the package to accept it.

A working mod means:

the package is valid

the manifest is correct

the DLL path is correct

the assembly loads

the runtime logic initializes safely

the mod still behaves correctly after the game moves into real play

That is the standard you should test against.

## Working Mental Model

If you want a clean mental model for ADH modding, use this:

ADH is a package manager plus a runtime bridge.

The package manager decides whether your mod can be discovered, described, imported, enabled, disabled, or updated.

The runtime bridge decides whether the code actually comes alive inside Aestik and behaves properly when the game is running.

Both parts matter equally.

If you design with that in mind from the beginning, your mod will be far easier to build, debug, and maintain.
