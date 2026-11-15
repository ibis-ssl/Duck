# Issue #128 — helper-free topology status

**Status: candidate only.** No helper or relay has been removed from product code, and no Windows/Docker packet route has been accepted. The diagrams.net figure [issue-128-host-runtimehost-topology-20261115.dio.svg](issue-128-host-runtimehost-topology-20261115.dio.svg) shows the candidate topology and marks unresolved paths. Its editable source is [issue-128-host-runtimehost-topology-20261115.drawio](issue-128-host-runtimehost-topology-20261115.drawio).

## Namespace candidate: CM4 owner, Simulator and Crane followers

The fixed CM4 contract and current AppHost settings statically suggest a narrow candidate:

- `cm4-sim` listens for Crane command UDP at loopback `127.0.0.1:12345`; `Crane` launch XML defaults its sim target to the same address and port. Current `CraneLaunchOverlay` rewrites that target because Crane normally has a separate container network namespace.
- CM4 sends its command output to `127.0.0.1:12346`; the Simulator listens on the configured Ibis port `12346`. Sharing the namespace could make this loopback path direct.
- CM4 feedback sockets bind `127.0.0.1:50100+id`. AppHost currently passes the CM4 container IPv4 to Simulator as `--ibis-feedback-addr`. A shared namespace candidate would need Simulator feedback addressed to `127.0.0.1` for the CM4 loopback bind to receive it; this combination is not packet-tested.

Thus putting all three containers in the CM4-owned namespace could, in principle, make the three existing loopback endpoints line up and remove the C helper and Crane target rewrite for that profile. This is only static endpoint alignment. AppHost does not currently register this topology, and no product code change was made.

### Aspire/DCP evidence

I81-07D exercised a dependent container with runtime `--network container:<owner>` and recorded that the follower failed to start. The Docker creation error was not captured, so the failure cause remains unknown. I81-07E used a standard bridge without the shared-network argument; its success is not evidence that namespace sharing works. Do not infer Aspire/DCP support from Docker Engine's general namespace capability.

Unresolved for this candidate: DCP model composition and startup, container ownership/identity, health endpoints, CM4 owner recreation, Simulator and Crane follower recreation, feedback destination configuration, and multicast visibility to other bridge resources. The minimum next evidence is a specifically authorized DCP run on the supported Windows Docker Desktop environment that records sanitized create/start diagnostics, `HostConfig.NetworkMode` for each exact owned container generation, and bounded cleanup. No such run was performed here.

## Windows RuntimeHost has a separate unresolved boundary

The namespace candidate addresses only CM4/Simulator/Crane loopback traffic. `Tracker.RuntimeHost` as a Windows process is outside Docker Desktop's Linux bridge. Current profiles use Vision multicast `224.5.23.2:10020`; TrackerWrapperPacket consumers use `224.5.23.2:11010`. Without a boundary adapter, neither Windows ingress nor return multicast/fan-out has been demonstrated. Removing the current UDP boundary relay would leave those paths unsupported until a direct endpoint path is proven.

Docker Desktop host networking status is unknown: the caller could not read an enabling setting from the settings store, and `docker info` does not report its enabled state. Official Docker documentation describes Desktop host networking as an opt-in feature (Desktop 4.34+) for layer-4 TCP/UDP; it does not establish multicast group membership or this AppHost topology. No setting was changed and no host-network test was run. See [Docker host network driver](https://docs.docker.com/engine/network/drivers/host/).

## Implemented selectable profiles and product names

These are distinct source-code profiles, not resources shown as running together with the current Windows-host trial:

- **base:** `simulator`, Game Controller, `crane`, Duck RuntimeHost; `visibility_graph` selects `cm4-sim`. The current host trial only accepts `Testing:Mode=base` and, as currently implemented, still has C-helper and UDP-relay dependencies.
- **comparison:** includes the `tigersmannheim/sumatra` product (TIGERs Sumatra), `tracker-tigers`, ER-Force tracker, and DebugHost.
- **match:** includes `tigers-blue` (TIGERs Sumatra), `autoref-tigers` (TIGERs AutoRef), SSL log recorder, and match controller; its host RuntimeHost combination is not supported by the current trial.

The figure separates the AppHost/Aspire control plane (resource registration, dependencies, health and recreation) from RuntimeHost/UDP data paths. Dashed red arrows mean unresolved; they are not demonstrated connections. The C helper, .NET boundary relay and Crane launch overlay appear only in a note identifying current code dependencies, not as target resources.

## Staged PR status and verification

- **PR-A #130** is a Draft based on `main`, branch `task/issue-128-launcher-dotnet`, head `61a31c55cd6d988efd0722990b935191975dc80d`. It only replaces the PowerShell launcher with a .NET file-based app and updates usage documentation. It intentionally preserves legacy `visibility_graph` behavior and helper preparation. It does not meet the Windows-host RuntimeHost publish/image-reuse goal or the all-helper-free goal. CI run [43677616613](https://github.com/ibis-ssl/Duck/actions/runs/43677616613) passed .NET tests and Linux publish verification; packet/full-stack jobs were skipped, and CI did not compile or run the file-based launcher itself.
- **PR-B work branch** is stacked on PR-A's branch: `task/issue-128-helper-free-topology`, based on `task/issue-128-launcher-dotnet`. Its intended scope is this evidence report and the figure only; no product code diff. PR creation was rejected by the GitHub tool's automatic review, so there is no PR-B number or URL. No retry or alternate route was used.
- **PR #129** is the prior aggregate exploratory Draft; its implementation and validation notes are historical intermediate work, not evidence that the new all-helper-free goal is complete. A focused assertion defect from CI run [43676349049](https://github.com/ibis-ssl/Duck/actions/runs/43676349049), job [132697238716](https://github.com/ibis-ssl/Duck/actions/runs/43676349049/job/132697238716), was corrected in test-only commit `26706eb`. The host-profile model test then passed 1/1 with `source /workspace/.duck-env/env.sh && dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 -p:BuildInParallel=false /nr:false --filter FullyQualifiedName~WindowsHostRuntimeProfileUsesRequiredUdpEndpointsAndDoesNotRegisterDuckImage`; this creates no containers and verifies only the model's resolved static endpoint values.
- A requested PR #129 body update to replace `Closes #128` was rejected by automatic review. Its body is unchanged and still contains that keyword. PR #129 was not merged; Issue #128 remains open. Do not claim a formal Development link was created for PR-A or PR-B; available GitHub tools expose no manual link action, and the PRs use only the non-closing text “Related to #128”.

## Checks and limits

- `git diff --check`: passed on the report/figure commit.
- Inkscape SVG export: exit 0. The SVG's embedded draw.io data was decoded and byte-compared with the `.drawio` source.
- No Docker, FA780 or DCP namespace execution was performed for this update. No pull, image build, image mutation, firewall/Desktop-setting change, issue update, or merge was performed.
