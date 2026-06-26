# ADH Aestik Modding Guide Part 3

## Runtime Design and In-Game Behaviour

This section focuses on what happens after ADH has already accepted the package and the DLL is being loaded into Aestik.

This is where many mods fail.

A clean package only gets you to the starting line.

A reliable runtime design is what makes the mod actually useful in real play.

## The Runtime Pattern

ADH runtime mods are expected to follow the runtime contract used by the current loader.

In practice that means your DLL should be shaped like a real ADH-targeted mod, not just a random assembly with arbitrary code.

The common pattern is:

1. implement the runtime mod interface
2. receive a context object
3. perform startup work in `Initialize`
4. perform cleanup in `Shutdown`
5. hook game systems carefully after startup

The current project’s Aestik health bar source is the best working example already available in your materials.

When you are unsure how to shape a real mod for this loader, use that example as your reference.

## Initialization

Initialization is where you register systems, set up listeners, and prepare your mod to react to the game.

Initialization is not always the place to do all actual gameplay work immediately.

That matters because Aestik startup does not mean active gameplay has already fully begun.

At initialization time:

some scenes may not be loaded yet

some objects may not exist yet

some managers may exist before the specific scene content you want

some UI roots may be rebuilt later

If your mod requires a specific gameplay object, you should wait for that object deliberately rather than assuming it is already there.

## Scene-Aware Design

Many ADH mods need scene-specific behavior.

If that is true for your mod, the clean approach is usually one of these:

listen for scene changes

create a persistent manager that watches for required objects

delay scene-specific setup until the scene you need is active

re-acquire references after scene changes if objects are destroyed and rebuilt

Do not assume that objects found once will stay valid forever.

For many Unity-based games, scene changes can completely invalidate cached object references.

## Heavy Startup Work

Mods that do too much immediately on load can make startup feel unstable or sluggish.

A better pattern is:

do light setup first

register listeners

defer heavier discovery until needed

cache expensive work carefully

separate one-time startup logic from repeated gameplay logic

This is especially important if your mod scans many objects, builds UI, loads assets, or patches a large amount of state right away.

## Cleanup

If your mod subscribes to events, creates persistent objects, or patches in-memory state, `Shutdown` matters.

Good cleanup protects against:

double registration

duplicate UI

stale event hooks

objects surviving longer than intended

mod reload problems

scene transition bugs

Even if the loader usually starts fresh with the game, writing proper cleanup still makes your mod easier to reason about and safer to maintain.

## Logging

Use logging to make problems diagnosable.

Good logging tells you:

when initialization started

what scene the mod is waiting on

whether required objects were found

when an important action ran

why a feature was skipped

Bad logging floods the output constantly and hides the useful information.

Do not spam every frame unless you are doing a short-lived debug session.

## Reflection

Reflection can be useful, but it should not be your first answer if a cleaner direct integration exists.

Reasons to keep reflection controlled:

it is easier to break across updates

it is harder to read later

it becomes painful to debug when names or structures move

it can create brittle dependencies on exact internal implementation details

If you do use reflection:

centralize it

guard it carefully

log failures clearly

avoid scattering it across dozens of unrelated files

## Runtime Failure Patterns

The most common runtime-side failures are:

initialization assuming too much too early

scene object references going stale

event subscriptions not being cleaned up

UI being created more than once

the wrong DLL being loaded

wrong runtime references during build

logic that only works in one scene and nowhere else

code that blocks startup too heavily

## A Good Runtime Mindset

Think of the runtime side in phases.

Phase one is startup safety.

Phase two is world discovery.

Phase three is scene-specific behavior.

Phase four is cleanup.

If you structure your mod that way, it becomes easier to test and easier to maintain.

## Practical Rule

Your mod should still behave properly after:

initial game startup

the first real gameplay scene

scene changes

temporary missing objects

being disabled and re-enabled in normal ADH use

If it only works in one narrow setup, it is not finished yet.
