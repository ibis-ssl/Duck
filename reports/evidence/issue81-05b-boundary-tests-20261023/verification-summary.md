# 06A1 NR007–NR009 boundary verification

- Test source SHA-256: 58c304d7d96c7812a816bd8407c316f1ceb58f22539a1572718446f714ab8993
- Existing protocol test source SHA-256: 9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06
- dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=false --filter FullyQualifiedName~RuntimeIdentityControlBoundaryTests: 11/11, exit 0 (oundary-suite-green-v1).
- Same project, no filter: 797/797, skip 0, exit 0 (ull-suite-unfiltered-v1).
- The separate filtered run FullyQualifiedName~Tracker.Tests was 730/730, exit 0; it is a subset and is not the full suite.
- dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj --no-restore -p:EnableSourceControlManagerQueries=false: exit 0 (
untimehost-build-v1).
- dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore -p:EnableSourceControlManagerQueries=false: exit 0, existing ASPIRE010 warning (pphost-build-v1).
- Product source hashes: RuntimeIdentityControlProtocol.cs 5a983432fa7d860eb6421587fbfa76710d9170a2e27bb588b8fb97737f586b37; Program.cs 4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117b.
- No container, Docker CLI connection, DCP, or exec operation was performed. Per-user Docker CLI and desktop-linux context were found by separate read-only inspection only.
- Duplicate-process incident raw records the exact PIDs and termination; the original test exit codes were not recoverable. Current test/build commands all exited.
