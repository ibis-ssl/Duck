# Issue #158: 起動前の Protocol submodule 準備設計

## 目的と範囲

起動専用コピーで Protocol submodule が未初期化のまま最初の `publish` / `build` / Docker 起動へ進み、`.proto` 不在による生成型エラーになる事態を防ぐ。対象は Issue #158 の準備・起動前検査であり、Issue #128 の受信障害、ネットワーク経路、Docker 通信の修正は含めない。

対象ソースは Duck `7c8e860899f08acd4b02a0cdf1b44eba180a9f21` 時点の `scripts/start-duck-simulation.cs`、`SslProto/SslProto.csproj`、`Testing/README.md`。期待する gitlink は `ssl-game-controller` の `9b0765fe7a07a55124f6ee957d4559348462d227` と `ssl-simulation-protocol` の `1e719a8d410cc84847ec69104da44fed13738291`。

## 最小契約

### 明示的な初回準備

- Issue #158 の最小準備は、承認済みのオンライン経路を使い、次の2パスだけを通常の `SslProto/src/external/...` へ準備する。`--recursive` は付けず、他の submodule を対象にしない。
  - `git submodule update --init -- SslProto/src/external/ssl-game-controller SslProto/src/external/ssl-simulation-protocol`
- 上のコマンドより前に、実際の準備先 worktree で親 HEAD と index の両 gitlink が期待 SHA と一致すること、2つの既存 checkout と変更状態、コマンド再接続先の index を確認する。再接続・checkout 等で作業先が変わった場合は、その先の index を読み直してから確認する。どれか一つでも不一致・変更あり・確認不能ならコマンドを実行せず停止する。利用者の index、既存 checkout、追跡・未追跡変更を reset、checkout、clean、stage で上書きしない。
- 現行 `tools/cloud/prepare.sh` の全リポジトリ初回準備は別契約であり、Issue #158 の最小準備として案内・置換しない。クラウド通常起動は変更しない。新しい明示準備へのクラウド統合が必要と別途確認された場合だけ、既存 prepare の機能・手順を削らず、既存処理の前に2つの Proto パスだけの HEAD/index/checkout/変更検査を加える最短の統合を検討する。全リポジトリ準備の既存範囲はそのまま別契約として扱う。

### Windows 通常起動の fail-closed preflight

- `--help` と `--stop-persistent` は preflight の対象外とする。通常起動では、最初の `publish`、`build`、Docker 操作、または起動用ファイル書込みより前に preflight を完了する。
- ローカル Git 照会から配置完了まで、選定した手順のすべての Git subprocess とその子孫に、空の `GIT_ALLOW_PROTOCOL` と `GIT_NO_LAZY_FETCH=1` を継承させる。個別の `protocol.<name>.allow` 設定に負ける `protocol.allow=never` は transport 拒否の保証に使わない。外部 hooks / filters が起動しない手順・環境だと変更前に確認する。確認不能または起動し得る場合は、どのファイルも変更せず停止する。Windows の Git 子孫とクラウド上の Git で同じ制約を確認する。
- 既存配置が準備済みなら読取りだけで検査する。未初期化の場合に限り、次の全条件を配置前に満たすときだけ、固定済みのローカル object store を使って標準 submodule 配置を一度だけ準備する。ローカル object store に無い commit、tree、blob が一つでもあれば、その起動中に取得せず理由と不足対象を示して停止する。
- checkout 前に対象 commit から checkout に必要な object 閉包をローカルのみで列挙し、partial clone の promisor object を含む欠落 tree/blob と object 内容のハッシュ不一致を検出する。`--no-fetch` だけを保証として扱わない。欠落・破損・検査不能は安全に復元できない状態として停止する。
- 書込みを始める前に、親の HEAD と index の両方で2つの gitlink が期待 SHA と一致すること、既存配置の状態、各ローカル object 閉包、proto 入力を両 submodule 分すべて検査する。片方だけの事前検査が済んだ状態で書込みを始めない。既存 submodule が異なる HEAD、または既存変更を持つ場合は書き換えず停止する。
- 必須 proto と submodule 内 import を確認する。`SslProto.csproj` の2つの `proto/**/*.proto` include と sim-protocol 側の3つの exclude を評価した実効ファイル集合を固定 gitlink の内容と照合し、submodule 内 import がすべて解決し、想定外の glob 対象がないことを確認する。必要な `state` / `vision` import directory と、準備済み worktree の実ファイルも確認する。Google Protobuf import と NuGet asset の解決は通常の restore / build に委ねる。欠落・余分・曖昧な submodule 入力は、生成型エラーへ進まず該当パスを示して停止する。
- 両方が準備済み、または安全なローカル準備を終えたあと、同一の読取り専用 preflight をもう一度両方に実行する。両方の再検査が成功した場合にのみ `publish` へ進む。
- 外部 transport 拒否と hooks / filters 不起動の確認を Windows とクラウド上で実施する。個別に `protocol.https.allow=always` を設定したケースでも外部通信が起きないこと、部分 clone の欠落 object で lazy fetch されないこと、外部 hook / filter の helper が起動せず、起動する構成なら変更前に停止することをテストする。

### 中断・失敗時の保証

2つの submodule の配置をまたぐファイルシステム上の原子性は保証しない。I/O エラーや中断の時点で、この実行が新たに配置したファイルが残る場合がある。既存の配置や利用者の変更を消すロールバックはしない。診断には、成功した配置、失敗した配置、残存し得る対象パス、エラー理由を含める。次回起動は状態を決め打ちせず、両方を最初から再検査する。再検査の両方が通らない限り最初の `publish` に進まない。

## 変更候補

- `scripts/start-duck-simulation.cs`: help / stop 判定後かつ成果物・起動用ファイル作成前に Windows 通常起動用 preflight を呼ぶ。Git subprocess の環境制約、読取り検査、安全な未初期化配置、失敗診断を閉じ込める。
- `Testing/README.md`: Issue #158 の2パス限定の明示準備と通常起動 preflight を記す。クラウド通常起動手順は変更しない。`tools/cloud/prepare.sh` は、新しい明示準備との統合が必要と確認された場合のみ、既存機能を保った条件付き変更候補とする。
- `SslProto/SslProto.csproj`: 原則変更しない。必要な import / glob 契約の確認元として扱い、入力切替を導入しない。

## 必須検証と完了条件

1. 両 submodule が正しい固定版で準備済みのケースでは、Git が読取りだけで、起動前検査が成功する。
2. 未初期化で、固定版に必要な全 commit/tree/blob と proto 入力が既存のローカル object store に揃うケースでは、標準配置を一度だけ行い、index と親 gitlink は不変のまま両方の再検査後に進む。
3. partial clone で必要な promisor tree/blob のいずれかが欠けるケースでは、空の `GIT_ALLOW_PROTOCOL` と `GIT_NO_LAZY_FETCH=1` の下で、個別 `protocol.https.allow=always` があっても外部 Git 通信・lazy fetch が発生せず、publish/build/Docker より前に不足 object を報告して停止する。`--no-fetch` または `protocol.allow=never` のみを使う試験ではこの条件を満たさない。
4. HEAD/index gitlink 不一致、alternate parent index、異なる submodule HEAD、保存 submodule index の不一致、既存 tracked/untracked 変更、破損 object 内容、検査不能な状態では、index と既存内容が変更されず、明確な診断で停止する。
5. 最初の準備前に片方の事前検査が失敗するケースでは、どちらの submodule も書き換えない。配置中の I/O エラーまたは中断では既存内容を削除せず、残存パスを示し、次回に両方を再検査する。
6. 必須 submodule import 欠落、追加 import directory 欠落、準備済み worktree の実入力欠落、想定外 glob 対象、include/exclude 集合の不一致は、型生成や build の失敗に委ねず preflight で停止する。Google Protobuf import と NuGet assets の検査は通常の restore / build が担い、preflight は NuGet cache を要求しない。
7. `--help` と `--stop-persistent` は preflight を呼ばない。通常 Windows 起動では最初の `publish`、`build`、Docker 操作より前に preflight が完了する。
8. 通常のクラウド起動には新しい照会・準備を追加しない。明示的な初回準備だけが既存の承認済みオンライン経路を使う。
9. hooks / filters を起動し得る設定を含むケースでは helper が一度も実行されず、起動しないと確認できない場合は書込み前に停止する。Issue #158 の明示準備は指定した2つの Proto パス以外の submodule を更新しない。HEAD/index gitlink 不一致、既存変更、確認不能時にも準備コマンドを実行しない。

## 非対象

依存追加、Docker / NuGet 通信の変更、Git 権限・全体設定変更、実機操作、受信障害の診断・修正、キャッシュ導入、MSBuild の入力選択変更、GitHub への公開。
