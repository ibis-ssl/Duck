# Issue #48 implementation record

Scope: ASPIRE-006F5, Crane and Duck match settings. Parent: #43; integration target: `design/issue18-aspire-test-orchestration`.

Accepted completion criteria:
- Model test pins Crane `team:=ibis`.
- Match mode has no `cm4-sim` dependency.
- Duck does not publish UDP 11010.
- Required `WaitForStart` relationship is asserted by the model test.

Dependencies #45 and #46 are closed. This task is tracked on dedicated branch `task/issue48-aspire-006f5`; its Draft PR #110 is formally linked to Issue #48 and Project 16.

The match-mode Crane default is set to `ibis`; other modes retain the `crane` default. Match mode explicitly sets Duck `Tracker__PublishUdp=false`; the AppHost model test asserts the effective environment setting, no UDP endpoint at 11010, the absence of `cm4-sim` and its dependency, Duck's `WaitForStart(simulator)`, and Crane's `WaitForStart(duck)` plus `WaitFor(game-controller)` readiness ordering. Match mode also passes no `CraneTargetAddressSettings`, so the registration uses the native Crane launch command without the target-address environment, `CraneLaunchOverlay.py` mount, or overlay command that would resolve `cm4-sim`. Base and comparison continue to use the existing target-address resolver and overlay.

Validation: the match model test failed before the team fix on `team:=crane` versus expected `team:=ibis`, then passed after it (1/1, 119 ms). A normal-review finding exposed that a missing host-published UDP endpoint did not suppress the outbound publisher. The corrected focused model test failed on the missing `Tracker__PublishUdp` override, then the match and comparison tests passed after the match-only suppression (2/2, 134 ms); this also checks comparison keeps the existing default configuration. An independent review then found the default match target-address overlay still selected `cm4-sim`. A new model assertion failed on the unexpected `DUCK_CRANE_TARGET_ADDRESS`; after the match-only target-settings guard, the match model passed 1/1 (115 ms) and the comparison model passed 1/1 (117 ms). Normal reviewer rechecked the target change and passed with no further finding. Fix commit `7038f2d6cf8d1e4507ca3a001631837277154abe` is pushed; exact-head CI run `41640384043` is pending and packet-flow is skipped. Earlier exact-head run `41638797927` succeeded at `acb4b358fe6d65985a431d2a8c3159e483a44905` (.NET tests, desktop acceptance policy and Linux publish validation); packet-flow and full-stack acceptance were skipped. Runtime integration remains unverified. Evidence is under `reports/evidence/issue48-red-20261103/`, `reports/evidence/issue48-green-20261103/`, and `reports/evidence/issue48-red-reviewfix2-20261103/` / `reports/evidence/issue48-green-reviewfix-20261103/`.
