# #114 Positive readiness for match participants

Parent: #43. This follow-up implements the existing match-readiness contract for Sumatra (`tigers-blue`), AutoRef (`autoref-tigers`), and `ssl-log-recorder`.

The match controller must receive fresh, positive, resource-specific readiness evidence tied to the resource instance, full container ID, and run ID. Running alone, or null/unknown health, is not readiness. The current fail-closed predicate is in PR #113; this issue establishes the missing producer-side signals and their real-container verification.

The accepted design at `aspire-simulation-test-environment.md` lines 42 and 237 requires checking participant readiness and implementing required receive/decode diagnostics in the corresponding container when no verification endpoint exists. Existing pinned images/launch arguments and bridge/ports remain the baseline.

Completion requires focused tests plus positive and negative checks against the actual target containers on the actual configured network. Mock-only notifications or synthetic evidence cannot establish completion. This work covers per-service readiness only; it does not run or replace #50's broader live match/ASPIRE-MATCH packet acceptance.
