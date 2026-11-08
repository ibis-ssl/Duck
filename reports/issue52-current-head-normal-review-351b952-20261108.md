# Issue #52: 通常レビュー

## レビュー対象

- PR: #119
- base: `task/issue50-aspire-006g` / merge base `1660cfb519f373c8263df09599a7e11f0a18e699`
- レビュー対象 HEAD: `351b95242ae00db7740a9e710b7110fcd1c17a0e`
- 範囲: `1660cfb519f373c8263df09599a7e11f0a18e699..351b95242ae00db7740a9e710b7110fcd1c17a0e` の42変更ファイルと直接依存
- レビュー種別: 現在のPR差分の初回通常レビュー
- レビュアー: `/root/issue52_normal_review_351b952`。実装・修正・実装担当の差分確認は担当していない。
- 要求モデル: `gpt-6.1-sol/high`（ユーザー指定）。実行時の適用モデル・推論量はこのランタイムから独立確認できないため `applied: null` / `spawn_succeeded_profile_unverified`。
- 分解方針: `decomposability: independent_workstreams`、`decomposition_policy: forbidden`、`parallelism_mode: single_agent`。通常レビューの同一担当継続を優先し、分割しなかった。
- 判定: **fail**。必要な修正は `I52-NR-010`（P2 / medium）1件。
- この判定はソースの通常レビューであり、Linux実受入、独立最終レビュー、Issue完了や統合許可を示さない。

## 対象要件と境界

Issue #52 本文と `ASPIRE-006G2` は、同じ有効な審判指令期間中に TIGERs と Crane の両チームのロボット位置が実通信で変化することを要求する。環境、受信数、送信元、標準出力・標準エラーを記録し、失敗時の途中証拠も保つ。今回のユーザー指示は Crane の既定 Planner に合わせ、失敗回避だけの Crane 動作変更を避けることである。

## 必要な修正

### I52-NR-010 — .NET移植で重複detection拒否の保護が失われた

- 重大度: **P2 / medium**。
- 起源: 対象HEADの通常レビュー。旧 `I52-IFR-001` の閉鎖時に追加された重複detection拒否契約の回帰を示す、新しい所見ID。
- 場所: `Testing/Duck.Testing.AppHost/SslLogRecordReader.cs:156-157`、正の証拠として扱う側は `Testing/Duck.Testing.AppHost/SslLogMotionAcceptanceVerifier.cs:105-107`。
- 内容: `SSL_WrapperPacket.Parser.ParseFrom` は単一messageフィールド `detection` が同じwrapper内に複数回現れると、それらをmergeする。以前の検証器が明示的に拒否した重複フィールドを検出する検査がなく、別々のdetectionフィールドに入れた黄・青ロボットを一つの有効frameとして観測する。
- 影響: 完全なgzip/SSL記録中のVision wrapperがすべて重複detectionを含む場合でも、位置変化を正の受入証拠として `Passed=true` にできる。これは移植前に閉鎖確認したparser保護を維持していない。
- 頻度・重大度の補足: ユーザー基準は「利用者1,000人規模全体で、おおむね月1回の発生」である。本レビューに実運用での重複detection発生件数・利用者母数・観測期間の統計はなく、既存ログからの自然発生も確認していないため、頻度は **unknown**。ソースと意図的な再現入力から分かるのは、当該入力が入るとfalse passになることだけであり、この月次基準を満たす発生率は主張しない。対象は受入verifierなので、契約上拒否すべきwrapperを正のmotion証拠に使えることが、Issue #52の「有効な実packetで双方の移動を確認する」という判定を直接損なう。原所見に付与した **P2 / medium** は維持し、頻度の推定だけによる変更はしない。`source_severity=P2/medium`、`current_severity=P2/medium`、承認済み再分類なし。
- 再現: リポジトリ外の一時.NET 10 consoleから、現在のRelease出力の検証器を呼び出した。NORMAL_START/counter=1を1件記録し、その後にVision recordを2件記録する。各wrapperに黄robot-1だけのdetectionと青robot-2だけのdetectionを別々の同名field 1として入れ、両者のxを0から20へ変えた。結果は `Passed=true, RecordStreamComplete=true, RecordCount=3, VisionRecords=2, CraneMovingRobotCount=1, TigersMovingRobotCount=1`。単一detectionに両色を入れた対照も同じ結果だった。
- 履歴との関係: `reports/issue52-motion-verifier-normal-review-20261107.md:19` の `a95ad91` 閉鎖記録と、`reports/issue52-motion-verifier-independent-final-review-e6e0e26-20261107.md:22` はmultiple detection fieldsの拒否を明記する。元の `I52-IFR-001` 自体は「検出後の不正suffixを読み飛ばす」P2所見であり、現在のParseFromはその不正suffixを拒否する。したがって元所見の全修正が再発したとは扱わず、閉鎖時に得た重複拒否保護の回帰として本IDで追跡する。元の重大度や過去の閉鎖結果は変更しない。旧 `a95ad91` / `e6e0e26` の閉鎖・合格判定を現在HEADへ転用しない。
- 必要な対応: 正のmotion証拠に渡す前にwrapper全体でdetectionが一意であることを検証する。同名detectionフィールドを黄/青に分割した再現を.NET回帰試験へ追加し、既存の不正suffix拒否・well-formed unknown field許容を保つ。実装担当による修正と局所検証後、この通常レビュアーが同じ所見を再確認する。

## Planner と CM4 経路の評価

Planner省略とtransport選択は別の変更として確認した。

- `Program.cs:489-497` と `BaseSimulationResourceRegistration.cs:356-377` は、Matchで解決済みPlannerが `rvo2` なら `PLANNER` と `planner:=` を省く。明示 `rvo2` も省くため、設定未指定と明示指定は区別していない。依頼元の既存調査と公式Crane developのlaunch既定値 `rvo2` を前提にすると、現在の既定と明示 `rvo2` は実効値が同じである。この省略自体を今回の停止原因や別Plannerへの変更とは認定しない。明示された別値 `visibility_graph` の環境変数と起動引数は維持されている。将来の既定値が変わった場合まで明示rvo2を固定する契約ではない。
- 今回の固定Crane imageはローカルDockerに存在せず、reviewerは実image内のlaunchファイルを独立取得していない。公式develop sourceと事前調査の一致は、最新HEAD実受入やimage内検査の代替にしない。
- MatchでCM4を常に選ぶ条件はPlannerから独立している。PRが新たに書き換えた `aspire-simulation-test-environment.md:37` だけを既存要求の根拠にはしない。
- PR baseと現在で同じgitlink `57910ff44a577cd2c2e055cf4d8fe31c4a4b127b` の `reference/specs/pc/simulation.md:3,6,12-13` は、CM4が位置制御を行い、Simulatorは位置制御を行わず、65B mode4→CM4→715B mode3という経路を説明する。16行の「Craneはまだ715B」という注意は古い版の警告で、現行imageの互換性根拠には使わない。
- 公式 `ibis-ssl/Orion_CM4` の固定commit `d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a` の `cm4/bridge/cm4_sim.cpp` は入力長715を要求し、範囲外robot_idを無視する。`CraneMode4PacketAdapter` の65Bを該当slotへコピーし、他slotを0xffにする形式はこれと整合する。過去run `42403429891` のSimulatorによる65B受信拒否とも整合し、位置指令をSimulatorへ直接渡す経路をCM4経由へ修正する技術的根拠がある。単にPlannerを変えて失敗を避ける変更とは認定しない。
- 実際に固定imageのadapter経路が動くことは未実証。公式の現在Crane senderは設定packet v2/28B、固定CM4の `config_packet.h` はv1/20Bである一方、固定Crane imageの対応source revisionは未確認。configuration relayの互換性はunknownとして保持し、実imageのpacketまたはログで確認する。推測に基づく変換追加は本レビューでは要求しない。

## Coverage

| 確認項目 | 判定 | 証拠・範囲 |
| --- | --- | --- |
| 要件・設計への適合 | `checked_finding` | Issue #52、ASPIRE-006G2、既存transport仕様を照合。motion証拠のparser保護にNR-010。 |
| 全変更ファイル・範囲 | `checked_no_finding` | 全42変更ファイルをruntime13、build/config5、test11、削除Python2、既存Python配布gate2、設計/進捗2、既存/現在report7として確認。無関係な追加依存はない。 |
| 正しさ・境界条件 | `checked_finding` | referee counter/window、gzip footer、重複ID、欠落座標、UDP slot整形、DNS/IP制約、overlay、TERM/KILL終了経路を確認。重複wrapperのみNR-010。 |
| API・設定・互換性 | `checked_no_finding` | optional blue-half値の2受信経路からfixtureまでの伝播、CLI、Planner、既存comparisonの登録を確認。現在のPlanner既定省略の評価は上記。 |
| エラー処理・証跡 | `checked_no_finding` | timeoutの5秒上限診断、元timeout保持、archive途中証拠、adapter faultによるROS停止、DCP helper欠落guardとworkflow別runner配置を確認。修正が実環境で有効かはheld。 |
| 機密性 | `checked_no_finding` | 新しいDCP表示はnamespace/name/stateだけで、完全status/envを出さない。過去artifact削除記録と現在の最小表示を確認。credential値の出力はしていない。 |
| 試験の十分性 | `checked_finding` | 既存.NETテストは重複robot IDと不正suffixを持つが、重複detectionを持たない。外部.NET probeでNR-010を再現。 |
| 対象HEADのCI | `held` | 対象351b952のCIはない。42425293153は0a71104に対する成功で、転用しない。 |
| レポート・進捗 | `checked_no_finding` | 過去の失敗/閉鎖履歴を保持し、現在の日本語記録は未受入・Draftを明示。最新reportの未コミット表現は作成時点の状態で、統合側がcommit identityを外部記録する。 |
| 回帰・保守性 | `checked_finding` | Python→.NET移植後に旧閉鎖保護の一部が落ちたNR-010。単一file publish出力は実際にexe+PDBだけで、native sidecar欠落を示す証拠はなかった。 |
| Linux実受入・image内互換性 | `held` | CM4起動、adapter、configuration relay、双方移動、試合完了、11003 source一意性を現在HEADで未実施。親の受入作業が担当。 |

## Validation evidence

- **無効・判定から除外**: 親が報告した局所.NET実行（17 passed、0 failed、0 skipped）は `require_escalated` なしで起動され、Git出力で `CodexSandboxOffline` が識別された。ユーザーの実行主体制約を満たさないため、対象HEADの合格証拠として採用しない。
- **無効・判定から除外**: 親の全.NET実行（1,343 passed、8 failed、2 skipped）も同じ実行主体違反により無効とする。数字は訂正の追跡用であり、合格・失敗・skipの有効な検証証拠に使わない。対象 `351b95242ae00db7740a9e710b7110fcd1c17a0e` に対する有効なローカルのリポジトリ試験結果は現在ない。CI `42425293153` は旧HEAD `0a71104` の結果であり、現在HEADへ転用しない。
- **実行主体の証拠不足・不採用**: 先行する実装報告の局所テスト成功には、実行主体を確認した一次証拠が添付されていない。この報告だけでは `fa780\donabe` での実行を証明できず、上記の無効な親実行を補完する合格証拠にも使わない。
- reviewer独立実行: リポジトリ外の一時.NET 10 probeを1回実行し、NR-010のcanonical対照と重複detection再現を確認。repoの実装・テストは変更していない。
- current Release single-file helper出力を読取り確認: executable 73,682,085 bytes とPDBのみ。新規buildはしていない。
- `git diff --check`: 成功。レビュー終了前のHEADは対象 `351b95242ae00db7740a9e710b7110fcd1c17a0e` のまま。
- 全shell/GitHub操作で `sandbox_permissions=require_escalated` を指定し、初回 `whoami` は `fa780\donabe`。Pythonは実行していない。
- full-stack run `42421208991` は旧HEADで起動前段に失敗し、現在HEADでの修正成立を示さない。

## Held / unexplored / next action

ソースの通常レビューではNR-010を修正して同じレビュアーによるfix verificationへ戻す。判定に必要なソース領域の未探索項目はない。固定image内のPlanner既定、設定packetの互換性、CM4/Simulator起動とLinux実通信は実受入側の未検証項目として親が担当する。全体テストとexact-head CI、最新imageによるASPIRE-MATCH-003がそろうまでDraft・未統合を維持する。

`reserved_report_paths`: `reports/issue52-current-head-normal-review-351b952-20261108.md`。normal persistenceであり、`report_attestation_allowed: false`。独立最終レビューの予約や判定ではない。commit、push、mergeは実施していない。
