## Features

- Allows players to loot auto turrets while powered
- Allows changing between Attack All and Peacekeeper mode as well
- Requires the player to be authorized to the turret and not be building blocked
- Does not allow authorizing, deauthorizing or rotating while powered
- Ignores NPC turrets

#### Side effects

This plugin works by tricking clients into believing each auto turret is off, by setting a single flag client-side. This allows looting powered turrets, but results in several other side effects which unfortunately **cannot be prevented**. However, depending on the type of server, some of these could be considered bonus features.

- The turret does not make a humming sound, but it still makes mechanical sounds as it rotates
- The turret's red or green laser is turned off
  - This prevents players from seeing the turret around corners (or sometimes through doors or walls) unless it has a laser sight or flashlight attachment
  - This also prevents players from being able to visually determine whether the turret is in peacekeeper mode

This makes the turrets less conspicuous (feature?) and so it's not possible to visually determine whether the turret is in peacekeeper mode (minor issue for most).

## Permissions

- `lootablepoweredturrets.owner` -- Turrets deployed by players with this permission can be looted while powered. Not required if the plugin is configured with `RequireOwnerPermission` set to `false` (default).

## Configuration

Default configuration:

```json
{
  "RequireOwnerPermission": false
}
```

- `RequireOwnerPermission` (`true` or `false`) -- While `true`, only auto turrets deployed by players with the `lootablepoweredturrets.owner` permission will be lootable. While `false`, all auto turrets can be looted while powered.
