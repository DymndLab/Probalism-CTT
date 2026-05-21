# BeaconTransmission

`BeaconTransmission` is a Probalism Contract Configurator parameter that checks
whether a `PBM-CB Communications Beacon` has triggered and, by default,
completed its downlink to KSC.

The player configures the beacon in the VAB by selecting a data package, modem,
and trigger altitude.

## What Probalism adds

```cfg
PARAMETER
{
    name = BeaconDownlink
    type = BeaconTransmission
    title = Downlink a Packet-class PROBA beacon transmission at 18 km

    minDataSize = 0.07
    minTriggerAltitude = 18000
}
```

For normal beacon contracts, treat `minDataSize` and `minTriggerAltitude` as
the required fields. `minDataSize` is not technically required by the plugin,
but a beacon contract should include either `minDataSize` or
`requiredPackageId` so the objective targets a real package tier.

By default, the parameter requires full downlink completion. Use
`requireComplete = false` only when the contract should care that the beacon
triggered, not that the data was fully received.

## Data packages

Use `requiredPackageId` to require one exact data package. Use `minDataSize`
when any package above a minimum size is acceptable.

| Package | Package ID |  Data   |   Mass   |    Cost    |
|  Ping   |    ping    | 0.03 Mb |  0.04 t  |  25  funds |
| Packet  |   packet   | 0.07 Mb |  0.08 t  |  60  funds |
| Burst   |   burst    | 0.12 Mb |  0.12 t  |  110 funds |
| Stream  |   stream   | 0.20 Mb |  0.16 t  |  180 funds |

## Modems

Use `requiredModemId` to require one exact modem. Use `minBandwidth` when any
modem above a minimum transmission speed is acceptable.

|     Modem     | Modem ID | Transmission Rate | Added Mass | Added Cost |
| PBM-075 Modem | pbm-075  |     0.75 kB/s     |   0.02 t   |  25 funds  |
| PBM-144 Modem | pbm-144  |     1.44 kB/s     |   0.04 t   |  60 funds  |
| PBM-288 Modem | pbm-288  |     2.88 kB/s     |   0.07 t   |  120 funds |
| PBM-336 Modem | pbm-336  |     3.36 kB/s     |   0.10 t   |  180 funds |
| PBM-560 Modem | pbm-560  |     5.60 kB/s     |   0.15 t   |  300 funds |

## Supported fields

### `minDataSize`

Minimum beacon package data size in Mb.

Default value:

- `0`

Recommended values:

- `0.03` = Ping or larger
- `0.07` = Packet or larger
- `0.12` = Burst or larger
- `0.20` = Stream

### `minTriggerAltitude`

Minimum trigger altitude in meters ASL.

Default value:

- `0`

Treat this as required for normal beacon contracts so the objective has a
meaningful flight target.

### `maxTriggerAltitude`

Maximum trigger altitude in meters ASL.

Default value:

- unlimited

Use this only when the beacon must fire inside an altitude band.

### `minBandwidth`

Minimum modem bandwidth in kB/s.

Default value:

- `0`

Recommended values:

- `0.75` = PBM-075 or better
- `1.44` = PBM-144 or better
- `2.88` = PBM-288 or better
- `3.36` = PBM-336 or better
- `5.60` = PBM-560

Omit this when any beacon modem is acceptable.

### `requiredPackageId`

Exact package requirement.

Supported values:

- `ping`
- `packet`
- `burst`
- `stream`

Use this instead of `minDataSize` when the contract should require one exact
package, not "this package or better."

### `requiredModemId`

Exact modem requirement.

Supported values:

- `pbm-075`
- `pbm-144`
- `pbm-288`
- `pbm-336`
- `pbm-560`

Use this instead of `minBandwidth` when the contract should require one exact
modem, not "this speed or better."

### `requireComplete`

Whether the beacon data must finish downlinking.

Default value:

- `true`

Set `false` only for trigger-only contracts.

## Examples

### Packet or larger, any modem

```cfg
PARAMETER
{
    name = BeaconPacket
    type = BeaconTransmission
    title = Downlink a Packet-class PROBA beacon transmission
    minDataSize = 0.07
    minTriggerAltitude = 18000
}
```

### Burst or larger, PBM-144 or better

```cfg
PARAMETER
{
    name = BeaconBurst
    type = BeaconTransmission
    title = Downlink a Burst-class PROBA beacon transmission
    minDataSize = 0.12
    minTriggerAltitude = 80000
    minBandwidth = 1.44
}
```

### Exact Stream package with exact PBM-560 modem

```cfg
PARAMETER
{
    name = BeaconStreamFast
    type = BeaconTransmission
    title = Downlink a Stream package through a PBM-560 Modem
    minTriggerAltitude = 170000
    requiredPackageId = stream
    requiredModemId = pbm-560
}
```

### Trigger-only story beat

```cfg
PARAMETER
{
    name = BeaconTriggered
    type = BeaconTransmission
    title = Trigger the PROBA beacon above 220 km
    minDataSize = 0.20
    minTriggerAltitude = 220000
    requireComplete = false
}
```
