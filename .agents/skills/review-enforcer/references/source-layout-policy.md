# Source Layout Policy

Open this when a review must check file naming, folder placement, partial type
layout, or design-doc path consistency.

This reference defines source layout rules the reviewer should apply. It does
not replace repository-specific design documents; when a design document gives a
stricter rule, use the design document as the source of truth.

## File Naming Rules

Allow dotted file names only when the framework or toolchain owns that shape.

Allowed examples:

- `.csproj`
- `.sln`
- `.razor.cs`
- `.razor.css`
- `.g.cs`
- `.Designer.cs`
- `.AssemblyInfo.cs`

Do not use dotted names as hand-written responsibility markers.

Avoid:

- `TrackerEngine.FrameCommit.cs`
- `TrackerEngine.BallTracking.cs`
- `TypeName.Responsibility.cs`

Prefer:

- `TrackerEngine/FrameCommit.cs`
- `TrackerEngine/BallTracking.cs`
- `TypeName/Responsibility.cs`

Do not pack the type name and responsibility into one hand-written file name
when a type-owned folder can express the same structure.

Avoid:

- `TrackerCoordinatorDiagnostics.cs`
- `TrackerCoordinatorDispatch.cs`

Prefer:

- `TrackerCoordinator/Diagnostics.cs`
- `TrackerCoordinator/Dispatch.cs`

## Folder Placement Rules

Use one top-level type per file as the default.

Multiple top-level types may share one file only when they are read and changed
as one unit, such as:

- parent/child DTOs
- a small enum or support type that is not useful alone
- one external configuration schema group
- one serialization schema group

Use type-owned folders for large partial types.

The folder name owns the type identity. The file name owns the responsibility.

Preferred shape:

```text
TrackerEngine/
  TrackerEngine.cs
  FrameCommit.cs
  BallTracking.cs
  RobotTracking.cs
  Geometry.cs
  Settings.cs
```

Do not change namespaces or public contracts just because files move into a
folder.

Keep meaningful domain folders such as `Configuration`, `Model`, and `Proto`
when they describe the source boundary better than a type-owned folder.

## Partial Type Rules

Use `partial` only when splitting one large type makes the type easier to read by
responsibility.

The main file should contain:

- fields
- constructors
- top-level orchestration methods
- the public or internal entry points that define the type's main role

Responsibility files should contain one coherent behavior area.

Examples:

- `FrameCommit.cs`
- `ProfileSwitch.cs`
- `Diagnostics.cs`
- `Dispatch.cs`

Every hand-written partial class declaration should have an XML summary that
states the responsibility of that file.

Example:

```csharp
/// <summary>
/// Coordinates engine event dispatch, snapshot updates, packet publishing, and observer notification order.
/// </summary>
public sealed partial class TrackerCoordinator
```
