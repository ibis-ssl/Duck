# Issue #82 controlled partial-start failure cleanup

## Result

One Windows / Docker Desktop run of `Tracker.Tests.AspireFullStackAcceptanceTests.CraneStartupExitCleansOnlyItsContainersAndPreservesOtherOwner` passed. This verifies one deliberately induced Crane process failure during an otherwise-started base stack; it does not establish cleanup for arbitrary unexpected failures or restart cleanup.

- Repository branch: `task/aspire-docker-desktop-496`
- Repository HEAD at test invocation: `d2076ec132d55c4b00cdcfb4bfacfbdd4869e523`
- The test-source change was uncommitted at invocation and is the tested working-tree version; test source SHA-256: `7D31D4955EB9802B9D819F1D727C36FFC4CB3144046D44865C3BF0DA3638D0A0`
- TRX: `%TEMP%\duck82-failure-cleanup-trx-final\issue82-partial-failure-cleanup-final.trx`
- Test start/end: `2026-11-01T03:37:43.2521334Z` / `2026-11-01T03:38:01.1389379Z`; test elapsed `17.8872077s`; outer `dotnet test` completed at `2026-11-01T03:38:01.2697266Z`, exit code 0 (1 passed, 0 failed, 0 skipped).
- Engine: `desktop-linux`, ID `5bc5ab8a-7340-47ba-8b7c-9f645375a54c`.

## Observed input and outcome

The test changes only the test builder's Crane entrypoint/arguments to `/bin/bash -c 'exit 82'` before `BuildAsync`; the image, dependency graph, ownership labels, network, and health checks remain the existing configuration. It creates a separate sentinel before AppHost build using the existing local Crane image, `--network none`, and no published ports.

Preflight inspected all five required local images successfully, recorded zero baseline containers, and reported `pull=none`. AppHost startup completed normally; the intended failure was proven separately from AppHost startup outcome. Exactly one Crane container was observed with full ID `3f27fd1bc3a74bac92cf7acdc1b995f2182d985ed6406d395c01435017006007`, Docker state `exited`, exit code `82`, and `Running=false`. The test asserted exactly one Crane `create`, `start`, and `die` event. The other four stack resources were each Running before Stop/Dispose:

- simulator: `578c9c4a42630e9c16b49e479cfa8d81193f628a3a7aca671ba7149ebf747104`
- game-controller: `5adc71cfc0ca1e8dacf0cd24691eb9c7d148e1e519c9fef5a285a17702d14646`
- cm4-sim: `e7628bbea66c56d0768c0dbd8c3095de1dd4c757ec45d5448ec92cf0987bce70`
- duck: `8596f2a3c559c65c8734272394dae58ce29875fab890d9953aa3a7bedb40fcfb`

After AppHost Stop/Dispose and before rescue cleanup, the test observed zero containers owned by this stack and verified that the same sentinel full ID `db174380e9f4fd9e79ea9980eae304f8b0f72d69b8931c998d051676028cc6e3` was still Running. It then removed only that sentinel by its exact ownership labels; final stack and sentinel owner inventories were both zero. The pre-run baseline inventory was empty and its IDs (none) remained present after cleanup. The test's cleanup assertions passed.

The existing local Duck build produced tag `duck:3a14706c24bc206108431547589044faff016b62`; its existing base runtime layer was reported cached. The generated Duck image was retained. The test log shows the RuntimeHost's existing localhost TCP diagnostic listener; therefore this report does not claim that no TCP port was published. No UDP host port was published by the sentinel; no new external UDP exposure was configured.

## Scope and remaining checks

This one controlled nonzero Crane exit is evidence for cleanup of the other DCP-owned resources after a partial start and for preserving a distinct owner. The test does not prove every unexpected failure mode or restart cleanup. It does not add a Linux execution result. Existing NET-003, normal start/stop, same-lock protection, and startup-cancellation evidence remain separate. Per-datagram sender tuple/interface and other original Issue #82 items remain as recorded in `Tracker/Design/tasks-status.md`.

Earlier test-only attempts were not successful trials: the first stopped before Crane appeared, the second treated normal `StartAsync` completion as a polling boundary, and the third appended rather than replaced Crane args (exit 126). The final run follows those identified test-harness corrections; it is the only passing controlled-failure observation in this report.