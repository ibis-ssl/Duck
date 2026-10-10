# ASPIRE-005 wrapper design review r5

## Review identity

- Mode: ordinary design re-review, exact immutable design content.
- Reviewed remote design-content SHA: `6673383584399d9c149dd640504b081a284d1fa3`.
- Target: `Tracker/Design/Testing/aspire-simulation-test-environment.md` at that commit.
- Local repository HEAD during review: `9d4197b169c739c0c1cd4a13a4c9eb8bb978aa38`. Remote commit object is absent locally; fetched the file from the exact commit URL. Target file SHA-256 `d746906089675fb2eff1f11ccfc592130a3533f80851db7e0acb6d6c36bd4767` matched the local file byte-for-byte. This binds the review to the requested content, not to the local commit identity.
- Previous reviewed content SHA: `76441699afbee0d322859c35acc8bbc3047b8b95`.
- Reviewer profile: parent assigned Luna medium (`gpt-6-luna` / `medium`); applied runtime profile is not independently observable.
- Scope: all prior wrapper design review scope, D3 closure, new readiness profiles and named readiness edges, whole-document consistency of base/match modes and hosted acceptance.
- Excluded: implementation, runtime/CI acceptance, pending validation artifacts, design changes, tests, image pulls, daemon changes, PR actions.

## Evidence inspected

- Exact target file fetched from `raw.githubusercontent.com/ibis-ssl/Duck/6673383584399d9c149dd640504b081a284d1fa3/Tracker/Design/Testing/aspire-simulation-test-environment.md`; SHA-256 compared to local file and matched.
- Diff from prior exact design content `76441699...` and relevant full text: readiness table, readiness graph, ASPIRE-006F4, wrapper-test requirements, match-mode readiness dependencies and acceptance, referee fixture contract.
- Searches for `HALT`, `STOP`, `game-controller`, readiness, `WaitFor`, `AddContainer`, and wrapper language throughout the exact target.

## Coverage dispositions

| Criterion | Disposition | Evidence / limitation |
| --- | --- | --- |
| D2: match-mode wrapper resource model and task graph | checked_no_finding | Services are wrapper resources; ASPIRE-006F5 requires readiness-based dependency graph. |
| D3: match-only wrapper readiness profiles and tests | checked_no_finding | New predicates for Sumatra output, AutoRef's own decoded input counters, and valid run-created logs; ASPIRE-006F4 and match resource model now require profiles, edges, tests, and diagnostics. |
| ASPIRE-003/004 historical implementation vs target topology | checked_no_finding | Current container-resource implementation is distinguished from wrapper target; no acceptance of failing runtime args is implied. |
| ASPIRE-005 hosted acceptance gating | checked_no_finding | Hosted acceptance requires wrapper migration, focused tests, and ordinary review; retries of failing AddContainer create are excluded as acceptance success. |
| Readiness/dependency consistency across base and match modes | checked_finding | Match mode uses initial `STOP` while shared Game Controller readiness requires initial `HALT` (D4). |
| Lifecycle/cancellation/forced-stop cleanup | checked_no_finding | Exact-owner bounded discovery, unresolved cleanup failure/residual, DCP shutdown budget and forced-stop residual remain specified. Runtime behavior not tested. |
| Scope, images, daemon/OS settings | checked_no_finding | No unrequested image or daemon/OS setting changes. |
| Validation adequacy | held | Design-only review; no implementation, fake Docker tests, hosted acceptance, or current-HEAD CI checked. |

## Findings

### D4 — medium — Game Controller readiness requires the wrong initial command for match mode

- Origin: new finding in this review.
- Location: `Tracker/Design/Testing/aspire-simulation-test-environment.md`, readiness table `game-controller` row (around line 161); match fixture contract (around line 150); match dependency graph (around lines 419–424).
- Description: The shared `game-controller` readiness profile requires decoding the initial `HALT` referee state. The same document specifies a distinct match-mode fixture with initial command `STOP`. In match mode, `tigers-blue`, `ssl-log-recorder`, `autoref-tigers`, `crane`, and `match-controller` all wait for ready `game-controller`. Under the stated shared predicate, the valid match fixture never makes the controller ready, so its dependents do not start even though `match-controller` explicitly supports HALT/STOP as start states.
- Impact: The match-mode dependency graph can deadlock on the documented, intended initial state. This conflicts with `ASPIRE-MATCH-001..005` and prevents the match acceptance path from reaching the controller fixture's supported STOP startup.
- Evidence: Exact target row states `game-controller` is ready after decoding initial `HALT`; exact target fixture states match mode uses initial `STOP`; exact target match sequence makes all participating wrappers wait on ready `game-controller` before `match-controller` validates HALT/STOP and sends a continue action.
- Required action: Make the Game Controller readiness predicate configuration/mode-aware: require `HALT` for the ordinary active-motion fixture and `STOP` for match mode, or define a shared predicate that accepts only the explicitly configured valid initial command. Add model/wrapper tests proving both fixtures become ready and that unexpected commands remain not-ready.

## Prior finding dispositions

- D1 (original delayed-create cleanup race): addressed at design level. Exact-owner bounded discovery, explicit nonzero failure/residual for unresolved create state, no single-query success claim, and delayed-create test remain present. Implementation correctness remains unverified.
- D2 (match resource type and stale `WaitForStart`): closed. Wrapper + Docker topology and readiness-based task graph remain explicit.
- D3 (missing match-only wrapper readiness profiles): closed. New table predicates cover `tigers-blue`, `autoref-tigers`, and `ssl-log-recorder`; named `WaitFor` dependencies, ASPIRE-006F4 coverage, and focused tests were added.

## Held and unexplored

- No implementation or readiness tests were run. The new Sumatra readiness condition requires run-generated tracker evidence; its concrete runtime attribution mechanism remains an implementation detail to verify against the pinned image during focused tests.
- AutoRef diagnostics/adapter feasibility and cm4-sim listener process-identity feasibility remain unverified design gates.
- No Docker execution, hosted acceptance, full match run, or exact-head CI evidence was checked.
- DCP first-class container-detail loss remains a documented tradeoff outside this review's approval scope.

## Validation adequacy and verdict

- Exact remote content was fetched and verified byte-identical to the local document; all prior review scope and the new readiness changes were inspected.
- Verdict: **fail**. D4 is a required design correction because the specified match-mode startup state cannot satisfy the shared Game Controller ready predicate.
- Next action: make the referee readiness predicate match-fixture-aware and add tests for both initial HALT and STOP; then review the corrected immutable content before implementation acceptance.
- No design/code changes, tests, hosted execution, PR actions, or merge were performed.
