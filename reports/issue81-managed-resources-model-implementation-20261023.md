# Sub-agent実行レポート

## タスク

Issue81 / PR84 / I81-05A。通常用3配布定義と既存固定参照を使う資源構成モデルの先行試験と実装。

## sub-agentを使う理由

同じ実装担当を継続し、親は要件・依存・台帳・公開を管理する。

## 対象範囲

基準HEAD1c9ac3562ab81be2a78d4383cf873c08706dd0ca。主設計179行と245行、手元Issue完了条件を照合し、本人必須・設計選択・担当追加条件を区別する。通常用3定義と固定参照、UDP非公開、localhost限定動的TCPの構成モデルだけを対象とする。先行する固定試験の実行時失敗を確認後、同じ期待値で最小実装する。

## 対象外

今回の単位ではI81-08、G1外部宛先/G3診断統合、全資源移行の完了を主張しない。これらは後続順序であり既存Issue全体の承認を撤回しない。環境・依存・認証・外部repo・拒否済み操作等は、本人の既存承認と具体操作を照合して扱う。独立最終は後続工程、mergeの許可なし。デバッガー固定版調査は任意。

## Dispatch profile

- selection_source: explicit_user_override / continuity_reuse
- task_kind: implementation
- signals: work_class=judgment_heavy; uncertainty=medium; change_radius=cross_module; criticality=high; repetition=single; decomposability=sequential_dependencies; decomposition_policy=forbidden; context_need=bounded_history
- executor: /root/issue81_luna_implementation
- original_requested: {model:gpt-6-luna,reasoning_effort:high,fork_turns:none}
- planned_runtime_profile: original requested retained; final role effect unobserved
- applied: null
- application_status: reused_existing_agent_profile
- runtime_profile_observability: final_profile_hidden
- role_plan: original default role; effect unknown
- override: 自動候補Sol highより本人の実装Luna high指定を優先。新規起動なし。
- report_persistence_mode: normal_persistence
- verification_capability: .NET実行はremote_ci_only。既存ローカル検査は対象入力を事前照合して再利用。
- parent_role: ASPIRE全体統括。本人09:43の役割訂正を適用。現在Issue81を進め、#18/#79/#80/#82/#43/#50の既存依存を管理する。新規作業の無制限着手はしない。
- scope_correction: 上位親の便宜的な通常構成限定と08禁止を撤回。本人の09:42「親の作業範囲を意図して狭めていない？大丈夫？」を受け、既承認Issue全体を順に継続し、追加の工程承認待ちを作らない。05Aは開始単位で権限上限ではない。
- preimplementation_state: fixed_test_sha256=0fbc0e30918c241d100b9b415b71a13f455153650e2f475ff824ef3eddf38b2e; technical_head=1c9ac3562ab81be2a78d4383cf873c08706dd0ca; commit=commit_pending; push=push_pending; ci_wait=not_started; runtime_red=unconfirmed. 遠隔のみ実行可能なため先行試験候補を通常pushしてコンパイル成立と実行時失敗を区別する。
- design_decision: 主設計の公開契約と通常用定義を実装する。版/digest要件の削除や別デバッグ方式採用はしない。追加の契約変更が必要なら先に親へ返す。

## 実行コマンド

- `git diff --check` — `/home/donabe/Duck-issue81-aspire-desktop-bridge`、終了値0。
- .NET試験は未実行。標準 .NET 試験は `remote_ci_only` のため、親による先行試験公開後の正確なCIで実行時Redを確認する。
## 対象ファイル

- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs` のみを変更。試験追加と共通の配布期待値ヘルパーを含む。ファイルSHA-256 `0fbc0e30918c241d100b9b415b71a13f455153650e2f475ff824ef3eddf38b2e`。
- 製品登録、Dockerfile、発行profile、主設計、canonical trackingは未変更。
## 指摘事項

- 先行試験は既存AppHostの実際のアプリケーションモデルを作成し、RuntimeHost・DebugHost・明示選択時のRefereeDriver.Hostを `ContainerResource` として確認する。各資源の `DockerfileBuildAnnotation` が該当プロジェクトの `bin/Release/net10.0/publish` をcontextにし、実Dockerfileと固定FROM digest・ENTRYPOINTに結び付くことを検査する。
- UDP端点がないこと、DebugHostの内部TCP 8080と動的host port・loopback設定、RefereeDriverの内部 `ws://game-controller:8082/api/control` とdefaultでの非選択を固定する。注釈の構成検査であり、DCP/Windowsの実際のhost bindや内部疎通は主張しない。
- 既存04B2の3発行profileとDockerfileの固定値を期待値に使用。実publish実体との検証は公開後CIへ残す。
## 結果

- 05A先行試験3件を `AppHostApplicationModelTests.cs` に追加し、期待値を固定した。対象変更はこの試験ファイルだけ。`git diff --check` は終了値0、試験ソースSHA-256は `0fbc0e30918c241d100b9b415b71a13f455153650e2f475ff824ef3eddf38b2e`。
- 固定期待値: RuntimeHost Dockerfile context=`Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish`、FROM `mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3`、ENTRYPOINT `Tracker.RuntimeHost.dll`; DebugHostとRefereeDriver.Hostはcontextを各プロジェクトの同じprofile出力先とし、FROM `mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca`、各DLLをENTRYPOINTとする。DebugHost TCP target port=8080、host portは未固定、annotation target host=`127.0.0.1`、UDP端点なし。RefereeDriverは `RefereeDriver:Enabled=true` と `Testing:ContainerMode=true` を明示した時だけモデルへ追加し、内部Game Controller URIは `ws://game-controller:8082/api/control`。
- 親へ先行試験公開を依頼する状態。CIでruntime Redが得られるまでは製品登録変更を行わない。
## リスク

- C#/.NET試験はremote CI待ちのため、コンパイル成立と実行時Redは未確認。API compile errorは機能Redとして扱わない。
- publish contextの存在と実際のファイル内容、DCPがWindowsで127.0.0.1へbindすること、旧wrapper経由のGame Controllerと内部URIの疎通は未検証。今回の試験はResource/Endpoint/Dockerfile構成を確認する。
- 既存AppHost登録は現時点で旧ProjectResource/ExecutableResourceであり、runtime RedはCIで確認する。通常資源の移行後は既存回帰試験の資源型期待値を更新しつつ、外部wrapperの従来のライフサイクル契約を保持する必要がある。

## 05A focused 文書検査と要件根拠の区分（2026-10-23）

- 要件根拠: Issue81の既存完了条件と主設計179–187/245–249を根拠として、今回の試験は実AppHostモデル上の通常3資源、UDP非公開、127.0.0.1限定の動的TCP、内部接続先を確認する。ContainerResource/Endpoint注釈の検査は設計上の構成契約であり、実DCPのWindows bindや実疎通は受入根拠に含めない。
- 設計選択: 既存のDockerfileBuildAnnotation、04B2で固定済みのimage参照、明示選択時だけのRefereeDriver構成をAppHostモデルへ照合する。全Issue完了や実機成立を今回の条件へ縮めず、05Aは順序上の作業単位として扱う。
- 担当側の追加条件: 05A先行の分割は親の工程整理であり、ユーザーの追加受入条件ではない。デバッガー固定版の調査/installer取得を05Aの停止条件にしない。実行前拒否されたinstaller GETは未実行の履歴として保つ。
- 1c9ac356との差分preflightは、実sourceからtasks 6追加行（36,37,38,39,53,58）とphases 8追加行（5–11,26）を抽出し、抽出fragment byte/hashとmirror入力の一致、baseline/current source hash、固定C#試験SHA `0fbc0e30918c241d100b9b415b71a13f455153650e2f475ff824ef3eddf38b2e`、lint設定8件のhashを実行直前にassertした。既存Windows Node v24.20.0/cache/mirrorと既存WSL venv Python 3.14.4を使用。1回の `--files` 引数へcanonical相対2pathを渡した。
- 追加行だけの結果: textlint exit 0; CSpell exit 1（tasks fragment: bind, Container, Draft, Issue）; Sudachi exit 1（bind/TCP/AddContainer/Draft/Issueとカタカナtoken）。PowerShell stream captureでは日本語tokenが文字化けしたが、同一fragment SHAとsourceのUTF-8 bytesは一致し、WSL内subprocessのstdout/stderr byte captureは「モデル」と出力した。
- 削除側も同じv4 mirror/config・canonical path・ツールで4 tasks行と1 phases行を比較検査した。textlint exit 0、CSpell exit 1、Sudachi exit 1。現在側で検出された英単語/CSpell token/katakana tokenはいずれも削除側で検出された語と重複し、変更に起因する新規語彙指摘は確認できなかった。終了値1を成功扱いにはせず、文書gateはheldのまま。
- Evidence bundle: `reports/evidence/issue81-05a-status-lint-20261023/final/`（inventory 31 entries; `inventory-v1.tsv` SHA-256は同ファイル）。実行raw、Windows側の元captureとWSL内Sudachi byte-capture、入力fragment、commands/exit比較、config/source/hash対応を保持。初回preflightの手入力hash誤りによる中断記録は親bundle直下に保存済みで、lintは起動していない。
- .NET試験はremote_ci_onlyで本担当環境から未実行。製品/C#試験/固定期待値は今回の文書確認で変更していない。

