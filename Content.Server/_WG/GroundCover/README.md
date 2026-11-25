# GroundCover

Weather that stays on the ground, and only on a surface: levels below one (caverns, cellars), the air above a planet and
its orbit have none (`SharedWolfWeatherSystem.IsSurface`). Rain wets open ground: the floor darkens a little and
puddles fill the low spots of a warped noise field: a few small ones in light rain, merging into larger pools only in
heavy rain, and drying over minutes after it stops. Puddles are clear water: the ground shows through, slightly darker
and cooler, fading out towards the rim so no edge is drawn round them, and they reflect the sky and anything standing a
tile above them. Single-pixel glints and specks twinkle on them, and raindrop rings catch the light while it rains. Wet
floor and puddles take their colour from the weather (`groundTint` on a `wolfWeather`, plain water when unset; blood rain
is blood red), eased in on the map (`WGGroundWeatherComponent.Tint`) and kept while the puddles dry. Rain also washes
decals flagged cleanable off open tiles near players, a couple of chunks a second and gradually
(`WashDecals`); street paint and other uncleanable decals stay, and so does anything under a roof or a vessel. Snow
weather lays snow cover on open tiles, up to three levels deep; over one level it slows walking, a shovel or spade
clears a tile, and it melts once no snow falls and the tile's air is above 0 C. On cover (half a level or more) steps
crunch with the snow tiles' sound and leave footprints. Roofed and indoor tiles stay dry and clear, by WolfWeather's
own cover rules.

How wet and snowy a weather makes the ground is set on each `wolfWeather` with `groundWetness` (0 to 1; puddles above
0.3) and `snowfall` (cover levels a minute). Wetness is one value per map (`WGGroundWeatherComponent`), networked in
fiftieths. Snow is a byte per tile in 16-tile chunks (`WGSnowCoverComponent` on the grid, server only), kept only where
snow has fallen. Chunks are worked a few a tick and only near players; one nobody saw catches up from the map's running
totals of snowfall and snow-free time when someone comes near. Each player is sent the chunks around them that changed
since they were last sent (`WGSnowCoverChunksMessage`).

Walking in snow is predicted. `SharedWGSnowSlowSystem` works out a mob's slowdown from the depth under it (the tile's,
or the average of its 3x3 if less, in three speed steps) whenever its tile or the depths change, and adds it in
`RefreshMovementSpeedModifiersEvent`; the same depths pick the footstep sound through `WGSnowFootstepEvent`, raised
from the mover. Both sides read the same depths: the server the copy it last sent (`WGSnowChunk.Sent`, taken at each
send), and for a player's mob only once that player was sent the chunk; the client what it got. The client predicts its
own mob only and works its slowdown out again after each game state, as the server's speed for a tick can lag its tile.
Footprints are client-side only (`WGSnowPrintsSystem`, `WGSnowPrintsOverlay`): each client lays them from the humanoids
it sees walk on cover, up to 6 a tile and 1500 in all, and they fade over four minutes, fill in under a third of a level
of fresh snow, fade as the snow under them melts thin (full while it is a level deep or as deep as when they were
laid, none when bare), and go when their tile is shoveled or melts bare. They use the BloodTrail footprint sprites.

Fire and heat melt snow. A lit radiant heater (fireplaces, torches, braziers, bonfires) melts the snow within its
warmth radius, falling off with distance. Anything on fire (`FlammableComponent.OnFire`: a burning mob or item) is
given a temporary radiant heater (radius 1.5, up to body heat; `WGBurnWarmthComponent` marks it) every two seconds
and loses it when the fire is out, so the same pass melts round it and nearby bodies and the warmth HUD feel it. An
atmos hotspot melts 12 levels a minute on its tile and half that on the eight round it (`MeltHotspots`).

Liquids on snow: a new puddle that is warm (its solution at 303 K or more, or holding blood, taken as 310 K) melts 1.5
depth units for each unit of it, up to 20 units, on its tile, once (`WGPuddleMeltComponent`).

Vehicle tracks: when a moving vehicle lays its tread marks (`WGVehicleTrackSystem`), it raises
`WGVehicleTreadsEvent` with the two tread points, and the snow under each is pressed down a level
(`TrackCompression`), once for each tile a tread enters. Each client also draws two tread lines along every vehicle's
path over snow (`WGSnowTracksSystem`, `WGSnowTracksOverlay`, up to 800 marks, five minutes), which fill in under fresh
snow and go when the tile is pressed bare, shoveled or melted.

Bonfires burn down and need fuel: see `_WG/Bonfires`.

The client draws it all with one shader in two passes. `WGGroundCoverOverlay` runs over the floor, under entities: it
takes the floor as the screen texture and draws the wet floor, puddles and snow from it, lighting them by hand. At Full
quality it also keeps a copy of the bare floor, and `WGGroundReflectionOverlay` runs after entities (before weather and
FOV): it redoes the first pass to find water pixels nothing was drawn over, and mirrors what stands above into them.
Ripple strength follows the rain falling now (each weather's `groundWetness` at its current strength). The Weather
detail option (`wg.wolf_weather.quality`) picks Full (everything), Reduced (wet ground and puddles, no reflections,
ripples or drop flecks) or Off (no wet ground). Snow always draws, as it slows walking. Only the view's own z-level
draws it.

To keep it cheap the overlay draws only the part of the view that holds wet or snowy tiles (and nothing, with no screen
copy, where none is in view), bakes the puddle height field into a texture once for each mask rect instead of redoing the
noise at every pixel, and checks only the two nearest raindrop cells. Snow depth goes into the mask sharp, then is blurred
(`WGWolfWeatherBlur`, a little over half a tile) into a depth and a share of ground under snow; the shader cuts the share's
0.5 contour, rippled by noise, so edges, melt holes round a fire and shoveled tiles are soft and round instead of tile
squares.

The look can be tuned live with server cvars, replicated to every client (`cvar <name> <value>` on the server, or
`sudo cvar ...` from an admin client):

- `wg.ground_cover.puddle_edge` (0.6): tiles from a puddle's rim to its full water; larger is a softer edge.
- `wg.ground_cover.puddle_opacity` (0.5): how much puddle water tints and darkens the ground under it, 0 clear to 1.
- `wg.ground_cover.puddle_coverage` (1): scales the water level; below 1 fewer and smaller puddles, about 1.35 floods.
- `wg.ground_cover.reflection` (1): scales the reflections of the sky and what stands above.
- `wg.ground_cover.sparkle` (1): scales glints, twinkling specks, rim highlights and ripple crests.
- `wg.ground_cover.wet_darkening` (1): scales how much rain darkens and saturates open ground.

Tiles of a vessel are blanked from the mask after the blur, so a ship sitting on the ground has no snow, wet floor or
puddles under or in it, and footprints and tracks of the ground below are skipped under it
(`WGGroundCoverSystem.IsUnderVessel`). A vessel never holds snow cover on the server either (`CanHoldCover`).

Snow and frost go by the air over a tile, and on a planet open sky takes the planet's air: built ground such as a
city street holds its own air from when it was laid, which the climate never moves, so Crocus kept its build-day
temperature and never took snow.

Lakes and ponds freeze (`WGFreezableWater` on `MonoFloorWaterEntity`, and `WGThrasciasLakeWater` on Thrascias). Nothing
about a world is hardcoded: the water reads the air its world's climate writes. The freeze and thaw points are data on the
component (`freezeAt` -2 C and `thawAt` 0 C by default; a pool that isn't water, such as the blood lake, sets `freezeAt: 0`
and never freezes). Once a chunk's air has held at or below its freeze point for
three minutes, ice spreads in from the shore two tiles a minute and thickens by a degree-minute for each degree of
frost (up to 480). Above its thaw point for three minutes warmth melts that thickness first, faster the warmer it is, and only
then thaws the ice from the shore the same way: a winter night's ice holds through a mild afternoon, and a warm
spring day melts it through. Ice is walkable and a little slippery: the water's wading slowdown is set to none, its floor occlusion
(the waist-deep look) and splash trigger are off and it gets friction contacts, and steps sound of ice, or crunch under
snow. Snow lies on ice and melts into open water. A pickaxe or crowbar breaks a hole (four seconds), which freezes over
again five minutes later. Water first seen in frost is frozen already, so Thrascias stays frozen and Asclepiu's lakes
freeze in winter and thaw in spring; warm worlds never freeze. It is worked with the snow, by the same chunks and only
near players (`WGIceCoverComponent` on the grid: each water tile's distance to the shore and how far the ice has spread
or thawed). A chunk nobody watched is caught up when someone comes back: a gap over ten minutes is walked in steps
along the world's own season and hour curve (`WGPlanetClimateSystem.GetClearTemperatureAt`, shifted to the air now), so a
lake left frozen through a winter is open after a spring and a summer, whatever the air on return. Freezing toggles state on the water entity
rather than swapping it, so biome chunks still unload it (the unloader counts a freeze or thaw as no change) and it
comes back with its chunk's ice. The client draws ice with `lake_ice.rsi`, the water's own shore shapes in ice, and
hides it under snow cover so the snow shows (`WGFrozenWaterVisualsSystem`).

Entry points: `WGGroundCoverSystem.StepMap`, `UpdateChunk`, `GetDepth`, `SetDepth`, `GetSentDepth`, `GetTileWetness`, `UpdateIceChunk`, `SetFrozen`, `BreakHole`
(server), `SharedWGSnowSlowSystem.GetSpeed`, the client `WGGroundCoverOverlay` (`UpdateWeather`, `DrawReflections`),
`WGSnowPrintsSystem` and `ground_cover.swsl`.

<!-- WOLFGATE-GENERATED START -->
<!-- Generated by python Tools/_WG/Ci/modules.py --write. Don't edit by hand. -->

## Files

### Server

- [`Content.Server/_WG/GroundCover/WGBurnWarmthComponent.cs`](WGBurnWarmthComponent.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.cs`](WGGroundCoverSystem.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.Decals.cs`](WGGroundCoverSystem.Decals.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.Fire.cs`](WGGroundCoverSystem.Fire.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.Heat.cs`](WGGroundCoverSystem.Heat.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.Ice.cs`](WGGroundCoverSystem.Ice.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.IceBreak.cs`](WGGroundCoverSystem.IceBreak.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.Liquids.cs`](WGGroundCoverSystem.Liquids.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.Network.cs`](WGGroundCoverSystem.Network.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.Shovel.cs`](WGGroundCoverSystem.Shovel.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.Snow.cs`](WGGroundCoverSystem.Snow.cs)
- [`Content.Server/_WG/GroundCover/WGGroundCoverSystem.Tracks.cs`](WGGroundCoverSystem.Tracks.cs)
- [`Content.Server/_WG/GroundCover/WGIceCoverComponent.cs`](WGIceCoverComponent.cs)
- [`Content.Server/_WG/GroundCover/WGPuddleMeltComponent.cs`](WGPuddleMeltComponent.cs)
- [`Content.Server/_WG/GroundCover/WGSnowCoverComponent.cs`](WGSnowCoverComponent.cs)
- [`Content.Server/_WG/GroundCover/WGSnowSlowSystem.cs`](WGSnowSlowSystem.cs)

### Shared

- [`Content.Shared/_WG/GroundCover/SharedWGSnowSlowSystem.cs`](../../../Content.Shared/_WG/GroundCover/SharedWGSnowSlowSystem.cs)
- [`Content.Shared/_WG/GroundCover/WGFreezableWaterComponent.cs`](../../../Content.Shared/_WG/GroundCover/WGFreezableWaterComponent.cs)
- [`Content.Shared/_WG/GroundCover/WGFrozenWaterSystem.cs`](../../../Content.Shared/_WG/GroundCover/WGFrozenWaterSystem.cs)
- [`Content.Shared/_WG/GroundCover/WGGroundCoverCVars.cs`](../../../Content.Shared/_WG/GroundCover/WGGroundCoverCVars.cs)
- [`Content.Shared/_WG/GroundCover/WGGroundWeatherComponent.cs`](../../../Content.Shared/_WG/GroundCover/WGGroundWeatherComponent.cs)
- [`Content.Shared/_WG/GroundCover/WGSnowCover.cs`](../../../Content.Shared/_WG/GroundCover/WGSnowCover.cs)
- [`Content.Shared/_WG/GroundCover/WGSnowFootstepEvent.cs`](../../../Content.Shared/_WG/GroundCover/WGSnowFootstepEvent.cs)
- [`Content.Shared/_WG/GroundCover/WGSnowShovelComponent.cs`](../../../Content.Shared/_WG/GroundCover/WGSnowShovelComponent.cs)

### Client

- [`Content.Client/_WG/GroundCover/WGFrozenWaterVisualsSystem.cs`](../../../Content.Client/_WG/GroundCover/WGFrozenWaterVisualsSystem.cs)
- [`Content.Client/_WG/GroundCover/WGGroundCoverOverlay.cs`](../../../Content.Client/_WG/GroundCover/WGGroundCoverOverlay.cs)
- [`Content.Client/_WG/GroundCover/WGGroundCoverSystem.cs`](../../../Content.Client/_WG/GroundCover/WGGroundCoverSystem.cs)
- [`Content.Client/_WG/GroundCover/WGGroundReflectionOverlay.cs`](../../../Content.Client/_WG/GroundCover/WGGroundReflectionOverlay.cs)
- [`Content.Client/_WG/GroundCover/WGSnowPrintsOverlay.cs`](../../../Content.Client/_WG/GroundCover/WGSnowPrintsOverlay.cs)
- [`Content.Client/_WG/GroundCover/WGSnowPrintsSystem.cs`](../../../Content.Client/_WG/GroundCover/WGSnowPrintsSystem.cs)
- [`Content.Client/_WG/GroundCover/WGSnowSlowSystem.cs`](../../../Content.Client/_WG/GroundCover/WGSnowSlowSystem.cs)
- [`Content.Client/_WG/GroundCover/WGSnowTracksOverlay.cs`](../../../Content.Client/_WG/GroundCover/WGSnowTracksOverlay.cs)
- [`Content.Client/_WG/GroundCover/WGSnowTracksSystem.cs`](../../../Content.Client/_WG/GroundCover/WGSnowTracksSystem.cs)

### Integration tests

- [`Content.IntegrationTests/Tests/_WG/GroundCover/CrocusWeatherTest.cs`](../../../Content.IntegrationTests/Tests/_WG/GroundCover/CrocusWeatherTest.cs)
- [`Content.IntegrationTests/Tests/_WG/GroundCover/FrozenWaterTest.cs`](../../../Content.IntegrationTests/Tests/_WG/GroundCover/FrozenWaterTest.cs)
- [`Content.IntegrationTests/Tests/_WG/GroundCover/GroundCoverOverlayTest.cs`](../../../Content.IntegrationTests/Tests/_WG/GroundCover/GroundCoverOverlayTest.cs)
- [`Content.IntegrationTests/Tests/_WG/GroundCover/GroundCoverRound12Test.cs`](../../../Content.IntegrationTests/Tests/_WG/GroundCover/GroundCoverRound12Test.cs)
- [`Content.IntegrationTests/Tests/_WG/GroundCover/GroundCoverTest.cs`](../../../Content.IntegrationTests/Tests/_WG/GroundCover/GroundCoverTest.cs)
- [`Content.IntegrationTests/Tests/_WG/GroundCover/GroundCoverVehicleTest.cs`](../../../Content.IntegrationTests/Tests/_WG/GroundCover/GroundCoverVehicleTest.cs)
- [`Content.IntegrationTests/Tests/_WG/GroundCover/LakeClimateTest.cs`](../../../Content.IntegrationTests/Tests/_WG/GroundCover/LakeClimateTest.cs)
- [`Content.IntegrationTests/Tests/_WG/GroundCover/SnowInteractionsTest.cs`](../../../Content.IntegrationTests/Tests/_WG/GroundCover/SnowInteractionsTest.cs)
- [`Content.IntegrationTests/Tests/_WG/GroundCover/SnowMarksTest.cs`](../../../Content.IntegrationTests/Tests/_WG/GroundCover/SnowMarksTest.cs)
- [`Content.IntegrationTests/Tests/_WG/GroundCover/SnowWalkTest.cs`](../../../Content.IntegrationTests/Tests/_WG/GroundCover/SnowWalkTest.cs)

### Prototypes

- [`Resources/Prototypes/_WG/GroundCover/ground_cover.yml`](../../../Resources/Prototypes/_WG/GroundCover/ground_cover.yml)

### Localization

- [`Resources/Locale/en-US/_WG/GroundCover/ground_cover.ftl`](../../../Resources/Locale/en-US/_WG/GroundCover/ground_cover.ftl)

### Textures

- [`Resources/Textures/_WG/GroundCover/lake_ice.rsi/`](../../../Resources/Textures/_WG/GroundCover/lake_ice.rsi/)
- [`Resources/Textures/_WG/GroundCover/Shaders/ground_cover.swsl`](../../../Resources/Textures/_WG/GroundCover/Shaders/ground_cover.swsl)

## Non-modular edits

- [`Content.Shared/Movement/Systems/SharedMoverController.cs`](../../../Content.Shared/Movement/Systems/SharedMoverController.cs): snow cover crunches underfoot over the floor's own sound
- [`Resources/Prototypes/Entities/Objects/Specific/Hydroponics/tools.yml`](../../../Resources/Prototypes/Entities/Objects/Specific/Hydroponics/tools.yml): clears snow cover
- [`Resources/Prototypes/Entities/Objects/Tools/tools.yml`](../../../Resources/Prototypes/Entities/Objects/Tools/tools.yml): clears snow cover
- [`Resources/Prototypes/Entities/Tiles/water.yml`](../../../Resources/Prototypes/Entities/Tiles/water.yml): lakes and ponds freeze over in cold air

<!-- WOLFGATE-GENERATED END -->
