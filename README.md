# 2D Shooter — Project Tracker

Top-down 2D arena shooter. Play vs bots (Easy/Normal/Hard) or online with friends.
Guns: Pistol, AR, SMG, Fire Gun. Throwables: Grenade, Smoke, Flash.
Mobile-style controls (joystick + fire button) + keyboard/mouse. Destructible walls.
Data-driven & pooled for performance. Custom in-editor Level Builder.

- **Unity:** 6000.3.6f1 (URP 2D)
- **Root folder for all our assets:** `Assets/_Game/`
- **Demo art:** Unity built-in sprites (Knob, Square, Circle, UISprite) — no external downloads needed.

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
| Bot AI | `AI/BotAI.cs` | Difficulty enum tunes accuracy/reaction/aggression |
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
- **FIRE button:** hold to shoot (above the aim stick). Move/aim/fire independent.
- **Desktop:** WASD/arrows move, mouse aims, hold Left Mouse to fire.
- **Reload:** R / Reload button.
- **Weapons:** keys 1–4 or the four weapon buttons (Pistol / AR / SMG / Fire Gun).
- **Throw:** G / THROW button. **Swap throwable:** Swap button (Grenade/Smoke/Flash).
- **Pause:** Esc or the II button.

## Customise the HUD (move & resize buttons)
Pause → **Edit Layout**. Then:
- **Drag** any stick or button to move it.
- **Tap** a control to select it, then use **− / +** to resize it.
- **Reset** restores defaults; **Done** exits.
Your layout is saved per device (PlayerPrefs) and restored automatically.
Code: `UI/DraggableHUDElement.cs`, `UI/HUDLayoutManager.cs`, `UI/HUDLayout.cs`.

## Editor tools (menu bar)
- `Shooter > Build Game` — (re)builds sprites, tiles, data, prefabs and the Game scene.
- `Shooter > Build UI` — (re)builds the Menu scene and in-game HUD.
- `Shooter > Build Networking` — (re)builds networked prefabs + scene rig.
- `Shooter > Level Builder` — design/paint new maps (see Phase 3).

## Testing online with a friend
1. Build the game (File > Build Settings > Build) OR use one editor + one build.
2. One player picks **Host**; the other picks **Join** and enters the host's LAN IP.
3. Same network works directly; over the internet the host must port-forward 7777
   (UDP) — or wire Relay (below) so no IP/port-forwarding is needed.

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
