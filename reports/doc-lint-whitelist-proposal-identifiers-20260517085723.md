# Sub-agent実行レポート

## タスク

- 目的: 旧 `temporary-doc-lint-terms` 一覧から、固有名詞、製品名、型名、設定名、略語、単位として whitelist 登録すべき候補を分類する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指定により、旧一時許可一覧を複数カテゴリに分けてサブエージェントで分担するため。

## 対象範囲

- 対象:
  - `git show HEAD:tools/lint/markdown-whitelist.yaml` 内の旧 `temporary-doc-lint-terms`
  - 非 `reports/**` Markdown 内の実使用箇所

## 対象外

- 対象外:
  - `tools/lint/markdown-whitelist.yaml` の編集
  - Markdown 本文の編集
  - lint script の変更

## 実行コマンド

- 実行コマンド:
  - `git show HEAD:tools/lint/markdown-whitelist.yaml | sed -n '/term: temporary-doc-lint-terms/,/description: 一時許可語/p'`
  - `rg --files -g '*.md' -g '!reports/**'`
  - `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --print0 | tr '\0' '\n'`
  - `rg -n --glob '*.md' --glob '!reports/**' --glob '!SslProto/src/external/**' '\b(DI|LINQ|README|SVG|XML|xUnit|Java|Referee|Tigers|Vision|Red|Medium|Core|Play|Stop|Input|appsettings|dotnet|protobuf|proto|trackedFrame|rdparty|Act|Arrange|Assert|On|Off|Compare|Web)\b'`
  - `rg -n --glob '*.md' --glob '!reports/**' --glob '!SslProto/src/external/**' 'Home\.razor|VisionBallMarker\.razor|VisionDetailsPanel\.razor|VisionFieldCanvas\.razor|VisionFieldLines\.razor|VisionRobotMarker\.razor|DI\b|LINQ\b|SVG\b|XML\b|xUnit\b|trackedFrame\b|\bREADME\b|\bReferee\b|\bJava\b|\bTigers\b|\bappsettings\b|\bprotobuf\b|\bproto\b|\bdotnet\b'`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/doc-lint-whitelist-proposal-identifiers-20260517085723.md`
  - 確認: `tools/lint/markdown-whitelist.yaml` の HEAD 版
  - 確認: `README.md`
  - 確認: `AGENTS.md`
  - 確認: `tools/lint/README.md`
  - 確認: `Tracker/README.appsettings.md`
  - 確認: `Tracker/Tracker.DebugHost/README.md`
  - 確認: `Tracker/Tracker.RuntimeHost/README.md`
  - 確認: `Tracker/Tracker.CaptureReplay/README.md`
  - 確認: `Tracker/Design/**/*.md`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 登録推奨:
    - `Tracker.DebugHost`: 実行体名として本文に出る複合製品名。`DebugHost` を含む単体識別子ではなく、`Tracker.DebugHost` として登録するのが妥当。
    - `Tracker.RuntimeHost`: 実行体名として本文に出る複合製品名。`RuntimeHost` 単体ではなく、`Tracker.RuntimeHost` として登録するのが妥当。
    - `Tracker.CaptureReplay`: 記録再生 CLI の実行体名として本文に出る。`CaptureReplay` 単体ではなく、製品名として登録するのが妥当。
    - `Tracker.Server`: 旧実行体名として履歴と設計に残る。旧称の固有名として登録するのが妥当。
    - `Tracker.Core`: サブプロジェクト名 / 名前空間境界として本文に出る。`Core` 単体では一般語なので、複合名だけ登録するのが妥当。
    - `TrackerCoordinator`: 型名として本文に出る。型名なので単体登録してよい。
    - `SSL_WrapperPacket`: proto 型名として本文に出る。型名なので単体登録してよい。
    - `WorldFrameCommitted`: 通知名 / event 名として本文に出る。識別子なので単体登録してよい。
    - `Vision Input`: 診断画面の表示元名として本文に出る。`Vision` / `Input` 単体ではなく、画面表示名として登録するのが妥当。
    - `Raw` / `Tracked`: 画面表示名として `Raw` / `Tracked` 表示に出る。一般語だが UI 表示名として本文に見えるため、表示名の説明付きなら登録可能。
    - `Fast Forward`: 再生操作の画面表示名として本文に出る。`fast` / `forward` 単体ではなく、表示名として登録するのが妥当。
    - `Capture On` / `Capture Off`: 記録開始 / 停止の画面表示名として本文に出る。`On` / `Off` 単体ではなく、表示名として登録するのが妥当。
    - `Layer A` / `Layer B`: 比較表示の画面表示名として本文に出る。`Layer` 単体ではなく、表示名として登録するのが妥当。
    - `appsettings.json`: 設定ファイル名として本文に出る。旧一覧の `appsettings` は単体登録せず、ファイル名または `Tracker.DebugHost/appsettings.json` alias として登録するのが妥当。
    - `Tracker:ActiveProfileName` / `Tracker:Profiles`: 設定キーとして本文に出る。`Tracker` 単体ではなく、設定キー全体として登録するのが妥当。
    - `ReorderWindowNs`: 設定名として本文に出る。識別子なので単体登録してよい。
    - `RuntimeHost:OperationLoopIntervalMilliseconds`: 設定キーとして本文に出る。識別子なので単体登録してよい。
    - `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds`: 設定キーとして本文に出る。識別子なので単体登録してよい。
    - `UI` / `API` / `CLI`: 略語として本文に繰り返し出る。一般英単語ではなく略語なので登録対象。
    - `JSON` / `JSONL`: 形式名の略語として本文に出る。略語なので登録対象。
    - `UDP` / `HTTP` / `HTTPS` / `IPv4`: 通信方式・規格名として本文に出る。略語なので登録対象。
    - `UUID` / `ID` / `NIC` / `OS` / `TDD` / `PR` / `DI`: 開発・設定・通信文脈の略語として本文に出る。略語なので登録対象。
    - `XML` / `SVG` / `LINQ` / `xUnit`: 技術名・形式名・ライブラリ名として本文に出る。固有の技術識別子として登録対象。
    - `ms` / `ns` / `mm` / `rad` / `Hz`: 単位として本文に出る。単位なので登録対象。
    - `Tigers`: 参照実装名として本文に出る。固有名詞なので登録対象。
    - `Referee`: `Referee program` など RoboCup SSL の構成要素名として本文に出る。単独では一般語にも見えるため、可能なら `Referee program` など複合 alias を添える。
    - `Java`: 参照実装の言語名として本文に出る。製品 / 言語名の固有名なので登録対象。
    - `.NET SDK` / `dotnet`: 開発環境名と CLI 名として本文に出る。`dotnet` はコマンドにも出るが、`.NET SDK` の alias として登録するなら妥当。
    - `protobuf` / `proto`: 通信形式 / ファイル形式として本文に出る。`Protocol Buffers` または `proto` 変換の意味説明付きで登録するなら妥当。
    - `Home.razor` / `VisionFieldCanvas.razor` / `VisionFieldLines.razor` / `VisionBallMarker.razor` / `VisionRobotMarker.razor` / `VisionDetailsPanel.razor`: component ファイル名として設計本文に出る。パスではなく component 名として本文に見えるため、登録対象。
  - 条件付き:
    - `README`: 文書種別名として本文に出るが、多くはファイル名・パス・リンク文脈。単体登録は避け、必要なら `Tracker.DebugHost README` のような複合表現か、対象ファイル名の一部として扱う。
    - `Web`: `Web UI` として出る場合は画面機能名の一部。`Web` 単体は一般語なので、登録するなら `Web UI` の複合語に限定する。
    - `Play` / `Stop`: 再生操作の UI 表示名として本文に出る。一方で一般動詞でもあるため、単体登録するなら「diagnostics playback の表示名」と明記する。より安全には `Play / Fast Forward / Stop` または `playback controls` の複合で扱う。
    - `Act` / `Arrange` / `Assert`: テストの AAA パターン名として使う場合だけ候補。今回確認範囲では実使用が薄く、単体登録するなら対象箇所の本文確認が必要。
    - `Compare`: `Raw / Tracked / Compare` の表示名として使う場合だけ候補。単体英単語なので、表示名としての実使用箇所に限定して説明する必要がある。
    - `trackedFrame`: 診断ログ field 名として本文に出る。コード風識別子なので候補だが、現行本文では inline code / report 参照に偏るため、ログ項目として whitelist に必要な場合だけ登録する。
    - `Red` / `Medium`: review severity や色名として使う場合だけ候補。単体英単語なので、severity 名などの具体的な表示分類としての用途が確認できる場合に限る。
  - 登録しない:
    - `Tracker` / `Kalman`: 指示どおり単体英語では登録しない。一般表記は `トラッカー` / `カルマン` を使う。
    - `Core` / `Vision` / `Input` / `On` / `Off` / `Layer`: 単体では一般英単語。必要なものは `Tracker.Core`、`Vision Input`、`Capture On`、`Capture Off`、`Layer A/B` のような複合語で扱う。
    - `appsettings`: 単体では設定一般の語に見えるため登録しない。`appsettings.json` または具体的な設定ファイル path / alias として扱う。
    - `dotnet`: コマンド行だけで出る箇所は whitelist 根拠にしない。本文で開発環境名を表す場合も、`.NET SDK` の alias として扱う。
    - `rdparty`: `3rdparty` の一部として出る語で、単体識別子ではない。必要なら `3rd party tracker` の複合語へ寄せる。
    - `abstraction`, `action`, `active`, `adapter`, `address`, `aggregate`, `algorithm`, `alignment`, `baseline`, `button`, `camera`, `capture`, `configuration`, `diagnostics`, `field`, `frame`, `geometry`, `host`, `latest`, `raw`, `render`, `snapshot`, `source`, `tracker-algorithm` などの一般英単語 / 一般設計語: 単独登録は禁止方針に反する。必要なら具体的な複合設計語へ寄せる。
    - `・コメント`, `・レビュー`, `・レビュー・` と一般カタカナ語: whitelist で英字識別子として扱う対象ではない。

## 結果

- 結果:
  - 実際に whitelist 更新案へ含めるべき最小候補:
    - `Tracker.DebugHost`
    - `Tracker.RuntimeHost`
    - `Tracker.CaptureReplay`
    - `Tracker.Server`
    - `Tracker.Core`
    - `SSL-Vision`
    - `ASP.NET Core`
    - `.NET SDK`
    - `SslProto`
    - `TrackerCoordinator`
    - `SSL_WrapperPacket`
    - `WorldFrameCommitted`
    - `Vision Input`
    - `Raw`
    - `Tracked`
    - `Fast Forward`
    - `Capture On`
    - `Capture Off`
    - `Layer A/B`
    - `appsettings.json`
    - `Tracker:ActiveProfileName`
    - `Tracker:Profiles`
    - `ReorderWindowNs`
    - `RuntimeHost:OperationLoopIntervalMilliseconds`
    - `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds`
    - `UI`
    - `API`
    - `CLI`
    - `JSON` / `JSONL`
    - `UDP` / `HTTP` / `HTTPS` / `IPv4`
    - `UUID` / `ID` / `NIC` / `OS` / `TDD` / `PR` / `DI`
    - `XML` / `SVG` / `LINQ` / `xUnit`
    - `ms` / `ns` / `mm` / `rad` / `Hz`
    - `Tigers`
    - `Java`
    - `protobuf` / `proto`
    - `Home.razor`
    - `VisionFieldCanvas.razor`
    - `VisionFieldLines.razor`
    - `VisionBallMarker.razor`
    - `VisionRobotMarker.razor`
    - `VisionDetailsPanel.razor`

## リスク

- 未解決のリスクまたは後続対応:
  - `Play` / `Stop` / `Compare` / `Referee` / `README` / `Web UI` は実使用があるが一般英単語成分が強い。追加する場合は、UI 表示名・RoboCup SSL 構成要素名・文書名などの意味を whitelist description で明示し、単体語として意味を広げない確認が必要。
  - 今回は `tools/lint/markdown-whitelist.yaml` を編集していないため、最終 whitelist 反映時にはユーザーの明示レビューが必要。
