using Robust.Shared.Configuration;

namespace Content.Shared._WG.CCVar;

/// <summary>
/// Settings for Wolfgate planets.
/// </summary>
[CVarDefs]
public sealed class PlanetCVars
{
    // LuaM: the planet stack needs both the surface and grid-network systems.
    public static bool NetworksEnabled(IConfigurationManager cfg) =>
        cfg.GetCVar(PlanetNetworks) && cfg.GetCVar(Content.Shared._LuaM.CCVar.LuaMCVars.ZLevelsGridNetworks);

    /// <summary>
    /// Master switch for Wolfgate planet networks: orbit layers, surfaces and the orbit FTL jumps.
    /// </summary>
    public static readonly CVarDef<bool> PlanetNetworks =
        CVarDef.Create("wg.planet_networks", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether planets run their weather and storms. Off keeps every world clear; the day cycle runs either way.
    /// </summary>
    public static readonly CVarDef<bool> PlanetWeather =
        CVarDef.Create("wg.planet_weather", false, CVar.SERVERONLY);

    /// <summary>
    /// Whether planet layers other than orbit are sent to a client only while a viewer is on or near them, instead
    /// of every planet tile going to every client on join. Needs the Lupine engine; applies to existing planets too.
    /// Off by default: a layer a client leaves is deleted there, and coming back (stairs, a sinkhole, a vehicle drop)
    /// left its walls and objects unrendered until relog. On again once the engine re-sends a returning layer's
    /// children whole.
    /// </summary>
    public static readonly CVarDef<bool> PvsRangeLimited =
        CVarDef.Create("wg.planet_pvs_range_limited", false, CVar.SERVERONLY);

    /// <summary>
    /// Whether planet ground gets an atmosphere, so rooms built on it hold their own air. Bare ground keeps the
    /// planet's air either way. Applies to networks built after it is set.
    /// </summary>
    public static readonly CVarDef<bool> TerrainAtmosphere =
        CVarDef.Create("wg.planet_terrain_atmos", true, CVar.SERVERONLY);

    /// <summary>
    /// Milliseconds a pass, ten a second, may spend loading the far part of planet load areas. The two chunks round
    /// each loader and the ground under hulls load at once regardless, and each layer loads one 16-tile block a pass
    /// whatever it costs. Zero loads everything the pass it comes into range, as upstream does.
    /// </summary>
    public static readonly CVarDef<float> TerrainLoadBudget =
        CVarDef.Create("wg.planet_terrain_load_budget", 4f, CVar.SERVERONLY);

    /// <summary>
    /// Chunks round a player's body that load in the pass the body comes into range, on top of its own 16-tile block
    /// and the tiles of <see cref="TerrainLoadNowPad"/>. Two was the old setting: thirty-six chunks in one pass.
    /// </summary>
    public static readonly CVarDef<int> TerrainLoadNowRing =
        CVarDef.Create("wg.planet_terrain_load_now_ring", 0, CVar.SERVERONLY);

    /// <summary>
    /// Tiles round a player's body whose 16-tile blocks load in the pass the body comes into range. The rest of the
    /// area loads nearest first over the passes after, from ground a body walks onto last.
    /// </summary>
    public static readonly CVarDef<float> TerrainLoadNowPad =
        CVarDef.Create("wg.planet_terrain_load_now_pad", 2f, CVar.SERVERONLY);

    /// <summary>
    /// Tiles a site must have before its ground is laid a few chunks a tick ahead of the grid merge instead of in the
    /// merge's one call. Zero merges every site in one call, as upstream does.
    /// </summary>
    public static readonly CVarDef<int> MergeSliceTiles =
        CVarDef.Create("wg.planet_merge_slice_tiles", 2048, CVar.SERVERONLY);

    /// <summary>
    /// Whether worlds are bounded to the circle their surface prototype gives: terrain outside it never loads, a
    /// boundary keeps mobs in, hulls can't descend outside it and the radar shows nothing there.
    /// </summary>
    public static readonly CVarDef<bool> Bounds =
        CVarDef.Create("wg.planet_bounds", true, CVar.SERVERONLY);

    /// <summary>Radius in tiles that overrides every surface prototype's; zero keeps each prototype's own.</summary>
    public static readonly CVarDef<int> Radius =
        CVarDef.Create("wg.planet_radius", 0, CVar.SERVERONLY);

    /// <summary>
    /// Whether a bounded world's whole ground is generated when its network is built, over the ticks after, so
    /// nothing streams in or unloads on the surface later. Needs wg.planet_bounds.
    /// </summary>
    public static readonly CVarDef<bool> Preload =
        CVarDef.Create("wg.planet_preload", true, CVar.SERVERONLY);

    /// <summary>Milliseconds a tick the preload may spend generating ground; one chunk always goes.</summary>
    public static readonly CVarDef<float> PreloadBudget =
        CVarDef.Create("wg.planet_preload_budget", 20f, CVar.SERVERONLY);

    /// <summary>Milliseconds a tick the preload may spend during the lobby countdown, low enough to keep the lobby responsive.</summary>
    public static readonly CVarDef<float> PreloadBudgetLobby =
        CVarDef.Create("wg.planet_preload_budget_lobby", 40f, CVar.SERVERONLY);

    /// <summary>Milliseconds a tick the preload may spend while the round start is held for the worlds to form.</summary>
    public static readonly CVarDef<float> PreloadBudgetHold =
        CVarDef.Create("wg.planet_preload_budget_hold", 250f, CVar.SERVERONLY);

    /// <summary>
    /// Milliseconds a tick world generation may spend on the rest: sites, cities, roads, camps, the boundary ring and
    /// what is let loose once the ground exists. A step that can't be split, such as loading one site's map, can run past it.
    /// </summary>
    public static readonly CVarDef<float> GenerationBudget =
        CVarDef.Create("wg.planet_generation_budget", 8f, CVar.SERVERONLY);

    /// <summary>Milliseconds a tick world generation may spend on the rest during the lobby countdown.</summary>
    public static readonly CVarDef<float> GenerationBudgetLobby =
        CVarDef.Create("wg.planet_generation_budget_lobby", 40f, CVar.SERVERONLY);

    /// <summary>Milliseconds a tick world generation may spend on the rest while the round start is held for it.</summary>
    public static readonly CVarDef<float> GenerationBudgetHold =
        CVarDef.Create("wg.planet_generation_budget_hold", 200f, CVar.SERVERONLY);

    /// <summary>
    /// Whether the round-start worlds of the lobby's preset are built, and preloaded, while the lobby is open; the
    /// sector bodies that spawn where they were built take them over at round start.
    /// </summary>
    public static readonly CVarDef<bool> Prebuild =
        CVarDef.Create("wg.planet_prebuild", true, CVar.SERVERONLY);

    /// <summary>
    /// Seconds the round start may wait past the lobby countdown for round-start worlds still forming; after that the
    /// round starts anyway, and the worlds still forming refuse descent until they are done.
    /// </summary>
    public static readonly CVarDef<float> RoundHoldMax =
        CVarDef.Create("wg.planet_round_hold_max", 180f, CVar.SERVERONLY);

    /// <summary>Whether worlds get their fixed cities (wgFixedCity), such as Crocus on Asclepiu.</summary>
    public static readonly CVarDef<bool> FixedCities =
        CVarDef.Create("wg.planet_fixed_cities", true, CVar.SERVERONLY);

    /// <summary>A wgPlanetPoiTable every world rolls its sites from instead of its surface's own; empty keeps each surface's.</summary>
    public static readonly CVarDef<string> PoiTable =
        CVarDef.Create("wg.planet_poi_table", "", CVar.SERVERONLY);

    /// <summary>
    /// Whether expedition consoles offer sites on the sector's worlds, flown to, in place of generated maps jumped
    /// to. A round with no free site falls back to the maps.
    /// </summary>
    public static readonly CVarDef<bool> PlanetExpeditions =
        CVarDef.Create("wg.planet_expeditions", true, CVar.SERVERONLY);

    /// <summary>
    /// Whether planet terrain nobody is near is unloaded. Off, upstream's unloader is all there is, and it lets go of
    /// almost nothing.
    /// </summary>
    public static readonly CVarDef<bool> TerrainUnload =
        CVarDef.Create("wg.planet_terrain_unload", true, CVar.SERVERONLY);

    /// <summary>
    /// Seconds a chunk must sit outside every loaded area, hull and build before it is unloaded. Long enough that the
    /// way back from a short trip is still there: loading it again is the dear part, and is done all at once.
    /// </summary>
    public static readonly CVarDef<float> TerrainUnloadIdle =
        CVarDef.Create("wg.planet_terrain_unload_idle", 180f, CVar.SERVERONLY);

    /// <summary>
    /// Milliseconds one unload pass may take, ten passes a second, one planet layer a pass. A pass always unloads one
    /// chunk, so a dense cavern chunk can run past it.
    /// </summary>
    public static readonly CVarDef<float> TerrainUnloadBudget =
        CVarDef.Create("wg.planet_terrain_unload_budget", 3f, CVar.SERVERONLY);

    /// <summary>
    /// Whether a preloaded ground puts away the untouched trees, rocks and plants nobody is near, keeping its tiles,
    /// and lays them again from the recipe when someone comes back. Off, the whole world's are kept all round.
    /// </summary>
    public static readonly CVarDef<bool> TerrainStash =
        CVarDef.Create("wg.planet_terrain_stash", true, CVar.SERVERONLY);

    /// <summary>
    /// Whether a planet layer's navmesh is built only round players and awake NPCs, a slice an update. Off, every
    /// changed chunk of the world is built in the next update, as upstream does.
    /// </summary>
    public static readonly CVarDef<bool> PathfindingLazy =
        CVarDef.Create("wg.planet_pathfinding_lazy", true, CVar.SERVERONLY);

    /// <summary>Tiles round a player or an awake NPC within which a planet layer's navmesh is built.</summary>
    public static readonly CVarDef<float> PathfindingRange =
        CVarDef.Create("wg.planet_pathfinding_range", 48f, CVar.SERVERONLY);

    /// <summary>Navmesh chunks of one planet layer built in one pathfinding update, nearest first.</summary>
    public static readonly CVarDef<int> PathfindingChunkBudget =
        CVarDef.Create("wg.planet_pathfinding_chunk_budget", 96, CVar.SERVERONLY);

    /// <summary>
    /// Whether a world's sites, wrecks, camps and city blocks nobody is near are paused where they stand, and woken as
    /// someone comes. Off, everything on a world runs all round.
    /// </summary>
    public static readonly CVarDef<bool> SiteStash =
        CVarDef.Create("wg.planet_site_stash", true, CVar.SERVERONLY);

    /// <summary>Seconds a site must have nobody within <see cref="SiteStashRange"/> before it is paused again.</summary>
    public static readonly CVarDef<float> SiteStashIdle =
        CVarDef.Create("wg.planet_site_stash_idle", 180f, CVar.SERVERONLY);

    /// <summary>Metres from a site's edge within which a player wakes it; past what a player can see.</summary>
    public static readonly CVarDef<float> SiteRestoreRange =
        CVarDef.Create("wg.planet_site_restore_range", 64f, CVar.SERVERONLY);

    /// <summary>Metres from a site's edge beyond which it may be paused once idle.</summary>
    public static readonly CVarDef<float> SiteStashRange =
        CVarDef.Create("wg.planet_site_stash_range", 96f, CVar.SERVERONLY);

    /// <summary>
    /// Milliseconds a tick may spend pausing and waking sites. A site a player is about to see is woken whole that
    /// tick regardless.
    /// </summary>
    public static readonly CVarDef<float> SiteStashBudget =
        CVarDef.Create("wg.planet_site_stash_budget", 2f, CVar.SERVERONLY);
}
