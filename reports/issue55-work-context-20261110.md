# Issue #55 work-context

- Issue: [ibis-ssl/Duck#55](https://github.com/ibis-ssl/Duck/issues/55) / ASPIRE-007A
- Issue title/state: `[ASPIRE-007A] Linux実機のネットワーク受入を完了する` / OPEN
- Requested profile: `gpt-6-luna / high`; actual applied model/profile: unverified (not observable here).
- Repository/worktree: `ibis-ssl/Duck`, `C:\Users\donabe\Documents\Codex\2026-11-07\task\Duck-issue55-aspire-007a`
- Branch/base: `task/issue55-aspire-007a` / `design/issue18-aspire-test-orchestration`
- Current HEAD: `eebc6c1a4cb139003830730b63591af36e61d75e`
- Working tree at inspection: only the pre-created report was untracked; no code/configuration edits were made.

## Authoritative scope and requirements

Issue #55 requires actual Linux + Docker Engine network acceptance for applicable `ASPIRE-NET-001`–`009` and match-mode `ASPIRE-MATCH-001`–`005`, recording OS/Docker versions, network settings, packet counts, source identity, stdout/stderr, and partial evidence on failure. It says not to count unrun or failed checks as success. Its dependencies, Issues #42 (`ASPIRE-006E`) and #50 (`ASPIRE-006G`), are both CLOSED on GitHub. The task-status table still identifies #55 as depending on those tasks and repeats the actual-packet evidence requirement.

The design does **not** conflict with Linux + Docker Engine as an OS-specific acceptance target. It describes Windows + Docker Desktop as the current product execution design, explicitly excludes a separately user-managed Ubuntu Docker Engine from that product path, and separately requires Linux/macOS compatibility to be established on each OS. It also explicitly says Linux Docker Engine success does not substitute for Windows Docker Desktop acceptance. Issue #55 is an acceptance-only task, not a request to change the product runtime design.

## Workflow and execution path

`.github/workflows/dotnet-test.yml` has two relevant Linux jobs:

- `ASPIRE-NET-002 Linux packet flow` runs only for `workflow_dispatch`.
- `ASPIRE full-stack Linux acceptance` runs for `workflow_dispatch` or a narrow allowlist of push/PR branches. Manual dispatch offers `comparison` and `match` modes; the full-stack job runs on `ubuntu-24.04` and records runner/Docker/network preflight data and acceptance artifacts.

Thus a repository-provided Linux/Docker execution path exists, and the dependencies are no longer open. The current branch is not in the automatic full-stack allowlist, so PR CI alone will skip these acceptance jobs. Whether the authorized GitHub actor can dispatch the workflow is not established by this read-only inspection. The delegated boundary explicitly forbids running Actions or Docker; no execution was attempted.

## Evidence state

- Exact-HEAD CI exists: run [42750763062](https://github.com/ibis-ssl/Duck/actions/runs/42750763062), PR event, `headSha=eebc6c1a4cb139003830730b63591af36e61d75e`, overall success. It only establishes ordinary test/publish/policy checks. Both `ASPIRE-NET-002 Linux packet flow` and `ASPIRE full-stack Linux acceptance` were **skipped**, so it is not Linux acceptance evidence.
- Issue body cites historical Linux `ASPIRE-NET-002` success run `36627447524` (0→187 packets / 3941 ms); repository history also cites `36625776852` (232 packets / 3960 ms). These are narrow, old NET-002 results and do not satisfy current-HEAD or complete #55 scope.
- More recent green/failed match runs recorded under Issues #52/#81 (including run `42595415674`) belong to other HEADs/tasks; one noted full-stack match failed to reach `POST_GAME`. They cannot be imported as #55 completion evidence.
- No same-HEAD run for #55 acceptance exists in the inspected evidence; no full NET/MATCH evidence package for this HEAD was found.

## Task context

```yaml
repository: ibis-ssl/Duck
issue_or_pr: "#55 / ASPIRE-007A"
task_id: ASPIRE-007A
mode: work-context
branch: task/issue55-aspire-007a
base_ref: design/issue18-aspire-test-orchestration
current_head: eebc6c1a4cb139003830730b63591af36e61d75e
reviewed_head: null
scope:
  - Run and preserve actual Linux + Docker Engine evidence for applicable ASPIRE-NET-001..009.
  - Run and preserve actual Linux match-mode evidence for ASPIRE-MATCH-001..005.
non_goals:
  - Change product runtime design or implement duplicate acceptance criteria as product issues.
  - Treat Windows/Docker Desktop, old Linux results, ordinary CI, or skipped acceptance jobs as completion.
write_boundary:
  allowed:
    - This pre-created report only.
  forbidden:
    - Docker execution, GitHub Actions dispatch/rerun, code/config changes, GitHub writes.
development_policy:
  method: acceptance-only; preserve real execution evidence, including failure diagnostics.
  testing_order: prerequisites and acceptance setup before real packet runs; not executed in this context pass.
validation:
  commands:
    - Read-only inspection of task/design/workflow/evidence and GitHub issue/run metadata.
  required_failure_diagnostics:
    - Preserve environment, Docker, network, application logs, packet counts/source identity, and partial artifacts on acceptance failure.
ci:
  matching_run: "42750763062 (exact HEAD; acceptance jobs skipped)"
  conclusion: "ordinary CI success; no matching acceptance result"
unknown:
  - Whether current GitHub actor has workflow-dispatch permission.
  - Whether the target real Linux/Docker environment is available and matches the acceptance prerequisites.
  - Whether current implementation and current dependency outputs are sufficient for every criterion when run.
blocked:
  - Execution is outside this delegated read-only context; do not dispatch or run Docker here.
remaining_risks:
  - Full scope is broader than NET-002 smoke evidence and includes comparison and match packet paths.
  - Exact-HEAD CI green status can be misread unless the two skipped Linux acceptance jobs are called out.
```

## Smallest safe next action

Arrange an authorized `workflow_dispatch` of the current HEAD in `comparison` mode, inspect its artifacts and failure diagnostics, then run `match` mode if comparison completes and the evidence supports proceeding. Treat each mode's outcome independently; do not close #55 based on dispatch success or skipped jobs. If the user expects a designated physical Linux host rather than the configured GitHub `ubuntu-24.04` runner, first resolve that environment expectation because Issue #55's Linux/Docker Engine wording does not itself name a specific host.
