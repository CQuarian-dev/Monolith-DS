## Shuttle console: entering the atmosphere
wg-shuttle-console-enter-atmosphere = Enter atmosphere: { $planet }
wg-shuttle-console-atmosphere-mode-tooltip = In atmosphere:
    Ordinary thrusters give half thrust.
    Landing thrusters give full thrust.
    Lift must reach 100% to take off or hover.

## Shuttle console: flight status and checklist
wg-flight-phase-orbit = In orbit: { $planet }
wg-flight-phase-landed = Landed: { $planet }
wg-flight-phase-lifting = Lifting off
wg-flight-phase-spooling = Engines spooling: { $countdown } s
wg-flight-phase-climbing = Climbing
wg-flight-phase-descending = Descending
wg-flight-phase-hovering = Hovering
wg-flight-phase-lift-lost = LIFT LOST: falling
wg-flight-ready = READY FOR LIFTOFF
wg-flight-not-ready = NOT READY
wg-flight-check-lift = Lift
wg-flight-check-lift-value = { $percent }% / 100%
wg-flight-check-power = Power
wg-flight-check-power-ok = OK, { $power } kW
wg-flight-check-power-short = Short { $short } kW
wg-flight-check-hold = Held: { $reason }
wg-flight-check-cargo = Cargo
wg-flight-check-cargo-value = { $tonnes } t
wg-flight-fix-convert = Fix: convert { $count } { $count ->
        [one] thruster
       *[other] thrusters
    } to landing thrusters.
wg-flight-fix-add = Fix: add { $count } more landing { $count ->
        [one] thruster
       *[other] thrusters
    }.
wg-flight-fix-convert-add = Fix: convert all { $convert } ordinary thrusters and add { $add } landing { $add ->
        [one] thruster
       *[other] thrusters
    }.
wg-flight-fix-power = Fix: add { $short } kW of power supply.
wg-flight-hint-grounded = Hold ascend or press Liftoff to take off.
wg-flight-hint-air = Ascend and descend change altitude.
wg-flight-hint-orbit = Hold descend to enter the atmosphere.

## Descent gates
wg-flight-landing-blocked = Can't set down here. There's something solid under the hull.
wg-flight-busy = This hull is already under way.
wg-flight-descent-refused = Atmospheric entry refused.
wg-flight-lift-warning = Lift is only { $percent }%. Use Enter atmosphere on the console to descend anyway.
wg-flight-power-warning = Not enough power to hover. Use Enter atmosphere on the console to descend anyway.

## Confirm dialog
wg-flight-confirm-title = Atmospheric entry
wg-flight-confirm-descend = Descend
wg-flight-confirm-abort = Stay in orbit
wg-flight-confirm-text = { $planet }: lift [color={ $colour }]{ $percent }%[/color] of the 100% needed to hover.
    Hover power: { $power } kW.
wg-flight-confirm-low-lift = Not enough lift: the hull will sink and can't climb back out.
wg-flight-confirm-falling = Far too little lift: the hull will fall and crash.
wg-flight-confirm-power-deficit = Not enough power: thrusters will cut out.

## Thruster examination
wg-thruster-atmosphere-mode = Atmosphere mode: half thrust. A landing conversion restores full thrust.
wg-thruster-landing-converted = Landing conversion: full thrust in atmosphere.

## Landing thruster conversion kit
wg-landing-kit-wrong-target = Use the kit on a linear thruster, not a gyroscope.
wg-landing-kit-not-anchored = Anchor the thruster before converting it.
wg-landing-kit-converted = Landing conversion installed: full thrust in atmosphere.

## Flight alarm situation codes
wg-alert-lift-lost = Lift Lost
wg-alert-lift-lost-desc = The hull no longer has the lift to hold itself up.
wg-alert-lift-lost-announcement = Caution: lift lost. Hull is descending.

wg-alert-dont-sink = Don't Sink
wg-alert-dont-sink-desc = Out of orbit and sinking into the atmosphere.
wg-alert-dont-sink-announcement = Don't sink. { $ship } is out of orbit and going down.

wg-alert-sink-rate = Sink Rate
wg-alert-sink-rate-desc = Descending faster than the hull can arrest.
wg-alert-sink-rate-announcement = Sink rate. Sink rate.

wg-alert-terrain = Terrain
wg-alert-terrain-desc = The ground is coming up.
wg-alert-terrain-announcement = Terrain. Terrain ahead.

wg-alert-too-low-terrain = Too Low - Terrain
wg-alert-too-low-terrain-desc = Below the last safe layer with no lift.
wg-alert-too-low-terrain-announcement = Too low. Terrain.

wg-alert-pull-up = Pull Up
wg-alert-pull-up-desc = Seconds from impact.
wg-alert-pull-up-announcement = Pull up. Pull up.

## Orbit decay
wg-shuttle-console-orbit-stable = Orbit: stable
wg-shuttle-console-orbit-decaying = Orbit decaying: { $seconds } s

wg-alert-orbit-decay = Orbit Decay
wg-alert-orbit-decay-desc = Station-keeping is lost; the hull is falling out of orbit.
wg-alert-orbit-decay-announcement = Orbit decaying: station-keeping lost. { $ship } is falling out of orbit.
wg-alert-orbit-decay-countdown = Orbit decaying: station-keeping lost, { $seconds } seconds to atmospheric entry.

wg-landing-kit-already-converted = This thruster already has a landing conversion.
wg-landing-kit-out-of-reach = Move closer to install the landing conversion.

wg-crash-apc-fault-examine = The power regulator is damaged and intermittently cuts out. Recalibrate it with a multitool.
wg-crash-apc-fault-repaired = You recalibrate the damaged APC regulator. Its output is stable again.

wg-orbit-flight-refused = There is no air to fly in up here.
wg-orbit-flight-stopped = There is no air up here and you stop flying.
