# Issue #46 intake note

## Task and scope

- Issue: `ibis-ssl/Duck#46` `[ASPIRE-006F3] 対戦用Simulator・Game Controllerを構成する` (S, OPEN, unassigned), Project `Duck` / Backlog.
- Parent #43 is OPEN / In progress; grandparent #18 remains OPEN. Dependencies #44 and #45 are both CLOSED / Done and their PRs #106/#107 are merged into the target branch.
- Accepted criteria: fix the match Simulator arguments, the sole producer for UDP 11003, Game Controller API, fixture mount, and fixed image reference in an application-model test.
- Existing task row `ASPIRE-006F3` is present in `Tracker/Design/tasks-status.md` and lists dependencies #44/#45. The parent phase tracks #43 as a child issue. No new issue or task split is needed for this S-sized scope.
- Non-goals: TIGERs/AutoRef/SSL recorder (#47), Crane/Duck wiring (#48), match-controller lifecycle (#49), live Docker/network acceptance, and closing #43/#18.

## Environment and target

- Existing Windows checkout only; no clone/worktree. Starting tracked tree is clean; preserved untracked reports/evidence are unrelated and will not be staged.
- Verified `origin/design/issue18-aspire-test-orchestration` HEAD: `9f1511751a1f585e0df8d069893eb8c0c24883f0`; PR #106 and #107 use this base. PR #28 is a separate open/dirty design-to-main PR, not the parent merge target.
- Dedicated branch: `task/issue46-aspire-006f3`, based on the verified target branch HEAD. No pre-existing PR or issue comments were found. Parent/grandparent closing references will not be added.

## Design and validation plan

- Governing design: `Tracker/Design/Testing/aspire-simulation-test-environment.md`; resource model requirements are also recorded in the existing `ASPIRE-006F3` task row. Check both against the new assertions before editing; update design only if the implementation exposes a missing accepted contract.
- Issue #46 explicitly requires a focused model test to fail before implementation. Implement one narrow model-test slice covering all five acceptance bullets, preserve real Red diagnostics, then implement only the missing resource-model behavior and fixture mounting/reference. Retain Green evidence and diagnostics.
- Bound validation to the focused Tracker model test and the relevant AppHost build/model path; use the required repo validation policy and keep full-stack Docker/network acceptance unclaimed.
- Requested implementation profile: `gpt-6-luna/high`; normal and independent review: `gpt-6.1-sol/high`. Report exact applied profiles only if observable; otherwise mark unverified.
- No merge, approval, deployment, broad unrelated test loop, or sibling Issue work.

## Current state

This note is the initial pre-implementation traceability commit so the authorized Draft PR can be created before the Red/Green slice. No product/test implementation has started.
