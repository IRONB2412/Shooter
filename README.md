# 2D Shooter — Project Tracker

Top-down 2D arena shooter. Play vs bots (Easy/Normal/Hard) or online with friends.
Guns: Pistol, AR, SMG, Fire Gun. Throwables: Grenade, Smoke, Flash.
Mobile-style controls (joystick + fire button) + keyboard/mouse. Destructible walls.
Data-driven & pooled for performance. Custom in-editor Level Builder.

- **Unity:** 6000.3.6f1 (URP 2D)
- **Root folder for all our assets:** `Assets/_Game/`
- **Art:** [Kenney "Top-down Shooter"](https://kenney.nl/assets/top-down-shooter) and
  [Kenney "Particle Pack"](https://kenney.nl/assets/particle-pack) — **CC0 / public domain**
  — plus procedurally generated tiles/background (see [Art pipeline](#art-pipeline--atlases)).
  Everything lives in `Assets/_Game/Art/` (`CREDITS.txt`). The procedural square/circle
  are still used for bullets and the blast-radius disk.

---

## Architecture (data-driven & scalable)

| System | Where | Notes |
|---|---|---|
| Match flow | `Core/GameManager.cs` | Spawns players/bots, tracks state |
| Health/damage | `Core/Health.cs`, `Core/IDamageable.cs` | Shared by players + destructibles |
| Object pooling | `Core/ObjectPool.cs` | Projectiles, effects, throwables |
| Player movement | `Player/PlayerController.cs` | Rigidbody2D top-down, aim to move dir/mouse |
| Input | `Player/InputProvider.cs` | Abstracts joystick+buttons vs keyboard/mouse |
| Weapons (data) | `Weapons/WeaponData.cs` (ScriptableObject) | Add a gun = new asset, no code |
| Weapons (runtime) | `Weapons/WeaponController.cs`, `Projectile.cs` | Fire, reload, pooled bullets |
| Throwables (data) | `Weapons/ThrowableData.cs` (SO) | Grenade/Smoke/Flash via one script |
| Throwables (runtime) | `Weapons/ThrowableController.cs`, `Throwable.cs` | Arc throw, effects on land |
| Bot AI | `AI/BotAI.cs` | State machine: Patrol (wander the whole map) → Chase (vision cone + line of sight; not through smoke/grass) → Search (last seen spot) → Heal (health packs). See [Gameplay rules](#gameplay-rules). Difficulty tunes vision/accuracy/reaction/speed/give-up. |
| Bot navigation | `AI/NavGrid.cs` | Walkability grid from the Floor tilemap + obstacle colliders; A* + string-pulling; refreshes when terrain is destroyed |
| Tall grass | `World/GrassField.cs`, `GrassPatcher.cs` | Fields of grass drawn above characters; hide units from bots |
| Health packs | `World/HealthPickup.cs`, `HealthPickupSpawner.cs`, `PickupSpot.cs` | Rare packs, offline only |
| Walls look | `World/WallAutoTiler.cs` | Auto-tiles Wall/Edge tiles into top-down "roofs" at load |
| Skins | `Player/CharacterSkin.cs` | Body sprite follows the equipped weapon; random bot skin per spawn |
| Settings | `Core/GameSettings.cs`, `UI/SettingsPanel.cs` | Move/aim stick sensitivity (PlayerPrefs) |
| Draw order | `Core/SortingOrders.cs` | One table of all sorting orders (see below) |
| Spawn rules | `World/SpawnPoint.cs` (`Pick`) | Picks the spawn farthest from living fighters |
| Destructible terrain | `World/DestructibleTilemap.cs` | Grenades/bullets carve tilemap tiles |
| Camera | `World/CameraFollow.cs` | Smooth follow, clamped to map |
| UI | `UI/*` | Main menu, matchmaking, HUD, joystick, pause |
| Level Builder | `Editor/LevelBuilderWindow.cs` | Create/save 2D maps + spawn points; drop your own sprites |
| Networking | `Net/*` | NGO; offline works standalone, online = host/join |

---

## Progress

### Done
- [x] Recon + README + folder scaffold
- [x] Phase 1: Offline core — movement, 4 guns, 3 throwables, bots (Easy/Normal/Hard),
      destructible tilemap, camera follow, pooling. Verified in play mode:
      player + 5 bots spawn, firing + grenade carving + rendering all work.
      Builder: `Shooter > Build Game` menu (or `GameSetup.BuildAll()`).

- [x] Phase 2: UI — Menu scene (Play vs Bots → difficulty + bot count, Play Online →
      host/join by IP, Exit) and in-game HUD (health bar, ammo, weapon name, kills,
      virtual joystick, hold-to-fire, throw/swap/reload buttons, 4 weapon buttons,
      pause overlay with Resume/Menu/Exit, flash-grenade screen blind).
      Builder: `Shooter > Build UI`. Verified: menu→game flow, HUD binds to player.
      NOTE: enabled Player Settings > Run In Background so play mode ticks when the
      editor is unfocused (otherwise it appears "frozen").

- [x] Phase 3: Level Builder — `Shooter > Level Builder`. New Level (copies the
      wired template), Scene-view painting (Floor/Wall/Destructible/Spawn/Erase),
      bulk Fill/Border/Clear, and "Create Tile from your own sprite" to paint with
      custom art. Compiles & registered.

- [x] Phase 4: Online multiplayer — Netcode for GameObjects 2.13.2. Networked
      Player/Bot prefabs (NetworkFighter + owner-authoritative transform), a
      NetworkManager + UnityTransport rig, server-side spawner, and MatchBootstrap
      that switches offline↔online from the menu. Verified: offline still spawns
      player+bots; **online host starts, listens, and spawns networked player+bots
      with no errors.** Host/Join by IP wired. Builder: `Shooter > Build Networking`.
- [x] Phase 5: Playtest — offline (movement/guns/grenade-carve/render) and online
      host verified in play mode. Enabled Run In Background.

### Everything is built. Optional follow-ups (nice-to-have, not blocking)
- [ ] Two-machine online test (needs a build + editor, can't be done from here).
- [ ] Online HUD binding (HUD currently binds the offline player; online health
      syncs via NetworkFighter but the HUD bars read the offline path).
- [ ] Networked bullet visuals (bullets are local visuals; hits/damage ARE
      server-authoritative via RPC, so combat is correct, but remote players don't
      see each other's tracer sprites).
- [ ] Internet matchmaking by code (Relay + Lobby) — see note below.

---

## How to run
1. Open `Assets/_Game/Scenes/Menu.unity` and press Play.
2. **Play vs Bots** → pick difficulty + bot count → Start Match.
3. **Play Online** → Host, or type a host's IP and Join (port 7777).

## Controls (twin-stick + fire button)
- **Left joystick:** movement only — the character walks where this stick points.
- **Right joystick (AIM):** drag to rotate aim & facing. Aiming does not fire.
- Sticks are **floating**: the ring re-centres under your thumb (no offset), trails the
  thumb past the edge, and snaps back on release. Each stick tracks its own finger.
- **One action button:** FIRE shoots in *Gun* mode and throws in *Grenade/Smoke/Flash*
  mode — never both at once. The **mode button** (above-left of it) cycles
  Gun → Grenade → Smoke → Flash → Gun. Keyboard `G` still throws.
- **Settings** (menu + pause): Move / Aim sensitivity sliders (0.5x-2x).
- On a phone the sticks + buttons all respond **at once** (multi-touch); each control
  locks to the finger that grabbed it, so other fingers can't hijack it.
- Not touching the AIM stick? You face (and shoot) the way you walk.
- **Aim trail:** a red laser shows exactly where your shots will go (stops at walls).
- **Android Back** = Pause in game, back/quit in the menu, exit in Edit Layout.

### Laptop / editor testing — keyboard twin-stick (all at once)
The **Device Simulator turns your mouse into a single finger** (so it can only hold
one on-screen control). Use the keyboard to drive everything simultaneously:
- **Move:** W A S D     **Aim:** Arrow keys     **Fire:** hold **Space**
- **Reload:** R   **Throw:** G   **Swap throwable:** Q   **Weapons:** 1 / 2 / 3 / 4
- In the normal Game view (not the Simulator) the real mouse also works:
  cursor aims, Left Mouse fires (a click on a HUD button never fires a shot).
- Keyboard reaches the game even if another editor window has focus.
- **Weapons:** keys 1–4 or the four weapon buttons (Pistol / AR / SMG / Fire Gun).
- **Throw:** G / THROW button. **Swap throwable:** Swap button (Grenade/Smoke/Flash).
- **Pause:** Esc or the II button.

## Particle effects
Pooled, layered ParticleSystems fire on game events — muzzle flash, bullet impacts,
grenade explosions (flash + fireball + smoke + sparks + debris, with screen shake),
blood, smoke clouds and flash-bangs. Every effect reads ONE texture
(`Art/fx/fx_sheet.png`, a 4x4 grid of Kenney particle sprites) through two materials
(alpha + additive), so a firefight costs a handful of draw calls. Bullets also get a glow trail.
Code: `World/GameEffects.cs` (static `GameEffects.Muzzle/Impact/Explosion/Blood/…`),
`World/PooledParticle.cs` (returns to the pool when ALL child systems finish).
Rebuild: `Shooter > Build Effects` (also run by Apply Art Pass).

## Sprite UI
UI uses [Kenney UI Pack](https://kenney.nl/assets/ui-pack) (CC0) in
`Assets/_Game/Art/ui/`: glossy 9-sliced buttons, grey square buttons for weapon
slots (with gun icons), ring+knob joysticks, a red round FIRE button, and panel
cards for the menu / pause / health bar. Rebuild: `Shooter > Build UI`.

## Level Builder — tile types & graphics
Open `Shooter > Level Builder`. Brushes: **Floor, Edge (boundary), Wall (solid),
Destructible, Door, Prop, Spawn, Erase**. All tiles are 1×1.
- **Edge / Wall:** solid & indestructible (Edge is meant for map boundaries).
- **Destructible:** grenades / Fire Gun carve it.
- **Door:** a placed object that auto opens when a player/bot is near and closes
  when clear (blocks movement, bullets & LOS while shut). Code: `World/DoorController.cs`.
- **Prop:** a placed solid, indestructible obstacle (cabinet, trolley, crate…).
  Set **"Prop graphic"** before placing — each prop can use a different sprite
  while sharing the same behaviour. Code: `World/Prop.cs`.

**Tile graphics (reskin):** the *Tile graphics* section has a sprite field per
tile type. Drop in a new sprite and **every placed tile of that type updates
instantly** (they share one asset). Assigned sprites are re-imported to 1×1 so
they fill a cell exactly. *Create a NEW tile from a sprite* makes a separate tile
asset instead of replacing one. Door/Prop prefabs + the Edge tile are created by
`Shooter > Build Game` (or auto-created when you open the Level Builder).

## Customise the HUD (move & resize buttons)
Pause → **Edit Layout**. Then:
- **Drag** any stick or button to move it.
- **Tap** a control to select it, then use **− / +** to resize it.
- **Reset** restores defaults; **Done** exits.
Your layout is saved per device (PlayerPrefs) and restored automatically.
Code: `UI/DraggableHUDElement.cs`, `UI/HUDLayoutManager.cs`, `UI/HUDLayout.cs`.

## Editor tools (menu bar)
- `Shooter > Apply Art Pass` — importer settings, sprite atlases, FX prefabs, skins, bullet
  trail, decor + wall auto-tiler wiring, menu backdrop. Idempotent; re-run after changing art.
- `Shooter > Upgrade Level` — turns solid wall blocks (at least 5x4, not the border) into
  hollow rooms (wood floor, 2 doorways, crates, health-pack spot) and adds the grass +
  pickup spawners. Idempotent.
- `Shooter > Build Effects` — rebuilds the six FX prefabs.
- `Shooter > Build Game` — (re)builds sprites, tiles, data, prefabs and the Game scene.
- `Shooter > Build UI` — (re)builds the Menu scene and in-game HUD (HUD layout positions
  saved in PlayerPrefs survive).
- `Shooter > Build Networking` — (re)builds networked prefabs + scene rig.
- `Shooter > Level Builder` — design/paint new maps (see Phase 3).

## Testing online with a friend
1. Build the game (File > Build Settings > Build) OR use one editor + one build.
2. One player picks **Host**; the other picks **Join** and enters the host's LAN IP.
3. Same network works directly; over the internet the host must port-forward 7777
   (UDP) — or wire Relay (below) so no IP/port-forwarding is needed.

## Netcode / online structure (important)
Netcode for GameObjects has two hard rules: the **NetworkManager must be a root
object**, and **no NetworkObject may share its GameObject or children**. Breaking
either pops a blocking editor dialog. To sidestep it entirely, the saved scene
contains **no NetworkManager** — `MatchBootstrap` builds the whole rig (NetworkManager
+ transport + a plain `NetworkGameManager` spawner) **at runtime** only for online
matches. Don't add a NetworkManager/NetworkObject to the Game scene by hand.

## Performance / feel (low-end Android)
- Player & bots use `Rigidbody2D.Interpolate` for smooth motion; visuals turn via
  `CharacterMotor.turnSpeed` (no snapping); camera follow `smoothTime = 0.06`.
- 60 FPS target, vsync off, screen never sleeps, landscape only (`Core/PlatformSetup.cs`).
- Wall/crate tilemaps use **one merged CompositeCollider2D** each instead of a collider
  per tile (cheaper physics, bullets and bot line-of-sight).
- Mobile URP asset: HDR/MSAA/shadows off, dynamic batching + SRP batcher on.
- HUD: dynamic widgets (sticks, health, ammo, kills) sit in their own sub-canvases so
  updates don't rebuild the whole HUD; labels don't take raycasts; safe-area aware.
- Impact/blood particles are rate-capped; bullets, effects & throwables are pooled;
  bot perception runs on their reaction-time cadence, not every frame.
- Android player settings: IL2CPP, ARMv7 + ARM64, optimized frame pacing.
- Re-apply after adding levels: **`Shooter > Optimize for Android`**.

## Online multiplayer note
Offline (vs bots) is fully self-contained. Online uses **Netcode for GameObjects**:
- **Host/Join by IP** works out of the box (same network / port-forward).
- **Internet matchmaking** (join by code from anywhere) needs Unity Gaming Services
  (Relay + Lobby) linked to *your* Unity cloud project in the Editor dashboard —
  that's a manual account step; code hooks are provided and marked `TODO(cloud)`.

## How to resume if credits run out
1. Read this README's **Progress** section — the first unchecked box is next.
2. All gameplay code is under `Assets/_Game/Scripts/`. Nothing outside `_Game/` is ours.
3. Main scene: `Assets/_Game/Scenes/Game.unity` (once created). Menu: `Menu.unity`.

---

## Gameplay rules
- **Speed:** player 6.6, bots 4.8 / 5.4 / 6.0 (Easy/Normal/Hard) — you can always outrun them.
- **Tall grass:** drawn above characters. A bot can't see (or shoot) a target in grass
  unless within **2.4 units**; once spotted it keeps you "found" for 2.5 s and closes in
  to ~3.4 units. You are always drawn on top of grass so you never lose yourself.
- **Shot while unaware:** a bot hit by someone it hasn't spotted turns hostile, runs to
  where the shot came from, then searches around it. (Offline bots only.)
- **Awareness is tied to the screen:** a bot that leaves the camera view for 0.5 s
  forgets you and wanders again; bots off-screen can't spot you or react to hits.
  Unaware bots wander across the whole map (rooms included) via the nav grid, and never
  carry a stale target back on screen.
- **Smoke:** bots can't see targets inside or behind a smoke cloud (`Weapons/SmokeZones.cs`).
- **Flash:** a flash-bang whites the screen out the instant it goes off if you're inside
  the radius (closer = longer); bots inside are blinded. **Walls block the flash.**
- **Grenades:** friendly fire is ON — they hurt the thrower and teammates; walls shield
  from the blast; thrown items bounce off walls.
- **Health packs:** rare (max 3 on the map, 40 HP, 45-80 s respawn); anyone hurt can
  take one, bots go for them below 45% HP. Offline only (online health is server-owned).
- **Spawns:** every spawn/respawn picks the point farthest from living fighters (14+ units
  when possible), offline and online.
- **Bot pathing:** A* around walls and through doorways, last-seen search, target lead
  (Normal/Hard), strafing in one direction at a time, stuck recovery.

## Art pipeline & atlases
```
Assets/_Game/Art/
  characters/   player (+rifle/smg poses), 4 bot skins x 3 poses      PPU 48
  environment/  floor, floor_indoor, wall/edge + walls/ (94 roof variants), destructible
  props/        crates, barrel, decor bushes/rocks, tall-grass clumps, health pack
  fx/           fx_sheet.png + materials (FX_Alpha, FX_Additive, FX_Trail)
  ui/           Kenney UI pack
  backgrounds/  forest_bg.png (menu backdrop)
  atlases/      Gameplay.spriteatlasv2 (characters+environment+props), UI.spriteatlasv2
```
Sprite Packer mode is **Sprite Atlas V2 (always on)**. Atlased source textures are
uncompressed (the atlas is compressed). To swap art, replace a PNG keeping its file
name (GUID references survive) and run **Apply Art Pass**.
Credits: Kenney (CC0) Top-down Shooter + Particle Pack; floor, walls, background and
the FX sheet layout were generated for this project.

## Draw order (`Core/SortingOrders.cs`, higher = on top)
0 floor · 1 walls/destructible · 3 decor · 5 doors · 6 props · 7 health packs ·
8 trails/blast disk · 9 bullets/grenades · 10 bots · 12-13 tall grass · 14-17 blood ·
18 aim line · 19 smoke · **20 local player** · 21-27 muzzle/impact/explosion/flash FX.
Keep new objects in the right band so the player can never be hidden.

## Git
`master` is the main branch (GitHub `IRONB2412/Shooter`). `Apk/` (build output) and `.utmp/`
are deliberately **not tracked** — don't commit them.

## Changelog
- **2026-10-06** — Flash fix: screen whiteout is instant (was delayed by the hold), closer =
  longer, walls block it (player + bots); grenade fuse uses real time.
- **2026-10-06** — Draw order: local player above grass/smoke/bots, aim line above grass
  (`SortingOrders`).
- **2026-10-02** — Bots: awareness tied to the screen; unaware bots wander the map;
  stale-target bug fixed; player faster than bots; grass concealment + alert-on-hit;
  verified with a scripted play-mode test (spawn spacing, wandering, forgetting,
  grass rules, shot-from-grass).
- **2026-10-02** — Level: rooms with doorways, tall grass, health packs, spawn separation,
  A* bot navigation (`Shooter > Upgrade Level`).
- **2026-10-02** — Art: Kenney art + generated tiles, top-down roof walls (`WallAutoTiler`),
  rebuilt particle FX, sprite atlases, folder restructure, forest menu backdrop
  (`Shooter > Apply Art Pass`).
- **2026-10-02** — Controls/UI: floating joysticks (no offset, per-finger), sensitivity
  settings, single FIRE/THROW button + mode button, compact movable layout toolbar,
  accurate bullet/grenade direction (cast from body, per-frame movement), aim-trail/bullet
  offset fix, refresh-rate frame target, smoke/flash/friendly-fire grenades.
