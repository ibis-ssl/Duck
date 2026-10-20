# Issue 77 / PR 76: manifest and readiness repair

## Scope

Target: ibis-ssl/Duck PR 76, branch task/aspire-fullstack-fixture-stability. Validation started from HEAD 7dc7b79b250af62268f34009bf3e0dea01fbae75. This report records only Issue 77 changes. Issue 78 movement-state monitoring and AutoRef mitigation remain out of scope and unimplemented.

## Changes

- AppHost now writes its resolved Crane team name, color, and stack ID into a run-specific JSON manifest using a same-directory temporary file and atomic no-overwrite rename. The manifest contains no secrets. The default filename is derived from a SHA-256 hash of the stack ID.
- The acceptance test reads the manifest for its explicit stack ID and rejects missing, malformed, invalid, or foreign-stack data. Workflow and local-start documentation pass the same stack ID and manifest path to the AppHost and test process.
- Crane team settings are covered through JSON and environment providers, and an AppHost CLI regression verifies the resolved non-default value in the emitted manifest.
- Game Controller recovery now has a fixed 10-second budget. Up to three requests are shared across startup and later recovery; a later recovery has only the unused attempts. It does not replenish attempts after recovery. A new request is suppressed unless its 2-second timeout and at least 1-second post-completion confirmation grace fit before the recovery deadline. Deadline checks also bound the control API probe.
- The production readiness monitor accepts TimeProvider, referee-packet, control-send, and control-probe test seams. Tests connect that monitor to the real wrapper for missing and mismatching team information, recovery to a matching packet, ready 503/live 200, three attempts followed by the full recovery wait, deadline failure, cancellation, and cleanup.

## Verification

- dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false: exit 0; 465 passed, 0 failed, 0 skipped.
- dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore -m:1 /nr:false: exit 0.
- dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:RunAspireFullStackAcceptance=true -m:1 /nr:false: exit 0.
- Existing comparison full-stack startup/readiness and acceptance sequence, with Testing:Crane:TeamName=pr76_custom_crane and TeamColor=YELLOW: readiness succeeded for the six owned resources and both comparison endpoints; manifest stack/team/color matched; acceptance exit 0, 2 passed, 0 failed. The full-stack test reached tracker comparison measurement, observed Halt → PrepareKickoffYellow → NormalStart, and measured 101.77872 mm movement. The second-owner check also passed when the workflow's apphost-process.txt record was provided. Run artifacts are under artifacts/aspire-full-stack/local-pr77-acceptance-20261020T014023Z-388467 and were sanitized.
- The run cleanup verified the exact duck-apphost owner and stack labels and left no same-stack containers. The AppHost process exited.
- git diff --check: exit 0.
- npm run lint:md: exit 1 before lint execution. WSL resolves npm to the Windows shim; CMD cannot use the WSL UNC working directory and looked for C:\Windows\package.json. This checkout has no Linux node, node_modules, or .agents lint runtime. No dependencies were installed.

The successful .NET builds reported the existing NU1902 MessagePack advisory and ASPIRE010 disabled Aspire CLI bundle warnings.

## Documentation checks

- `npm run lint:md`: exit 1 because the worktree has no local `node_modules` and the wrapper could not find `textlint`; no dependencies were installed.
- Textlint 15.7.0, run directly against README, this design document, and this report using the existing local dependency tree: exit 0 for all three files.
- CSpell 9.8.0, using the repository wrapper and existing config/dictionary against the same three files: exit 0, 0 findings in README and the design document; the report was skipped by the existing `reports/**` ignore rule.
- SudachiPy 0.6.11 / SudachiDict-core 20260428: exit 0 for README and the design document. The report is excluded by the existing Markdown target configuration.
- Yomiyasu checkout `0df47749139dfd64ad3d55e7d53d3839e5874848`, run in JSON mode against all three files: exit 0. Its non-strict advisories include list-heavy content and existing sentence-ending/colon patterns; no strict mode was used.

## Publication state

Commits `13b62b1995b21a626bcdfb9ae17a2518b1b70489`, `eccfcb02d5ed6fab60dcdc2e68d0a4107e7791a9`, and `3e2f93e38003a850fdf5f9fa02265f6c88611d64` were pushed by normal fast-forward to `origin/task/aspire-fullstack-fixture-stability`; each explicit push exited 0. This report was then corrected with the final per-document lint results and pushed by another normal fast-forward. Final remote readback confirmed the requested branch at the current pushed HEAD. The local branch's configured upstream remains `origin/design/issue18-aspire-test-orchestration`; pushes targeted the requested PR branch explicitly. No new PR, issue, or branch was created.
