# Issue #43: TIGERs versus Crane normal review

## Scope and identity

Normal review of the #43 aggregate 006F match implementation and relevant composition dependencies in Duck PR #28. This is a bounded match-slice review, not an exhaustive PR-wide review.

- Reviewed HEAD: `c8daa93a44d8e9b37e62a41457259f6e3057063c`
- Base: `main` (`c88bb49a9b16903aafca7c5c809ba35afea5f1e9`)
- Merge base: `449296725fc69dc004818ede2e8ad59a52ef2d27`
- Scoped range: `ee1eb7c4a024134ddf2fadf8f029528ee6d609f8..c8daa93a44d8e9b37e62a41457259f6e3057063c`
- Worktree: detached `C:\wpr28review`; clean before and after review.
- Reviewer: separate normal-review agent; requested profile `gpt-6.1-sol/high`; exact applied profile unverified.

## Requirement and design coverage

- 006F1–F2 fixtures/configuration and match resource topology: checked, no required finding. Blue TIGERs Mannheim, Yellow ibis, FRIENDLY, initial STOP; match mode excludes comparison-only resources.
- 006F3–F5 simulator, Game Controller, TIGERs/AutoRef/recorder, Crane/Duck configuration and mounts: checked, no required finding in the inspected paths.
- 006F6 match lifecycle and edge cases: checked, no required finding in the inspected paths. Readiness, HALT/STOP/PREPARE/NORMAL_START, continuation, half progression, maximum-time failure, POST_GAME, and incomplete final evidence are represented in production and focused tests.
- Shared control/config transport: one held concern, NR43-001 below.
- Composition dependencies, positive participant evidence, placement identity, recorder freshness, atomic result publication, failure diagnostics, image references, mounts, labels and control targeting: checked in inspected paths; no other required finding identified.
- PR-wide implementation, legacy reports/evidence, unrelated comparison/base/native/tooling changes: unexplored. PR #28 contains about 6,836 changed files; this report does not attest to the full PR.

## Finding and user-policy disposition

NR43-001 · Medium · Control/config WebSocket messages have no size limit.

Location: `Testing/Duck.Testing.RefereeDriver/RefereeDriverNetworkAdapters.cs:237–251`. `ReceiveTextMessageAsync` appends fragments to `MemoryStream` without an accumulated-byte limit and decodes the entire buffer. It is used by autoContinue/config and control paths. The direct source fact is the missing bound; memory pressure from unusually large or continuing output is a hypothesis. No observed occurrence or frequency evidence was found. The explicit 006F1–F6 criteria reviewed do not require a 1 MiB limit on every control/config reader; the 1 MiB limit is implemented by the separate state reader.

Under the user's stated threshold (“fix review findings at about one occurrence per month across 1,000 users; otherwise record and leave”), the finding is held/non-blocking. The evidence does not establish that threshold, but also does not establish a lower frequency. No fix is required now. A realistic normal-match reproduction, frequency evidence from incidents, or an explicit applicable acceptance requirement could change this disposition.

No severity reclassification was made; the recorded Medium severity is preserved while required status is held under the user's policy.

## Validation and runtime evidence

- PR #28 exact-head CI run `42230112644`: .NET tests SUCCESS; `ASPIRE-NET-002 Linux packet flow` SKIPPED; `ASPIRE full-stack Linux acceptance` SKIPPED.
- These skipped checks are not passes. The reviewed #43 criterion is aggregate 006F1–F6 completion/design fixation; 006G and 007A/B/C are separate tracker tasks.
- PR #116's committed report describes an actual match reaching POST_GAME, passed persisted result, final readiness, nine placements, recorder CRC/EOF and cleanup. These are reported results read from the report; raw runtime evidence was not independently reproduced.
- That report retains Windows full-project validation at 1296 passed, 1 failed, 2 not run. The failure is unresolved, and its isolated passing rerun does not erase the original failure. No basis was found to assign it to changed 006F paths.
- No tests were run during this review.

## Verdict

`pass_with_held` for the scoped 006F normal review. No required finding remains under the user's frequency policy. This is not an independent-final-review verdict, a PR-wide review, or merge approval. PR #28 remains OPEN.

## Next action

Proceed to the required independent review of the frozen candidate HEAD through review-enforcer. Keep skipped acceptance unrun and the broader unexplored PR scope explicit.