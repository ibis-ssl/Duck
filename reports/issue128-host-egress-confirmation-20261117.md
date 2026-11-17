# Duck #128 / PR #136: recovered evidence and host eth0 confirmation, 2026-11-17

## Result

A new synchronized host-network observation directly captured Simulator Vision on the Linux VM host namespace's eth0: **572 IPv4 fragments representing 286 matching two-fragment datagram IDs**, source 192.168.65.3, destination 224.5.23.2:10020, TTL 1. The Windows UDP socket received **0 Simulator datagrams** during the overlapping interval, while local marked multicast controls reached that socket before and after.

The established failure interval is now **after the Linux eth0 observation point and before Windows socket delivery**. This does not distinguish the VM-to-backend handoff, Desktop backend, Windows ingress, or Windows filtering. A Linux capture on eth0 is an observation before the downstream delivery, not proof that the backend accepted or transmitted those frames.

Windows Pktmon recorded no packet events, including no positive-control packets. Its zero result therefore does not establish absence at Windows ingress. Do not assign the fault specifically to Docker's backend, TTL, fragmentation, or Windows Firewall from this evidence.

## Target and recovered state

- Existing worktree: C:\Users\donabe\Documents\Codex\2026-11-15\task-9\Duck-PR136-FA780.
- Actual machine/user: FA780 / fa780\donabe. Normal-user PowerShell through the reviewed require_escalated executor; no CodexSandboxOffline execution.
- Branch: task/issue-128-cm4-compose-restacked; HEAD 844a7fb6d1079080e625c690489296fd1e8333ab.
- Base identity retained from the preceding report: design/issue-128-cm4-shared-namespace-restacked; remote PR not re-read in this recovery check.
- verification_capability: local_execution_available. This is a bounded network investigation, not acceptance, code review, or independent final review.
- The default local executor failed before command launch with orchestrator_helper_launch_failed / access denied. The reviewed normal-user executor subsequently confirmed fa780\donabe. Desktop Commander listed FA780 Offline and was not used to run commands. This was a technical launch failure, not an automatic approval rejection.
- Initially no running Docker containers, dotnet RuntimeHost process, or 10020 UDP endpoint was found. Existing stopped containers were preserved.
- Existing two changed source/test files and five untracked reports were retained. Their SHA256 values match the preceding boundary report/final-check evidence. No clone, worktree, checkout, stash, reset, commit, push, PR comment, or merge.
- Governing current instruction: investigate original multicast before relay implementation. Read the specified host-network comparison and the previously unreported multicast-boundary investigation. Used local work-context-manager and report-writer instructions for scope/evidence reporting; no new parent implementation lifecycle or agent dispatch.

The prior worker's result was present on disk despite its response being unavailable to the parent. All **17 files** in its saved hashes.json match their stored hashes. result.json and gc-logs.txt confirm that at 11:40:58.588628634Z Linux GC decoded the Simulator field geometry inside the Windows zero-reception observation phase. commands.jsonl and cleanup records confirm both prior containers were removed. The existing report itself matches its final-check SHA256 79D4ECC08B72B1D48CF1C8993C32EFECFA06AD8AC3B5140305233EA1A4DF5872.

Recovered primary evidence: %TEMP%\duck136-mcast-boundary-20261117T114052Z. This is predecessor evidence verified now, not a second newly executed GC experiment.

## Authorization and bounded new observation

The original user replies to the Alpine/tcpdump proposal were recovered with user_message_read: 2026-11-16T10:38:17Z, "この対応中は常に許可", and 10:38:39Z, "一時的ではなく常に". The present task authorizes multicast boundary investigation with existing images subject to safety limits. A narrow host-network eth0 observation was submitted through the reviewed executor and executed successfully. No privileged container, additional capabilities, host PID namespace, promiscuous mode, or host filesystem mount was used. No broad VM capture or network/firewall configuration change was performed.

Only the existing images were used with --pull=never:

- alpine@sha256:f59abe656b692d901e184be3445e084786602108599d2a51d229c0f5a48c71bf. Official packages libpcap 1.10.7-r0 and tcpdump 4.99.6-r1 were installed only into the disposable observer container. No image commit or build.
- Simulator ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd, image sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79.

Both used --network host. Simulator used the unchanged multicast argv:

```text
./bin/simulator-cli -g 2020B --realism None --ibis-port 12346 --ibis-team-color yellow --ibis-feedback-addr 127.0.0.1
```

No --localhost, GC, AppHost, RuntimeHost, CM4, Crane, or relay was started. Avoiding GC keeps the existing unicast contention finding separate from this original-multicast observation.

Run: duck136-host-egress-20261117T134516Z.
Primary evidence: %TEMP%\duck136-host-egress-20261117T134516Z.
Reusable exact script: that directory's probe.ps1 (also %TEMP%\duck136-host-egress-probe-20261117.ps1).

Linux capture: timeout -s INT 6 tcpdump -p -i eth0 -s 128 -U -w /tmp/vision.pcap "dst host 224.5.23.2 and ip proto 17". The group/protocol filter includes noninitial IPv4 fragments; no UDP port-only filter.

Windows: temporary IPv4 UdpClient, wildcard10020, reuse enabled, five successful per-address multicast joins. Marked controls were sent via loopback and the local Ethernet address before Simulator startup, and loopback afterward. Pktmon used only the group IP filter, all registered components, 128-byte snapshots, 16MB circular ETL, then removed only this run's named filter. Before this run Pktmon was idle with no filters.

| Observation | UTC interval / evidence | Result |
| --- | --- | --- |
| Windows marked controls before | 13:45:17.2320974-17.5222783 | 3 marker receptions for 2 sent markers; duplicates are not independent sends |
| Windows Simulator observation | 13:45:17.8911730-24.1508583 | 0 non-marker datagrams, 0 bytes |
| Linux stored frames | 13:45:18.031850-22.590911 | 572 frames, 286 initial fragments to UDP10020, 286 matching noninitial fragments |
| Windows marked control after | 13:45:24.1519080-24.3371767 | 1 marker reception |
| Pktmon active interval | approximately 13:45:17.174-24.382 | 0 packet events, EventsLost=0, BuffersLost=0; no positive-control capture |

The Windows ETL text renders in Tokyo Standard Time (22:45); script timestamps and Linux pcap timestamps above are UTC (13:45). Intervals overlap; this is not inferred from separate unsynchronized runs.

Offline parsing of the stored pcap found only source192.168.65.3, destination224.5.23.2, IP protocol17, TTL1, and Ethernet destination01-00-5E-05-17-02. Each of the 286 datagram IDs has offsets0 and1480. Snapshots are truncated to128 bytes: paired fragment headers prove observed fragmentation, not full protobuf decoding. tcpdump reports572 captured,696 received by filter,0 dropped by kernel. The stored record does not include every frame received by the filter; do not treat286 as the complete six-second production count or a zero-loss stream measurement.

## New Windows configuration evidence

A read-only firewall snapshot found enabled inbound UDP/TCP Block rules, Private/Public profiles, all local ports, for:

- C:\Program Files\dotnet\dotnet.exe.
- C:\Users\donabe\AppData\Local\Programs\DockerDesktop\resources\com.docker.backend.exe.

pwsh.exe has enabled inbound Allow rules for those profiles. The Ethernet and Wi-Fi connections were Public; Tailscale was Private. HostNetworkingEnabled remained true. No rule or setting was changed. Firewall logs were not enabled; no actual matching drop record was obtained.

These rules are relevant candidates, not demonstrated packet-drop causes. In particular the temporary receiver runs in allowed pwsh.exe, yet still receives no Simulator traffic; a dotnet-specific block alone cannot explain that probe. Backend inbound rules do not by themselves prove blocking of this outbound-VM path. Local loopback success and previous localhost unicast success do not validate external multicast policy.

Docker's current official documentation confirms Desktop uses a Linux VM and com.docker.backend network proxy, and host mode has layer4 behavior without direct host-interface access. It does not establish that this exact flow is unsupported or identify a dropping component:

- https://docs.docker.com/desktop/features/networking/
- https://docs.docker.com/engine/network/drivers/host/

## Boundary disposition and next distinct action

| Boundary | Disposition |
| --- | --- |
| Simulator generation / local Linux consumer | Predecessor's verified geometry decode; new Vision frames corroborate generation |
| Linux host namespace eth0 observation | Direct new pcap evidence, including both fragments |
| eth0 observation to Desktop backend acceptance/output | Unresolved; no matching backend ingress/output trace |
| Windows ingress to application | Unresolved; Windows capture lacks positive-control validation |
| Windows local marked multicast to same socket | Working before and after; does not prove external multicast ingress |

Next work should validate a Windows observation point with a known packet traversing the same ingress path, then correlate the actual Vision flow through the Desktop backend handoff. Preserve group/protocol filters and fragment coverage. Do not simply repeat the zero-receive experiment, disable Firewall, increase TTL, alter multicast routes, or implement a relay as if causality were proven. A Firewall change would be a separate configuration/security decision and was not attempted or requested in this run. Exact backend observation capability remains the technical blocker; no denied/pending tool operation remains.

The prior --localhost source/test candidate remains incomplete and unpushed. Full all-consumer Vision fanout, Tracker output, CM4 feedback and lifecycle acceptance remain unverified. No product test rerun or CI run was needed for this report-only investigation; it supplies no new full-stack pass.

## Preservation, evidence identity and cleanup

Source/report hashes were compared with prior recorded values and retained in preserved-worktree.json. This report is the only repository addition in this recovery run. No RDMCP-runtime or RemoteDesktopMCP-data access/change, new Python, Docker restart, image pull, persistent service, or network repair.

Ownership labels were verified before removal. New Simulator and observer containers were both removed. Pktmon stopped successfully, its named filter was removed, and final status showed idle/no filters. At 13:47:20Z there were no dotnet processes or UDP10020 endpoints; Docker running-container output was empty. All primary captures were copied out before observer removal.

| Evidence file | SHA256 |
| --- | --- |
| linux-eth0.pcap | B8D011770AE0141E95BD79178B35B814330EF5962D4A066626E267F4A7A8E0AC |
| windows.etl | BD011DC8118B9D47BB2DE2A25B1C097677FBC86A720D5A6E3B0B7F43A5C412A3 |
| result.json | 414FA57CCB67B34BEB69264EC8B89D527BCB84369F755FA86163CA48DFE31416 |
| offline-summary.json | DEFA43A7632E3EB4C27EE9D21DAFDD44E2FC1271F3D9FB3B5FE530C75FE0908A |
| probe.ps1 | 870D98023587B221CC1F7488CD630957CA00A7585ADA3D4303883F2C92DA6FF8 |
| firewall-readonly-snapshot.json | D8101677A809437A9E5EFAF11ADA83C402661CB2663C124A71378D83325A3277 |

Commit/push/CI wait/merge: not required for this bounded investigation; not performed. No subagent dispatch or model switch was requested; the applied model/reasoning profile is not observable from these command results.
