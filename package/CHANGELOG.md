# Changelog

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
