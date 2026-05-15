# Probalism-CTT

Restructuring the community tech tree a bit to focus on an initial probe program with a build to Kerbalism fueled crewed missions. I am doing this mostly as a learning exercise but am definitely putting a lot of thought into what my own version of a probes first playthrough looks like.

## Core Philosophy

- Place parts into a clear progression so milestone technology is always useful as it is unlocked
- Keep parts in the CTT tech branch that fits their 'identity' (ie, science parts in the science branch)
- Embrace the risk to kerbals brought on by Kerbalism with a probes first start. Develop your space program, test rockets, build confidence in your systems before launching Kerbals into the heavens

## Background

I just never loved all parts of a probes first mod so I decided to make my own. Much thanks to PBC and Unkerballed Start, as both inspired me to figure out how to write all this in the first place. I looked at how both those mods performed patching of parts and could not have gotten this thing working without the ability to look at how others accomplished their own goals. Happy to be able to piggy back for my own thing here.

Since I started I've grown the vision for Probalism quite a bit, now looking to include a storyline with packed contracts to support telling that story. This element is still very much in work.

## Installation

To install, place the GameData folder inside your Kerbal Space Program folder. If asked to overwrite files, do so.

### Required Mods

[Kerbalism](https://github.com/Kerbalism/Kerbalism),
[Community Tech Tree](https://github.com/post-kerbin-mining-corporation/CommunityTechTree),
[Module Manager](https://github.com/sarbian/ModuleManager)

### Supported Mods

[Near Future Spacecraft](https://github.com/post-kerbin-mining-corporation/NearFutureSpacecraft),
[Near Future Exploration](https://github.com/post-kerbin-mining-corporation/NearFutureExploration),
[Near Future Construction](https://github.com/post-kerbin-mining-corporation/NearFutureConstruction),
[Near Future Launch Vehicles](https://github.com/post-kerbin-mining-corporation/NearFutureLaunchVehicles),
[Stockalike Station Parts Expansion Redux](https://github.com/post-kerbin-mining-corporation/StationPartsExpansionRedux),
[Planetside Exploration Technologies (MMSEV)](https://github.com/benjee10/Benjee10_MMSEV),
[ReStock / ReStock+](https://github.com/PorktoberRevolution/ReStocked),
[Airplane Plus](https://github.com/blackheart612/AirplanePlus),
[Universal Storage II Finalized](https://github.com/linuxgurugamer/universal-storage-2)

## Custom Contract Behaviours

Custom behaviors have been added to facilitate storytelling moments within contracts.

`Probalism.ContractBehaviours.dll` currently provides:

### `SetReliabilityState`

Edits Kerbalism `Reliability` state on a tracked vessel.

Reference:

- [docs/SetReliabilityState-wiki.md](docs/SetReliabilityState-wiki.md)

### `SetKSCFacilityMinLevel`

Ensures one or more KSC facilities are at least a required level when the
contract is accepted.

Reference:

- [docs/SetKSCFacilityMinLevel-wiki.md](docs/SetKSCFacilityMinLevel-wiki.md)

## License

MIT © Dymndheartkilla
