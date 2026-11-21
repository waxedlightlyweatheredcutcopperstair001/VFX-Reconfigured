# Volumetric FX Reconfigured 0.5.1

Volumetric explosions, fire, smoke and wreckage, and volumetric engine dust and steam, for Kerbal Space Program
1.12.5, in one mod. Based on **Volumetric Explosion FX** 1.1.1 and **Volumetric Ground FX** 1.1.0 by Warnix (both MIT),
reworked.

## Install

1. Remove `GameData/VolumetricExplosionFX` and `GameData/Volumetric Ground FX` if you have them. This mod replaces both;
   with either still installed every effect is drawn twice (you'll get a warning in flight).
2. Copy the `VolumetricFXReconfigured` folder into `GameData`.

Windows (DirectX 11). One toolbar button opens the settings, in flight and at the Space Center.

## What's different

**Explosions**
- Faster: fireballs and their smoke play 1.5 times faster (setting). The shockwave and flying debris keep their pace.
- Glowing gas gathers at the top and outer edges of the rising plume, over a darker, smoky body, with fainter smoke
  around it, instead of an opaque orange ball.
- A bright, white-hot flash (Explosion brightness) whose glow lasts by the explosion's size: small blasts flash
  briefly, big ones glow longer (the setting is a medium explosion's, 1.5 s). Then the smoke's opacity halves every 3 seconds and it is gone after a minute at most. Only
  fires (fuel tanks, batteries) glow longer. Nuclear explosions glow and linger much longer.
- Batteries short out in an arc flash, then vent jets of sparks and flame like overheating cells.
- What explodes depends on the part: wings, control surfaces, structural, fairing and command/probe parts break into
  debris with dust and smoke that hangs for a few seconds and settles; batteries flash and burn by their charge; fuel
  tanks blow up by how full they are, and an empty tank doesn't explode at all.
- BDArmory: a bomb or missile only gets its full warhead explosion when BDArmory actually sets it off. Destroyed unarmed
  it makes a small cook-off; an unarmed nuclear weapon doesn't explode at all.
- Nuclear air bursts: the fireball rises clear of the ground into a rolling cloud with a rounded underside, no dust stem
  above a low burst, and a faint ring of dust swept along the ground. Ground bursts keep their stem and skirt.

- Distant explosions are veiled by the atmosphere like the land around them (Atmospheric haze; works with Scatterer).

**Exotic fuels** (only with the mods that add them; any tank holding them)
- Antimatter (Far Future Technologies; KSP Interstellar uses the same name): drawn at a tenth of FFT's yield (its big
  tanks: about 10 to 260 kt). The annihilation's gamma rays light a huge, soft-edged sphere of air in a violet-blue glow
  round a white-hot core; then a pale, clean cloud (no soil, little smoke) that rises and fades. Larger and fainter in
  thin air; only a flash in vacuum.
- Graviolium (Blueshift): an electric-blue flash, then a gravitational lens for 3 to 6 s (15 to 600 m across with the
  tank's size): the world behind bent into an Einstein ring and arcs round a small black core with a blue rim, a ring of
  stretch-and-squeeze rippling outward, and a collapse with a last blue burst.
- Lithium (Near Future Propulsion): a white-crimson metal fire with molten droplets and dense white smoke, burning on.
- Nuclear Salt Water (Far Future Technologies): blue criticality flashes, a steam blast and a white steam cloud.
- Fission Pulse Units (Far Future Technologies): a chain of nuclear flashes around a dusty mushroom.

**Engine dust**
- The big billowing launch plumes only appear on launch pads (not a plane on the runway).
- Jet engines raise fainter, more transparent dust instead of rocket plumes.

**Settings** (toolbar button, Simple theme by default): *Explosion look* (speed, brightness, glow time, smoke shadows, smoke half-life, longest smoke, smoke opacity, glow
at the plume's edges, nuclear glow and smoke, exotic fuel explosions, explosion and particle limits) and *Ground effects* (the engine dust
settings), as well as the original pages.

## Licence

MIT. Copyright (c) 2026 Warnix (Volumetric Explosion FX, Volumetric Ground FX) and VFX Reconfigured contributors.
See `LICENSE` and `CREDITS.md`.
