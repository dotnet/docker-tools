# ImageBuilder V2 models

Follow these guidelines closely when making changes to ImageBuilder V2 models.
These guidelines follow [RFC 2119](https://www.rfc-editor.org/rfc/rfc2119).

## Models

- Serialized types MUST NOT contain behavior.
  Behavior MAY be implemented using extension members.
- A serialized type MUST NOT have a property that duplicates the dictionary key it is stored under.
- Deserialization MUST NOT perform semantic validation.
- Validation MUST report every error, not only the first.
- `Models.V2` MUST NOT depend on [`Models.V2.Projections`](./Projections/).
- `Models.V2` and [`Models.V2.Projections`](./Projections/) MUST NOT depend on [`Models.Manifest`](../Manifest/).

## Projections

Projections, such as [`ImageEntry`](./Projections/ImageEntry.cs) and [`PlatformEntry`](./Projections/PlatformEntry.cs), are flattened views of the serialized objects.

- Projections MUST reference the serialized objects they project, and MUST NOT copy, compute or resolve values.
- Projections MUST NOT contain behavior.
  Behavior MAY be implemented using extension members.
