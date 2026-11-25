wg-planet-signals-tag = { $vessels } { $vessels ->
        [one] vessel
       *[other] vessels
    }, { $sites } { $sites ->
        [one] site
       *[other] sites
    }, { $people } { $people ->
        [one] person
       *[other] people
    }
wg-planet-signals-tag-expedition = Your expedition is here
wg-planet-signals-tag-depot = {$depot}: land on its berths
wg-planet-signals-title = Signals from { $planet }
wg-planet-signals-expedition = Your expedition: { $site } at { $x }, { $y }
wg-planet-signals-vessels = Vessels
wg-planet-signals-sites = Sites
wg-planet-signals-none = Nothing on this world is giving off a signal.
wg-planet-signals-unknown-vessel = Unknown vessel
wg-planet-signals-vessel = { $name }, { $layer }, at { $x }, { $y }
wg-planet-signals-site = { $name } at { $x }, { $y }
wg-planet-signals-site-claimed = { $name } at { $x }, { $y }, expedition under way
wg-planet-signals-layer-ground = on the ground
wg-planet-signals-layer-air = in flight
wg-planet-signals-layer-orbit = in orbit
wg-planet-signals-marker-vessel = { $name }, { $layer }
wg-planet-signals-marker-expedition = Expedition: { $site }
wg-planet-signals-marker-distance = Expedition { $distance } m
wg-planet-signals-people = { $count } { $count ->
        [one] person
       *[other] people
    } on this world. Only those with a medical tracker can be named.
wg-planet-signals-tracked = Medical trackers
wg-planet-signals-person = { $name }, { $condition }, { $layer }, at { $x }, { $y }
wg-planet-signals-marker-person = { $name } ({ $condition })
wg-planet-signals-condition-alive = alive
wg-planet-signals-condition-critical = critical
wg-planet-signals-condition-dead = dead
wg-planet-signals-layer-underground = underground
