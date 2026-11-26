# Changelog

## 0.11.1
- Explosions behind EVE's volumetric clouds are hidden by them again. EVE (the raymarched clouds preview) lays its clouds
  over the scene at the start of the transparent pass, long before the moment 0.3.x looked for them, so large explosions
  were always drawn over the clouds. The explosion is now dimmed by EVE's own cloud image (how much light gets through
  the clouds on each pixel), by the share of that cloud lying in front of it: wholly behind a deck it disappears, inside
  one it fades with depth, below or in front of the clouds it shows in full. Ground-level fog and rain layers count too.
  No more full-screen copy of the image for this.
- With Scatterer: the front of a fireball no longer keeps Scatterer's sky and EVE's clouds from being drawn behind it
  (its soft edge showed the dark scene behind them). The front now goes into the depth buffer after them.
- Fixes from a code review:
  - Time zones from other mods (EffectsApi.TimeScale: Far Future Weaponry's tachyon field) now work. A zone set in
    LateUpdate was always one frame old when read, and was ignored.
  - Flames on burning wreckage stop with the rest in photo mode's freeze and in time zones.
  - Rebuilding the effects (changing a setting such as the number of explosions) no longer leaks the slots' meshes.
- Shaders changed (Composite; new FrontDepth replaces CloudProbe): rebuild with Unity (`tools/make.py`).

## 0.11.0
- A "debris" weapon look for other mods (EffectsApi.SetPartLook; Far Future Weaponry's Tachyon Gun): a part
  destroyed while tagged breaks apart, drawn as its wreckage only, with no fireball, fire or smoke trail. A warhead or an
  exotic fuel inside still goes off.

## 0.10.1
- Time zones (EffectsApi.TimeScale) now stop fire and smoke too:
  - The fire volumes and flame sheets flickered on the global shader clock. They now run on their own explosion's
    clock (`_FxLag`, the time it has missed). That also makes photo mode freeze them.
  - Flying wreckage, its fires and smoke trails slow and stop inside a zone.
- Shaders changed (FireVolume, Flame): rebuild with Unity.

## 0.10.0
- EffectsApi 3 (for Far Future Weaponry's tachyon field):
  - `TintSphere`: a sphere that tints everything inside it, with a crisp rim and a faint lattice on its surface,
    visible from inside and out. New shader, `TintSphere.shader`.
  - `TimeScale`: explosions inside a zone slow down or stop (their volumes and particles), until it lets them go.
- Gravitational lens (Graviolium tanks, and other mods' lenses): seen from close by, a lens no longer jumps to about
  twice its size when its ripple ring ends. It shrank to fit the ring's quad; it now shrinks by its own reach.
- Shaders changed: rebuild with Unity.

## 0.9.0
- EffectsApi 2 (for Far Future Weaponry): `LensCore`, a lens with a black core of any size (none at all: bent space
  without a black hole), and `SetLookAt`, a look for explosions near a point for a few seconds.
- The "antimatter" weapon look: a pink flash and a cloud at under half the opacity. Antimatter tanks are unchanged.
- Fixes for weapon looks: a "none" bomb broken unarmed shows its ordinary wreckage (it showed nothing), and a tagged
  bomb still on its plane no longer hides explosions near it.

## 0.8.0
- Effects for other mods (Far Future Weaponry): a public `VFXReconfigured.EffectsApi` with a gravitational lens and a
  glow (drawn over everything, like the Graviolium lens and exotic flashes) and weapon looks. A weapon part can be tagged
  "antimatter" (the antimatter blast at its own yield), "mushroom" (green flames, dark green smoke) or "none" (no explosion
  or wreckage drawn: the other mod draws its own effect).
- Green flames in the explosion and nuclear shaders, for the mushroom look (shaders changed: rebuild with Unity).

## 0.7.0
- New settings window: one plain look (the BDArmory Lite style) and three tabs instead of eight pages.
  - Performance: limits, rendering (volumetric explosions, resolution, detail, distortion, lighting, engine flame light,
    adaptive, video memory saver) and draw distances.
  - Explosion Effects: General, Fire & smoke, Debris & impacts, Nuclear.
  - Ground Effects: Effects, Look, Ground marks. Shows four ground effect switches (dust, launch pad steam, water
    spray, vacuum ejecta), the engine flame light and the number of ground effect sources, which were config-file only.
  - Shorter labels and hints; rows that depend on another switch are greyed out while it's off.
  - Removed: the five other window themes, layouts, opacity and size; Quality and Quick setup presets; the Intensity
    slider; the Compatibility page; the look editor (and its "compare with 1.0"). Values already saved are kept (the
    look editor's comparison is always off).
  - Defaults now also resets the ground effects.

## 0.6.0
- Spark sound: a hiss with crackling pops while battery or lithium sparks fly, and a pop as each battery jet bursts out
  (3D, at the ship volume, silent while paused). Generated by the mod: no sound files.
- Spark jets about ten times bigger in reach: battery sparks shoot up and out at 70-130 m/s (lithium droplets
  30-70 m/s), reaching 100 m and more, wider (20-45 cm) and much denser (500-1,500 a second), gone within 1-3 s; the
  battery's jet flames are bigger too.
- Fix: a battery's blue arc flash never showed (the explosion's volume was drawn over it). It, and the other short
  flashes (antimatter, Graviolium, nuclear salt water, pulse units), are now drawn over everything, with a crackling
  blue flash and blue light for about a second and a half.

## 0.5.1
- Spark jets you can actually see: battery and lithium sparks were 3-7 cm dots with ~20 cm streaks (a pixel at most from
  a normal camera distance) and shared the 256-particle ember pool. They now have their own system (up to 1,500), are
  drawn as long motion-blurred streaks that stay visible from afar, white-hot cooling to orange-red, falling and
  bouncing off the ground, and come much denser (a battery jet: 150-600 sparks a second at 14-30 m/s, with flame along
  the jet).
- A battery or an exotic fuel tank keeps its own effect: next to an exploding tank it was merged into that fireball,
  and its sparks (or glow, or lens) never appeared.

## 0.5.0
- More exotic fuel explosions (Explosion look > Exotic fuel explosions), for any part holding them:
  - Lithium (Near Future Propulsion): a white-crimson metal fire. A white flash, then a flickering glare that lights the
    land pink-white, a fountain of molten droplets, crimson-tinted flames, dense white smoke, and a fire left burning
    where it lands. 6 to 30 s by the mass of lithium.
  - Nuclear Salt Water (Far Future Technologies): the solution pools into a critical mass. Blue criticality flashes
    (a fraction of a second, repeating twice more, weaker, as the boiling solution falls back together), a steam
    explosion with a pale fireball, and a white steam cloud with a faint grey haze.
  - Fission Pulse Units (Far Future Technologies): a chain detonation. 3 to 7 units (1 kt each; 3 for FFT's smallest
    magazine) go off one after another a fraction of a second apart, each a blinding flash, around a dusty grey-brown
    mushroom of their combined yield.
- Batteries vent like real cells in thermal runaway: after the arc flash, jets of sparks and flame burst out one after
  another (1 to 4 jets, 1 to 7 s by charge), each with a pop, under grey-white smoke.

## 0.4.1
- Fix: a large black square at an antimatter blast and at the start and end of a Graviolium lens. The glow's soft
  edge overflowed against the sky (its depth is the far plane) and drew NaN, as black, over the glow's whole quad.
- Antimatter's fireball and cloud glow violet-blue round a white-hot core instead of a nuclear fireball's red-orange.

## 0.4.0
- Exotic fuel explosions (setting: Explosion look > Exotic fuel explosions), for any part holding the fuel; nothing
  changes without the mods that add it.
  - Antimatter (Far Future Technologies): drawn at a tenth of FFT's 36 GJ per unit (its tanks: about 10 kt to 260 kt;
    small amounts a bomb-sized blast). A violet-blue glow of ionised air several fireballs wide (wider and fainter in
    thin air, only a flash in vacuum) round a white-hot core that glows half as long as a nuclear one, lighting the land
    violet-white; a clean, pale lavender-to-white cloud with no soil drawn up.
  - Graviolium (Blueshift): an electric-blue flash and light, then a gravitational lens (15 m to 600 m with the amount,
    3 to 6 s): Einstein ring and arcs, a black core with a blue rim, a gravitational-wave ring rippling outward, then a
    collapse with a second ring and a last blue burst. The lens bends everything behind it, clouds and explosions too.
  - Visual only: no damage is added.

## 0.3.3
- Explosions inside a cloud layer are now hidden by it in part, by how deep in the layer they are: seen from above, a
  nuclear stem standing in the cloud deck disappears into it while the cap above shows. Before, only an explosion
  wholly below (or above) the whole layer was hidden, and EVE's Kerbin layers merge into one band from 1.75 to 11.5 km,
  so nothing in that band was ever hidden. Needs the shader bundle rebuilt (tools/make.py does it).

## 0.3.2
- Cloud diagnostics: KSP.log now says whether EVE's cloud layers were found (and why not), every layer's altitudes,
  the cloud materials' render queues, and when the video memory saver turns off hiding explosions behind clouds.

## 0.3.1
- Atmospheric haze varies with height: a cloud reaching into thin air is hazed less at its top than at its base.

## 0.3.0
- Atmospheric haze: distant explosions, nuclear clouds, splashes and engine ground clouds are veiled in the sky's colour
  by the air between them and the camera, like the land around them (they stayed crisp and saturated). Original model:
  the air's real density along the view, blue-shifted with distance, warmer at dusk, dark at night; follows Scatterer's
  strength for the planet when installed. Setting: Explosion look > Atmospheric haze.

## 0.2.0
- New default window theme, Simple: the BDArmory Lite look (near-black, grey, white, orange).
- Explosion brightness setting (default 1.4): a brighter, whiter-hot flash (1.0: the original, dimmer and redder).
- The glow lasts by explosion size: small blasts flash briefly, big ones glow up to three times the setting; nuclear
  fireballs by yield (the setting is a 20 kt bomb's; a megaton glows about a minute).
- Smoke shadows are no longer drawn on things near the camera (they painted dark, smoky patches on your own craft
  when flying low past explosions), and can be turned off (Explosion look > Smoke shadows).

## 0.1.0
First version: Volumetric Explosion FX 1.1.1 and Volumetric Ground FX 1.1.0 in one mod, with reworked explosion timing
and look, part-dependent explosions, BDArmory arming awareness, nuclear air bursts, launch-pad-only plumes and fainter
jet dust, and one settings window. See README.md.
