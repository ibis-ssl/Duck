# Issue #48 implementation record

Scope: ASPIRE-006F5, Crane and Duck match settings. Parent: #43; integration target: `design/issue18-aspire-test-orchestration`.

Accepted completion criteria:
- Model test pins Crane `team:=ibis`.
- Match mode has no `cm4-sim` dependency.
- Duck does not publish UDP 11010.
- Required `WaitForStart` relationship is asserted by the model test.

Dependencies #45 and #46 are closed. This task is tracked on dedicated branch `task/issue48-aspire-006f5`; its Draft PR #110 is formally linked to Issue #48 and Project 16.

The match-mode Crane default is set to `ibis`; other modes retain the `crane` default. Match mode explicitly sets Duck `Tracker__PublishUdp=false`; the AppHost model test asserts the effective environment setting, no UDP endpoint at 11010, the absence of `cm4-sim` and its dependency, Duck's `WaitForStart(simulator)`, and Crane's `WaitForStart(duck)` plus `WaitFor(game-controller)` readiness ordering.

Validation: the match model test failed before the team fix on `team:=crane` versus expected `team:=ibis`, then passed after it (1/1, 119 ms). A normal-review finding exposed that a missing host-published UDP endpoint did not suppress the outbound publisher. The corrected focused model test failed on the missing `Tracker__PublishUdp` override, then the match and comparison tests passed after the match-only suppression (2/2, 134 ms); this also checks comparison keeps the existing default configuration. Initial exact-head CI run `41637729431` succeeded at the pre-review-fix head `d2a9d28b961adebb9d752662dc25f8fd122118ef`; the review-fix commit still requires matching CI. Evidence is under `reports/evidence/issue48-red-20261103/`, `reports/evidence/issue48-green-20261103/`, and `reports/evidence/issue48-red-reviewfix2-20261103/` / `reports/evidence/issue48-green-reviewfix-20261103/`.
