# Issue #49: normal match continuation

## Scope and changes

This follow-up completes the existing normal match path in PR #116. Its integration target is the design branch. It does not merge parent #43 or PR #28, change Crane, or add a new acceptance requirement.

- Match mode mounts Sumatra's supported user configuration read-only, enabling Blue control and disabling Yellow control. Comparison/base configuration is unchanged.
- The existing bounded WebSocket reader keeps fragment assembly and cancellation. Its default logical-message budget is now 1 MiB so ordinary Game Controller history does not exceed the old 4 KiB budget.
- The ball fixture recognizes the captured kickoff preparation and the two normal PRE-to-active-half transitions, retaining fresh packet/counter, stage, score, team, control-context and simulator-identity checks.
- A newer matched official placement may supersede an earlier unconfirmed episode. The old episode is recorded as superseded, never confirmed; stale/mismatched requests and duplicate sends remain rejected.
- Official placement supports the neutral restart selected by Game Controller as well as team DIRECT. This sends only the ball-placement command and confirms the subsequent native restart; it does not inject FORCE_START or bypass the initial kickoff.
- Windows participant-status publication preserves atomic replacement and retries only transient replacement failures for at most 400 ms. Persistent denial still fails and cancellation remains effective. ACL, OS and network settings are unchanged.

## Verification and truthful history

The original Blue-only model change did not have a separately captured prior Red. That process gap is retained.

| Observed defect | Prior failing regression | Green evidence |
| --- | --- | --- |
| Bounded GC messages and normal half progression | 4 failed, 5 passed | Related classes 42/42 |
| Later official placement blocked by an earlier pending episode | 1 failed, 6 passed | Related classes 49/49 |
| GC-selected neutral restart placement rejected | 1 failed, 2 passed | Related classes 52/52 |
| Brief Windows reader lock terminates status publication | 1 failed, 2 passed | All three affected classes 67/67 |

All listed test commands reached a recorded exit state. The final focused suite and AppHost Release build exited 0. Windows sharing cases use real files and are explicitly skipped on other platforms. The final Host publish and the rebuilt AppHost were bound to the exercised source fingerprints.

The recorded Red/Green executions preceded the history-preservation commits and ran against recorded working-tree source fingerprints. The test-first history includes the behavior-preserving status-write test seam; it is not described as an earlier commit execution or backdated evidence.

## Real match outcome

The approved fixed-image normal Docker match reached POST_GAME and persisted a passed result. Final participant, API/referee and vision readiness were complete. Nine official placements were confirmed, including GC-selected neutral restarts. The unchanged Crane image and match-only Blue control setting were checked against the actual runtime configuration. The fixture did not inject a forced kickoff start.

The trial watchdog checked each team's initial observed movement once and did not rearm during legal waiting. Data freshness, monitoring loss, process and whole-run limits remained active. Recorder received SIGINT, exited 0, and its archive passed full gzip CRC/EOF validation. Cleanup left no owned containers or networks. Earlier failed/corrupt trial evidence was not used as this success evidence.

This establishes the normal match outcome, not the original delayed-return run's complete causal reconstruction or every Linux/comparison acceptance item under parent #43. Initial-teleport acknowledgement is not inferred from a UDP send or made a new requirement. Private telemetry, individual times/coordinates, local paths, container identifiers and rejected-comment payloads are withheld.

## Review and remaining publication gates

The same normal reviewer passed the bounded source fixes with no required findings. Requested profile was gpt-6.1-sol/high; the exact applied profile is unverified. Source review and real runtime evidence remain distinct.

Final committed-candidate validation, same independent reviewer closure, exact published-HEAD required CI, current base/conflict/protection checks and permitted child integration remain to be recorded. No PR approval action or parent merge is implied.

Skill reflection: no skill change is needed for these project-specific runtime corrections. The workflow limitations and initial Blue-only prior-Red gap are retained rather than retroactively claimed complete.
