## Map and marker names
wg-planet-orbit-map-name = { $planet } orbit
wg-planet-orbit-marker-name = { $planet } orbital marker
wg-planet-network-name = { $planet } planet network
wg-planet-air-map-name = { $planet } airspace { $level }
wg-planet-cloud-map-name = { $planet } cloud deck
wg-planet-lower-map-name = { $planet } lower level { $level }
wg-shuttle-map-surface = { $name } (surface)
wg-shuttle-map-sector = { $name } (sector space)
wg-shuttle-map-transit = Between levels
wg-shuttle-map-level = Level { $level }
wg-planet-asclepiu-surface = Verda

## FTL
wg-shuttle-console-in-transit = Cannot engage FTL while changing altitude.

## Shuttle console orbit controls
wg-shuttle-console-enter-orbit = Enter orbit: { $planet }
wg-shuttle-console-leave-orbit = Leave orbit: { $planet }
wg-shuttle-console-orbit-none = No planet in range
wg-orbit-no-hull = This console is not aboard a flyable hull.
wg-orbit-no-network = { $planet } has no orbit layer to drop into.
wg-orbit-no-sector = This orbit layer has no sector body to return to.
wg-orbit-not-in-sector = { $planet } is not in this system.
wg-orbit-not-in-orbit = This hull is not in planet orbit.
wg-orbit-outside-bounds = Outside the world's edge; no ground to descend to here.
wg-orbit-out-of-range = Too far from { $planet } for orbital insertion.
wg-orbit-refused = Orbital insertion refused.
wg-orbit-hopping = Already on the way.
wg-orbit-in-ftl = Can't do this mid-jump.
wg-orbit-protected = This ship can't be moved.
wg-orbit-anchored = This ship is anchored in place.
wg-orbit-world-forming = { $planet } is still forming. Try again in a moment.

## Round-start hold
wg-planet-round-hold = Worlds are still forming. The round starts as soon as they are done.
wg-planet-round-hold-expired = Worlds are still forming, but the round is starting anyway.

## wgplanet command
cmd-wgplanet-desc = Build, inspect and tear down Wolfgate planet networks.
cmd-wgplanet-help = Usage: { $command } <list | build <planet> | delete <planet> | spawn <surface id> | system <starSystem id> | tp <planet>>
cmd-wgplanet-disabled = Planet networks are off. Set wg.planet_networks to true.
cmd-wgplanet-invalid-args = Expected one of: list, build, delete, spawn, system, tp.
cmd-wgplanet-unknown-planet = No sector planet named "{ $planet }".
cmd-wgplanet-unknown-surface = No wgPlanetSurface prototype "{ $surface }".
cmd-wgplanet-unknown-system = No starSystem prototype "{ $system }".
cmd-wgplanet-no-surface = { $planet } has no Wolfgate surface.
cmd-wgplanet-already-built = { $planet } already has a network ({ $network }).
cmd-wgplanet-not-built = { $planet } has no network.
cmd-wgplanet-built = Built { $layers } layers for { $planet }: network { $network }, orbit { $orbit }.
cmd-wgplanet-build-failed = Failed to build a network for { $planet }; see the server log.
cmd-wgplanet-deleted = Deleted the network for { $planet }.
cmd-wgplanet-row = { $planet } | surface: { $surface } | network: { $network }
cmd-wgplanet-row-none = none
cmd-wgplanet-empty = No sector planets registered.
cmd-wgplanet-system-set = Set { $system } on map { $map }.
cmd-wgplanet-no-map = Run this with a map in mind: attach to an entity on the sector map first.
cmd-wgplanet-tp-done = Moved to the orbit layer of { $planet }.
cmd-wgplanet-hint-sub = <list|build|delete|spawn|system|tp>
cmd-wgplanet-hint-planet = <planet name>
cmd-wgplanet-hint-surface = <wgPlanetSurface id>
cmd-wgplanet-hint-system = <starSystem id>

# Unsanctioned worlds
wg-orbit-unsanctioned-title = Unsanctioned world
wg-orbit-unsanctioned-text = [color=#ff3030][bold]{ $planet } is not a sanctioned world.[/bold][/color]
    Entering its orbit is a breach of sector law, and nobody is coming for a hull lost there.
wg-orbit-unsanctioned-proceed = Enter orbit
wg-orbit-unsanctioned-abort = Abort
wg-orbit-unsanctioned-unconfirmed = { $planet } is unsanctioned. Confirm the entry at the console.

# Gravity well
wg-gravity-well-warning = Warning: { $ship } is adrift inside the gravity well of { $planet }. Restore thrust or be pulled into orbit.

## Planet Control admin panel and command
wg-planet-control-title = Planet Control
wg-planet-control-refresh = Refresh
wg-planet-control-none = No worlds in this sector
wg-planet-control-not-built = Not built yet: nobody has visited. Only the sanction can be set.
wg-planet-control-status = { $time } local, { $weather } shown as { $wolf } ({ $seconds } s to next change), { $gravity } g
wg-planet-control-climate = { $season }, { $temperature } outside ({ $left } min of the season left)
wg-planet-control-climate-none = No seasons, { $temperature } outside
wg-planet-control-season = Season
wg-planet-control-time = Time
wg-planet-control-set = Set
wg-planet-control-dawn = Dawn
wg-planet-control-noon = Noon
wg-planet-control-dusk = Dusk
wg-planet-control-midnight = Midnight
wg-planet-control-weather = Weather
wg-planet-control-weather-clear = Clear
wg-planet-control-weather-seconds = Seconds the weather holds before the planet's own cycle resumes
wg-planet-control-gravity = Gravity
wg-planet-control-sanctioned = Sanctioned world
cmd-planetcontrol-desc = Lists the sector's worlds, or sets one's time, weather, season, gravity or sanction.
cmd-planetcontrol-help = Usage: planetcontrol [<planet> time <HH:MM> | weather <id|clear> [seconds] | season <id> | gravity <g> | sanctioned <true|false>]
cmd-planetcontrol-invalid-args = Unknown planet, field or value.
cmd-planetcontrol-no-change = Nothing changed.
cmd-planetcontrol-row = { $planet }: { $time }, { $weather } shown as { $wolf }, season { $season }, { $temperature }, { $gravity }g, sanctioned={ $sanctioned }
cmd-planetcontrol-season-none = none
cmd-planetcontrol-row-unbuilt = { $planet }: not built, sanctioned={ $sanctioned }
cmd-planetcontrol-weather-clear = clear
cmd-planetcontrol-applied = { $planet }: { $changes }
