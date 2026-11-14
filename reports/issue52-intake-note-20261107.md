# Issue #52 / ASPIRE-006G2 intake

## Scope and links

- Issue: [#52](https://github.com/ibis-ssl/Duck/issues/52)
- Parent Issue: #50
- Child PR base: `task/issue50-aspire-006g`
- Parent PR: [#118](https://github.com/ibis-ssl/Duck/pull/118), still Draft
- Dependency: #51, closed and merged into the parent branch at `1660cfb519f373c8263df09599a7e11f0a18e699`.
- Task size: small; one measurable `ASPIRE-MATCH-003` acceptance criterion.

## Acceptance criterion

Using real packets, establish that Crane and TIGERs both change position during the same valid active referee-command window. Preserve the environment, packet receive counts, sender identity, stdout/stderr and partial evidence. A skipped or failed run is not a pass.

## Work boundary

The current repository has no verifier that correlates both teams' observed robot positions with one active referee window. This branch will add a focused offline packet-evidence verifier and tests that use synthetic packet records, without adding dependencies or changing environment settings. Synthetic/unit evidence will not be described as the live acceptance result.

Docker execution is held until the runtime version used for the #43 acceptance is confirmed to match the approved version condition. No Docker command is part of intake or offline implementation.

## Starting identity

- Parent branch HEAD used for this intake: `1660cfb519f373c8263df09599a7e11f0a18e699`.
- Child branch: `task/issue52-aspire-006g2`.
- Worktree: `C:\Users\donabe\Documents\Codex\2026-11-07\task\Duck-issue52-aspire-006g2`.
