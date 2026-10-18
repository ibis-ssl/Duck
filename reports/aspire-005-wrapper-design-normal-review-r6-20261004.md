# ASPIRE-005 wrapper design review r6

## Review identity

- Mode: ordinary design re-review, final whole-document coherence pass.
- Reviewed remote design-content SHA: `4c9732a4e289b7714597d2e5620fb06a388bb474`.
- Target: `Tracker/Design/Testing/aspire-simulation-test-environment.md` at that commit.
- Local repository HEAD during review: `931cfa3d3105aba539dedec198e7282b64ffa731`. Remote commit object is absent locally; the design file was fetched from the exact target URL. Its SHA-256 (`2d93712be33cbee06e98f7edb2ec4f0a6c34e948e9133c213f1ce25a8d37e54f`) matches the local file byte-for-byte. The verdict applies to this exact design content, not to the local repository commit identity.
- Previous exact design target: `6673383584399d9c149dd640504b081a284d1fa3`.
- Reviewer profile: parent assigned Luna medium (`gpt-6-luna` / `medium`); applied runtime profile is not observable.
- Scope: all previous review scope and findings, mode-specific readiness fix, historical vs target wording, per-service wrapper migration, dependencies, lifecycle/cleanup, evidence and completion criteria.
- Excluded: implementation/runtime acceptance, pending validation artifacts, design changes, test execution, CI, Docker/image/daemon operations, PR actions.

## Evidence inspected

- Fetched target design from `raw.githubusercontent.com/ibis-ssl/Duck/4c9732a4e289b7714597d2e5620fb06a388bb474/Tracker/Design/Testing/aspire-simulation-test-environment.md`; SHA-256 matched the local design file.
- Compared exact target against prior target `6673383584399d9c149dd640504b081a284d1fa3`.
- Read the whole target and searched/checked the current-state and migration narrative; base/match readiness tables and graphs; HALT/STOP fixtures; ASPIRE-003/004/005; ASPIRE-006F3/F4/F5; match acceptance; cancellation and graceful/forced cleanup; test plan and completion criteria.
- Confirmed no design path was modified during this review. Earlier child-owned report artifacts remain untracked and are not treated as target content or validation evidence.

## Coverage dispositions

| Criterion | Disposition | Evidence / limitation |
| --- | --- | --- |
| Current AppHost background vs proposed final topology | checked_no_finding | Existing AddContainer implementation is clearly distinguished from the wrapper target and described as failing in the pinned hosted runtime. |
| ASPIRE-003/004 historical implementation vs target wrappers | checked_no_finding | Their current resource implementation is labeled historical; final topology is per-service wrappers. |
| ASPIRE-005 hosted acceptance gate | checked_no_finding | Hosted acceptance is deferred until wrapper migration, focused model/wrapper tests, and ordinary review; retries of the known AddContainer create failure cannot qualify. |
| Base and match wrapper topology | checked_no_finding | Basic and match mode identify Docker services as individual wrapper resources and preserve Duck as project resource. |
| Service readiness and dependency graphs | checked_no_finding | Match-only wrapper predicates, named edges, test requirements, and mode-specific Game Controller readiness are explicit. |
| Game Controller initial state consistency | checked_no_finding | `HALT` for base/comparison; `STOP` for match; unknown/unexpected command remains not-ready; focused tests must prove both valid and invalid cases. |
| Prior D1 delayed-create cleanup race | checked_no_finding (design disposition) | Bounded owner-label discovery and explicit unresolved nonzero failure/residual remain specified. No runtime correctness is inferred. |
| Graceful/forced shutdown | checked_no_finding | DCP 15-second stop budget, child reaping, owned-container cleanup, and explicit SIGKILL/host-loss/daemon-outage residual are documented. |
| Scope and prohibited changes | checked_no_finding | No new image, daemon/OS configuration, or broad cleanup operation is introduced. |
| Validation evidence adequacy | held | Read-only design review only. No wrapper tests, hosted run, implementation review, or CI was performed. |

## Findings

**No findings.** The mode-specific Game Controller readiness correction closes D4: the `game-controller` row now accepts only the configured expected command (`HALT` in base/comparison, `STOP` in match), and `ASPIRE-006F3` / `ASPIRE-MATCH-002` require tests for both mode paths and rejection of unexpected states.

## Prior finding dispositions

- D1 (delayed Docker create/cancellation cleanup race): addressed at design level. Exact-owner bounded discovery, explicit nonzero cleanup failure/residual for unresolved state, no single-query success claim, and delayed-create failure injection are retained. Runtime behavior remains unverified.
- D2 (match-mode resource model and stale `WaitForStart` task requirement): closed. Match services are wrapper resources and the task requires a readiness-based graph.
- D3 (match-only wrapper readiness profiles): closed. Sumatra, AutoRef, and SSL log recorder predicates, explicit readiness edges, and model/wrapper test coverage are now required.
- D4 (Game Controller `HALT` readiness vs match fixture `STOP`): closed. Readiness is mode-specific and unexpected commands are rejected; task and acceptance contracts require checks for both expected states.

## Held and unexplored

- No wrapper implementation or focused tests were executed/reviewed. Sumatra run-owned tracker-packet attribution, AutoRef diagnostic/adapter feasibility, SSL log record decoding, and cm4-sim process-identity listener probing remain implementation verification gates.
- No Docker daemon cancellation race, hosted full-stack acceptance, active match, or matching current-HEAD CI was checked.
- DCP first-class container-detail loss remains a documented product tradeoff; approval is outside this review.
- The future comparison-extension phrase “container resources” remains broad, but no immediate contradiction was found in the bounded accepted migration scope.

## Validation adequacy and verdict

- Exact remote target content was fetched and matched byte-for-byte to the local design copy. The entire document and all prior finding locations were rechecked.
- Verdict: **pass_with_held**. No design finding remains. Implementation feasibility, focused evidence, and hosted acceptance remain held gates and are not represented as complete.
- Next action: implementation may proceed under the document's gates; before hosted acceptance, close each readiness/lifecycle implementation gate with focused tests and ordinary implementation review, then run the required hosted acceptance on the matching target.
- No design/code changes, tests, hosted execution, PR actions, or merge were performed.
