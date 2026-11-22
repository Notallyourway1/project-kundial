# Project Kundial v0.1

This release contains the Project Sundial Payload plugin, assembly version 0.5.7.

A standalone part for Kerbal Space Program 1.12.x.

## Historical background

In July 1954, physicist Edward Teller presented Livermore's Sundial proposal to the U.S. Atomic Energy Commission's General Advisory Committee. The proposed thermonuclear device had a target yield equivalent to **10,000 megatons of TNT**, or **10 gigatons**. Sundial was never built or tested, so there are no measured results for a Sundial detonation.

The ribbed casing is a fictional depiction for KSP. The part's dimensions, mass, and the distances and timings of its effects are gameplay choices.

## Install or upgrade

1. Quit KSP and build the plugin from this source checkout as described below.
2. Open the KSP folder that contains your existing `GameData` folder.
3. Copy the checkout's `GameData/ProjectSundial` folder into your existing `GameData`. If your file manager asks, replace the old `ProjectSundial` folder. The DLL should be at `GameData/ProjectSundial/Plugins/ProjectSundial.dll`.
4. Start KSP and find **Project Sundial Payload** under **Utility** or search for **Sundial**. Career and Science modes unlock it with Heavy Rocketry. The v0.1 part identifier is retained for existing craft and saves.

## Fly a test

The 24-tonne KSP part is 11 m long and tapers from a 3.75 m body to a 2.5 m stack mount. Existing craft retain their part but may need a larger fairing and more lift capacity. Add a stock decoupler if you want to release it in flight. On an uncrewed Sandbox test craft, right-click the payload, select **Arm Sundial**, then **Start Detonation Countdown**. The fuse runs for 10 seconds of game time. **Disarm / Cancel Countdown** returns it to Safe. The editor's Action Groups tab offers the same controls.

In an atmosphere, a bright textured core and its own soft halo form a fireball with a **25 km visual radius** (50 km across). The fireball swells from the initial flash and slows as it grows. Its surface churns and cools from white to amber, with a soft, shifting halo. Smoke fades in after the fireball peaks. The hot center rises into the rolling cap, which cools to slate grey with pale billows and an amber underside. Animated 3D billows build the crown, rolling rim, and smoke column, while a low dust skirt and a pale shock front spread outward. The front follows the planet’s curvature with a raised, uneven crest and a broad, diffuse dust wake. The front triggers a stronger shake lasting about one second when it reaches your flight camera. The shake is weaker at higher altitudes and longer distances. For a sea-level burst on stock Kerbin, the cloud tops out near **36 km**, with a cap that fans out to roughly **50 km across**. The height follows the atmosphere remaining above the burst. A vacuum detonation uses a short flash and expanding glow. Watch the effects in flight view. Sundial temporarily widens the stock destruction-camera zoom limits to include your selected distance when the craft you are viewing is hit.

The damage reaches parts within 2.2 km on same-body vessels with active physics, including the launch vehicle if it remains in range. Other Sundial payloads inside that radius detonate with their own effect. The timer persists across quicksaves and vessel switches. If the fuse expires while the payload is packed or unloaded, detonation waits until its physics resume.

## Sources and credits

- U.S. Atomic Energy Commission, [minutes of General Advisory Committee meeting No. 44, July 1954](https://blog.nuclearsecrecy.com/wp-content/uploads/2021/10/1954-07-02-GAC-Meeting-No-44-Sundial-and-Gnomon.pdf). The Livermore discussion records the proposed yield.
- Alex Wellerstein, [The untold story of the world’s biggest nuclear bomb](https://thebulletin.org/2021/11/the-untold-story-of-the-worlds-biggest-nuclear-bomb/), Bulletin of the Atomic Scientists.
- U.S. Department of Energy, [Decision and Order on Sundial records](https://www.energy.gov/oha/articles/fic-15-0005-matter-alex-wellerstein), on the partial release of Sundial and related project records.
- taniwha, [io_object_mu](https://github.com/taniwha/io_object_mu), used to export and verify the KSP model.

The ZIP includes the plugin source; the compiled DLL must be rebuilt for this version (see **Building**). It does not require DLC or other mods.

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

The project website lives in [docs](docs/). See [PUBLISHING.md](PUBLISHING.md) for setup.
