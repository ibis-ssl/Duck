# fa80b23 exact PR CI

- Repository/PR: ibis-ssl/Duck#84, `https://github.com/ibis-ssl/Duck/pull/84`; event `pull_request`; workflow `.NET tests`; run `39885155079`, attempt 1, completed success.
- PR head `fa80b23aa362ed98c4beaf21605eeb5d1b18cbb2`; base `88cb20f2b5cea46eb90bff80ba8d535639625de0`; checkout merge commit `9dcc20c0beb6991543d242dc777bd732f76a291b`, from log line `HEAD is now at 9dcc20c Merge fa80b23... into 88cb20f...`.
- `Run dotnet test`: 776 passed, 0 failed, 0 skipped.
- `Verify Linux application publish outputs`: passed; RuntimeHost, DebugHost, RefereeDriver.Host all published and output contract passed.
- PDB verifier: 10/10 tests passed (seven existing cases plus three real PE/portable-PDB fixture cases). Actual DebugHost PDB identity matches the assembly across full GUID16+Stamp4 identity; all 112 documents are under `/src/`.
- `ASPIRE-NET-002 Linux packet flow` and `ASPIRE full-stack Linux acceptance` were skipped. Failure diagnostics/upload steps were skipped after success; artifacts API reports 0 artifacts. No container launch or runtime attach was performed by this CI workflow.
- The final `gh run watch 39885155079 --repo ibis-ssl/Duck --exit-status` session completed with exit 0. No rerun was issued.
