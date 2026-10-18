# ASPIRE-005 wrapper design review r3

## Review identity

- Mode: bounded ordinary design re-review, with cross-document consistency scan.
- PR #61 remote design-content commit reviewed: `bb774b98b890ecad9a71353104b83409f2d0ae04`.
- Local repository HEAD during review: `5818ab518c9197ef55454f007b2f0c962fcd52d1` (`docs(aspire): align match mode wrapper contract`). The requested remote content commit was not present as a local Git object; the design file was fetched from that exact commit and its SHA-256 (`ee543ab0c6e7278f664f263a91ead56172d514e1135782600d04b582181d643f`) matched the local file byte-for-byte. Thus content review is bound to the requested immutable file content; repository commit identities differ.
- Target: `Tracker/Design/Testing/aspire-simulation-test-environment.md`.
- Prior review target: `cda34b5c24392f77de0822912ce9e14e0b4abbe2`; this pass evaluates its follow-up changes, not that prior target again.
- Reviewer profile: parent assigned Luna medium (`gpt-6-luna` / `medium`); actual runtime-applied profile is not observable.
- Scope: confirm D2 edits, scan relevant whole-document consistency across base, match, comparison/future-extension, readiness, lifecycle, and migration completion language.
- Excluded: implementation/runtime acceptance, pending validation artifacts, design edits, CI, PR actions, image pulls, daemon changes.

## Evidence inspected

- Fetched exact design file from raw GitHub URL pinned to `bb774b98b890ecad9a71353104b83409f2d0ae04`; SHA-256 compared with local design file and matched.
- Inspected the exact commit's GitHub diff for its two changed portions: `ASPIRE-006F5` and the match-mode resource table.
- Searched the full design text for `AddContainer`, `WithContainerRuntimeArgs`, `WaitForStart`, wrapper references, and Docker image/container language; read surrounding match-mode and readiness contracts.
- Inspected local HEAD and commit parent for execution-context identity. No code/test/runtime behavior was examined as acceptance evidence.

## Coverage dispositions

| Criterion | Disposition | Evidence / limitation |
| --- | --- | --- |
| D2: match-mode service execution form | checked_no_finding | Match-mode table now identifies simulator, game-controller, crane, tigers-blue, autoref-tigers, and ssl-log-recorder as executable wrapper + Docker container. |
| D2: match dependency/task contract | checked_no_finding | `ASPIRE-006F5` now requires the wrapper readiness-based dependency graph, removing the stale `WaitForStart` test requirement. |
| Current AppHost background vs desired migration | checked_no_finding | Document separates existing failing `AddContainer` host-network implementation from the proposed wrapper migration. |
| Readiness contract across base and match modes | checked_finding | Initial readiness table defines only the four base services, while match mode introduces additional wrappers and says the match controller waits for target wrapper readiness (D3). |
| Lifecycle/cancellation/forced stop | checked_no_finding | Exact labels, bounded discovery, nonzero unresolved-cleanup failure, graceful sub-15-second deadline, and documented forced-stop residual remain explicit. |
| Evidence and completion criteria | checked_no_finding | Model, wrapper, hosted acceptance, inspect/log, readiness, and shutdown evidence are separately specified. Match-only wrappers' readiness predicates/model coverage remain a gap under D3. |
| Future comparison-resource wording | checked_no_finding | Future extension's “container resources” does not itself claim Aspire `ContainerResource`; no direct contradiction found for this bounded review. Clarifying wrapper execution form there would reduce ambiguity but is not independently verdict-blocking. |
| Validation adequacy / runtime acceptance | held | Document-only review; no implementation or runtime acceptance was executed or assessed. |

## Findings

### D3 — medium — match-mode wrappers have no specified readiness profiles

- Origin: initial finding in this bounded re-review.
- Location: `Tracker/Design/Testing/aspire-simulation-test-environment.md`, “初期 readiness 契約” table and “対戦モードの起動順序” steps 2–4 (around lines 156–163 and 414–418); match-mode wrapper table (around lines 395–400).
- Description: The general contract says every wrapper exposes live/ready and `ready` requires a service-specific preparation probe, not merely a Running container. The readiness table is explicitly labeled “initial” and covers only `simulator`, `game-controller`, `cm4-sim`, and `crane`. Match mode now adds `tigers-blue`, `autoref-tigers`, and `ssl-log-recorder` as wrapper resources, and the match-controller startup step waits for “target wrapper readiness,” but the design does not say what those additional wrappers' ready predicates are or whether they participate in that wait. Their acceptance criteria prove later behaviors (tracker output, AutoRef activity, saved logs), but do not define the wrapper's pre-start readiness state. `ASPIRE-006F4` checks image/network/arguments without expressly testing wrapper registration or health profiles.
- Impact: An implementation could report match-only wrappers ready on Docker Running alone, contrary to the stated readiness contract, or leave `match-controller` waiting on an undefined set/meaning of ready resources. Model-test coverage could pass while omitting these wrapper health contracts.
- Evidence: Exact target file content has the “初期 readiness 契約” table for four services; match-mode table includes three additional wrapper resources; match-mode startup says match-controller waits on target wrapper readiness; `ASPIRE-006F4` does not name wrapper health/profile checks.
- Required action: Define match-mode readiness predicates for `tigers-blue`, `autoref-tigers`, and `ssl-log-recorder`, or explicitly state they have no dependency-edge readiness requirement and are validated by the match fixture instead. Name the exact wrappers that `match-controller` waits for. Extend the match model/wrapper tests to assert those profiles and edges. Avoid treating container Running alone as ready.

## Prior finding dispositions

- D2 from review of `cda34b5c24392f77de0822912ce9e14e0b4abbe2`: **closed** at design level. The match-mode resource table now specifies wrapper + Docker container for its Docker-backed resources, and `ASPIRE-006F5` now requires a readiness-based dependency graph instead of `WaitForStart`.
- D1 from review of `1bd298838d2a2a7c6eed6d648b76997fd57b4f69`: remains addressed at design level. Bounded exact-owner discovery, explicit nonzero cleanup failure for unresolved create state, possible-residual recording, no one-query cleanup success, and delayed-create failure injection remain in the exact reviewed content. Runtime correctness is not established.

## Held and unexplored

- No wrapper implementation, dependency fixture, fake Docker test, Docker daemon race, hosted run, or end-to-end match mode was executed.
- The cm4-sim listener-ownership feasibility gate and DCP container-detail tradeoff remain unverified/unapproved as previously noted.
- The future comparison section still uses the broad phrase “container resources”; this review found no direct contradiction, but the wrapper migration’s applicability to those future services could be stated explicitly in that future task.
- No current-HEAD CI was checked; this is a design-document review only.

## Validation adequacy and verdict

- Exact target file content was independently fetched and matched byte-for-byte to the local file. Relevant commit diff and whole-document terminology/context were reviewed.
- Verdict: **fail**. D2 is closed, but D3 leaves readiness semantics and test coverage incomplete for three match-mode wrappers.
- Next action: specify readiness profiles or explicit non-dependency treatment for the match-only wrappers, and update `ASPIRE-006F4` / match-controller dependency tests accordingly; then review the corrected immutable content.
- No design/code changes, tests, hosted execution, PR actions, or merge were performed.
