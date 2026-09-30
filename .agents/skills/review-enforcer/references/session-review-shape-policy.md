# Source Shape Review Policy

Open this when a review touches source layout, naming, partial types, XML
documentation, test documentation, or inline comments.

This file is the entry point. Load only the detailed policy needed for the
review scope:

- [source-layout-policy.md](source-layout-policy.md): file naming, folder
  placement, and partial type layout.
- [source-documentation-policy.md](source-documentation-policy.md): production
  XML documentation, test documentation, and inline comments.

Repository-specific design documents are still the source of truth when they
define stricter rules.

## When To Load Details

Load `source-layout-policy.md` when the diff includes:

- file renames
- new folders
- moved types
- partial type splits
- dotted file names
- design-doc paths that reference source layout

Load `source-documentation-policy.md` when the diff includes:

- public, protected, or internal API surface
- configuration schemas, DTOs, serialization models, or UI state
- test methods, test classes, fixtures, helpers, or test doubles
- normal comments immediately above types, members, `[Fact]`, or `[Theory]`

When the diff includes both layout and comments, load both detail files.

## Review Blocking Rules

Treat these as blocking findings:

- a hand-written `Type.Responsibility.cs` file remains where a type-owned folder
  should be used
- a hand-written `TypeResponsibility.cs` partial file remains where
  `Type/Responsibility.cs` should be used
- a design document names a file path that no longer matches the real source
  layout
- a public, protected, or internal API lacks required XML documentation
- a configuration schema or DTO property lacks required XML documentation
- a `[Fact]` or `[Theory]` does not have an XML summary immediately above it
- a test method still uses a normal comment as its primary "what this verifies"
  explanation
- a partial class responsibility file lacks a class-level XML summary

Treat these as non-blocking concerns unless the repository has a stricter local
rule:

- missing XML documentation on tiny private helpers
- missing comments on obvious arithmetic helpers
- wording that could be clearer but already preserves the correct contract
- inline comments that are slightly inconsistent but not misleading

Treat these as user-confirmation-required gaps:

- the repository has no authoritative coding standard and two plausible shapes
  conflict
- a file may be generated or framework-owned, but the reviewer cannot confirm it
  from the workspace
- enforcing the rule would require changing public API shape rather than only
  source layout or documentation

## Minimal Checklist

Before closing a review that touches source layout or comments, check:

- Dotted names are framework/toolchain owned only.
- Large partial types use `TypeName/Responsibility.cs`.
- Namespaces and public contracts did not change due to file moves.
- Public, protected, and internal API surface has XML summaries.
- Configuration schema and DTO properties have XML summaries.
- Important private boundary methods have XML summaries.
- Every `[Fact]` and `[Theory]` has an XML summary immediately above it.
- Normal comments are local implementation notes, not member contracts.
