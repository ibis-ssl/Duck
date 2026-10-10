# ASPIRE-005 wrapper design review r4

## Review identity

- Mode: ordinary design re-review, whole-document coherence.
- Exact remote design-content SHA: `76441699afbee0d322859c35acc8bbc3047b8b95`.
- Target: `Tracker/Design/Testing/aspire-simulation-test-environment.md` at that commit.
- Local repository HEAD during review: `eeb26fc5ecfe7a119ec53bc94bb7bffb5bf7eca4`. The remote commit object is absent locally, so the file was fetched from the exact commit URL. Its SHA-256, `53f1947c3f8eb1fc28b13ddabac38f10cc0a4d88605d4a13c1f024c817810c4a`, matches the local design file byte-for-byte. This review is bound to the specified file content, not to an assertion that local repository HEAD equals the remote commit.
- Previous review content target: `bb774b98b890ecad9a71353104b83409f2d0ae04`; comparison against it isolated the new ASPIRE-003/004/005 paragraphs.
- Reviewer profile: parent assigned Luna medium (`gpt-6-luna` / `medium`); actual runtime-applied profile is not observable.
- Scope: all prior review scope—historical AppHost state vs desired topology, AddContainer host-network failure vs wrapper migration, match-mode wrappers/dependencies/readiness, lifecycle and bounded cleanup, evidence/completion gates—plus the new ASPIRE-003/004 historical-vs-target wording and ASPIRE-005 hosted-acceptance gate.
- Excluded: implementation/runtime acceptance, pending validation artifacts, design changes, test execution, CI, image pulls, daemon changes, PR actions.

## Evidence inspected

- Fetched the target design file from `raw.githubusercontent.com/ibis-ssl/Duck/76441699afbee0d322859c35acc8bbc3047b8b95/...`; compared SHA-256 to the local design file (exact match).
- Compared exact target content with prior target `bb774b98b890ecad9a71353104b83409f2d0ae04`.
- Searched the whole target for `AddContainer`, `WithContainerRuntimeArgs`, `WaitForStart`, wrapper/resource terminology, ASPIRE-003/004/005, and readiness/dependency/acceptance criteria; inspected surrounding sections.
- Inspected local HEAD and working tree identity. The two prior child-owned reports remain untracked; no other local files were changed by this review.

## Coverage dispositions

| Criterion | Disposition | Evidence / limitation |
| --- | --- | --- |
| Current AppHost state vs design-start background | checked_no_finding | Current-state paragraph accurately records existing AppHost and container-resource implementation. |
| Legacy AddContainer/host-network method vs wrapper migration | checked_no_finding | Current failure is explicitly historical and rejected as final acceptance; target topology uses per-service wrappers. ASPIRE-003 and ASPIRE-004 now distinguish current implementation from wrapper target. |
| D2 match-mode resource table and ASPIRE-006F5 | checked_no_finding | Match Docker-backed services are listed as wrapper + Docker container; task now requires readiness-based graph. |
| Base readiness/dependency semantics | checked_no_finding | Health-based dependencies and four initial service predicates are explicit; Duck project-start is not represented as service readiness. |
| Match-mode wrapper readiness/dependency semantics | checked_finding | Three match-only wrappers have no stated preparation predicate although match-controller waits for target wrapper readiness (D3). |
| Startup failure, cancellation, graceful stop, forced stop | checked_no_finding | Exact-owner lookup retry and unresolved failure, bounded graceful budget, and forced-stop residual are documented. No runtime verification. |
| ASPIRE-005 hosted acceptance gate | checked_no_finding | Hosted acceptance is explicitly deferred until wrapper migration, focused tests, and ordinary review; retrying current AddContainer create failure cannot count as acceptance. |
| Evidence and completion criteria | checked_no_finding | Model/wrapper/hosted acceptance evidence are separated; no claim that current AppHost implementation is complete. D3 remains a specific readiness-contract gap. |
| Scope discipline / daemon and image changes | checked_no_finding | No added image, daemon setting, OS setting, or broad cleanup behavior. |
| Validation adequacy | held | Read-only document review only; no implementation, runtime, or CI acceptance reviewed. |

## Findings

### D3 — medium — match-mode wrappers lack service-specific readiness contracts

- Origin: carried from the prior review of target content `bb774b98b890ecad9a71353104b83409f2d0ae04`; rechecked and still open on this target.
- Location: `Tracker/Design/Testing/aspire-simulation-test-environment.md`, “初期 readiness 契約” table and “対戦モードの起動順序” steps 2–4 (approximately lines 156–163 and 414–418); match-mode resource table (approximately lines 397–402); `ASPIRE-006F4` / `ASPIRE-MATCH-001..005`.
- Description: The general contract says every wrapper exposes `/health/live` and `/health/ready`, and `ready` requires a service-specific preparation probe rather than Docker Running alone. The readiness table explicitly defines only the initial four services. Match mode adds `tigers-blue`, `autoref-tigers`, and `ssl-log-recorder` as wrapper resources and says `match-controller` waits for target wrapper readiness, but their wrapper-ready predicates and exact participating resources are not defined. The later acceptance items establish tracker output, AutoRef activity, and artifacts during match execution, but do not specify the wrappers' pre-match readiness conditions. `ASPIRE-006F4` checks images/network/ports/arguments and does not expressly require wrapper registration/readiness profiles.
- Impact: Match-mode implementation may equate wrapper ready with container Running or leave the match-controller dependency set undefined, contrary to the document's health contract. Model tests could cover the base four while leaving match-mode wrapper health untested.
- Evidence: Exact target bytes retain “初期 readiness 契約” listing four services; exact target match table includes the three additional wrappers; exact target startup step 4 waits on “target wrapper readiness”; exact target ASPIRE-006F4 omits those wrapper health/profile checks.
- Required action: Define readiness predicates for `tigers-blue`, `autoref-tigers`, and `ssl-log-recorder`, or state explicitly that they have no dependency-edge readiness requirement and are validated by the match fixture. Name exactly which wrappers `match-controller` waits for. Extend `ASPIRE-006F4` / match model and wrapper tests to verify the selected behavior. Do not use Docker Running alone as ready.

## Prior finding dispositions

- D2 from review of `cda34b5c24392f77de0822912ce9e14e0b4abbe2`: **closed**. The prior changes remain present in this target: match services use wrapper + Docker container, and `ASPIRE-006F5` requires readiness-based dependencies.
- D1 from review of `1bd298838d2a2a7c6eed6d648b76997fd57b4f69`: **addressed at design level**. Exact target retains bounded owner-label discovery after cancellation, nonzero cleanup failure and residual recording when state remains unresolved, prohibition on one-shot cleanup success, and delayed-create failure injection. Runtime correctness remains unverified.

## Held and unexplored

- No wrapper implementation or focused tests were run/reviewed; cm4-sim listener-identity feasibility remains a design gate.
- No hosted full-stack or match-mode execution, Docker daemon cancellation race, or current-HEAD CI was checked.
- DCP first-class container-detail loss remains a documented tradeoff; approval is outside this review.
- The future comparison extension still calls its additions “container resources”; no immediate contradiction was found, though specifying that future host-network services use wrappers would improve clarity.

## Validation adequacy and verdict

- The requested exact remote design file was fetched and its content hash matched the local counterpart. Relevant edits and the entire surrounding design contract were inspected.
- Verdict: **fail**. D3 remains a required readiness-contract correction for match-mode wrappers. The new ASPIRE-003/004/005 edits are coherent and D2 is closed.
- Next action: specify the match-only wrapper readiness predicates or explicitly exempt those services from dependency readiness, and make the match model test contract exact; then re-review the immutable corrected content.
- No design/code changes, tests, hosted execution, PR actions, or merge were performed.
