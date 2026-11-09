# Rooftop Survival (Unity 6)

Survive on the roof of a skyscraper in a ruined, overgrown city. Farm and build traps by day,
rappel to the street for supplies, and be back on the roof before dark. Nights 1-10 are safe;
from night 11 attacks are possible. If your base is overrun the run is wiped and you start on a
brand-new random roof.

## One-time setup: import the Polyfork models
Unity only imports files that are inside the `Assets` folder, and the scripts load models from
`Assets/Resources/Models`.

1. In Explorer, create `Assets\Resources\Models`.
2. Move every `.glb` from the `Rooftop game` folder into it.
3. Move the eight `kit-*.glb` files back out (put them in `Assets\Kits` or leave them in
   `Rooftop game`). They are 11-42 MB each and are not used yet.
4. Switch back to Unity and let it import (the first import takes a while).

Needs the glTFast package (`com.unity.cloud.gltfast`, already installed).
If the models are missing the game still runs with grey boxes and shows a message.

## Play
Press Play in any scene. A scene with no GameClock gets built automatically.

| Key | Action |
|---|---|
| WASD / mouse | Move / look |
| Shift / Space | Sprint / jump |
| E | Plant, water, harvest the nearest planter |
| T | Place a cattle-grid trap (2 wood) |
| G | At the roof edge: rappel to the street. At your building's base: climb back up |
| Hold N | Fast-forward time (testing) |

## Scripts
- Core: `GameClock`, `RunManager`, `NightDirector`, `SceneBootstrap`
- Base: `RoofGenerator`, `FarmPlot`, `BaseStructure`, `RoofZone`
- World: `StreetBuilder` (skyline, street, vehicles, plants, wildlife)
- Enemies: `AnimalAI` (wander, chase, bite; set `yawOffset` to 180 if an animal walks backwards)
- Player: `PlayerController` (movement, health, grapple), `Inp`
- Assets: `ModelLibrary` (loads and auto-grounds models), `GlbLoader` (optional runtime loader)
- UI: `Hud`
