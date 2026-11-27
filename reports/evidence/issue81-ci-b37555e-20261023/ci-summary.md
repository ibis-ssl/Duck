# b37555e exact PR CI

- PR #84: https://github.com/ibis-ssl/Duck/pull/84; workflow `.NET tests`; run `39887015100`, event `pull_request`, attempt 1, completed success.
- Head `b37555e27ba82f9b78b1e6a4ccb754531419786c`; base `88cb20f2b5cea46eb90bff80ba8d535639625de0`; checkout merge SHA `eb31335` (run log: `HEAD is now at eb31335 Merge b37555e... into 88cb20f...`).
- Standard `dotnet test`: 776 passed, 0 failed, 0 skipped.
- Three managed Linux publish outputs passed: RuntimeHost, DebugHost, RefereeDriver.Host.
- PDB verifier: 10/10 tests passed (seven existing plus three fixture cases). Actual DebugHost PDB has matching full CodeView GUID16 + Stamp4 identity and all 112 documents under `/src/`.
- `ASPIRE-NET-002 Linux packet flow` and `ASPIRE full-stack Linux acceptance` were skipped. Failure diagnostics/upload steps were skipped on success; artifacts API total_count is 0. No container launch or debugger attach was run by this workflow.
- Existing `gh run watch 39887015100 --repo ibis-ssl/Duck --exit-status` session completed exit 0; no rerun.
