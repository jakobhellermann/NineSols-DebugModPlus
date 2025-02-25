# DebugModPlus

## Features

### Speedrun Timer

The speedruntimer can be used to time segments and track your PB.
The main shortcuts you need to know are
- `Set Startpoint` (unbound by default)
- `Set Endpoint` (unbound by default)

After those are set, the time between entering the startpoing and the endpoint will be tracked.
There's also some further customization, like autostarting time on savestate load or room entry (`Timer Mode`)
and there are additional shortcuts for `Reset Timer`, and `Clear Checkpoints`.

#### Ghost

When you enable `Record Ghost`, your segments will be recorded, and your PB replayed.
You can configure the color in the settings.

### Savestates

**TODO**: implement quicksaves

Savestates are separated into `quicksave` slots and a paged view of all saved states.

- `Open Page (Save)`: Select a slot to create a savestate in (`Keypad+`)
- `Open Page (Load)`: Select a slot to load a savestate from (`Keypad=`)
- `Open Page (Delete)`: Select a slot to create a savestate in (`Keypad-`)

Followed by `0-9` to select a slot, or `←`/`→` to go through pages.

All savestates are stored in `Nine Sols/ModData/DebugModPlus/Savestates`.
The files named `{number}-name.json` are the ones that will be displayed in the pages, and you can change the names as you like.

You can filter what you store in a savestate using the `Savestate filter` config option. `Player` and `Flags` should be stable, `Monsters` is more experimental. Please report any bugs or inconveniences!

### Miscellaneous
- Enable in-game Debug Console (`Ctrl+.`)
- Add basic debug settings UI (`Ctrl+,`)
  - (Basic) save states
  - Hitbox viewer (Ctrl+B)
  - Freecam (Ctrl+M)
  - Time control (Play/Pause, Advance frame, Fastforward)

### Map teleport

- Click in Map panel to teleport to that location
  - Ctrl+Click to force reloading the scene

### FSM Inspector

When you bind `FSM Picker Modifier` to `LControl`, ctrl-clicking an entity with a state machine will display some info text on the screen.

## Configuration
All config options be be changed by either
- manually editing `Nine Sols/Config/DebugModPlus.cfg`
- In the `Config Editor` of `r2modman`
- At runtime through [BepinExConfigurationManager](https://thunderstore.io/c/nine-sols/p/ninesolsmodding/BepinExConfigurationManager/) (default keybind `F1`)

---

Feature requests are welcome in the modding or speedrunning discord (`@dubisteinkek`).
