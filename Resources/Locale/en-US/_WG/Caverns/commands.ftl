cmd-wgcavern-desc = Inspect caverns, visit their gates, carve mouths by hand and sample cavern terrain.
cmd-wgcavern-help = Usage: { $command } <list | tp <planet> [pad|mouth] | mouths <planet> | open | stats <planet>>
cmd-wgcavern-disabled = Caverns are off. Set wg.caverns to true.
cmd-wgcavern-invalid-args = Expected one of: list, tp <planet> [pad|mouth], mouths <planet>, open, stats <planet>.
cmd-wgcavern-unknown-planet = No built planet network named "{ $planet }".
cmd-wgcavern-no-cavern = { $planet } has no cavern, or its cavern has no gate.
cmd-wgcavern-empty = Nothing to list.
cmd-wgcavern-row = { $planet } | cavern: { $cavern } | mouths: { $mouths } | below: { $players }
cmd-wgcavern-row-none = none
cmd-wgcavern-mouth-row = { $kind } | anchor: { $origin } | tiles: { $tiles } | climb tile: { $climb }
cmd-wgcavern-mouth-kind = { $kind ->
    [gate] Gate
    [cell] Cell
   *[admin] Admin
}
cmd-wgcavern-tp-done = Moved to the gate { $target } of { $planet }.
cmd-wgcavern-no-map = Attach to an entity first.
cmd-wgcavern-not-ground = Stand on a planet's ground above a cavern first.
cmd-wgcavern-open-done = Carved a mouth around { $origin }.
cmd-wgcavern-open-refused = Can't carve a mouth here: { $reason ->
    [grid] a ship or debris is over it.
    [built] something built stands in the hole.
    [mob] someone is standing in the hole.
    [cavern] this planet's cavern is missing.
   *[mouth] there is a mouth there already.
}
cmd-wgcavern-hint-sub = <list|tp|mouths|open|stats>
cmd-wgcavern-stats-started = Sampling { $size }x{ $size } cavern tiles around { $centre }...
cmd-wgcavern-stats = { $planet }: { $size }x{ $size } tiles around { $centre } are { $open }% open; the largest region you can walk without crossing lava, plasma or acid holds { $largest }% of the walkable ground, and { $veins }% of the rock is ore. The square holds { $lights } lights, and { $glow }% of the tunnel floor in its middle { $inner }x{ $inner } is within a { $walk }-tile walk of one.
cmd-wgcavern-hint-planet = <planet name>
cmd-wgcavern-hint-target = <pad|mouth>
