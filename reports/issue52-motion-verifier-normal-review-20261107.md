# Issue #52: normal review and fix verification

## Review identity

- PR: #119; base: `task/issue50-aspire-006g` at `1660cfb519f373c8263df09599a7e11f0a18e699`
- Latest reviewed source HEAD: `a95ad91c8e45718c56219110063739a111301034`
- Reviewer: `/root/issue52_normal_review`
- Requested profile: `gpt-6.1-sol/high` (spawn metadata); runtime could not independently introspect model/effort.
- Verdict at latest source HEAD: **pass_with_held**.

## Findings and closure

Initial normal review at `a597c981967cd9347360699911f62b080ded4f7c` found three P2/medium issues; all were closed without severity change:

- **I52-NR-001:** contiguous referee intervals and uint32 serial counter ordering prevent delayed/regressed commands from reopening old motion windows. Equal counters can only continue the same live interval; forward wraparound is accepted.
- **I52-NR-002:** duplicate robot IDs are excluded within a frame, preventing ambiguous detections from implying temporal motion.
- **I52-NR-003:** partial header/payload and invalid record length preserve decoded summaries and exact errors while status remains `incomplete`.

The independent review at `81b79124ec3a5e9702e8c63b7c30c8adb99f7910` found **I52-IFR-001 (P2/medium)**: the Vision wrapper parser stopped after the first detection field, allowing malformed trailing protobuf bytes to count as valid movement. At `a95ad91c8e45718c56219110063739a111301034`, normal fix verification closed the finding: the outer wrapper is fully consumed, duplicate detection fields are rejected, malformed suffixes cannot count as valid Vision packets, and well-formed unknown fields are preserved.

## Validation evidence

- At `a95ad91c8e45718c56219110063739a111301034`, reviewer ran **15/15 committed non-writing tests**, including malformed-wrapper cases, plus 11 composed wrapper probes. Seven malformed suffix probes failed safely with no valid Vision evidence; four well-formed unknown-field controls remained accepted.
- Owner ran the complete committed suite, including CLI output: **16/16 passed**. Both Python files compiled; committed delta `git diff --check` passed.
- Exact-head CI [42381288692](https://github.com/ibis-ssl/Duck/actions/runs/42381288692) succeeded. Standard job `128429249210` passed; NET-002 and full-stack were skipped. CI did not execute the Python motion suite.
- Earlier normal finding checks are recorded against their exact SHAs in repository history. The independent report for the failed `81b7912` candidate is retained separately; its verdict does not transfer to `a95ad91`.

## Held items

This normal review covers the offline verifier source only. Fresh independent review of `a95ad91` is pending. Live `ASPIRE-MATCH-003`, primary evidence of Docker-version equality with #43, current-run network/source identity, and parent-branch integration remain incomplete. The historical #49 archive is not current acceptance evidence.
