# Duck #128 作業記録（2026-11-18）

## 現状

- 作業ブランチは `task/issue-128-cm4-compose-restacked`、HEAD は `2ba4de2`。既存の多数の未コミット差分を保全中。commit / push はまだ実施していない。
- relay-free の標準 session 起動は helper-free Compose に切替済み。固定 CM4 / Simulator / Game Controller / Crane イメージを同一 Compose network と CM4 owner namespace で起動し、Duck RuntimeHost はその network に参加する。
- RuntimeHost Vision 再送機能は削除済み。Tracker の `eth0` multicast publish、Crane `/world_model` の `tracker_detected: true`、CM4 `/robot_feedback` の valid packet 増加を実機で確認済み。owner 再作成後も AppHost と namespace follower が復旧した。
- 直近の対象テストは 12/12 pass。`git diff --check` は通過（改行コード警告のみ）。Docker / dotnet の作業プロセスは現在なし。
- Windows native RuntimeHost から Docker Linux consumers への直 multicast は実測で Vision 受信 0。経路は未解決であり、Firewall / route 変更はしていない。
- `--persistent` も helper-free Compose に切替済み。AppHost 停止後に Compose project / manifest が保持され、同一 project の再利用、stack/run ID 限定の `--stop-persistent` cleanup を実機検証した。Compose/AppHost persistent model・lifecycle テストは 10/10 pass。
- RuntimeHost の publish context に `.cs` / `.csproj` / `.sln` はなく、Duck ソースの bind mount もない。コンテナにはコンパイル済み `Tracker.RuntimeHost.dll` と依存 DLL が入るため、「ソースを入れない」は満たすが、「Duck の実行コードもコンテナに入れない」という強い解釈は満たさない。
- `.agents/skills` は `..\..\CodexSkill\skills` を指すシンボリックリンクだがリンク先が存在せず、development-orchestrator の適用範囲を本文から確認できなかった。リポジトリに `AGENTS.md` は見当たらない。

## 2026-11-18 追加作業: 実測と説明を同期

- Windows native direct multicast の既存測定を再実行せず、受信0件の記録を維持。Helper-free Compose の既存 run で Crane `/world_model` の `tracker_detected=true` / `available_tracker=true` を確認した証拠を、Windows native の成果とは区別して README / work/status docs と Mermaid topology に反映。
- README の古い `HostNetworkingEnabled=true` 手順、default DCP/launcher の補助 binary 説明、host RuntimeHost が成功したかのように読める記述を更新。標準 launcher の helper-free Compose と旧 DCP/comparison/match profile の範囲を分離し、compiled `Tracker.RuntimeHost.dll` を含む container 経路を次順位の暫定 fallback と明記。
- Host-networking relay-only option は現在 AppHost が早期に拒否するため、残っていた UDP port conflict test を relay-disabled contract assertion に置換。
- 対象テスト: Compose/AppHost/publisher/RuntimeHost diagnostics・operation loop の選択 test **64 passed / 0 failed**。Docker context が必要な `BaseModeDoesNotAddComparisonOnlyResources` は別途 `fa780\donabe` で process-only PATH に Docker CLI を加え、context `desktop-linux` を確認後 **1 passed / 0 failed**。NU1900 vulnerability metadata network restriction と既存 package / analyzer warnings は残る。`git diff --check` は通過。
- GitHub PR #136 の読み取りでは state OPEN、base `design/issue-128-cm4-shared-namespace-restacked`、head `task/issue-128-cm4-compose-restacked`、remote HEAD `2ba4de2`。PR 本文は旧 relay 構成・古い HEAD の確認数を最新状態のように記述しているため、現測定値・撤去 relay・暫定 fallback・残件へ更新が必要。
- この記録時点では作業差分は未 commit / 未 push。後続作業では差分レビュー、通常 commit/push、PR #136 本文の更新を行う。main への merge はしない。

## 現在の作業 / 次の一手

主な経路の後続作業は Windows-Docker direct multicast ingress / egress の未解決点である。対象 Compose lifecycle は build、テスト、persistent Docker 再起動・cleanup を確認済み。

## 未解決

- Windows native RuntimeHost の Vision multicast ingress と Windows-Docker direct boundary。
- 旧 AppHost session 終了時の具体的 monitor 例外（再現試験は成功、元 run の例外ログは残っていない）。
- Crane Tracker receiver の packet acceptance counter / packet capture。Crane world model は独立した処理証拠だが socket-level capture ではない。

## Persistent Docker 実測

通常ユーザー `fa780\donabe`、Docker context `desktop-linux`、CLI 29.9.1 で固有 stack `duck128persist20261118a` / project `duck-7833515c1303a641422f` を起動。AppHost 初回停止後も owner、Simulator、Game Controller、Crane、Duck RuntimeHost の計 5 containers と Compose network / manifest が残り、同じ stack ID での再起動後は同じ 4 peripheral Compose containers が稼働、RuntimeHost health は ready=true、Vision packets 684 を受信した。Aspire は proxyless health endpoint の dynamic host port が変わるため persistent Duck RuntimeHost container を再作成する警告を出した。Compose peripherals/network は再利用された。

`--stop-persistent --stack-id duck128persist20261118a` は該当 Compose container/network ownership labels を検証してから RuntimeHost container を削除し、project `down` を実行。最終検査は owned containers 0、Compose networks 0、manifest 不在。fixed images と firewall/security settings は変更していない。AppHost は両回とも Ctrl+C で停止したため launcher の session 終了コードは 1 だったが、保持・再起動・cleanup は確認できた。

## 2026-11-18 Windows native direct multicast result (provisional)

今回の no-relay 実測では、RuntimeHost が `0.0.0.0:10020` に bind、Windows 側が `224.5.23.2` に WSL vEthernet (ifIndex 41) 等から join 済みでも Vision 受信 0 件だった。Simulator は `--localhost` なし、Linux namespace `172.18.0.2/16` の `eth0` / gateway `172.18.0.1` 経由。Game Controller は同じ namespace で `224.5.23.2:10020 [eth0]` を listen し、Geometry 更新を記録したため、Windows へ渡る境界を越えた証拠はない。Windows route の選択 interface は未確定。route/firewall/setting 変更は行っていない。

したがって helper-free Compose は実験用経路として扱い、Duck の実行 DLL を含まない最終構成としては扱わない。標準トポロジーの確定・公開前に、Duck code をコンテナに入れないという優先方針と direct Windows receive の未解決結果を照合する。
