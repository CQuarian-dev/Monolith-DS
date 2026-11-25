# Caverns

Every planet network gets a cavern at depth -1: a biome-backed map built with the network and deleted with it,
roofed by the ground above and dark except where daylight falls through a gap in the ground. Each of the six worlds has
its own cavern (`wgCavern`, one per `wgPlanetSurface`) with its own air, light and geology (`Biomes/<world>.yml`):
the Underkarst's wet limestone and plunge pools, the Cinder Vaults' basalt and lava tubes, the Sandstone Galleries'
pillared halls and fossil beds, the Umbral Deeps' chromite crawls, shadow groves and pink geodes, the Rime Galleries'
ice halls and plasma lakes, and the Gut's flesh throats, stomachs and blood channels. Rock is solid and veined with the
world's ores, chambers hold its decor and wildlife (`WGCavernFauna<World>`, under the planet fauna caps). Each world's
glowing plant (`Entities/flora.yml`: glowcaps, ember lichen, lamp agaves, shadow blooms, rime thistles, nerve clusters)
lines its tunnels and chambers as the main light below ground. The Gut also pools digestive acid in its stomachs, which
digests whoever wades in, air or not, but spares its own creatures and anyone on a catwalk (`WGDigestiveAcidSystem`);
whoever it burns hisses with a looping sizzle until they leave it or die (`WGDigestiveAcidHissSystem`),
and chokes some throats with tendons that slow you (`Entities/gut.yml`).
The Rime Galleries, Umbral Deeps and Gut are laid out as big halls of their own, each a family of floor tiles (`Tiles/<world>.yml`), life
(`Entities/Life/<world>.yml`, `Locale/en-US/_WG/Caverns/life_<world>.ftl`, spawner tables `WGCavernFauna<World><Hall>`) and soundscape
(`Ambience/<world>.yml`, named by the cavern's `ambience`): the Rime Galleries' snowdrift hollows (rime bats, frost weavers, rime caps,
hearth lichen), frozen falls of cyan, blue and dark ice (ice slimes, crystal crabs, frost bulbs, hanging icicles that fall in shards) and
dark-shored plasma galleries (plasma crabs, plasma blooms); the Umbral Deeps' chromite crawls and bedrock galleries, shadow groves of
blue-black floor (deep bats, shadow cats, umbral caps), geode chambers (geode crabs, pickable geode clusters, bursting geodes, radiant
crystal, acid spitters) and a deep xenonid nest of hive floor, wild weeds, resin walls and eggs; the Gut's stomachs (bile slugs, gastric
moss, acid polyps), nerve tangles (antibodies, nerve whips that shock), an ossuary of calcified bone (bone burrowers, marrow caps, bone piles)
and clotted blood banks with leeches. Harvestables are common and hazards rare; every hall keeps the world's open share and tunnels connected.
Caverns come with planets: `wg.caverns` (`CavernCVars`) is on by default, so every network built while
`wg.planet_networks` is on gets one; set it false to build worlds without them (it applies to networks built after it
is set). Integration tests run with it off unless they turn it on. Ships never go below
ground, and nobody on or above the ground loads the cavern under them except near a mouth, where it shows through the
hole.

The Underkarst, the Cinder Vaults and the Sandstone Galleries are split into zones, each a stretch of ground over a
hundred tiles across picked by one low-frequency noise (halls by default, a second zone where it runs high, a third
where it runs low). A zone opens its caverns wider than the default and floors them in its own tile inside a rim of
the common chamber floor, and has its own plants (a harvestable and a rare hazard from the LivingWorld components),
wildlife table and fauna marker (`WGCavernFauna<World><Zone>`), all in `Resources/Prototypes/_WG/Caverns/Life/`:
the Underkarst's dripstone halls (flowstone, droplet lichen, grasping roots, pale bats and crabs), flooded grottos (wet
rock, pools, reeds, acid pitchers, frogs and crabs) and mushroom hollows (cave soil, cave caps, spore puffballs, cave
spiders); the Cinder Vaults' ash chambers (magma lakes, the argocytes), obsidian galleries (cinder shelves, fire pods,
cinder spiders, ember vipers) and sulphur vents (sulphur blooms, vent caps, slag ticks, sulphur carp); the Sandstone
Galleries' pillared halls (gallery aloe, sand snares, dust bats, crabs), fossil beds (marrow caps, brittle geodes,
pallid sandvipers) and buried ruin quarter (gravebloom, dust pods, crypt ticks). Each of the three plays its own
cavern soundscape (`ambience`) instead of the muffled surface one. The animals are recoloured by
`Tools/_WG/Caverns/gen_life_karst_cinder_sandstone.py`.

Every world has a gate mouth near its centre: a pinned hole in the ground, grown from its own seed to the world's shape
and size (`WGCavernMouthShape`: a sinkhole, a skylight, a sand funnel, a rift, a moulin or a throat, never a plain
rectangle), with a solid lip and rim decor in proportion to its size. Looking into a mouth you see the cavern below
through it, drawn by CE's z-level renderer: the landing floor or plunge pool, the stairs and whatever stands down
there, lit where daylight falls in. A shade over each hole tile only frames that view: on the dual grid it draws the
lip crumbling over the hole's own tiles, so the hole shows no square corner, and leaves their middle clear
(`WGCavernShadeVisualsSystem`, art from `Tools/_WG/Caverns/gen_pits.py`). The art is the same on every world and has
no ground of its own: the client lays the most common floor round each shade under its lip, so a hole cut in a road or a
jelly patch keeps that floor at its edge. In the cavern a pinned, rock-free pad holds
the world's landing tile under the hole and, under the hole tile at the edge on the stairs side, walkable stairs up to the lip
(rock-cut steps, ice steps or flesh ridges, CE's stone stairs recoloured per world). Players walk in and take a small fall whose damage depends on the landing tile;
examining a shade tells them where it goes, what the air below is like and how hard the landing is. Falling is the way down;
walking the stairs is the way back up, and down again without a fall. Whatever a walker is pulling comes with them, up or down. Admins use `wgcavern` to list caverns, teleport to a gate (`tp <planet> [pad|mouth]`), list mouths,
carve one by hand (`open`) and measure the terrain around them (`stats <planet>`: open share, connectivity on foot,
ore share and how much of the tunnel floor is within a short walk of a light, over a 192-tile square, read from noise
by `WGCavernSampler`, the same sampler the cavern tests use).

There are ways down everywhere, not only at the gate. On a world that preloads its ground (the Planets
`WGPlanetPreloadStartingEvent`) every cell inside the circle is claimed first, in one go, since the lazy claims below
only ever cut unloaded ground; a site must lie wholly inside the circle. The ground is split into cells about 96 tiles across, and each
cell's mouth is claimed ahead of whoever loads terrain there (players, admin ghosts, and the eye on the ground of anyone
flying above), never into loaded ground or ground about to load (`wg.cavern_claims`, on by default). Any ground tile
that becomes a real hole, dug, blown, pried, cut or taken by an RCD, is fitted out within a tick by the hole queue:
pinned, the world's landing tile under it in a cleared 3x3 of cavern floor (floor someone laid there is left as it
is), a shade, and cut stairs under it, coming out on a solid neighbour, unless
stairs are a walk of 8 steps or less away; tiles a chunk unload empties are told apart by the pin and left alone. A shovel digs a 15 s
shaft through natural ground it can't otherwise dig (Fervidus basalt, the Asclepiu plains, Thrascias ice, Aerumna
chromite, Carcinoma flesh, the bedrock under dug snow). Acid no longer opens ground over a cavern that no tool can dig,
and nothing opens the cavern floor: it has no base turf, and a cavern tile emptied some other way is filled again.
Only someone standing on the ground sees down a hole; from the air it shows dark. A floored room built in a cavern
holds its own air, as on the surface (the Planets terrain atmosphere); the cavern floor and the pad under a mouth,
named through `WGTerrainOpenTilesEvent`, are bare ground and keep the cavern's air.

Stairs can be built too. *Cavern stairs* (construction menu, Structures: 10 steel, 8 s,
unbolted with a wrench for the steel back) go on a cavern floor and open the ground tile above them, coming out on the
side they climb towards; from the surface, dig a shaft, drop down and build them under it. They are CE high ground
under a hole, so a mob walks them up and down with one level change and no fall, and whatever it is pulling changes
level with it. The recipe is refused, with the reason, under laid floor, under something built or a parked ship, and
where the tile at their top is a hole or built on. Opening takes what the biome grew on the hole and the tile at the
top and pins both, on ground that isn't loaded too, and stairs that could not open, such as under a ship that landed
while they were built, try again every 5 s. The hole queue gives a hole over stairs, these or a map's own stairway (any CE high ground), no landing and no
cut stairs, and a plain shade that draws no lip: a stairwell, not a sinkhole. Once built stairs are gone the hole
gets both, and the lip. The cut stairs are the caverns' own: they open the ground above them the same way, stand in the
registry (`WGCavernGroundComponent.Stairways`), are not taken apart, and come back if something deletes them.

Entry points: `WGCavernSystem` adds the cavern map through the Planets `WGPlanetLowerLayersEvent`, then fits it out on
`WGPlanetNetworkBuiltEvent` (its own atmosphere, no day cycle, sun shadows or parallax, the roof colour), links the
ground to it with `WGCavernGroundComponent` and asks `WGCavernMouthSystem` to claim the gate. `WGCavernMouthSystem`
evaluates mouth cells from pure noise (`.Claims.cs`: seeded candidates, ground and cavern checks, stamping, and the
polling that claims them within a small budget a tick), runs the hole queue (`.Holes.cs`) and keeps the registry of
cells, mouths, shades and cut stairs on the ground; its API is `GetGate`, `TryClaimCell`, `EvaluateCell`,
`TryOpenMouth` and `TryGetNearestMouth`, and for stairs (`.Stairs.cs`) `CheckStairs`, `TryOpenStairs`, `IsOpenAbove` and
`RefitHole`. `WGCavernStairsSystem` opens the ground when stairs appear, answers the recipe's `WGCavernStairsSite`
condition (a client only knows whether it stands in a cavern; the server checks the ground above and pops up why
not), and carries a pull that ended on the stairs across the level change. `WGCavernDigSystem` digs the shovel shafts and tells the marked block in
`PryTileReaction` which ground acid leaves alone. The shaft examine is `SharedWGCavernShaftSystem`, fed by the air reading
(`WGCavernAirClassifier`) each shade stores when it spawns. Whatever a walker on stairs was pulling is brought along by the stairs system (`BringPulled`): a level change clears every joint, so a tick later, once the hand that held the pull is free again,
it puts what was pulled on its puller's spot on the new level and takes hold of it again with a tile of rope. `WGCavernSystem` also deletes surface wildlife that drops
into a cavern because its ground chunk unloaded (`BiomeSystem.WgIsChunkLoaded`), and each second mirrors the ground's
clock onto the cavern (watches read "Underground") along with its soundscape: the surface's day and night ambience,
quieter and muffled through the rock (`surfaceAmbienceVolume`, `surfaceAmbienceOcclusion`), which the Planets player
eases in as you go down, or the cavern's own `ambience` played clear when it names one. Marked CE edits keep the
cavern safe to have and show it: the Planets hull guard (`WgClosedToHulls`); the eye cap in `CEZLevelsSystem.View.cs`,
which stops z-level eyes at a ground layer unless `WGCavernEyeSystem` finds one of its holes in the view of a viewer
standing on that ground (with a wider margin to lose it than to gain it, and never for a ghost that loads no terrain);
on the client, `WgAddCavernPass` in CE's z-level renderer, which draws the cavern under the observer's own ground while
a mouth is in view (`WGCavernViewSystem`); and a check in `ParallaxOverlay` that keeps the sky out of caverns and
mouths; and two lines in CE's z-physics: `WgSteppedDown` keeps a slow step down onto stairs from counting as a fall,
which would open a worn parachute, and `WgSealedAbove` makes the ground a ceiling for anything rising in a cavern
(a flyer, an atmospheric jetpack) even where its terrain isn't loaded and so has no tiles. The server tells a hole
from unloaded ground by the pin; a client can't, so it predicts the way up only for a body stairs have carried there
and leaves anything airborne to the server. The design, including what is still to come (the rest of F4 and the mining loop), is in
`Docs/_WG/Caverns/CAVERNS_DESIGN.md`.

<!-- WOLFGATE-GENERATED START -->
<!-- Generated by python Tools/_WG/Ci/modules.py --write. Don't edit by hand. -->

## Files

### Server

- [`Content.Server/_WG/Caverns/BiomeSystem.Caverns.cs`](BiomeSystem.Caverns.cs)
- [`Content.Server/_WG/Caverns/CEZLevelsSystem.Caverns.cs`](CEZLevelsSystem.Caverns.cs)
- [`Content.Server/_WG/Caverns/WGCavernCommand.cs`](WGCavernCommand.cs)
- [`Content.Server/_WG/Caverns/WGCavernDigSystem.cs`](WGCavernDigSystem.cs)
- [`Content.Server/_WG/Caverns/WGCavernEyeSystem.cs`](WGCavernEyeSystem.cs)
- [`Content.Server/_WG/Caverns/WGCavernGroundComponent.cs`](WGCavernGroundComponent.cs)
- [`Content.Server/_WG/Caverns/WGCavernMouthShape.cs`](WGCavernMouthShape.cs)
- [`Content.Server/_WG/Caverns/WGCavernMouthSystem.Claims.cs`](WGCavernMouthSystem.Claims.cs)
- [`Content.Server/_WG/Caverns/WGCavernMouthSystem.cs`](WGCavernMouthSystem.cs)
- [`Content.Server/_WG/Caverns/WGCavernMouthSystem.Holes.cs`](WGCavernMouthSystem.Holes.cs)
- [`Content.Server/_WG/Caverns/WGCavernMouthSystem.Preclaim.cs`](WGCavernMouthSystem.Preclaim.cs)
- [`Content.Server/_WG/Caverns/WGCavernMouthSystem.Stairs.cs`](WGCavernMouthSystem.Stairs.cs)
- [`Content.Server/_WG/Caverns/WGCavernSampler.cs`](WGCavernSampler.cs)
- [`Content.Server/_WG/Caverns/WGCavernStairsSystem.cs`](WGCavernStairsSystem.cs)
- [`Content.Server/_WG/Caverns/WGCavernStairsSystem.Pulled.cs`](WGCavernStairsSystem.Pulled.cs)
- [`Content.Server/_WG/Caverns/WGCavernSystem.cs`](WGCavernSystem.cs)
- [`Content.Server/_WG/Caverns/WGCavernViewerComponent.cs`](WGCavernViewerComponent.cs)
- [`Content.Server/_WG/Caverns/WGDigestiveAcidHissComponent.cs`](WGDigestiveAcidHissComponent.cs)
- [`Content.Server/_WG/Caverns/WGDigestiveAcidHissSystem.cs`](WGDigestiveAcidHissSystem.cs)

### Shared

- [`Content.Shared/_WG/Caverns/CESharedZLevelsSystem.Caverns.cs`](../../../Content.Shared/_WG/Caverns/CESharedZLevelsSystem.Caverns.cs)
- [`Content.Shared/_WG/Caverns/SharedWGCavernShaftSystem.cs`](../../../Content.Shared/_WG/Caverns/SharedWGCavernShaftSystem.cs)
- [`Content.Shared/_WG/Caverns/SharedWGCavernStairsSystem.cs`](../../../Content.Shared/_WG/Caverns/SharedWGCavernStairsSystem.cs)
- [`Content.Shared/_WG/Caverns/WGCavernAir.cs`](../../../Content.Shared/_WG/Caverns/WGCavernAir.cs)
- [`Content.Shared/_WG/Caverns/WGCavernLayerComponent.cs`](../../../Content.Shared/_WG/Caverns/WGCavernLayerComponent.cs)
- [`Content.Shared/_WG/Caverns/WGCavernMouthSpec.cs`](../../../Content.Shared/_WG/Caverns/WGCavernMouthSpec.cs)
- [`Content.Shared/_WG/Caverns/WGCavernPrototype.cs`](../../../Content.Shared/_WG/Caverns/WGCavernPrototype.cs)
- [`Content.Shared/_WG/Caverns/WGCavernShaftComponent.cs`](../../../Content.Shared/_WG/Caverns/WGCavernShaftComponent.cs)
- [`Content.Shared/_WG/Caverns/WGCavernShaftDigDoAfterEvent.cs`](../../../Content.Shared/_WG/Caverns/WGCavernShaftDigDoAfterEvent.cs)
- [`Content.Shared/_WG/Caverns/WGCavernStairsComponent.cs`](../../../Content.Shared/_WG/Caverns/WGCavernStairsComponent.cs)
- [`Content.Shared/_WG/Caverns/WGCavernStairsSite.cs`](../../../Content.Shared/_WG/Caverns/WGCavernStairsSite.cs)
- [`Content.Shared/_WG/Caverns/WGDigestiveAcidComponent.cs`](../../../Content.Shared/_WG/Caverns/WGDigestiveAcidComponent.cs)
- [`Content.Shared/_WG/Caverns/WGDigestiveAcidSystem.cs`](../../../Content.Shared/_WG/Caverns/WGDigestiveAcidSystem.cs)

### Client

- [`Content.Client/_WG/Caverns/CEClientZLevelsSystem.Caverns.cs`](../../../Content.Client/_WG/Caverns/CEClientZLevelsSystem.Caverns.cs)
- [`Content.Client/_WG/Caverns/ScalingViewport.Caverns.cs`](../../../Content.Client/_WG/Caverns/ScalingViewport.Caverns.cs)
- [`Content.Client/_WG/Caverns/WGCavernShadeVisualsSystem.Covered.cs`](../../../Content.Client/_WG/Caverns/WGCavernShadeVisualsSystem.Covered.cs)
- [`Content.Client/_WG/Caverns/WGCavernShadeVisualsSystem.cs`](../../../Content.Client/_WG/Caverns/WGCavernShadeVisualsSystem.cs)
- [`Content.Client/_WG/Caverns/WGCavernShadeVisualsSystem.Floors.cs`](../../../Content.Client/_WG/Caverns/WGCavernShadeVisualsSystem.Floors.cs)
- [`Content.Client/_WG/Caverns/WGCavernStairsSystem.cs`](../../../Content.Client/_WG/Caverns/WGCavernStairsSystem.cs)
- [`Content.Client/_WG/Caverns/WGCavernViewSystem.cs`](../../../Content.Client/_WG/Caverns/WGCavernViewSystem.cs)

### Integration tests

- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernAmbienceTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernAmbienceTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernBiomeTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernBiomeTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernCommandTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernCommandTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernFallTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernFallTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernFixture.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernFixture.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernFlightTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernFlightTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernGenerationTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernGenerationTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernGutTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernGutTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernHoleTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernHoleTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernHullTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernHullTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernLifeKarstCinderSandstoneTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernLifeKarstCinderSandstoneTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernLifeTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernLifeTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernMouthTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernMouthTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernNetworkTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernNetworkTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernOrbitalFallTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernOrbitalFallTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernPitFloorTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernPitFloorTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernPreclaimTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernPreclaimTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernPrototypeTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernPrototypeTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernRampTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernRampTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernRoofTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernRoofTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernStairwayTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernStairwayTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernViewerEyeTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernViewerEyeTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernViewTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernViewTest.cs)
- [`Content.IntegrationTests/Tests/_WG/Caverns/CavernWildlifeTest.cs`](../../../Content.IntegrationTests/Tests/_WG/Caverns/CavernWildlifeTest.cs)

### Unit tests

- [`Content.Tests/_WG/Caverns/CavernAirTest.cs`](../../../Content.Tests/_WG/Caverns/CavernAirTest.cs)
- [`Content.Tests/_WG/Caverns/CavernPassTest.cs`](../../../Content.Tests/_WG/Caverns/CavernPassTest.cs)

### Prototypes

- [`Resources/Prototypes/_WG/Caverns/Ambience/aerumna.yml`](../../../Resources/Prototypes/_WG/Caverns/Ambience/aerumna.yml)
- [`Resources/Prototypes/_WG/Caverns/Ambience/carcinoma.yml`](../../../Resources/Prototypes/_WG/Caverns/Ambience/carcinoma.yml)
- [`Resources/Prototypes/_WG/Caverns/Ambience/thrascias.yml`](../../../Resources/Prototypes/_WG/Caverns/Ambience/thrascias.yml)
- [`Resources/Prototypes/_WG/Caverns/Biomes/aerumna.yml`](../../../Resources/Prototypes/_WG/Caverns/Biomes/aerumna.yml)
- [`Resources/Prototypes/_WG/Caverns/Biomes/asclepiu.yml`](../../../Resources/Prototypes/_WG/Caverns/Biomes/asclepiu.yml)
- [`Resources/Prototypes/_WG/Caverns/Biomes/carcinoma.yml`](../../../Resources/Prototypes/_WG/Caverns/Biomes/carcinoma.yml)
- [`Resources/Prototypes/_WG/Caverns/Biomes/fervidus.yml`](../../../Resources/Prototypes/_WG/Caverns/Biomes/fervidus.yml)
- [`Resources/Prototypes/_WG/Caverns/Biomes/merak.yml`](../../../Resources/Prototypes/_WG/Caverns/Biomes/merak.yml)
- [`Resources/Prototypes/_WG/Caverns/Biomes/thrascias.yml`](../../../Resources/Prototypes/_WG/Caverns/Biomes/thrascias.yml)
- [`Resources/Prototypes/_WG/Caverns/caverns.yml`](../../../Resources/Prototypes/_WG/Caverns/caverns.yml)
- [`Resources/Prototypes/_WG/Caverns/Entities/decor.yml`](../../../Resources/Prototypes/_WG/Caverns/Entities/decor.yml)
- [`Resources/Prototypes/_WG/Caverns/Entities/fauna.yml`](../../../Resources/Prototypes/_WG/Caverns/Entities/fauna.yml)
- [`Resources/Prototypes/_WG/Caverns/Entities/flora.yml`](../../../Resources/Prototypes/_WG/Caverns/Entities/flora.yml)
- [`Resources/Prototypes/_WG/Caverns/Entities/gut.yml`](../../../Resources/Prototypes/_WG/Caverns/Entities/gut.yml)
- [`Resources/Prototypes/_WG/Caverns/Entities/Life/aerumna.yml`](../../../Resources/Prototypes/_WG/Caverns/Entities/Life/aerumna.yml)
- [`Resources/Prototypes/_WG/Caverns/Entities/Life/carcinoma.yml`](../../../Resources/Prototypes/_WG/Caverns/Entities/Life/carcinoma.yml)
- [`Resources/Prototypes/_WG/Caverns/Entities/Life/thrascias.yml`](../../../Resources/Prototypes/_WG/Caverns/Entities/Life/thrascias.yml)
- [`Resources/Prototypes/_WG/Caverns/Entities/mouths.yml`](../../../Resources/Prototypes/_WG/Caverns/Entities/mouths.yml)
- [`Resources/Prototypes/_WG/Caverns/Entities/stairs.yml`](../../../Resources/Prototypes/_WG/Caverns/Entities/stairs.yml)
- [`Resources/Prototypes/_WG/Caverns/levels.yml`](../../../Resources/Prototypes/_WG/Caverns/levels.yml)
- [`Resources/Prototypes/_WG/Caverns/Life/ambience_karst_cinder_sandstone.yml`](../../../Resources/Prototypes/_WG/Caverns/Life/ambience_karst_cinder_sandstone.yml)
- [`Resources/Prototypes/_WG/Caverns/Life/asclepiu.yml`](../../../Resources/Prototypes/_WG/Caverns/Life/asclepiu.yml)
- [`Resources/Prototypes/_WG/Caverns/Life/fervidus.yml`](../../../Resources/Prototypes/_WG/Caverns/Life/fervidus.yml)
- [`Resources/Prototypes/_WG/Caverns/Life/merak.yml`](../../../Resources/Prototypes/_WG/Caverns/Life/merak.yml)
- [`Resources/Prototypes/_WG/Caverns/Recipes/stairs.yml`](../../../Resources/Prototypes/_WG/Caverns/Recipes/stairs.yml)
- [`Resources/Prototypes/_WG/Caverns/tiles.yml`](../../../Resources/Prototypes/_WG/Caverns/tiles.yml)
- [`Resources/Prototypes/_WG/Caverns/Tiles/aerumna.yml`](../../../Resources/Prototypes/_WG/Caverns/Tiles/aerumna.yml)
- [`Resources/Prototypes/_WG/Caverns/Tiles/asclepiu.yml`](../../../Resources/Prototypes/_WG/Caverns/Tiles/asclepiu.yml)
- [`Resources/Prototypes/_WG/Caverns/Tiles/carcinoma.yml`](../../../Resources/Prototypes/_WG/Caverns/Tiles/carcinoma.yml)
- [`Resources/Prototypes/_WG/Caverns/Tiles/fervidus.yml`](../../../Resources/Prototypes/_WG/Caverns/Tiles/fervidus.yml)
- [`Resources/Prototypes/_WG/Caverns/Tiles/merak.yml`](../../../Resources/Prototypes/_WG/Caverns/Tiles/merak.yml)
- [`Resources/Prototypes/_WG/Caverns/Tiles/thrascias.yml`](../../../Resources/Prototypes/_WG/Caverns/Tiles/thrascias.yml)

### Localization

- [`Resources/Locale/en-US/_WG/Caverns/caverns.ftl`](../../../Resources/Locale/en-US/_WG/Caverns/caverns.ftl)
- [`Resources/Locale/en-US/_WG/Caverns/commands.ftl`](../../../Resources/Locale/en-US/_WG/Caverns/commands.ftl)
- [`Resources/Locale/en-US/_WG/Caverns/entities.ftl`](../../../Resources/Locale/en-US/_WG/Caverns/entities.ftl)
- [`Resources/Locale/en-US/_WG/Caverns/life_aerumna.ftl`](../../../Resources/Locale/en-US/_WG/Caverns/life_aerumna.ftl)
- [`Resources/Locale/en-US/_WG/Caverns/life_asclepiu.ftl`](../../../Resources/Locale/en-US/_WG/Caverns/life_asclepiu.ftl)
- [`Resources/Locale/en-US/_WG/Caverns/life_carcinoma.ftl`](../../../Resources/Locale/en-US/_WG/Caverns/life_carcinoma.ftl)
- [`Resources/Locale/en-US/_WG/Caverns/life_fervidus.ftl`](../../../Resources/Locale/en-US/_WG/Caverns/life_fervidus.ftl)
- [`Resources/Locale/en-US/_WG/Caverns/life_merak.ftl`](../../../Resources/Locale/en-US/_WG/Caverns/life_merak.ftl)
- [`Resources/Locale/en-US/_WG/Caverns/life_thrascias.ftl`](../../../Resources/Locale/en-US/_WG/Caverns/life_thrascias.ftl)

### Textures

- [`Resources/Textures/_WG/Caverns/digestive_acid.rsi/`](../../../Resources/Textures/_WG/Caverns/digestive_acid.rsi/)
- [`Resources/Textures/_WG/Caverns/glow_flora.rsi/`](../../../Resources/Textures/_WG/Caverns/glow_flora.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/antibody.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/antibody.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/asclepiu/cave_crab.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/asclepiu/cave_crab.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/asclepiu/cave_frog.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/asclepiu/cave_frog.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/asclepiu/cave_spider.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/asclepiu/cave_spider.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/asclepiu/pale_bat.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/asclepiu/pale_bat.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/bile_slug.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/bile_slug.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/deep_bat.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/deep_bat.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/fervidus/cinder_spider.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/fervidus/cinder_spider.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/fervidus/ember_viper.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/fervidus/ember_viper.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/fervidus/slag_tick.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/fervidus/slag_tick.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/fervidus/sulphur_carp.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/fervidus/sulphur_carp.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/merak/crypt_tick.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/merak/crypt_tick.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/merak/dust_bat.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/merak/dust_bat.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/merak/gallery_crab.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/merak/gallery_crab.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/merak/pallid_viper.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/merak/pallid_viper.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/rime_bat.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/rime_bat.rsi/)
- [`Resources/Textures/_WG/Caverns/Life/rime_slime.rsi/`](../../../Resources/Textures/_WG/Caverns/Life/rime_slime.rsi/)
- [`Resources/Textures/_WG/Caverns/Mouths/attributions.yml`](../../../Resources/Textures/_WG/Caverns/Mouths/attributions.yml)
- [`Resources/Textures/_WG/Caverns/Mouths/pit.rsi/`](../../../Resources/Textures/_WG/Caverns/Mouths/pit.rsi/)
- [`Resources/Textures/_WG/Caverns/Mouths/pit_lips.png`](../../../Resources/Textures/_WG/Caverns/Mouths/pit_lips.png)
- [`Resources/Textures/_WG/Caverns/Stairs/steel.rsi/`](../../../Resources/Textures/_WG/Caverns/Stairs/steel.rsi/)

### Tools

- [`Tools/_WG/Caverns/gen_flavour.py`](../../../Tools/_WG/Caverns/gen_flavour.py)
- [`Tools/_WG/Caverns/gen_life.py`](../../../Tools/_WG/Caverns/gen_life.py)
- [`Tools/_WG/Caverns/gen_life_karst_cinder_sandstone.py`](../../../Tools/_WG/Caverns/gen_life_karst_cinder_sandstone.py)
- [`Tools/_WG/Caverns/gen_pits.py`](../../../Tools/_WG/Caverns/gen_pits.py)
- [`Tools/_WG/Caverns/gen_stairs.py`](../../../Tools/_WG/Caverns/gen_stairs.py)

### Docs

- [`Docs/_WG/Caverns/CAVERNS_DESIGN.md`](../../../Docs/_WG/Caverns/CAVERNS_DESIGN.md)

## Non-modular edits

- [`Content.Client/_CE/ZLevels/Core/ScalingViewport.CEZLevels.cs`](../../../Content.Client/_CE/ZLevels/Core/ScalingViewport.CEZLevels.cs): the cavern shows through the ground's holes.
- [`Content.Client/Parallax/ParallaxOverlay.cs`](../../../Content.Client/Parallax/ParallaxOverlay.cs): no sky in a cavern or through a cavern mouth.
- [`Content.IntegrationTests/PoolManager.Cvars.cs`](../../../Content.IntegrationTests/PoolManager.Cvars.cs): tests that want a cavern turn caverns on themselves
- [`Content.Server/_CE/ZLevels/Core/CEZLevelsSystem.View.cs`](../../_CE/ZLevels/Core/CEZLevelsSystem.View.cs)
  - the level above the next eye, for the ground cap below.
  - under a ground layer, eyes only on its cavern and only while a hole is in view.
  - track the level above the next eye.
- [`Content.Server/Chemistry/TileReactions/PryTileReaction.cs`](../../Chemistry/TileReactions/PryTileReaction.cs): acid never opens ground over a cavern that no tool can dig, such as Aerumna's chromite.
- [`Content.Shared/_CE/ZLevels/Core/EntitySystems/CESharedZLevelsSystem.Update.cs`](../../../Content.Shared/_CE/ZLevels/Core/EntitySystems/CESharedZLevelsSystem.Update.cs)
  - stepping down onto stairs is not a fall
  - ground over a cavern is a ceiling even where it isn't loaded
- [`Resources/Prototypes/Entities/Mobs/NPCs/elemental.yml`](../../../Resources/Prototypes/Entities/Mobs/NPCs/elemental.yml): no BaseMob parent, so crabs and golems couldn't walk up stairs
- [`Resources/Prototypes/Entities/Mobs/NPCs/silicon.yml`](../../../Resources/Prototypes/Entities/Mobs/NPCs/silicon.yml): no BaseMob parent, so bots couldn't walk up stairs
- [`Resources/Prototypes/Entities/Mobs/Player/guardian.yml`](../../../Resources/Prototypes/Entities/Mobs/Player/guardian.yml): no BaseMob parent, so guardians couldn't walk up stairs
- [`Resources/Prototypes/Recipes/Lathes/Packs/engineering.yml`](../../../Resources/Prototypes/Recipes/Lathes/Packs/engineering.yml): shovels dig cavern shafts on planets

<!-- WOLFGATE-GENERATED END -->
