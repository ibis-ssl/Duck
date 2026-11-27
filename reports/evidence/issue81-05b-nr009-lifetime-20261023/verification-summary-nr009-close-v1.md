# NR009 lifetime evidence closeout

Source state for Red: Protocol 44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18f; fixture 30a22fe1c8031f36ea8aec66951555999e0efa7f74d9986fc71dae95f1f9005d. `red-v2`: 2 passed/1 failed (late-fault observation), exit 1. UDS release and child-process setup in same fixture passed after harness correction.

Source state for Green/full/build: Protocol 94bcc52414e0802cf049f80427145d663825f801a11d5f88e0ecf0eb2fd57e21; Program 4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117b; fixture 30a22fe1c8031f36ea8aec66951555999e0efa7f74d9986fc71dae95f1f9005d. `green-v1` 3/3, child-process focused 1/1, unfiltered suite 808/808; all exit 0, skip 0. RuntimeHost build exit 0, 0 warnings/errors; AppHost build exit 0, one existing ASPIRE010 warning and 0 errors.

`child-process-diagnostic-v1` is setup failure only: launching test-bin copied DLL exited 134 because Microsoft.Extensions.Hosting could not resolve. `child-process-project-output-v2` uses the actual project output plus runtimeconfig/deps and passed after the expected 30-second idle deadline with child code 2. No container/DCP operation occurred.

Every test/build was waited to terminal exit. Detailed command, raw output, and hashes are stored by generation in sibling directories. This summary describes local current-source verification only; not PR CI or ordinary review closure.
