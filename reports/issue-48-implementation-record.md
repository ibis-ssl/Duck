# Issue #48 implementation record

Scope: ASPIRE-006F5, Crane and Duck match settings. Parent: #43; integration target: `design/issue18-aspire-test-orchestration`.

Accepted completion criteria:
- Model test pins Crane `team:=ibis`.
- Match mode has no `cm4-sim` dependency.
- Duck does not publish UDP 11010.
- Required `WaitForStart` relationship is asserted by the model test.

Dependencies #45 and #46 are closed. This task is tracked on dedicated branch `task/issue48-aspire-006f5`; its Draft PR #110 is formally linked to Issue #48 and Project 16.

The match-mode Crane default is set to `ibis`; other modes retain the `crane` default. The existing AppHost model test now asserts `team:=ibis`, the absence of `cm4-sim` and its dependency, no Duck UDP endpoint at 11010, Duck's `WaitForStart(simulator)`, and Crane's `WaitForStart(duck)` plus `WaitFor(game-controller)` readiness ordering.

Validation: the focused model test failed before the fix on `team:=crane` versus expected `team:=ibis`, then passed after the fix (1/1, 119 ms). The same match-mode test plus `CraneLaunchUsesCraneTeamNameAndSeparateYellowColor` passed together (2/2, 125 ms), confirming the other mode default remains unchanged. Evidence is under `reports/evidence/issue48-red-20261103/` and `reports/evidence/issue48-green-20261103/`.
