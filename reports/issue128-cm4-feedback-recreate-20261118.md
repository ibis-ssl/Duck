# Duck #128: CM4 feedback と owner 再作成確認（2026-11-18）

## 要件の位置づけ

- Issue #128 の当初の完了条件は、Windows host RuntimeHost と Docker 周辺構成の候補・互換性・未検証事項を比較し、推奨する判断と必要な計測を報告すること。relay-free はこの選択肢比較に含まれるが、Issue 本体の必須条件ではない。
- 後続の `I128-UDP-02` は relay-free の双方向経路を成立させる別の開発目標として記録されている。今回の結果は relay を使う既存 Compose host-runtime 経路の検証であり、これを満たしたとは扱わない。
- Compose host-runtime では Simulator が `--ibis-feedback-addr 127.0.0.1` を使い、CM4 owner、Simulator、Crane が同じ network namespace に入る。したがってこの経路で owner の IP/DNS が変わる必要はない。owner 変更時は AppHost monitor が namespace follower を再作成する。これとは別に、standard DCP resource の Simulator は確認済みの現在の CM4 bridge IPv4 を `--ibis-feedback-addr` に渡す実装と単体テストを持つ。

## 実機条件

- 実行 user `fa780\donabe`、Docker context `desktop-linux`。
- 起動前に稼働中 container がないことを確認。対象固定 image は全てローカルにあり、Compose は `--pull never` を使った。
- 起動: `dotnet run --file scripts/start-duck-simulation.cs -- --host-runtime`。プロセス環境にだけ `Testing__Compose__HostNetworkingEnabled=true` を設定。
- Run/stack ID: `local-20261118T043743Z-7c65bc0b9179473598d0d3b77e9b613a`。
- Compose project: `duck-a92bb143c7b52a0535d4`。

## owner 置換前

- `cm4-sim` container ID: `382cdf079cab3c269eda1fd428959aea9629ea26d77d11661e74b43e301a2d6f`。
- host namespace relay の `gethostbyname("host.docker.internal")` は IPv4 `192.168.65.254` を返した。
- Crane `/robot_feedback` の ROS publisher `robot_receiver_node` が `crane_msgs/msg/RobotFeedbackArray` を発行。`ros2 topic echo --once` で `robot_id=0`, `packet_frequency_hz=125.0`, `valid_packet_count=14308`, `invalid_packet_count=0` を観測した。
- RuntimeHost `/health/vision`: `VisionPacketsReceived=6966`, `SuccessfullyDecodedCount=6966`, `DecodeFailureCount=0`。
- Bridge `duck-tracker-udp-ingress` は container ID `99764fb75e8d1c1db50ffe5f5f82897d361c5e2a328afd60b38e7958753a22a8`、host UDP port `61393`。

## owner 置換後

専用 project の `cm4-sim` だけに `docker compose up --pull never --force-recreate --no-deps --detach cm4-sim` を実行した。その後 AppHost monitor が namespace follower を再作成した。

| service | 置換前 ID prefix | 置換後 ID prefix |
| --- | --- | --- |
| cm4-sim | `382cdf079cab` | `e8e452caf598` |
| simulator | `56f4f0dd6132` | `05819f4c517d` |
| game-controller | `3e093ef6f501` | `52efbf1e8d1e` |
| crane | `37b1ed6a4164` | `b7458a3392bb` |
| duck-udp-boundary-relay | `84957661907b` | `b17694c5b1dc` |
| duck-tracker-udp-ingress | `99764fb75e8d` | `99764fb75e8d` (再作成対象外) |

- 再作成された relay 内で `gethostbyname("host.docker.internal")` を再実行し、IPv4 `192.168.65.254` を得た。Docker Desktop host gateway の値は前後で同じだった。これはプロセス再起動時の再解決を示すが、DNS address change の追従は試していない。
- Crane が再作成後に `/robot_feedback` の ROS message を再び発行。sample は `robot_id=0`, `packet_frequency_hz=125.0`, `valid_packet_count=2101`, `invalid_packet_count=0`。新しい Crane process の feedback receiver が有効データを受けた証拠である。
- RuntimeHost `/health/vision` は `VisionPacketsReceived=9707`, `SuccessfullyDecodedCount=9707`, `DecodeFailureCount=0`。Vision sender port は置換前の `61394` から置換後 `51394` に変わったが受信・decode は続いた。
- Bridge ingress の container ID と UDP host port `61393` は変わらなかった。

## テスト・片付け

- 実行した対象テスト（`dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~ComposeOwnerReplacementRecreatesNamespaceFollowersWithoutPullingImages|FullyQualifiedName~WindowsHostRuntimeProfileUsesRequiredUdpEndpointsAndDoesNotRegisterDuckImage|FullyQualifiedName~StandardSimulatorStartsAfterCm4AndUsesItsCurrentOwnedAddress"`）: **6 passed / 0 failed**。
- AppHost を終了後に専用 Compose project が残存したため、同じ project 名・manifest を指定して `docker compose down --remove-orphans` を実行。専用 project の container 0、Compose project なし、Temp manifest 削除済みを確認した。volume は削除していない。他の project は操作していない。
- Docker image pull、Windows Firewall / network security 変更は行っていない。
- Build/test 時の warning: NU1900 (NuGet vulnerability service への socket access が制限) と既存の ASPIRE010 / analyzer warnings。target tests は成功。

## 結果と残件

Compose host-runtime の CM4 feedback loopback 契約は実データで確認でき、owner 置換時の namespace followers 自動再作成後も Crane feedback と RuntimeHost Vision decode が復帰した。`host.docker.internal` は relay 再生成時に同じ IPv4 へ再解決された。現行 code と targeted owner-replacement test で条件を満たしたため製品コード変更は不要だった。

未検証: Docker Desktop host gateway の IP が変わった時の動的 DNS 再解決、standard DCP resource における CM4 bridge IPv4 の live replacement、TIGERs profile、boundary relay-free 通信。Issue #128 は open のまま、`I128-UDP-02` と helper-free 全体受入は未完了。
