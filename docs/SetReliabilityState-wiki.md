# SetReliabilityState

`SetReliabilityState` is a Probalism Contract Configurator behavior that edits
Kerbalism `Reliability` state on a vessel.

## What Probalism adds

```cfg
BEHAVIOUR
{
    type = SetReliabilityState

    partName = liquidEngine3_v2

    broken = true
    needMaintenance = true

    popupTitle = Service Bulletin
    popupBody = Telemetry indicates the article is overdue for service.
    popupImageURL = Probalism-CTT/Flags/OmegaTronLogo
}
```

The Probalism-specific pieces are:

### `partName`

Matches the part's internal name.

### `partIndex`

Zero-based. Use this when more than one part matches the same selector.

## State-edit fields

### `broken`

Marks the component as malfunctioned or failed.

### `critical`

Marks the failure as critical.

### `quality`

Sets standard or high quality.

- `true` = high quality
- `false` = standard quality

### `needMaintenance`

Marks the component as needing service.

### `inspectNow`

Sets `last_inspection` to the current game time.

### `lastInspection`

Explicitly sets the inspection timestamp.

Do not use this together with `inspectNow`.

### `addIgnitions`

Adds to the current ignitions-used count.

This can be used to artificially shorten the lifespan of a new engine, or to push a used engine beyond its warrantied ignition limit.

### `popupTitle`

Popup title text.

### `popupBody`

Popup body text.

### `popupImageURL`

Optional image path for the popup.

If neither `popupTitle` nor `popupBody` is present, no popup is shown.

## Notes on Visibility

### `broken` and `critical`

In my testing these are the most useful. Kerbalism surfaces them clearly and in a way thats hard to miss visually.

### `needMaintenance`

needMaintenance is a real Kerbalism state, but it is not readily visible in all scenes. In my testing, it showed up properly in the Kerbalism info panel only once the player was actively controlling the affected vessel. For contracts where the state change will take place outside of the vehicle's scene, pair it
with a popup if you want the player to know there was a state change.

### `quality`

This applies correctly, but Kerbalism does not make it very visible in normal
flight UI. Pair it with a popup if the player is meant to notice the upgrade.

## Examples

### Break an antenna

```cfg
BEHAVIOUR
{
    name = BreakAntenna
    type = SetReliabilityState
    onState = CONTRACT_ACCEPTED
    vessel = SpaceGate-2
    partName = HighGainAntenna5
    broken = true
}
```

### Repair an engine

```cfg
BEHAVIOUR
{
    name = RepairMainEngine
    type = SetReliabilityState
    onState = PARAMETER_COMPLETED
    parameter = DockWithTarget
    vessel = SpaceGate-2
    partName = liquidEngine3_v2
    broken = false
    critical = false
    needMaintenance = false
    inspectNow = true
}
```

### Start an engine near its ignition limits on the launch pad

```cfg
PARAMETER
{
    name = OnPad
    type = ReachState
    situation = PRELAUNCH
}

BEHAVIOUR
{
    name = OutOfWarrantyEngine
    type = SetReliabilityState
    onState = PARAMETER_COMPLETED
    parameter = OnPad
    vessel = SpaceGate-2
    partName = liquidEngine3_v2
    addIgnitions = 10
}
```

### Mark ECLSS as needing service and tell the player

This one doesn't seem to work like i think it should.

In testing, ECLSS reliability has not been behaving
as intended. For now, assume life-support and other process-controller reliability edits are experimental.

If your goal is to ruin a Kerbal's day by sabotaging their life-support
hardware, you will have to wait until i do a bit more tinkering on this front.

```cfg
BEHAVIOUR
{
    name = FlagECLSSMaintenance
    type = SetReliabilityState
    onState = CONTRACT_ACCEPTED
    vessel = DeepHab-1
    partName = kv2Pod
    needMaintenance = true

    popupTitle = ECLSS Service Bulletin
    popupBody = Telemetry indicates the life-support article is overdue for service.
    popupImageURL = Probalism-CTT/Flags/YourMissionPNG
}
```

### Promote a component to high quality on success

```cfg
BEHAVIOUR
{
    name = UpgradeParachute
    type = SetReliabilityState
    onState = CONTRACT_SUCCESS
    vessel = TestArticle-1
    partName = parachuteSingle
    quality = true
    inspectNow = true

    popupTitle = Quality Upgrade Applied
    popupBody = Great work! The firmware has been patched to better handle unsafe chute deployments. Expect reliability to be far better moving forward.
}
```
