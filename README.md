# Project Kundial v0.4

Project Sundial Payload is a standalone part for Kerbal Space Program 1.12.x. The v0.4 release contains the v0.8.1 plugin. The source and changelog use the plugin version.

## Attaching and the structural cage

The payload has a stack node at its bottom (size 2) and one at its nose, and it can attach to the side of other parts and take parts on its own side. In the VAB, right-click the payload and set **Structural cage** to **On** to wrap the casing in a yellow steel frame. The cage adds 3 tonnes and 2,500 funds (`cageMass` and `cageCost` in `Sundial.cfg`), parts attached to the side sit on its outside, and it is destroyed with the payload. It is purely a visual and attachment option and does not change the blast.

## Historical background

In July 1954, physicist Edward Teller presented Livermore's Sundial proposal to the U.S. Atomic Energy Commission's General Advisory Committee. The proposed thermonuclear device had a target yield equivalent to **10,000 megatons of TNT**, or **10 gigatons**. Sundial was never built or tested, so there are no measured results for a Sundial detonation.

The ribbed casing is a fictional depiction for KSP. The part's dimensions, mass, and the distances and timings of its effects are gameplay choices.

## Install or upgrade

1. Quit KSP and extract the [ready-to-install ZIP](https://notallyourway1.github.io/project-kundial/downloads/project-kundial-v0.4.zip). It includes the compiled plugin. If you downloaded the source ZIP, [build the plugin](#building) first.
2. Open the KSP folder that contains your existing `GameData` folder.
3. Copy the archive's `GameData/ProjectSundial` folder into your existing `GameData`. If your file manager asks, replace the old `ProjectSundial` folder. The DLL should be at `GameData/ProjectSundial/Plugins/ProjectSundial.dll`.
4. Start KSP and find **Project Sundial Payload** under **Utility** or search for **Sundial**. Career and Science modes unlock it with Heavy Rocketry. The v0.1 part identifier is retained for existing craft and saves.

If the part shows up in KSP but has no activation buttons in flight, check the DLL path in step 3. Restart KSP after copying the plugin.

## Fly a test

The 24-tonne KSP part is 11 m long and tapers from a 3.75 m body to a 2.5 m stack mount. Existing craft retain their part but may need a larger fairing and more lift capacity. Add a stock decoupler if you want to release it in flight. On an uncrewed Sandbox test craft, right-click the payload, select **Arm Sundial**, then **Start Detonation Countdown**. The fuse runs for 10 seconds of game time. **Disarm / Cancel Countdown** returns it to Safe. The editor's Action Groups tab offers the same controls.

In an atmosphere, a bright textured core and its own soft halo form a fireball with a **2.8 km visual radius** (5.6 km across, scaled to Kerbin). The fireball swells from the initial flash and slows as it grows. Its surface churns and cools from white to gold-orange, with bright cream veins running across it and a soft, shifting halo. A burst close to the camera opens with a white-out flash. Smoke fades in after the fireball peaks. The hot center rises into a broad, flat-topped mushroom cap of cream billows lit by the sun, with cool blue-grey shading, glowing orange patches, a bright warm underside and a dark reddish stem that flares at its base. Animated 3D billows build the crown, rolling rim, underside and smoke column, while a low dust skirt and a pale shock front spread outward. The front follows the planet’s curvature with a raised, uneven crest and a broad, diffuse dust wake. The front travels up to about 36 km over 95 seconds, starting fast and slowing as it spreads, with a tall condensation wall, a trailing second crest and a dust wall that is lit orange at first and cools to tan, and it gives the flight camera a brief shake when it passes your viewpoint. The shake is weaker at higher altitudes and longer distances. For a sea-level burst on stock Kerbin, the cloud tops out near **11 km**, with a cap that fans out to roughly **27.5 km across**, about two and a half times as wide as the cloud is tall. The height is capped to 90% of the atmosphere remaining above the burst. The cloud and smoke last about 80 seconds and the whole effect about 100. A white flash tints the sky and briefly lifts the ambient light, a faint shimmering shell of hot air surrounds the fireball while it cools, and a warm light rides the crest of the shock front. A detonation above the atmosphere looks completely different: there is no fireball dome, ground shock front or mushroom cloud. You see a white flash and bloom, two thin shock rings that reach about 45% farther than the debris shell, expand over 9 and 11 seconds and fade over about 30 seconds, 56 streaks of hot debris that cool from white to red, a few thin lingering wisps, a glow on the air and ground below the burst and a brief camera jolt when the camera is close. The burst keeps a minimum size on screen so it stays readable from far away, and the whole effect lasts 42 seconds. Watch the effects in flight view. Sundial temporarily widens the stock destruction-camera zoom limits to include your selected distance when the craft you are viewing is hit.

In an atmosphere, the damage reaches parts within 2.2 km on same-body vessels with active physics, including the launch vehicle if it remains in range. Above the atmosphere (or on a body with none), there is no blast wave to absorb the X-rays, so the damage reaches **8 km**. Parts beyond 2.2 km are only destroyed if no other part blocks the straight line from the burst to them. Terrain does not shield. Only vessels within physics range are affected, normally about 2.5 km, so the full 8 km applies only if you have extended the loading distances. `vacuumBlastRadius` and `vacuumShielding` in the part config control this. Other Sundial payloads inside that radius detonate with their own effect. The timer persists across quicksaves and vessel switches. If the fuse expires while the payload is packed or unloaded, detonation waits until its physics resume.

## Sources and credits

- U.S. Atomic Energy Commission, [minutes of General Advisory Committee meeting No. 44, July 1954](https://blog.nuclearsecrecy.com/wp-content/uploads/2021/10/1954-07-02-GAC-Meeting-No-44-Sundial-and-Gnomon.pdf). The Livermore discussion records the proposed yield.
- Alex Wellerstein, [The untold story of the world’s biggest nuclear bomb](https://thebulletin.org/2021/11/the-untold-story-of-the-worlds-biggest-nuclear-bomb/), Bulletin of the Atomic Scientists.
- U.S. Department of Energy, [Decision and Order on Sundial records](https://www.energy.gov/oha/articles/fic-15-0005-matter-alex-wellerstein), on the partial release of Sundial and related project records.
- taniwha, [io_object_mu](https://github.com/taniwha/io_object_mu), used to export and verify the KSP model.

The source ZIP includes the part assets and build scripts. Build `ProjectSundial.dll` before installing. The mod requires no DLC or other mods.

## Building

### Windows

1. Install [Visual Studio Build Tools](https://visualstudio.microsoft.com/downloads/) with the **.NET desktop build tools** workload.
2. Open **Developer Command Prompt for Visual Studio** from the Start menu and change to the project folder. Use `cd /d` if it is on a different drive.
3. Run the Windows build script with the path to your own copy of KSP 1.12.x. For a default Steam installation:

```bat
build-windows.cmd "C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed"
```

Use the folder containing `Assembly-CSharp.dll`. Some installations use `KSP_Data\Managed`. The script writes `GameData\ProjectSundial\Plugins\ProjectSundial.dll`. Then follow the [install steps](#install-or-upgrade) to copy the full mod folder into KSP.

### macOS and Linux

With Mono and `mcs` installed, run the POSIX build script. On macOS, pass the Managed directory inside the app bundle.

```sh
sh build.sh "/path/to/KSP.app/Contents/Resources/Data/Managed"
```

On Linux, pass the Managed directory under `KSP_Data` or `KSP_x64_Data`, depending on your install.

Run `Source/BuildCasing.py ASSET_DIRECTORY FONT.ttf` with Pillow installed and the io_object_mu project’s `mu.py` on the Python module path to regenerate the casing assets.

## Changes in v0.8.1

- Faster and longer shockwaves. Ground: the front reaches about 36 km (`ShockRangeScale` 3.0), lasts 95 s and starts out faster; the whole effect now runs 100 s while the cloud keeps its 80 s timeline. Orbit: the rings reach 45% farther, finish expanding in 9 s and 11 s, fade over about 30 s, and the burst lasts 42 s.
- Self-contained extras that need no other mod: a sky and ambient light flash, a shimmering heat-haze shell around the fireball, and a warm light that rides the shock crest. The haze is a glow, not true refraction.
- Performance: the cloud sorts every third frame and rotates billows once, the fireball halo uses fixed arrays, and the shock front reuses per-row values.
- Not run in game by the author: everything in v0.8.1. The cage and attach changes below were tested as v0.8.0.

## Changes in v0.8.0

- A stack node on the nose, plus surface attach in both directions (`srf` bulkhead profile).
- Optional structural cage in the VAB (`Structural cage` toggle), with its own collider so side-attached parts sit on it. Tested in game.

## Changes in v0.7.8

- Brings the v0.6.2 effect sizes into the v0.7 line: 2.8 km fireball, 11 km cloud, `visualRadius` 12 km in the part config, a quieter camera shake (1.6 degrees) and the improved shock-camera crossing check. The orbital burst keeps the size it had at v0.7.7.
- The Linux/macOS and Windows build scripts now reference `UnityEngine.UI.dll`, which the line-of-sight check needs.
- Release tested in game as v0.7.7 (orbital). This combined v0.7.8 build has not been run in game by the author.

## Changes in v0.7.7

- Longer shockwaves. Ground: the shock front lasts 70 s (was 38 s), travels 1.7 times `visualRadius` (about 20 km), stays strong for most of its life and fades late. Space: the two shock rings keep expanding for 12 s and 14 s and fade over about 19 s, and the burst lasts 30 s.
- Better ground shock front: a taller condensation wall, a secondary trailing crest, and a dust wall lit orange by the fireball at first that cools to tan over about 14 s. The ground changes have not been run in game by the author.

## Changes in v0.7.6

- Fixed the planet's atmosphere rendering breaking when the orbital effect ended. The camera hold handed the camera back to a vessel the game still reported as active but that had just been destroyed. It now only does so for a living vessel and logs what it found to KSP.log ("Camera hold ended").

## Changes in v0.7.5

- New orbital (vacuum) detonation: a white flash and bloom, two shock rings, 56 streaks of hot debris that cool from white to red, a few thin wisps, a glow on the air and ground below the burst, a brief camera jolt for a close camera, and a minimum on-screen size so the burst stays readable from far away.
- The camera hold follows the burst while the effect lasts.

## Changes in v0.7.3 and v0.7.4

- The camera no longer jumps when the vessel it is following is destroyed by the burst. The flight camera is pinned to a small anchor at the burst point before the vessel is destroyed, and the anchor stays until you switch vessel or leave the scene.

## Changes in v0.7.2

- Performance only: the cloud mesh reuses its buffers and index data instead of rebuilding them every frame, the shock front and fireball surface do less per-vertex work, the line-of-sight check no longer allocates, and the build script uses `-optimize+`.

## Changes in v0.7.0

- Sundial detects whether it detonates inside or above the atmosphere and behaves differently. This also applies on bodies without an atmosphere.
- Above the atmosphere the damage radius grows from 2.2 km to 8 km, with line-of-sight shielding beyond 2.2 km. The info panel lists both radii and the detonation message says "in vacuum".

## Changes in v0.6.2

- Increased the fireball visual radius to 2.8 km and the nominal cloud height to 11 km on stock Kerbin.
- Extended the shock front to 12 km over 38 seconds. The flight camera shakes for about one second when the front reaches its position, with less movement at higher viewpoints and longer distances.

## Changes in v0.6.1

- Rescaled the whole effect to Kerbin: the fireball is 2.4 km in radius (it now roughly matches the 2.2 km damage radius), the cloud tops out near 10 km with a cap about 25 km across, and the shock front and camera tremor reach 8.5 km.
- The cloud height, early cloud size and cap proportions are constants at the top of `SundialDetonationEffect.cs`; the fireball and shock-front sizes are `fireballRadius` and `visualRadius` in the part config.

## Changes in v0.6.0

- Added a larger, flatter mushroom cloud with a cap about 2.5 times as wide as the cloud is tall.
- The cloud is lit by the sun with cream highlights, blue-grey shadow, glowing orange patches, a bright underside and a dark reddish flared stem.
- The fireball stays gold-orange with bright cream veins instead of cooling to deep red.
- A white-out flash covers the screen for a camera close to the burst.
- The cloud now uses 570 billows and the effect lasts 80 seconds (was 58). The new tuning constants are at the top of `SundialDetonationEffect.cs`.

## Changes in v0.5.7

- The shock-front camera tremor now also triggers when the flight camera is directly above ground zero, where the crossing check previously never fired.

## Changes in v0.5.x (since v0.4)

- Fireball is a textured, animated core with its own halo; the smoke cloud is built from animated 3D billows.
- A curved, ground-following shock front crosses the planet and shakes the flight camera when it reaches it.
- The stock destruction-camera zoom limits are widened temporarily so your chosen camera distance survives the hit.
- The cloud top follows the atmosphere remaining above the burst (about 36 km on Kerbin) and the whole effect lasts 58 s.

## Changes inherited from v0.3.1

- Other Sundial payloads inside the damage radius now detonate with their own effect instead of being destroyed silently.
- The countdown readout keeps updating while the vessel is packed and shows "Detonation pending" once the fuse has expired.
- Starting the countdown from an action group without arming first now shows a message.
- Added fallback transparency shaders and a log warning when no compatible shader is found.
- Part config declares `bulkheadProfiles = size2`.

## GitHub Pages

The [project website](https://notallyourway1.github.io/project-kundial/) hosts the source ZIP. See [PUBLISHING.md](PUBLISHING.md) to update the download.
