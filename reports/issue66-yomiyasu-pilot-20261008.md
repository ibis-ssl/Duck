# 文書推敲パイロット 実施報告

## 対象と報告時点

- 作成日時：2026-10-08T07:47:01.223027+09:00 / 2026-10-07T22:47:01.223027+00:00
- リポジトリ：[ibis-ssl/Duck](https://github.com/ibis-ssl/Duck)
- 対象：[Issue #66](https://github.com/ibis-ssl/Duck/issues/66)
- 作業PR：[下書きPR #67](https://github.com/ibis-ssl/Duck/pull/67)
- 原文の開始HEAD：`449296725fc69dc004818ede2e8ad59a52ef2d27`
- 本文の推敲を含む技術HEAD：`3d4ae941cb807ba1aacd4d92cfc877d388cd889d`
- 報告種別：実装担当による作業・検証報告

対象2文書から8段落を選定し、設計書2段落とREADME1段落の計3段落を変更した。残る5段落は原文を維持した。対象2文書の変更前・最終案、および台帳2文書を加えた公開対象4文書の最終状態で、既存の3種類のlintが成功した。

担当者の自己点検と補助点検は実施した。独立レビューは未実施であり、依頼元keroによる自然さ・意味保持の読み直しと、継続採用の判断も未確認である。本報告はこれらの受入条件を完了扱いにしない。マージは利用者が行う。

## 目的・範囲・受入条件

yomiyasu v1.1.0を固定して、既存文書の意味を保ちながら文章を読みやすくできるかを少数の段落で試す。対象は次の2文書とし、各文書10段落以下、合計20段落以下という上限の中で、変更前に選定した8段落を評価した。

| 対象文書 | 選定 | 最終変更 | 原文維持 |
| --- | ---: | ---: | ---: |
| `Tracker/Design/Core/tracker-architecture-plan.md` | 4 | 2 | 2 |
| `Tracker/Tracker.DebugHost/README.md` | 4 | 1 | 3 |
| 合計 | 8 | 3 | 5 |

対象範囲は文の区切りや説明順序の調整と、その結果の検証・記録である。Issue #31の作業、Issue #64・PR #65の文書検査作業、製品のソースコードやAPIの変更、lint設定・許可一覧・依存関係設定の変更は含めない。

受入条件は、固定版とライセンス、実行範囲、導入・解除方法の確認、意味保持、既存3lintの成功、依頼元による読み直しと継続判断である。静的評価の数値だけで、人による読み直しを代替しない。

## 実行環境・対象の識別

以下に実際の端末、作業ディレクトリ、ブランチ、使用した実行環境、依存関係、対象ソースの指紋、証拠の保存場所を示す。接続先端末のパス、チャット環境のパス、公開済みの成果物を区別して扱う。

- 作業環境: `Windows-11-10.0.22631-SP0`、`C:\WINDOWS\system32\cmd.exe`
- 実行場所: `C:\Users\donabe\RemoteDesktopWorkspace\Duck-issue66-yomiyasu`
- Node: `v24.20.0`、Python: `3.14.7`
- 全文検査の共有実体: `CodexSkill@9ebde2cecc272bf65754f36628f1072805272117`（変更なし）
- 最終公開前のソース指紋: `7ef04be43bf77d049e88aec2e4ec51e9edbfe5afce5e4f5972ab44b76aacb54f`
- ブランチ: `docs/issue66-yomiyasu-pilot`
- 作業用yomiyasu実体: `artifacts/issue66/yomiyasu`
- Python依存の実測版と監査ハッシュ: `reports/diagnostics/issue66-yomiyasu/toolchain.json`
- 検証ログの保存先: `reports/diagnostics/issue66-yomiyasu/validation.zip`

コミット前の検証結果は、その時点のHEADだけでなく対象ファイルの指紋と併せて読む。技術HEADの検証と、報告資料を含む後続コミットの検証も区別する。

## yomiyasuの固定・ライセンス・実行範囲

使用版はv1.1.0、固定した完全SHAは `0df47749139dfd64ad3d55e7d53d3839e5874848` である。[取得元](https://github.com/nanaism/yomiyasu)の名称だけでなく、このSHAを再現時の識別に用いる。

`skills/yomiyasu/LICENSE` のMITライセンス（Copyright 2026 nanaism）と、`skills/yomiyasu/UNICODE-LICENSE.txt` のUnicode License V3（Copyright 1991–2026 Unicode, Inc.）を確認した。実体をコピーして配布する場合は、これらの著作権表示と許諾通知を保持する。監査対象には、次のPython実装3ファイルを含めた。

- `skills/yomiyasu/scripts/yomiyasu_lint.py`：静的な文章検査の実行入口
- `skills/yomiyasu/scripts/yomiyasu_diff.py`：変更前後の差分検査の実行入口
- `skills/yomiyasu/scripts/markdown_visibility.py`：Markdownの構造・表示判定に使う共通モジュール

これらはPython標準ライブラリと同梱モジュールで動作する。作業専用のクローンから説明と参照資料を手動で読み込み、対象の段落を抽出した試料に対して検査した。推敲・検査の実行範囲では、ネットワーク通信、外部モデルの呼出し、設定ファイルへの書込みを行っていない。取得や依存関係の導入と、検査スクリプト自体の動作は区別している。`importlib` による読込も隣接する同梱モジュールを対象としていることを確認した。

### 導入を再現する手順

1. 上記の実行環境と同じ前提を満たす作業専用領域を用意し、対象リポジトリの原文HEADとファイルの指紋を確認する。
2. 新しい作業専用領域で、次のように完全SHAを取得してチェックアウトする。配置先は今回の実行記録と同じ相対パスにする。版名のタグから最新版へ追従する設定は追加しない。

```bash
git init artifacts/issue66/yomiyasu
git -C artifacts/issue66/yomiyasu remote add origin https://github.com/nanaism/yomiyasu.git
git -C artifacts/issue66/yomiyasu fetch --depth 1 origin 0df47749139dfd64ad3d55e7d53d3839e5874848
git -C artifacts/issue66/yomiyasu checkout --detach 0df47749139dfd64ad3d55e7d53d3839e5874848
git -C artifacts/issue66/yomiyasu rev-parse HEAD
```

3. 固定した実体のライセンス、`SKILL.md`、必要な参照資料と上記3ファイルを確認する。恒常的なスキル登録をせず、作業専用の配置から手動で利用する。
4. `selection.json` の段落ID・原文行・原文ハッシュを照合し、文書ごとに変更前と最終案の試料を用意する。試案を最終案へ混入させない。
5. `yomiyasu_lint.py` は試料を引数にして `--json` で実行する。`yomiyasu_diff.py` は変更前・最終案の順に試料を渡し、`--stance=説明 --json` で実行する。実際のコマンド、試料の指紋、終了値は後掲の検証記録を使う。
6. 既存3lintには対象文書の全文を渡す。公開前の確認では台帳2文書も含む4文書を対象とし、記録した実行ディレクトリと依存関係で再現する。

再現用の専用worktreeで、`tools/lint/README.md` に従って `.agents/skills` を記録した `CodexSkill` の固定コミットへ接続する。`validation.zip` には `artifacts/issue66/run_checks.py`、元の実行器のハッシュと一致する `run_checks-initial.py`、各段階の試料と完全なコマンド引数・ログを収録した。変更前の検査には選定記録だけを追加した `9a0c7519cba3d242ccfb68b4b74546ddd6274cc6`、最終本文の検査には技術HEADを使用する。公開前の4文書はこの資料を含むPRの内容を使う。検査器は既存の段階フォルダを上書きしないため、再実行は新しい証跡保存先で行う。

Windowsの作業ディレクトリで実行した検査の入口は次のとおり。対象ファイルは実行器内で列挙し、3lintには必ず全文を渡している。

```bat
python artifacts/issue66/run_checks-initial.py before
python artifacts/issue66/run_checks.py final
python artifacts/issue66/run_checks.py publication-final
```

### 既存lintの依存関係

既存lint側のrequirementsを一括導入する試みは、ChikkarPyの配布メタデータが `0.0.0` として扱われる不一致により失敗した。この失敗を導入成功として扱っていない。

今回の3lintに必要な `SudachiPy==0.6.11`、`sudachidict_core==20260428`、`PyYAML==6.0.3` のみをvenvへ導入し、成功した。パッケージ設定、ロックファイル、requirements、lint設定、許可一覧は変更していない。これら3パッケージは既存lintの実行用であり、yomiyasuの標準ライブラリだけで動作する検査実装とは別である。

Node依存は既存のロックファイルから `npm ci --ignore-scripts --no-audit --no-fund` で導入した。Pythonの必須3依存の導入コマンドは `.venv/Scripts/python.exe -m pip install sudachipy==0.6.11 sudachidict_core==20260428 PyYAML==6.0.3`。ChikkarPyを使う任意の語彙抽出器は実行していない。

### 導入解除の手順と実施状況

解除方法は、実行環境欄で識別した作業専用のyomiyasuクローンを削除することである。削除前に対象パスと用途を照合し、必要な実行記録がクローンの外へ保存されていることを確認する。恒常的なスキル登録や設定書込みを行っていないため、その登録・設定を戻す操作はない。

**クローンの削除は実施していない。** 本作業で確認したのは解除方法であり、実際に削除して解除後の状態を確認したという証拠はない。既存lint用venvの扱いはyomiyasuクローンの解除と区別する。

この作業用配置だけを解除する場合のPowerShell例は次のとおり。作業ディレクトリで実行し、最後の結果が `False` になれば配置がなくなったことを確認できる。

```powershell
Remove-Item -LiteralPath .\artifacts\issue66\yomiyasu -Recurse -Force
Test-Path -LiteralPath .\artifacts\issue66\yomiyasu
```

## 選定段落と採否

選定時の段落ID、位置、変更の採否、維持した条件を次に示す。段落数は変更前に固定した単位で数えた。

| 文書 | 基準行 | ID | 採否 | 理由 |
| --- | ---: | --- | --- | --- |
| 設計書 | 7 | `architecture-01` | 修正 | 追跡エンジンの分離と実行体の役割分担を別文にした。構成名と役割は保持した。 |
| 設計書 | 119 | `architecture-02` | 修正 | 受信条件を保存対象に掛けたまま、別系統での保存と再生・比較の目的を順に示した。『これにより』は直前の保存を指す。 |
| 設計書 | 133 | `architecture-03` | 原文維持 | 少なくとも、通常経路、必ず、元のバイト列か復元可能な参照、という範囲と強さが重要なため原文を維持した。 |
| 設計書 | 162 | `architecture-04` | 原文維持 | 公式の通信形式の不足と内部フレームの必要性の関係が明確なため原文を維持した。 |
| README | 3 | `debughost-01` | 修正 | アプリケーションの定義と表示確認・任意配信を別文にした。必要に応じて、確認しながら、という条件を保持した。 |
| README | 73 | `debughost-02` | 原文維持 | 内部カウンタの成功とUDP実送信の違い、および設定を併せて判断する依頼を保持した。 |
| README | 100 | `debughost-03` | 原文維持 | 要求できること、既定の起動設定、通常はHTTPS、という限定を保持した。 |
| README | 128 | `debughost-04` | 原文維持 | 試案では文を分けた結果『ます』が3回連続した。原文の因果関係と自然な文末を優先して試案を不採用にした。 |

採用した3段落はいずれも一つの文を二つに分ける変更である。設計書では、各プロジェクトの役割と、受信パケットの保存・比較目的を読み分けやすくした。READMEでは、アプリケーションの種類・受信機能と、ブラウザでの確認・必要に応じた配信を文で分けた。

採用箇所以外の本文は維持した。対象2文書の形式上の変更はUTF-8 BOMの付加に限り、改行はCRLFを維持した。台帳として `Tracker/Design/tasks-status.md` と `Tracker/Design/phases-status.md` に今回の作業状態を記録した。

## 変更前と最終案の全文比較

次の比較は選定した8段落の原文と最終案を示す。`debughost-04` は不採用の試案ではなく、原文を維持した最終状態として読む。

### architecture-01

- 基準位置: `Tracker/Design/Core/tracker-architecture-plan.md` の7行目
- 判断: 追跡エンジンの分離と実行体の役割分担を別文にした。構成名と役割は保持した。

**変更前**

`Tracker.Core` に自動レフェリー向けの高品質な追跡エンジンを分離実装し、本番寄りの実行体は `Tracker.RuntimeHost`、デバッグ・診断用の Web UI は `Tracker.DebugHost` として分ける。

**最終案**

`Tracker.Core` に自動レフェリー向けの高品質な追跡エンジンを分離実装する。本番寄りの実行体は `Tracker.RuntimeHost`、デバッグ・診断用の Web UI は `Tracker.DebugHost` として分ける。

### architecture-02

- 基準位置: `Tracker/Design/Core/tracker-architecture-plan.md` の119行目
- 判断: 受信条件を保存対象に掛けたまま、別系統での保存と再生・比較の目的を順に示した。『これにより』は直前の保存を指す。

**変更前**

CaptureOn 中に同じ公式トラッカーのマルチキャスト用の通信アドレスと通信ポートで受信した `TrackerWrapperPacket` は、後から自前トラッカーの内部出力、自前トラッカー自身の公式形式のパケット、外部トラッカーのパケットを再生・比較できるように、別系統で保存する。

**最終案**

CaptureOn 中に同じ公式トラッカーのマルチキャスト用の通信アドレスと通信ポートで受信した `TrackerWrapperPacket` を、別系統で保存する。これにより、後から自前トラッカーの内部出力、自前トラッカー自身の公式形式のパケット、外部トラッカーのパケットを再生・比較できるようにする。

### architecture-03

- 基準位置: `Tracker/Design/Core/tracker-architecture-plan.md` の133行目
- 判断: 少なくとも、通常経路、必ず、元のバイト列か復元可能な参照、という範囲と強さが重要なため原文を維持した。

**変更前**

補助 JSONL ファイルの各記録は、少なくとも次を保持する。スナップショットは表示用データとして扱ってよいが、それだけでは比較元データとして不十分である。通常経路では、受信パケットの元のバイト列か、そのバイト列を復元できる参照を必ず保持する。round-trip（書き込み後の読み戻し）で、保存済み記録から元のバイト列を復元または再デコードできるようにする。

**最終案**

補助 JSONL ファイルの各記録は、少なくとも次を保持する。スナップショットは表示用データとして扱ってよいが、それだけでは比較元データとして不十分である。通常経路では、受信パケットの元のバイト列か、そのバイト列を復元できる参照を必ず保持する。round-trip（書き込み後の読み戻し）で、保存済み記録から元のバイト列を復元または再デコードできるようにする。

### architecture-04

- 基準位置: `Tracker/Design/Core/tracker-architecture-plan.md` の162行目
- 判断: 公式の通信形式の不足と内部フレームの必要性の関係が明確なため原文を維持した。

**変更前**

公式の通信形式だけでは自動レフェリーに必要な情報が不足するため、`Tracker.Core` はより豊かな内部の追跡フレームを持つ。

**最終案**

公式の通信形式だけでは自動レフェリーに必要な情報が不足するため、`Tracker.Core` はより豊かな内部の追跡フレームを持つ。

### debughost-01

- 基準位置: `Tracker/Tracker.DebugHost/README.md` の3行目
- 判断: アプリケーションの定義と表示確認・任意配信を別文にした。必要に応じて、確認しながら、という条件を保持した。

**変更前**

`Tracker.DebugHost` は SSL-Vision の UDP パケットを受信し、ブラウザで未加工入力と追跡結果の表示を確認しながら、必要に応じて公式形式の `TrackerWrapperPacket` を UDP で配信する ASP.NET Core アプリケーションです。

**最終案**

`Tracker.DebugHost` は SSL-Vision の UDP パケットを受信する ASP.NET Core アプリケーションです。ブラウザで未加工入力と追跡結果の表示を確認しながら、必要に応じて公式形式の `TrackerWrapperPacket` を UDP で配信します。

### debughost-02

- 基準位置: `Tracker/Tracker.DebugHost/README.md` の73行目
- 判断: 内部カウンタの成功とUDP実送信の違い、および設定を併せて判断する依頼を保持した。

**変更前**

`Publish OK` / `Publish Fail` は現在実装の内部カウンタです。`PublishUdp=false` のときも追跡フレームの処理自体は成功扱いになり、`Publish OK` が増えることがあります。実送信の有無は `Tracker:PublishUdp` と送信先設定を合わせて判断してください。

**最終案**

`Publish OK` / `Publish Fail` は現在実装の内部カウンタです。`PublishUdp=false` のときも追跡フレームの処理自体は成功扱いになり、`Publish OK` が増えることがあります。実送信の有無は `Tracker:PublishUdp` と送信先設定を合わせて判断してください。

### debughost-03

- 基準位置: `Tracker/Tracker.DebugHost/README.md` の100行目
- 判断: 要求できること、既定の起動設定、通常はHTTPS、という限定を保持した。

**変更前**

設定プロファイルの切り替えは HTTP API からも要求できます。既定の起動設定では `UseHttpsRedirection()` が有効なため、通常は HTTPS の接続先を使ってください。

**最終案**

設定プロファイルの切り替えは HTTP API からも要求できます。既定の起動設定では `UseHttpsRedirection()` が有効なため、通常は HTTPS の接続先を使ってください。

### debughost-04

- 基準位置: `Tracker/Tracker.DebugHost/README.md` の128行目
- 判断: 試案では文を分けた結果『ます』が3回連続した。原文の因果関係と自然な文末を優先して試案を不採用にした。

**変更前**

SSL-Vision から受信した UDP パケットを、protobuf デコード前のバイト列として `jsonl.gz` に保存します。各行には `receivedAt`、送信元の通信アドレスと通信ポート、受信データを Base64 で符号化した文字列が入るため、後から同じ順序で `SSL_WrapperPacket` に戻してトラッカーへ再投入できます。デコードに失敗したパケットも保存対象です。

**最終案**

SSL-Vision から受信した UDP パケットを、protobuf デコード前のバイト列として `jsonl.gz` に保存します。各行には `receivedAt`、送信元の通信アドレスと通信ポート、受信データを Base64 で符号化した文字列が入るため、後から同じ順序で `SSL_WrapperPacket` に戻してトラッカーへ再投入できます。デコードに失敗したパケットも保存対象です。

## 静的評価と不採用の判断

| 文書の選定試料 | 変更前 | 初回試案 | 最終案 | 判断 |
| --- | ---: | ---: | ---: | --- |
| 設計書 | 100 | 100 | 100 | 選定4段落のうち2段落を変更 |
| README | 100 | 95 | 100 | 選定4段落のうち1段落を変更 |

READMEの初回試案では、`debughost-04` の文を分けた結果、「ます」の連続に関する警告が1件となり、静的評価が100から95へ下がった。この分割案を不採用として原文へ戻し、最終案は100・警告0となった。設計書の静的評価は100から100であった。

不採用案にも意味保持の補助点検は行ったが、それだけで採用を決めていない。最終的な差分は3段落に限定した。`changes.json` に試案の記録が残る場合も、採否表と本報告の最終比較を区別して参照する。

最終差分検査では、設計書の試料の文数が7から9へ増え、指示語「これにより」が所見に出た。これは直前の別系統での保存を受け、原文の再生・比較の目的を保持している。READMEの文数は9から10へ増え、アプリケーションの定義から現在動作の説明を分けた箇所が文末の変化として出た。「必要に応じて」という配信条件は保持した。README内の既存の2つの操作上の依頼は変更前後とも同じ所見であり、文書の用途に合うため維持した。これらは担当者の文脈判断であり、スクリプトによる意味保持の証明ではない。

## 検証結果と診断記録

対象2文書の全文に対し、変更前と最終案の両方でtextlint、CSpell、Sudachiによる許可一覧検査が成功した。対象範囲とソース指紋、各コマンドの終了値、標準出力・標準エラーなどの診断記録は次の表に示す。

| 段階 | 対象 | textlint | CSpell | Sudachi | HEAD / ソース指紋 |
| --- | --- | ---: | ---: | ---: | --- |
| before | 対象2文書 | 0 | 0 | 0 | `449296725fc69dc004818ede2e8ad59a52ef2d27` / `e7c831133340606a59ad1367a2f1696ae011417273894cf5bc94b7af22ae7c5a` |
| after | 対象2文書 | 0 | 0 | 0 | `9a0c7519cba3d242ccfb68b4b74546ddd6274cc6` / `1a0040bae5dc00bc699f0320d1a1e303db7a2a586bd53772ad8d32682b7b3683` |
| final | 対象2文書 | 0 | 0 | 0 | `9a0c7519cba3d242ccfb68b4b74546ddd6274cc6` / `d9f9d9ead7cee72d514cfe871169e770a01bae0dbb88f3dea265e378a3e5a8c3` |
| publication | 4文書 | 0 | 0 | 1 | `3d4ae941cb807ba1aacd4d92cfc877d388cd889d` / `842052efbdbb69a5e3c281340978891a6025319ddd8a3ef8716c13feb1628f91` |
| publication-final | 4文書 | 0 | 0 | 0 | `3d4ae941cb807ba1aacd4d92cfc877d388cd889d` / `7ef04be43bf77d049e88aec2e4ec51e9edbfe5afce5e4f5972ab44b76aacb54f` |


台帳2文書を加えた公開前の初回検証では、新たに使った「コード」という表記2箇所が許可一覧検査で失敗した。既存の「ソースコード」という表記へ修正し、`publication-final` では4文書すべてについて3lintが成功した。許可一覧や検査設定を緩めて通したものではない。

この初回失敗とChikkarPyの導入失敗は、最終成功と分けて診断記録に残す。今回は文書推敲の前後検証であり、これらの失敗を製品実装のTDDにおけるRedとして扱わない。

範囲照合の初回は、ASCII名称の出現順まで固定した検査がREADMEの定義文の移動を検出して停止した。本文全体が予定した3行の変更と一致する確認を保ち、名称・数値は同じ個数、インラインコード・コードブロック・URLは同じ順序という照合へ修正し、最終照合は成功した。操作や条件の順序は担当者が別途読んで確認した。照合方式とハッシュは `preservation.json`、経緯は `toolchain.json` に残した。

### CIと対象HEAD

技術HEAD `3d4ae941cb807ba1aacd4d92cfc877d388cd889d` に対応する.NET testsは、[run 37696957547](https://github.com/ibis-ssl/Duck/actions/runs/37696957547)で成功した。

台帳と報告資料を含む後続コミットは別のHEADとなる。次の記録は生成時点の技術HEADを対象としており、最終公開の実結果はPRコメントに残す。技術HEADの成功を、資料反映後のHEADの検証結果として代用しない。

- 技術変更HEAD: `3d4ae941cb807ba1aacd4d92cfc877d388cd889d`
- 対応CI: [.NET tests](https://github.com/ibis-ssl/Duck/actions/runs/37696957547)、実行番号 `37696957547`、`3d4ae941cb807ba1aacd4d92cfc877d388cd889d` に一致し `success`。
- この資料を含む最終公開HEADは生成時点で未コミット。最終push後の対応CIをPRコメントで別途記録し、この技術HEADの結果を代用しない。

## 意味保持の点検と評価の限界

担当者の自己点検と補助点検では、変更前後の主体、目的、条件、義務・任意の強さ、時点・順序、比較対象、識別子を照合した。採用した3段落について、これらの情報の脱落や意味の変化は見つからなかった。

具体的には、`Tracker.Core`・`Tracker.RuntimeHost`・`Tracker.DebugHost`の役割分担、CaptureOn中という保存条件、通信アドレスと通信ポート、三つの比較対象、必要に応じたUDP配信を保持している。`TrackerWrapperPacket` と `SSL_WrapperPacket` など、異なる型名を混同していない。

静的評価の100は、この検査における数値である。原文も100だったため、数値上の改善を示した結果ではない。警告0や既存lintの成功だけでは、読みやすさの改善、読者による理解、技術的な意味保持を証明できない。意味保持の点検は担当者側の確認であり、独立レビューでも依頼元の受入判断でもない。

今回の結果は選定した2文書・8段落の範囲に限られる。別文書や全体語彙整理での効果、継続利用時の作業量については、この結果から判断していない。

## 証跡の保存先

- [事前選定と原文ハッシュ](diagnostics/issue66-yomiyasu/selection.json)
- [8段落の採否・初回試案・最終案](diagnostics/issue66-yomiyasu/comparison.json)
- [範囲と保護対象の照合結果](diagnostics/issue66-yomiyasu/preservation.json)
- [実行環境・固定版監査・依存関係](diagnostics/issue66-yomiyasu/toolchain.json)
- [全段階の検証ログと実行器](diagnostics/issue66-yomiyasu/validation.zip)
- [ログ一式のSHA-256と各収録ファイルの指紋](diagnostics/issue66-yomiyasu/validation-manifest.json)
- [生成元3 Skillの完全な出力を保持した引継ぎ](../handoffs/issue66-yomiyasu-pilot-20261008.yaml)

レポート自体は推敲パイロットの対象にしていない。検査結果の記録として作成した。既存の `reports/**` 除外設定は変更していない。製品コード、テスト実装、GitHub Actionsの定義も変更していない。既存の失敗時診断収集を確認し、今回の文書変更のための追加テストは作成していない。

## 残件と次の行動

| 項目 | 状態と次の行動 |
| --- | --- |
| 依頼元による読み直し | keroによる自然さ・意味保持の確認は未確認。最終比較を読み、採用した3段落と原文を維持した5段落を確認する。 |
| 継続採用の判断 | keroの判断は未確認。このパイロットの範囲を超える適用は、継続判断を得てから扱う。 |
| 独立レビュー | 未実施。自己点検・補助点検を独立レビュー済みとは扱わない。 |
| 導入解除 | 作業専用クローンの削除方法を示した。削除と解除後の確認は未実施。 |
| 資料反映後のCI | 上記CI節の対象HEAD一致記録に従う。対応するrunが未取得なら、その状態を未確認のまま残す。 |
| マージ | 利用者が行う。担当者はマージしていない。 |

最終比較、検証記録、失敗時の診断記録をPRからたどれる状態にし、依頼元の読み直しと継続採用判断へ渡す。本報告と別にPRコメントへ簡易報告を掲載し、引継ぎには実行環境、完全な生成元出力、残件、保存・公開状況を保持する。
