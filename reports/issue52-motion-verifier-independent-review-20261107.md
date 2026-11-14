# Issue #52 / PR #119 independent final review

## Verdict and immutable identity

- Review mode: `independent_final_review`.
- Verdict: **fail** for the offline verifier source scope; one required P2/medium finding, I52-IFR-001.
- Reviewed implementation HEAD: `81b79124ec3a5e9702e8c63b7c30c8adb99f7910`.
- Base: `task/issue50-aspire-006g` at `1660cfb519f373c8263df09599a7e11f0a18e699`.
- Range: `1660cfb519f373c8263df09599a7e11f0a18e699..81b79124ec3a5e9702e8c63b7c30c8adb99f7910` (10 commits, six changed files, 704 added lines).
- Reviewer: `/root/issue52_independent_final_review`; this reviewer did not implement the change or fixes and did not serve as normal reviewer. The complete candidate diff was inspected independently before relying on normal-review conclusions.
- Requested profile: `gpt-6.1-sol/high`, as supplied by the caller's task/spawn declaration. Actual applied model/effort cannot be independently introspected in this reviewer runtime; the requested profile is not an applied-profile attestation.
- This review does not establish live `ASPIRE-MATCH-003` acceptance or completion of Issue #52.

## Required finding

### I52-IFR-001 — malformed vision wrappers can produce a passing motion verdict

- Severity: **P2 / medium**.
- Origin: this independent final review, at the reviewed implementation HEAD above.
- Location: `scripts/verify_aspire_motion_acceptance.py:113–116`, especially the early `break` at line 116; positive evidence is subsequently counted at lines 273–285.
- Description: `_vision_positions` stops consuming `_protobuf_fields(payload)` immediately after finding the first detection field. It therefore never validates the remainder of the outer protobuf wrapper. An otherwise useful detection prefix followed by malformed protobuf bytes is accepted as a valid vision packet and can establish motion.
- Impact: a complete, checksum-valid SSL archive containing no valid vision wrapper can receive `status: passed`, `validVision: 2`, and `sameWindowMotionObserved: true`. Invalid packet payloads must not become positive packet acceptance evidence.
- Evidence: using the committed fixture helpers, build one NORMAL_START/counter-7 referee record, then two vision records whose Yellow/Blue robot-0 positions change from `(0,0)/(1,0)` to `(0.2,0)/(1.2,0)`. Append `b'\xff'` to each vision payload before packaging it into a complete gzip SSL log. Fully consuming `_protobuf_fields` rejects both payloads with `Malformed protobuf varint.`; `assess_archive_bytes` nevertheless returns `passed`, two valid vision packets, one successful NORMAL_START window, and both movement flags true.
- Sibling probes at the same immutable HEAD also falsely passed when each detection prefix was followed by a truncated fixed32 field (`b'\x7d\x00'`), a truncated length-delimited field (`b'\x7a\x05\x00'`), an invalid wire type (`b'\x7f'`), or field number zero (`b'\x00'`). The corresponding full-stream parser rejected each suffix. A well-formed unknown varint field (`b'\x78\x01'`) remained accepted, as expected.
- Required action: consume and validate the entire outer wrapper before returning detection evidence. Add regression coverage for malformed data after a detection field, including the sibling truncation/wire-type cases, while retaining support for well-formed unknown fields. Invalid wrappers must not increment `validVision` or establish robot motion. Run the committed motion suite and normal fix verification, then obtain a fresh independent review at the new frozen HEAD.

Compact reproduction, with `t` representing the committed test module and `m` the reviewed verifier:

```python
records = [t._record(3, 0, t._referee_packet(2, 7))]
for second, x in [(1, 0.0), (2, 0.2)]:
    payload = t._vision_packet(
        [t._robot(x, 0.0)], [t._robot(1.0 + x, 0.0)]
    ) + b'\xff'
    records.append(t._record(4, second, payload))
result = m.assess_archive_bytes(t._archive(records))
# Actual: status == 'passed'; packetCounts.validVision == 2.
# Expected: malformed wrappers provide no positive motion evidence.
```

No fixes were made by this reviewer. The sibling probes loaded implementation and fixture-helper source through `git show` at the full reviewed SHA and executed entirely in memory.

## Coverage dispositions

| Required criterion | Disposition | Evidence / assessment |
| --- | --- | --- |
| Requirements and design | `checked_finding` | Read Issue #52, ASPIRE-006G2 task row, ASPIRE-MATCH-003 acceptance row, and current match-start design. The offline source does not yet reliably exclude malformed vision packets (I52-IFR-001); live acceptance is held. |
| Entire diff and changed files | `checked_no_finding` | Inspected all six changed files: verifier, tests, intake, implementation report, normal-review report, and phases tracking. No unrelated product changes or dependencies were added. |
| Correctness and edge cases | `checked_finding` | Counter ordering, wraparound, separate intervals, STOP/malformed reset, duplicate IDs, finite coordinates, and partial records were inspected; malformed outer-wrapper suffixes reproduced I52-IFR-001. |
| Direct dependencies and compatibility | `checked_no_finding` | Compared existing SSL v1 reader/tests and protobuf project integration. Record type/header/endianness contracts agree. This standalone Python tool changes no existing API, configuration, or workflow. |
| Error handling and diagnostics | `checked_finding` | Record-tail/gzip failures remain non-passing and preserve decoded counts/summaries where available. Outer-wrapper payload errors can be skipped by the early return path (I52-IFR-001). |
| Security and secrets | `checked_no_finding` | No new network endpoint, credentials, subprocess invocation, or external dependency; archive is caller-selected local input. No secret handling change found. |
| Tests and validation adequacy | `checked_finding` | Twelve non-writing committed tests passed independently. Existing cases omit malformed wrapper tails; the added probes establish the missing regression class. The CLI test was inspected but not executed because this reviewer was restricted from writing test outputs. |
| Current-HEAD CI | `checked_no_finding` | Exact-candidate-associated PR run 42380852132 succeeded, with the checkout and skips detailed below. It does not execute this new motion suite. |
| Reports, tracking, documentation | `checked_no_finding` | Committed reports accurately distinguish ff1ce32 source validation, historical #49 parser evidence, and held current live acceptance. PR body still contains older 5/5/a597c98 validation prose; caller owns its update. No historical evidence was promoted to current acceptance. |
| Regression and maintainability | `checked_finding` | Source is focused and dependency-free; the wrapper generator must be fully exhausted to maintain the parser's validity contract (I52-IFR-001). |
| Live ASPIRE-MATCH-003 and Docker version gate | `held` | No Docker or live run performed. The caller owns establishing primary #43 Docker-version evidence before proceeding. |
| Existing runtime/API/product changes | `not_applicable` | No such files changed. |

No required source-review area remains unexplored. Live behavior and source endpoints remain explicitly unverified, not passed.

## Validation and exact evidence

- Implementation blob: `1e3abbac0e90f7d2f72165159837a843bd5a5309`.
- Committed test blob: `a05670a7743ffc75461376af59f30bbee896eadf`.
- The verifier and tests are unchanged between `ff1ce32b8d9245f3ee0a46b575d035f785839e56` and reviewed HEAD; intervening changes are reports/tracking. This preserves provenance of the owner's recorded 13/13 source-suite result at ff1ce32 without describing it as an independently executed full suite at 81b7912.
- Reviewer independently ran **12/12 non-writing committed tests**, all passed, with `python -B`. The CLI output-writing test was excluded under the review boundary. No test file or expectation was changed.
- Reviewer independently reproduced I52-IFR-001 and five malformed suffix classes entirely in memory. The well-formed unknown-field control passed.
- `git diff --check` passed for the entire immutable base-to-candidate range.
- GitHub Actions run [42380852132](https://github.com/ibis-ssl/Duck/actions/runs/42380852132) completed successfully for the candidate. Job `128427956431` (`test`) succeeded; logs show checkout of GitHub's PR test merge `b8ca686` merging reviewed HEAD into the specified base. The test log reports **1,307 passed, zero failed, four skipped, 1,311 total**. This is exact-candidate-associated CI on the PR test merge, not a claim that Actions checked out the branch SHA directly.
- `ASPIRE-NET-002 Linux packet flow` job `128427957819` and `ASPIRE full-stack Linux acceptance` job `128428566632` were **skipped**. These skips provide no acceptance result. Workflow inspection and logs confirm the Python motion tests were not executed by this run.
- The historical #49 archive smoke result is reported evidence only; this reviewer did not rerun it or treat it as current #52 acceptance.

## Normal-review continuity

The committed normal-review report closes I52-NR-001, I52-NR-002, and I52-NR-003 at `ff1ce32b8d9245f3ee0a46b575d035f785839e56`, each **P2 / medium**. This independent pass inspected the unchanged source and the committed regressions for stale counters/interval separation, duplicate IDs within a frame, and partial-record summary preservation. No severity reclassification is made. I52-IFR-001 is a new finding in a separate outer-wrapper validation path.

## Held items, ownership, and next action

The caller retains ownership of primary evidence that the current Docker version matches the approved #43 runtime, then the authorized current-run live ASPIRE-MATCH-003 execution and its OS/Docker/network details, receive counts, sender identity, stdout/stderr, and failure/partial evidence. SSL log v1 does not contain source endpoints; this report does not establish those endpoints. Issue #52, PR #119 acceptance, and child-to-parent integration remain incomplete. Main-branch merge is outside this task's authorization.

First fix I52-IFR-001 and obtain normal fix verification plus a fresh independent final review at the replacement immutable HEAD. The present verdict does not transfer to that replacement target. Keep live acceptance held until the version condition is established from primary evidence.

## Report persistence boundary

- Reserved path: `reports/issue52-motion-verifier-independent-review-20261107.md` (supplied by the caller as the reserved report path).
- `reviewed_implementation_head`: `81b79124ec3a5e9702e8c63b7c30c8adb99f7910`.
- `report_attestation_head`: `null`.
- `report_attestation_allowed`: **false** for completion at this failed candidate. Required implementation fixes invalidate this candidate's completion identity and require a new review.
- Before accepting an administrative attestation for a later passing review, the caller must verify: the path was reserved before freeze; exactly one commit follows that review's implementation HEAD; its first parent is that HEAD; its diff changes only the reserved report path(s); it states the exact reviewed HEAD and administrative nature; no executable, Skill, design, workflow, configuration, tracking, handoff, or product file changes; and no later commit exists. Record the validated implementation/attestation identity pair.

This reviewer wrote only this reserved report. No Docker command, fix, merge, CI invocation, PR/Issue mutation, or link change was performed.
