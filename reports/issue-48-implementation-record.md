# Issue #48 implementation record

Scope: ASPIRE-006F5, Crane and Duck match settings. Parent: #43; integration target: `design/issue18-aspire-test-orchestration`.

Accepted completion criteria:
- Model test pins Crane `team:=ibis`.
- Match mode has no `cm4-sim` dependency.
- Duck does not publish UDP 11010.
- Required `WaitForStart` relationship is asserted by the model test.

Dependencies #45 and #46 are closed. This task is tracked on dedicated branch `task/issue48-aspire-006f5`; no implementation or test changes are included in this initial record.

Validation will use the narrowest existing AppHost model test(s) covering the listed criteria, followed by the repository's required review and exact-head CI workflow.