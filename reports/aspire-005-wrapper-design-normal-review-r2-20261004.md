# ASPIRE-005 wrapper design review r2

## Review identity

- Mode: ordinary design review, whole-document coherence pass.
- PR/branch: PR #61, `task/pr28-aspire-005`.
- Reviewed HEAD: `cda34b5c24392f77de0822912ce9e14e0b4abbe2`.
- Target: `Tracker/Design/Testing/aspire-simulation-test-environment.md` at the reviewed HEAD.
- Comparison: prior design review targeted commit `1bd298838d2a2a7c6eed6d648b76997fd57b4f69`; this is a new target and these findings apply to the updated document only.
- Reviewer profile: parent assigned Luna medium (`gpt-6-luna` / `medium`); final runtime-applied profile cannot be independently observed here.
- Scope: current AppHost state vs initial background, old AddContainer/host-network method vs migration, base and match-mode resource models, readiness/dependencies, lifecycle/cancellation/forced-stop cleanup, evidence and completion criteria.
- Excluded: code changes, implementation/runtime acceptance, hosted artifacts, pending local validation artifacts, daemon settings, image pulls, PR actions.

## Evidence inspected

- `git rev-parse HEAD` and `git show --stat` for target identity.
- `git diff 1bd298838d2a2a7c6eed6d648b76997fd57b4f69..cda34b5c24392f77de0822912ce9e14e0b4abbe2 -- Tracker/Design/Testing/aspire-simulation-test-environment.md`.
- Full target document, especially current state, basic resource model, readiness/dependency, lifecycle, battle-mode resource table and dependency list, task completion table, and migration completion criteria.
- Current AppHost `Testing/Duck.Testing.AppHost/Program.cs` and project design context as relevant to legacy-vs-proposed topology.

## Coverage dispositions

| Criterion | Disposition | Evidence / limitation |
| --- | --- | --- |
| Current AppHost accurately separated from original background | checked_no_finding | §現状 calls out implemented AppHost and corrects the obsolete “no AppHost” claim. |
| Existing host-network AddContainer failure vs migration applicability | checked_finding | Main/basic topology distinguishes history from target; battle-mode table still says Docker image for shared services (D2). |
| Per-service wrapper topology and Duck project resource | checked_no_finding | Basic topology and overall completion criteria specify individual wrappers and preserve Duck AddProject. |
| Readiness predicates and dependency graph | checked_finding | Base graph is health based, but `ASPIRE-006F5` still mandates `WaitForStart` (D2). Match-mode table also omits wrapper execution form. |
| Failure, cancellation, forced-stop cleanup | checked_no_finding | Exact identity, bounded exact-owner discovery, unresolved cleanup failure, sub-15-second graceful budget, and forced-stop residual are explicit. |
| Prior delayed-create finding D1 | checked_no_finding (design action addressed; runtime evidence held) | Single lookup is replaced by bounded discovery; no success claim when state remains ambiguous; nonzero cleanup failure, residual recording, and delayed-create test are required. This does not prove implementation correctness. |
| Evidence and completion criteria | checked_no_finding | Model/wrapper/runtime acceptance are separated; concrete inspect, readiness, cleanup, and end-to-end artifacts are listed. |
| Scope discipline / prohibited changes | checked_no_finding | No daemon/OS changes, no image additions or broad cleanup introduced. |
| Validation adequacy / runtime acceptance | held | This pass is document review only; implementation and acceptance have not been reviewed or run. |

## Findings

### D2 — medium — stale match-mode resource model and dependency contract

- Origin: initial finding in this review of the updated design.
- Location: `Tracker/Design/Testing/aspire-simulation-test-environment.md`, “TIGERs vs Crane 対戦モード” resource table (around lines 395–400) and task table `ASPIRE-006F5` (around line 259).
- Description: The basic-mode sections and migration completion criteria say simulator, game-controller, crane, and cm4-sim are individual Aspire executable wrapper resources which own Docker containers; they also require health-based readiness. The battle-mode resource table still lists simulator, game-controller, crane, tigers-blue, autoref-tigers, and ssl-log-recorder simply as “Docker image,” without stating whether the three shared services reuse the wrapper resources. Separately, `ASPIRE-006F5` still requires “necessary `WaitForStart`” to be fixed by model tests, despite the migration contract saying start-only `WaitForStart` must not be used as readiness. This leaves the later match topology and its acceptance/model contract ambiguous about which services migrate and whether its dependencies are readiness-based.
- Impact: An implementation can satisfy the basic-mode wrapper criteria while keeping match-mode shared services as direct AddContainer resources, or can encode the old start-only dependency contract in the task model. The document’s whole-stack migration and battle-mode evidence would then disagree.
- Required action: Update the battle-mode resource table and `ASPIRE-006F5` to state that shared Docker services use the wrapper executable-resource abstraction in match mode too (with mode-specific image args/config), and specify health-based `WaitFor` edges where readiness is required. If some battle-mode images intentionally remain direct container resources, list the exact exceptions and their compatible networking/readiness contract. Remove or explicitly scope historical `WaitForStart` test language.

## Prior finding disposition

- Prior review D1 (medium, on commit `1bd2988`): addressed at design level in the new lifecycle section. It now requires bounded exact-owner discovery after cancel, makes inability to establish create state a nonzero cleanup failure, records possible residuals, prohibits treating a single query as cleanup success, and requires delayed-create failure injection. Bounded retries do not prove that a daemon will never create later; the updated contract correctly treats unresolved state as a possible residual instead of promising cleanup. Actual retry protocol/deadline behavior and implementation tests remain unverified and must be reviewed during implementation.

## Held and unexplored

- Wrapper implementation feasibility, including cm4-sim process-identity listener probing, remains a stated gate and was not tested here.
- No runtime behavior, Docker daemon timing, hosted full-stack acceptance, or matching current-HEAD CI was checked.
- DCP first-class container-detail loss is documented; parent acceptance of that tradeoff was not resolved in this review.
- This review does not attest the implementation or pending local validation artifacts.

## Validation adequacy and verdict

- Review validation: exact HEAD confirmed; document diff and full relevant context inspected. No tests were run because the target is a design document and this assignment is read-only design review.
- Verdict: **fail**. D2 is a required whole-document coherence correction. D1’s design-level action is addressed; its runtime correctness remains held.
- Next action: reconcile the match-mode resource and dependency/task entries with the wrapper migration, then review the corrected immutable design target before implementation acceptance.
- No design/code change, test, PR action, or merge was performed.
