# SetKSCFacilityMinLevel

`SetKSCFacilityMinLevel` is a Probalism Contract Configurator behavior that
ensures one or more KSC facilities are at least a required level.

It is meant for narrative or progression support cases where a contract should
guarantee a facility capability instead of asking the player to buy it first.

## What Probalism adds

```cfg
BEHAVIOUR
{
    name = PrepareRescueInfrastructure
    type = SetKSCFacilityMinLevel
    onState = CONTRACT_ACCEPTED

    FACILITY
    {
        facility = AstronautComplex
        minLevel = 2
    }

    FACILITY
    {
        facility = TrackingStation
        minLevel = 2
    }
}
```

## Supported fields

### `onState`

Currently supported value:

- `CONTRACT_ACCEPTED`

This behavior is intentionally narrow. It runs when the contract is
accepted and raises facility levels as needed.

### `FACILITY`

Each `FACILITY` node describes one facility requirement.

Required fields:

- `facility`
- `minLevel`

## `facility`

Supported values:

- `Administration`
- `AstronautComplex`
- `LaunchPad`
- `MissionControl`
- `Observatory` (ResearchBodies addition)
- `ResearchAndDevelopment`
- `Runway`
- `SpaceplaneHangar`
- `TrackingStation`
- `VehicleAssemblyBuilding`

## `minLevel`

Integer target floor for that facility.

Behavior semantics:

- if the facility is already at or above `minLevel`, nothing happens
- if the facility is below `minLevel`, it is raised to meet the requirement
- the behavior never lowers a facility level

If `minLevel` is above that facility's max level, the behavior clamps to the
facility maximum.

## KCT integration

If Kerbal Construction Time is not installed, the behavior raises the stock
facility level directly.

If Kerbal Construction Time is installed, the behavior is KCT-aware:

- if the facility is already sufficient, it does nothing
- if KCT has a queued upgrade for that facility below the requested level, it
  promotes that queued target
- it then resolves the queued KCT upgrade immediately before ensuring the final
  facility level

This behavior is designed for emergency or narrative use cases, not normal
player-purchased facility progression.

## Cost associated with this behavior

This behavior does not charge the player funds.

Use it when the contract should guarantee capability, not when the player is
meant to choose and pay for an upgrade.

## Examples

### Prepare a rescue launch

```cfg
BEHAVIOUR
{
    name = PrepareRescueInfrastructure
    type = SetKSCFacilityMinLevel
    onState = CONTRACT_ACCEPTED

    FACILITY
    {
        facility = AstronautComplex
        minLevel = 2
    }

    FACILITY
    {
        facility = TrackingStation
        minLevel = 2
    }
}
```

### Ensure ResearchBodies observatory access

```cfg
BEHAVIOUR
{
    name = EnsureObservatoryOnline
    type = SetKSCFacilityMinLevel
    onState = CONTRACT_ACCEPTED

    FACILITY
    {
        facility = Observatory
        minLevel = 1
    }
}
```

### Raise several facilities together

```cfg
BEHAVIOUR
{
    name = PrepareDeepSpaceProgram
    type = SetKSCFacilityMinLevel
    onState = CONTRACT_ACCEPTED

    FACILITY
    {
        facility = MissionControl
        minLevel = 2
    }

    FACILITY
    {
        facility = ResearchAndDevelopment
        minLevel = 2
    }

    FACILITY
    {
        facility = Observatory
        minLevel = 1
    }
}
```
