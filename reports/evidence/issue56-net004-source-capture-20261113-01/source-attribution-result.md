# NET-004 UDP sender attribution, same-run capture

- Run: `issue56-net004-source-20261113-01` (comparison mode; Docker context `desktop-linux`).
- RefereeDriver `/health/referee`: HTTP 200, `isReady=true`, `reason=Matched`; 8,303 decoded datagrams, 0 decode failures at 2026-11-13T16:54:37Z.
- RefereeDriver diagnostic `remoteEndpoint` and `sourceKey`: `172.18.0.2:40294`.
- Same-run Game Controller container ID `0703a59a4dcaa6bc0451c0d905b3c0d4ea858d8c032e43e6d44f61d7e67718fa` had IP `172.18.0.2`; its log says it published referee packets to `224.5.23.1:11003` from `172.18.0.2:40294` at 16:50:58.695Z.
- The health snapshot contained 64 recent referee commands, all with the single source key `172.18.0.2:40294`.
- Conclusion: sampled received datagrams are directly correlated with the Game Controller logged local UDP endpoint; both source IP and source port match. This is runtime evidence for Game Controller attribution in this run. The bounded health snapshot is not a packet capture of every datagram over the full run.
- Other comparison resources present: Tigers (AppHost comparison configuration sets `gameController=false` and `publishRefereeMessages=false`; runtime command includes its custom referee multicast address) and ER-Force (`--gc-port 11003`). The saved source snapshot still observes only the Game Controller endpoint.

Committed evidence files: `source-attribution-summary.json`, `health-referee.json`, `game-controller-relevant-lines.txt`, `tracker-tigers-command.json`, `tracker-erforce-command.json`, and `cleanup-readback.json`.
