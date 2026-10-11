# NavalCommand — Composed equipment and observer instances

**Status:** Approved type direction in [Task 014](../014/TASK.md). The earlier [method-first API sketch](API_DESIGN_PROPOSAL.md) remains deferred. Task 013 records game evidence; its uncertain catalog and public date applicability recommendations are superseded by this design.

## Authoritative definitions

`Catalog` contains a context revision, unit types and shared equipment definitions, including definitions unused by deployed units. `UnitType` composes a tagged platform description (including helicopters) and alternative resolved `Loadout`s. Each loadout enumerates definitive `EquipmentFit`s referencing catalog equipment IDs with nominal resource quantities and optional physical traits. Empty exhaustive collections mean truly empty. Definitions contain no observation knowledge states. An omitted optional trait means not represented or not applicable. Future adapter retrieval must fail if required resolution fails; scenario dates and service years are resolved by that adapter, not exposed as public applicability types.

One `EquipmentType` composes independent sensing, damage, jamming, deception, communication and storage capabilities. Operations specify the capability they employ, accepted target inputs, action, duration, supported modes and resource costs. A sonobuoy has deployment and continuing sensing operations; a jammer uses channel capacity rather than mandatory ammunition; a fuel tank provides storage and optional jettison. Physical arcs and orientation are an optional separate dimension. No weapon/sensor root families or mandatory physical device identity are needed.

## Observer instances

`UnitInstance` is the sole representation for friendly units and contacts. Its observer/session scoped subject is tagged as an own-unit ID or opaque track ID. Only own-unit IDs authorize orders. Components hold display/type identity, kinematics, navigation, order eligibility, aircraft state and observation metadata. `View` has one instance collection. Contact data comes only from legitimate observer information; a class identification never implies a confirmed installed loadout.

An `EquipmentInstance` is an observed operational capability, potentially backed by several physical devices. Type identity, activation, readiness, damage and each resource amount have independent knowledge. Physical traits are optional. Missing applicable components differ from unknown values inside present components.

`Knowledge<T>` is Known(value), Estimated(value, source), or Unknown with no value. Its default is Unknown. No confidence is invented. `ObservedCollection<T>` is Complete, Partial or Unknown; default Unknown has no members. Complete empty means definitely empty, Partial empty means incomplete enumeration with no reportable members, and Unknown nonempty is invalid.

## Contracts and ownership

These small strongly typed C++ values live in the existing header-only `World.Api` target in `Domain.Api`; `Orders.Api` remains its only domain dependency. A separate catalog target currently adds no useful dependency boundary. There is no generic ECS, string-key component bag, extra configuration layer or platform inheritance hierarchy.

`Gateway.Api` owns one canonical Protobuf representation matching these components, knowledge states, coverage and subject tags. C++ and C# use generated bindings from those same sources. All consumers migrate together; pre-release evolution permits breaking interfaces and removes superseded types rather than adding compatibility projections. Catalog retrieval, equipment extraction and engagement APIs remain future work.
