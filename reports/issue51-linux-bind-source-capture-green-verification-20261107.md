# Sub-agent report

## タスク

- 目的: issue #51 の変更後の Python verifier tests と関連 C# 受信・権限回帰 tests を、現行の未コミット source 一式で独立実行して Green を確認する。
- タスク種別: verification / test execution。

## sub-agent を使う理由

- 検証実行は固定 sub-agent 作業であり、実装者と Green 実行者を分離する。

## 対象範囲

- `scripts/tests/test_match_referee_source_capture.py` と既存 `test_verify_aspire_match_acceptance.py`。
- Linux 上の `MatchLinuxBindPermissionsTests`, `RefereeSenderTupleCaptureTests`, `RefereeSourceCaptureLifecycleTests` と関連する referee receive / host / match controller / participant status tests。
- WSL2 Ubuntu に置かれた同一 `/mnt/c` source に対する実行。テスト専用の ignored artifacts と結果ディレクトリのみ作成する。

## 対象外

- source / test / workflow 編集、テスト失敗を直すための変更、Docker/AppHost/containers 起動、dependency install、environment settings/certificate changes、issue/PR/comment/commit/push/merge。
- GitHub Actions 実行・再実行、実マッチ受入成功の主張。

## Dispatch profile

- selection inputs: test execution; bounded technical, medium uncertainty, cross-module, high criticality for Linux acceptance evidence, single repetition, sequential dependency, fresh.
- selection source: automatic.
- observed decomposability: sequential_dependencies.
- decomposition policy / disposition: forbidden; one executor on the exact same source.
- proposed profile: none.
- approval status / evidence: not required.
- requested profile: gpt-6.1-sol / high (specified task profile).
- agent role / default-role plan: default role; explicit role override none.
- role config evidence / profile effect: collaboration dispatch exposes model/reasoning only; exact final profile may remain unobservable.
- planned runtime profile: gpt-6.1-sol / high.
- applied profile: null until observable.
- application status: pending dispatch.
- runtime profile observability: pending.
- fork policy: none.
- reasons / constraints: independent bounded validation; retain all source and environment state.

## Commands

- Independent verification only, against the implementation report's dirty-source manifest. Both bounded native commands completed once; no test rerun was performed.
- Formal `require_escalated` capability command: `wsl.exe --list --verbose`, exit 0. Ubuntu was running with WSL version 2; docker-desktop was stopped. Raw stdout/stderr are retained as `wsl-list.stdout.txt` / `wsl-list.stderr.txt` (WSL list stdout is UTF-16LE).
- Formal `require_escalated` execution command: `wsl.exe --distribution Ubuntu --exec /bin/bash /mnt/c/Users/donabe/Documents/Codex/2026-11-07/task/issue51-green-verification-evidence/run-green.sh`, wrapper exit 0. The retained script performs environment and exact-source checks, then the following commands sequentially from `/mnt/c/Users/donabe/RemoteDesktopWorkspace/Duck-issue51-aspire-006g1`.

```bash
timeout 180s python3 -B -m unittest discover -s scripts/tests -p 'test_*match*.py' -v

timeout 600s dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter 'FullyQualifiedName~Tracker.Tests.MatchLinuxBindPermissionsTests|FullyQualifiedName~Tracker.Tests.RefereeSenderTupleCaptureTests|FullyQualifiedName~Tracker.Tests.RefereeSourceCaptureLifecycleTests|FullyQualifiedName~Tracker.Tests.RefereeReceive|FullyQualifiedName~Tracker.Tests.RefereeDriverHost|FullyQualifiedName~Tracker.Tests.MatchControllerWorkerTests|FullyQualifiedName~Tracker.Tests.MatchParticipantStatusProjectionTests' --artifacts-path artifacts/issue51-green-verification-20261107 --logger 'trx;LogFileName=issue51-linux-bind-source-capture-green.trx' --results-directory /mnt/c/Users/donabe/Documents/Codex/2026-11-07/task/issue51-green-verification-evidence/dotnet-results
```

- Python: 2026-11-07T10:12:43+00:00 through 2026-11-07T10:12:44+00:00; exit **0**. Full unittest output is in `python.stderr.txt`; `python.stdout.txt` is empty.
- .NET: 2026-11-07T10:12:44+00:00 through 2026-11-07T10:13:19+00:00; exit **0**, not a timeout. Full build/test stdout is in `dotnet.stdout.txt`; `dotnet.stderr.txt` is empty. Command strings, starts, completions and exit files are retained separately.
- Environment: machine `FA780`; Ubuntu 26.04.1 LTS; Linux `6.18.54.1-microsoft-standard-WSL2`, x86_64; Bash `5.3.9(1)-release`; effective UID/GID `1000:1000`; `/usr/bin/python3` version `3.14.4`; `/usr/bin/dotnet` SDK `10.0.114`; Git, timeout and sha256sum available at `/usr/bin/`. Source filesystem is `v9fs`, mounted from the same Windows worktree. Full bounded environment evidence is in `linux-environment.stdout.txt`; its stderr is empty, exit 0. Windows manifest collection used machine `FA780`, OS `Windows-11-10.0.22631-SP0`, Python `3.14.7 (tags/v3.14.7:823f032, Aug  5 2026, 10:51:32) [MSC v.1944 64 bit (AMD64)]`.
- Linux read-only Git used explicit `--git-dir=/mnt/c/Users/donabe/repos/Duck/.git/worktrees/Duck-issue51-aspire-006g1 --work-tree="$repo"` to resolve the Windows worktree's metadata. Observed branch and HEAD match the target. No Git settings were changed.
- Native Linux `sha256sum --check` verified all 13 implementation-manifest inputs before Python, between commands, and after .NET. Windows before/after manifests also compare all source/test and preserved evidence content.

## Files

- Target: `ibis-ssl/Duck` #51 / PR #117, branch `task/issue51-aspire-006g1`, baseline and final technical HEAD `b2fd92ec7dae7e913c22714920001066e72c789a`, dirty current-task implementation. Base ref was not re-resolved; no CI evidence was queried.
- Evidence directory, Windows: `C:\Users\donabe\Documents\Codex\2026-11-07\task\issue51-green-verification-evidence`; native Linux: `/mnt/c/Users/donabe/Documents/Codex/2026-11-07/task/issue51-green-verification-evidence`.
- Ignored build output: `<repo>/artifacts/issue51-green-verification-20261107` (confirmed by `git check-ignore` before execution). Keeping it within the repository allowed AppHost root discovery; no source layout workaround or source edit was needed.
- TRX: `dotnet-results/issue51-linux-bind-source-capture-green.trx` beneath the evidence directory; SHA-256 `c9ae6d17f870e98442176542d03eeebe50379b93eee866e42da4d009fe7dbbc3`. Parsed totals, test-group counts, skip reasons and warning occurrences are retained in `result-summary.json`.
- Identity/preservation evidence: `manifest-before.json`, `manifest-after.json`, `source-manifest.sha256`, `linux-source-before.txt`, `linux-source-between.txt`, `linux-source-after.txt`, and before/after Git status. Exact-source manifest SHA-256: `9db78d4e0e6410111e1738a44189ea4c1ef53c01e4683d1a5563cd9687e3c53e`.
- All **13** implementation-manifest files, **141** test input files, **71** preserved repository artifacts/reports (including every file in `run-42356804056-artifacts/`), and **40** retained Red evidence files match before/after. The implementation report and prior Red reports were preserved. Existing ignored Red build outputs were retained and were not used as Green output destinations.
- Only this report's child-owned tail is updated. Its original bytes are retained as `green-report-original.bin`; the complete parent-owned prefix through Dispatch profile is preserved byte-for-byte, SHA-256 `2c643a4fce4c28b9c3ed411321521a373e575ed3c41e1b2d84d1250817244016`. Pre-existing garbled parent-owned text is intentionally preserved.

| Exact implementation/test input | Unchanged SHA-256 |
|---|---|
| `scripts/tests/test_match_referee_source_capture.py` | `69f218e5fb4d122c26da6963221d486f8d1a1c22e84819adbcf5a8b0312ba53f` |
| `scripts/verify_aspire_match_acceptance.py` | `690287f3a956be75a4386236b320edeaea4ccb93d0ab14ac67d4af629b351ee0` |
| `Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs` | `dfdac8f85d7e00cb0dbd769fcbdc185584e67b066f61e5f07bc7d2a022839da8` |
| `Testing/Duck.Testing.AppHost/LinuxMatchBindAccess.cs` | `5e37c575e66c0bf18c53ca0cf3d0cdd5ae00bf0b0fff0b7f3a608cb88ea62e9e` |
| `Testing/Duck.Testing.AppHost/MatchParticipantStatusProjection.cs` | `afd0d4eca2bd4d2f81c073349ba9b24b886e077c9b8f5112137d8113a38da09f` |
| `Testing/Duck.Testing.AppHost/Program.cs` | `8ab7e8994929f1679f5fae1f4ce1bda1b5266441ef118796409261494b0cd18f` |
| `Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs` | `012c5dd34843ba8c92082a85a72468cca2040d1aaeea256dc44e5c2b0ab2bd40` |
| `Testing/Duck.Testing.RefereeDriver/RefereeSourceCapture.cs` | `ba8a74a7b2b0c68db9493d900e3854cd6e3668219c12a5b3b8af66e88586d4db` |
| `Testing/Duck.Testing.RefereeDriver.Host/MatchControllerWorker.cs` | `34b7b2ffbdb1616bef8f05cb3c6a2cb40f37eda1f9424bdd51ff572afbeb8e26` |
| `Testing/Duck.Testing.RefereeDriver.Host/RefereeDriverApplication.cs` | `193068a22ccedbcda4236e216e0c451fee6e623740eda081dee735f8c9bfd901` |
| `Tracker/Tracker.Tests/MatchLinuxBindPermissionsTests.cs` | `079ffac4ffc0b84e4bf7005ceb9b873de4ee9a8f57be03b408d55ebcb6ae5dc4` |
| `Tracker/Tracker.Tests/RefereeSenderTupleCaptureTests.cs` | `3b5b99139f7f23c6fa09bf83277179df10f5e91fd0f2bcfefc2b78611f87258e` |
| `Tracker/Tracker.Tests/RefereeSourceCaptureLifecycleTests.cs` | `00a78f2650dd70da93d7ee0a4ae99f76325f5690590d1c7f4cc62d98e3b38582` |

## Handoff

- The bounded independent native Python and Linux .NET results support **Green for the specified tests on the exact dirty source manifest**. They do not constitute an independent code-review verdict or the full local equivalence gate.
- Native Linux execution reached permission assertions and source-capture lifecycle regressions. The newly added real UDP, identity/epoch reset, incomplete-abort and stale pending-receive regressions passed. These results supersede no historical evidence; prior Red and its initial output-layout harness failure remain retained.
- Live Linux container bind-write acceptance and real match source-capture acceptance remain separate. No Docker/AppHost resources were launched by the verification command. No real publishing process/container attribution, full-match completion, POST_GAME or MATCH-003/004/005 result is established.
- Next required action belongs to the parent: assess the retained focused Green evidence and arrange the separately authorized Linux runtime acceptance and independent review.

## Results

- Verification capability: `local_execution_available`, demonstrated through approved WSL2 native Python/.NET execution and retained successful test evidence. Execution environment is runtime-local Windows plus WSL2 on the same machine/source, not a remote CI substitute.
- Python: **10 tests passed, 0 failures, exit 0**, including source-capture verifier cases and existing match-acceptance diagnostic/failure tests.
- .NET: **115 total, 113 passed, 0 failed, 2 skipped, exit 0**. TRX reports 113 executed and 2 not executed. The two skips are explicitly Windows-only: `AtomicStatusWriteRecoversAfterBriefWindowsReaderLock` and `AtomicStatusWriteDoesNotWaitForeverForPersistentWindowsReaderLock`; reason: `Exercises Windows file replacement sharing semantics.`

| Native .NET class | Passed | Skipped |
|---|---:|---:|
| `Tracker.Tests.MatchControllerWorkerTests` | 11 | 0 |
| `Tracker.Tests.MatchLinuxBindPermissionsTests` | 8 | 0 |
| `Tracker.Tests.MatchParticipantStatusProjectionTests` | 13 | 2 |
| `Tracker.Tests.RefereeDriverHostAutoDriveTests` | 16 | 0 |
| `Tracker.Tests.RefereeDriverHostCompositionTests` | 16 | 0 |
| `Tracker.Tests.RefereeDriverHostHealthTests` | 18 | 0 |
| `Tracker.Tests.RefereeReceiveCacheRaceTests` | 1 | 0 |
| `Tracker.Tests.RefereeReceiveCancellationTests` | 1 | 0 |
| `Tracker.Tests.RefereeReceiveDiagnosticsTests` | 1 | 0 |
| `Tracker.Tests.RefereeReceiveFailureTests` | 4 | 0 |
| `Tracker.Tests.RefereeReceiveReadinessBoundaryTests` | 2 | 0 |
| `Tracker.Tests.RefereeReceiveRestartTests` | 2 | 0 |
| `Tracker.Tests.RefereeReceiveSingleRunTests` | 1 | 0 |
| `Tracker.Tests.RefereeReceiveSubscriberRegressionTests` | 5 | 0 |
| `Tracker.Tests.RefereeReceiveSubscriberTests` | 1 | 0 |
| `Tracker.Tests.RefereeReceiveUdpLifecycleTests` | 2 | 0 |
| `Tracker.Tests.RefereeReceiveUdpSettingsAndDisposalTests` | 7 | 0 |
| `Tracker.Tests.RefereeSenderTupleCaptureTests` | 1 | 0 |
| `Tracker.Tests.RefereeSourceCaptureLifecycleTests` | 3 | 0 |

- Source/test identity is unchanged through execution. Before/after HEAD, branch, manifests and Git status match. A preservation script invoked from the task directory initially hit Git's dubious-ownership guard (exit 128 for its Git subprocess); the same read-only script succeeded from the original repository working directory. No safe.directory setting or environment change was made, and no tests were retried.
- No denied, pending or uncertain action remains. Both formal WSL requests were granted and completed. No dependency installation command, environment-setting/certificate modification, source/test/workflow edit, GitHub Actions invocation, issue/PR/comment/commit/push/merge action, or nested delegation occurred.
- Standard `dotnet test` restore/build ran as part of the planned command; no SDK/tool/package-install command was issued. No first-run/certificate-creation output appeared in this run.
- Full local equivalence gate: not started. Live container acceptance: not run. Commit/push: unauthorized/not performed. CI wait/current-HEAD CI success: not obtained in this phase. Applied model/reasoning profile remains unobservable; requested profile and parent-owned Dispatch profile are unchanged.
- Persistence: normal verification report at the reserved path `reports/issue51-linux-bind-source-capture-green-verification-20261107.md`, without commit or publication. Used repository work-context-manager, implementation-worker, report-output-manager and report-writer instructions within the supplied verification-only boundary.

## Risks

- Permission fixtures execute on WSL2's Windows-backed `v9fs` source/build filesystem. Passing targeted permission assertions here does not establish real Linux container image-user access, bind writes or setgid inheritance on the eventual Linux acceptance host.
- Python captures are synthetic, while UDP lifecycle tests use bounded test traffic. Successful exact-source checks do not prove a unique sender throughout an actual match, traffic beyond the capture window, or publishing process/container identity.
- Build/analyzer warning occurrences: `ASPIRE010`: 1, `CS0618`: 13, `CS8602`: 1, `CS8604`: 2, `NU1902`: 4, `xUnit2012`: 1, `xUnit2031`: 11. Full diagnostics are retained in stdout, including existing MessagePack `NU1902`, Aspire `ASPIRE010`, compiler and xUnit analyzer warnings. They did not prevent compilation or tests; warning remediation was outside this verification scope.
- Broader regression testing, native Windows-only sharing tests, the full repository gate, independent review and live Linux acceptance remain unexecuted here. No unsupported executor or dependency blocker remains for these two completed bounded commands.
