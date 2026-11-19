# Duck #128 / PR #136: multicast boundary investigation (2026-11-17)

## Outcome

The current host-network run proves that Simulator Vision reaches a Linux consumer, while the Windows UDP socket receives no Simulator datagram. The same Windows socket successfully receives a local multicast control before and after the run. The remaining fault interval is between Linux multicast delivery/egress and Windows application delivery. **VM egress, Desktop backend ingress/egress, and Windows ingress have not been separately captured in this run, so the exact dropping component is still unresolved.**

This investigation does not implement a relay or promote the existing localhost-only candidate to a complete solution.

## Identity, scope and preservation

- Machine/user: FA780 / `fa780\donabe`; PowerShell through the officially reviewed `exec_command(sandbox_permissions=require_escalated)` path. The default executor failed to launch its non-elevated sandbox helper with access denied; no sandbox repair or bypass was attempted. Desktop Commander reported no online device and was not used to execute commands.
- Existing worktree: `C:\Users\donabe\Documents\Codex\2026-11-15\task-9\Duck-PR136-FA780`.
- Branch: `task/issue-128-cm4-compose-restacked`; HEAD `844a7fb6d1079080e625c690489296fd1e8333ab`.
- GitHub readback: PR #136 OPEN, same head SHA and branch, base `design/issue-128-cm4-shared-namespace-restacked`.
- Scope: existing records/source review plus one distinct bounded observation adding Linux receiver evidence and Windows local multicast controls. No clone/worktree, build, pull, image/tag modification, product/test edit, network configuration change, firewall action, new dependency, helper/relay, commit, push, PR comment, or merge. RDMCP-runtime and RemoteDesktopMCP-data were not accessed.
- Existing two changed source/test files and four untracked reports were preserved byte-for-byte. This report is the only repository addition by this investigation.
- Verification capability: `local_execution_available`, demonstrated by reviewed normal-user PowerShell and existing Docker CLI. This is network investigation evidence, not full-stack acceptance or an independent review.

## Instructions and governing requirements

The repository-root `AGENTS.md` was absent. `.agents/skills` points to `..\..\CodexSkill\skills`, which did not resolve here. Read the actual `C:\Users\donabe\Documents\Codex\CodexSkill\skills\development-orchestrator\SKILL.md`, `work-context-manager/SKILL.md`, and `report-writer/SKILL.md`. CodexSkill was clean on main; local HEAD and read-only remote main both resolved to `8ee6e9f80a0362a4d066c4ea39fd3e87002fc829`. No skill update was necessary. This is the delegated investigation worker, not a new parent implementation lifecycle; no further agents were dispatched.

The user's current instruction prioritizes multicast investigation over a relay. The design `Tracker/Design/Testing/aspire-simulation-test-environment.md:149` requires fresh Vision for the selected consumers; its shared-namespace candidate at line 513 explicitly leaves Windows/Docker UDP and multicast fanout unverified. Native Windows RuntimeHost, fixed images and preservation of all required consumers remain requirements. TDD/product tests and publication gates are not applicable to this report-only investigation; the existing localhost candidate remains incomplete.

## Existing evidence reassessment

1. Read `reports/issue128-host-network-comparison-20261117.md` and the three existing UDP reports. Host networking + multicast previously gave Windows receive 0. Localhost unicast alone decoded 502 packets/HTTP200; GC addition stopped reception, removal restored it. These are separate multicast and unicast findings.
2. Read the retained VM diagnostic ZIP and `duck128-vm-investigation-20261117.json`, `duck128-offline-followup-20261117.json`, `duck128-sender-record-search-20261117.json`. Earlier bridge-mode sender “16 datagrams / 32 fragments / TTL1” is **secondary reported evidence**: its primary tcpdump output was not recovered, and overlap with the Windows ETL was not independently established. Do not describe it as a newly verified simultaneous capture.
3. Retained Pktmon ETL covers 2026-11-16T22:31:39.4959598Z–22:31:41.6949834Z; offline analysis recorded 0 packet events and 0 lost events/buffers. Capture component coverage and sender overlap remain limitations. No new Pktmon capture was started.
4. The old VM snapshot had `mc_forwarding=0`, empty multicast routes, and host networking disabled at that earlier time. It is not a live-state explanation of the later enabled-host-network run. TTL1 and absent multicast routing could matter for bridge-to-VM forwarding, but causality was not tested. A failed host-mode experiment, which bypasses the per-project bridge, cannot be explained solely by that bridge.
5. Docker Desktop CLI said `stopped`, but `docker --context desktop-linux info` returned engine 29.9.1 and Docker Desktop, and subsequent runs worked. VM logs show daemon/dataplane initialization at 11:38:39–40Z during these checks. Treat the CLI status as insufficient to establish engine unavailability. No explicit restart command was issued in this investigation.

## New bounded observation

Run ID: `duck136-mcast-boundary-20261117T114052Z`.

Primary evidence directory: `%TEMP%\duck136-mcast-boundary-20261117T114052Z`.
Script: `%TEMP%\duck136-boundary-probe-20261117.ps1`.

Only the existing Simulator `ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd` and GC `robocupssl/ssl-game-controller:3.20.3` images were used, with `--pull=never --network host`. No AppHost or RuntimeHost process was started. CM4/Crane were not needed for this discriminating check; this is not a four-image acceptance run.

Simulator command, confirmed by inspect:

```text
./bin/simulator-cli -g 2020B --realism None --ibis-port 12346 --ibis-team-color yellow --ibis-feedback-addr 127.0.0.1
```

No `--localhost` argument. GC used `-visionAddress 224.5.23.2:10020`. Its other settings were image defaults; it listened for Tracker on 10010 in this isolated probe. This is not the full-stack configuration.

Windows used a temporary IPv4 `UdpClient`, wildcard 10020 bind, `ExclusiveAddressUse=false`, `ReuseAddress=true`, matching the relevant setup in `RuntimeVisionReceiverService.cs:210`. It successfully joined 224.5.23.2 on 172.24.96.1, 192.168.1.103, 192.168.1.148, 100.127.51.85 and 127.0.0.1. Before/after controls sent one uniquely marked datagram to the same group/port through Windows loopback; controls were counted separately from Simulator traffic.

| Phase (UTC) | Observation |
| --- | --- |
| 11:40:52.9127585–11:40:53.7116975 | Windows local multicast control: 1 marker received |
| 11:40:53.9461214–11:40:57.9627182 | Simulator alone: Windows 0 datagrams |
| 11:40:58.2895065–11:41:02.3276730 | Simulator + Linux GC: Windows 0 datagrams |
| 11:40:58.588628634 | GC logs decoded geometry change from 12×9 to Simulator 2020B 9×6 field |
| 11:41:02.5877138–11:41:03.3584863 | Same Windows socket local multicast control: 1 marker received |

The geometry update is evidence of actual Linux application reception, beyond listener startup. It occurs inside the Windows observation phase. It does not provide an exact Vision packet count.

## Linux and Desktop boundary observations

`docker exec` only read existing `/proc`/sysctl state inside the Simulator's host namespace, before and after GC addition:

- Namespace `net:[4026532223]`, Linux address `192.168.65.3`; default route on eth0 via 192.168.65.1. This is the Linux daemon/VM namespace, not the Windows NIC namespace.
- Before GC, `/proc/net/igmp` had no 224.5.23.2 membership, even though Windows had already joined. After GC, eth0 had `021705E0` (224.5.23.2), Users=2. Windows memberships are not reflected as native shared Linux memberships in this observation; absence before GC is not itself a packet-drop counter.
- `mc_forwarding` for all/eth0 remained 0; `/proc/net/ip_mr_cache` empty. These are facts, not proof that changing forwarding would fix Desktop proxying. Locally originated host-namespace multicast does not require ordinary multicast forwarding between Linux interfaces.
- Linux UDP InDatagrams increased 3→243; eth0 transmit packets 8497→15593; FragOKs 247→519. These are namespace-wide counters including Simulator feedback and GC traffic, **not** Vision-specific egress proof or a count of Vision fragments.
- Preserved 12,927 dataplane and 45 hostnetwork log lines within the overall observation interval. They trace loopback traffic and bind/unbind activity; none mentions 224.5.23.2. Logging coverage for multicast is unknown, so this absence cannot locate the drop.
- Backend logs explicitly register `localhost fwding - udp/0.0.0.0:10020` for the GC listener around 11:40:58.590–596Z. This shows automatic UDP port forwarding, not group membership propagation or multicast fanout.
- One hostnetwork error at 11:40:58.325872021Z concerns forwarding TCP 127.0.0.1:8081, already in use on Windows. GC nevertheless decoded Vision. No observed 10020 bind error was found. The UI-forward conflict is recorded, but is not an established cause of the multicast failure and was not repaired.

Docker documents that Desktop uses a Linux VM and a Windows backend network proxy; outbound traffic is translated to host networking operations. Desktop host networking operates at layer 4 and does not grant containers direct access to Windows host interfaces. These explain why `network_mode: host` cannot be treated as native Windows NIC sharing, but do not independently prove that multicast is impossible:

- https://docs.docker.com/desktop/features/networking/
- https://docs.docker.com/engine/network/drivers/host/

## Boundary verdict and next discriminating step

| Boundary | Current evidence | Verdict |
| --- | --- | --- |
| Simulator → Linux Vision consumer | Same-run GC geometry decode | Working in this host namespace |
| Linux network stack → VM eth0/backend ingress | Route and broad counters only; no Vision-specific frame/trace | Unresolved |
| Desktop backend → Windows network ingress | Port-forward control logs only; no synchronized packet capture | Unresolved |
| Windows ingress → temporary UDP socket | No Simulator datagrams; ingress not captured | Unresolved |
| Windows local multicast → same socket | Marked controls received before/after | Working for local loopback; does not prove external/backend firewall permission |

Strongest supported inference: the problem concerns the cross-OS multicast path rather than a universal Simulator generation failure or universal Windows multicast socket failure. **It is not yet defensible to assign the fault specifically to Docker's backend or Windows firewall.** Host networking did not remove this boundary. The old unicast GC contention is not sufficient to explain the new multicast-alone result.

The next genuinely discriminating observation would record Vision at the daemon namespace's eth0/backend handoff and Windows ingress in the same bounded interval, saving the raw records before cleanup. Capture both IPv4 fragments by group/protocol, not only UDP port, and align clocks/causal ordering. A permitted observation method and exact scope for the VM/backend point must be established first: existing Simulator/CM4 and VM checks found no tcpdump, and the earlier Alpine permission was scoped to the CM4 namespace, not an unrestricted VM-wide capture. Do not introduce privileged observers, new images/dependencies, broad VM capture, configuration changes, or a relay as an implied next action. If that observation cannot be authorized/supported, report this interval as unresolved rather than rerunning the same reception experiment.

## Evidence identity and cleanup

All Docker commands and their start/end/exit codes are in `commands.jsonl`; the run completed as `observed`. Ownership labels were verified before stopping/removing exactly the two newly created containers. Both removed successfully; final running container output was empty. Both Windows sockets were disposed. No persistent child process was created. No existing stopped container was removed. Docker remains available with the previously enabled host-network setting untouched.

Key SHA256 values (paths relative to the primary evidence directory):

| File | SHA256 |
| --- | --- |
| result.json | 439A57ECE6CFCDBA97F4350C0B26670CAD3ACD9314ADB7318511AF56D003115B |
| gc-logs.txt | C94C19195B7B4AF4628665589E3EC245D6EF11C23033593052D862C43E758306 |
| linux-before-gc.txt | 9D1C5B6C87C998B0DD34D1803BC822F74B81C74BF6DA04DA893507D0B0050874 |
| linux-with-gc.txt | 40599F56913A99A213EB4FE3519DF5E66DA515EFBB48FF6FCD4BA7D13D420ED6 |
| backend-network-window.txt | 58DAD6218CD592B3B5F0F2209B2132236D43D78827FBA479F1C94FDFCA2A9FBD |
| vm-network-window.jsonl | 108320D2D3CEC38AB2DAFDCD21208518A837DAE41AA0C6139C2631F55D423F73 |
| commands.jsonl | B23EB4F5C3C8CD12173939DA6961A09523816DFFCCB9907937C604FE10803407 |

Pre-existing source/report hashes were checked before and after this work:

- `BaseSimulationResourceRegistration.cs`: E7B26582209F5BAAC2A6F09B062310DA451AF0E91310FBEC7F1A3BBD08E1EE38
- `AppHostApplicationModelTests.cs`: B6A96E176DD6A9C6824EA5DA7EBAF097EEF83C2AA76829E24D9FE0F722981B79
- `issue128-host-network-comparison-20261117.md`: B52B7EA4A79D95AE1F2D5AA57E329629EE64D72C25B38B7EBEA18D5D02F7F503
- `issue128-udp02-runtime-confirmation-20261116.md`: 2F84E59ACF45FFF2EACBC2EC6FE563889FCA846AA43B888D4684080710D55323
- `issue128-udp02-runtimehost-listener-health-20261115.md`: 550893970FAA032767D57F5030983FE52A265AD020A6A1B543A84B75819D51EB
- `issue128-udp02-simulator-vision-start-condition-20261115.md`: F043A1E1F10D112CFB6167A0B318795BEAF26F213BB96B93106632180A734E00

Commit/push/CI wait: not required for this bounded investigation; not performed. Existing localhost changes remain uncommitted and unpushed. Overall Issue #128 acceptance, all-consumer fanout, Tracker output, CM4 feedback and lifecycle remain unresolved. No merge was performed.
