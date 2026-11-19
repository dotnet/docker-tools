# ImageBuilder V2 models

`Microsoft.DotNet.ImageBuilder.Models.V2` holds the V2 manifest and image artifact details models.
They live side by side with the old models until the old ones are removed. Keep the two separate:
no adapters or conversions between them.

## Model shape

- Records hold data only: no methods, computed properties or comments. All behavior goes in
  extension members (C# 14 extension blocks). This applies to every type, without exceptions.
- Keep the model minimal. Add a field or member only when a concrete consumer needs it, and be
  able to name the use case it serves.
- Don't store what can be derived. A record doesn't store its own key; its identity comes from
  where it sits in the file.
- The manifest is explicit, generated output. Don't add templating, defaults, inheritance or
  variables.
- Identity is a value key (`ImageKey`, `PlatformKey`) with no references back to its parent.
  Build string keys with `KeyPatterns.Separator`, never a hardcoded separator.

## Naming

- Use a standard's term only when you actually follow the standard: OCI index and manifest,
  registry and repository. Otherwise use plain names. `Platform` is whatever is passed to
  `docker --platform`, not an imitation of the OCI platform object.
- Avoid ambiguous names and stutter, such as `platform.Platform` or a parameter named `owner`.
- In messages, refer to types with `nameof`, not hardcoded type names.

## Serialization

- JSON only, through System.Text.Json. Each serializer has its own `JsonOptions` property, which
  may be assigned from the shared defaults.
- Let System.Text.Json enforce the shape: required members, nullability, unknown members and
  duplicate keys. Don't duplicate those checks by hand.

## Validation

- Keep validation separate from deserialization.
- Check only rules the model itself relies on: key-part format, tag uniqueness per published repo
  name, and each index digest belonging to a manifest.
- Don't check what a downstream tool already checks (for example, Docker validates platform
  strings). Don't add speculative null, empty or format checks.
- Report every error in one go, not just the first one.
- Don't add a dependency just to support validation.

## Projections

`Models.V2.Projections` holds flattened views of the manifest for querying, such as `ImageEntry`
and `PlatformEntry`.

- An entry is a projection: one node plus its key and its ancestors, held by reference. Entries
  never copy, compute or resolve values, and they are created on demand.
- Entries are data-only records. All behavior goes in `ProjectionExtensions`.
- The dependency only goes one way: `Models.V2` never refers to `Projections`.
- If an entry seems to need a computed value, it's turning into a view model. Put the logic in an
  extension instead.
- Expose named properties, never `KeyValuePair`. Callers shouldn't need to know the model's
  internals, such as how an image reference is built or which tags apply to a platform. That
  knowledge belongs in an extension.

## Code style

- No complex expressions in a `foreach` header. Assign the query to a named local first.
- Break LINQ chains across lines.
- Don't nest calls in ways that hide intermediate results; use named locals.
- No local functions, and avoid deep nesting of loops.
- Use blank lines between logical blocks.
- Add comments only when the code needs clarifying.
