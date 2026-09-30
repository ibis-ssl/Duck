# Source Documentation Policy

Open this when a review must check XML documentation, test documentation, or
inline comments.

This reference defines documentation rules the reviewer should apply. It does
not replace repository-specific design documents; when a design document gives a
stricter rule, use the design document as the source of truth.

## Production XML Documentation Rules

Write XML summaries for public, protected, and internal API surface.

This includes:

- classes
- records
- interfaces
- enums
- public/protected/internal properties
- public/protected/internal methods
- configuration schema properties
- DTO properties
- UI state properties
- serialization schema properties

Private methods usually do not need XML documentation.

Add XML documentation to private methods when they are a boundary or ordering
point that future edits can easily break, such as:

- dispatch ordering
- flush ordering
- profile switch application
- diagnostics schema construction
- capture file or replay schema handling
- schema conversion
- Kalman or identity-assignment boundaries

A good summary should describe the contract a maintainer must preserve, not only
repeat the method name.

Prefer:

```csharp
/// <summary>
/// Applies the active profile only after the engine emits ProfileSwitched, then keeps publisher and snapshot state aligned.
/// </summary>
```

Avoid:

```csharp
/// <summary>
/// Handles profile switching.
/// </summary>
```

When a property represents an external setting, include the operational meaning
of null, zero, empty string, units, or default values when that meaning matters.

## Test Documentation Rules

Every `[Fact]` and `[Theory]` test method should have an XML summary immediately
above the attribute.

Use this shape:

```csharp
/// <summary>
/// Verifies that a profile switch request resolves the named profile and applies it through the coordinator.
/// </summary>
[Fact]
public void RequestProfileSwitch_ResolvesNamedProfileAndAppliesItThroughCoordinator()
```

The summary should say what behavior the test proves. It should not merely
repeat the method name with spaces.

Test classes should have XML summaries when they group a meaningful contract or
behavior area.

Shared fixtures, helper classes, and test doubles should have XML summaries when
multiple tests depend on them.

Do not use a normal comment as the primary explanation for a test method.

Avoid:

```csharp
// Verifies that missing active profiles fail with the profile name.
[Fact]
public void Resolve_WithMissingActiveProfile_Throws()
```

Prefer:

```csharp
/// <summary>
/// Verifies that resolving a missing active profile fails with the missing profile name.
/// </summary>
[Fact]
public void Resolve_WithMissingActiveProfile_Throws()
```

Normal comments inside a test method are acceptable for assertion groups, test
data setup, or local reasoning that is not the method's main contract.

## Inline Comment Rules

Use XML documentation for type, member, and test-method contracts.

Use `//` comments only for local explanations inside a method body.

Good uses of `//`:

- explaining why an order must be preserved
- marking assertion groups
- clarifying non-obvious test data
- documenting an invariant directly next to the code that relies on it

Avoid comments that merely restate the code.

Avoid comments that promise future behavior not implemented by the current code.

If a normal comment sits directly above a type, property, method, `[Fact]`, or
`[Theory]`, convert it to XML documentation unless it is intentionally a local
implementation note.
