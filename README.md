# Rooftop Survival - Phase 1 starter scripts (Unity)

Untested: written without a Unity install, so expect to fix a typo or two on first compile.

## Setup
1. Unity 6 LTS, 3D (URP) template. New project named RooftopSurvival.
2. Copy `Assets/Scripts` into your project's Assets folder.
3. Window > Package Manager > "+" > Add package by name: `com.unity.cloud.gltfast`
   (lets Unity import the Polyfork .glb files).
4. Download .glb models from polyfork.dev. Drag them into Assets/Models/. Each becomes a prefab.
   Check polyfork's terms page for use outside three.js before shipping.
5. Scene setup (one scene is enough for now):
   - Empty "Managers": add RunManager, GameClock, NightDirector.
   - GameClock: assign your Directional Light as `sun`.
   - Empty "Roof" (position 0,0,0): add RoofGenerator, set roofRoot = itself.
   - Child of Roof: Box Collider (big, covers roof, tall) + RoofZone.
   - Player with tag "Player" (any CharacterController for now).
   - Trap prefab: add BaseStructure, set defense value.
   - Planter prefab: add FarmPlot, optionally assign a crop model as cropVisual.
6. Play. Set GameClock.timeScale to 20 to rush to Night 11 and test attacks.

## What each script does
- GameClock: day/night, sun, events (dawn / dusk warning / nightfall).
- RunManager: save/load, permadeath wipe, new random roof seed.
- RoofGenerator: seeded random roof, size, planters, clutter, starting supplies.
- NightDirector: nights 1-10 safe; from 11 random attacks vs your defense score.
- BaseStructure: buildable defenses with health.
- FarmPlot: plant, water, grow per day, harvest.
- RoofZone: knows if the player is on the roof at nightfall.
- GlbLoader: optional runtime GLB loading.
