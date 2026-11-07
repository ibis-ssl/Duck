# Issue #52: normal review and fix verification

## Review identity

- PR: #119; base: `task/issue50-aspire-006g` at `1660cfb519f373c8263df09599a7e11f0a18e699`
- Final reviewed HEAD: `ff1ce32b8d9245f3ee0a46b575d035f785839e56`
- Reviewer: `/root/issue52_normal_review`
- Requested profile: `gpt-6.1-sol/high` (spawn metadata); reviewer runtime could not independently introspect model/effort.
- Verdict: **pass_with_held** for the offline verifier source scope.

## Findings

The initial review at `a597c981967cd9347360699911f62b080ded4f7c` found three P2/medium issues. All are closed at the final HEAD without severity changes:

- **I52-NR-001:** contiguous referee intervals plus uint32 serial counter ordering prevent delayed/regressed command packets from reopening an old motion window. Equal-counter packets continue only the same live interval; forward wraparound is handled.
- **I52-NR-002:** duplicate robot IDs are excluded within a frame, so spatially ambiguous detections cannot count as temporal motion.
- **I52-NR-003:** decoded window summaries and exact parse errors are retained after partial headers, partial payloads, or invalid record lengths, while status remains `incomplete`.

The review also confirmed the restored separate-window regression fixture. The initial false-positive sequences were reproduced before fixes; forward/reverse wraparound, equal-counter handling, malformed counters, and newer-counter recovery were checked after fixes. No new required finding was reported.

## Validation evidence

- At `658bee763d2d77dd99ec4b0ecf11e3f59d9e95ad`, the reviewer ran **11/11 committed non-writing tests** and nine schema-complete composed probes; the owner separately ran the complete suite **12/12 passed** at that head. The reviewer excluded the CLI test because it writes output.
- At final source HEAD `ff1ce32b8d9245f3ee0a46b575d035f785839e56`, the reviewer ran **12/12 committed non-writing tests**, uint32 bound checks, and two composed malformed-reset/newer-counter-recovery probes. The owner ran the complete committed suite, including the CLI case, **13/13 passed** at that head.
- Both Python sources compiled; `git diff --check` passed.
- Exact-head CI [42380286902](https://github.com/ibis-ssl/Duck/actions/runs/42380286902) succeeded: standard test job `128426282750` passed; NET-002 and full-stack were skipped. CI did not execute the Python motion tests.
- The final source and worktree were clean at review.

## Held items

This review covers the offline verifier only. Live `ASPIRE-MATCH-003`, Docker version equality with #43, current-run network/source identity, independent final review, and parent-branch integration remain incomplete. The historical #49 archive is not current acceptance evidence.
