# Issue #128 I128-NS-01 実装可否報告

## 実行範囲

- 作業担当: `/root/issue128_ns_implementation`
- 対象: Issue #128 の I128-NS-01（Windows-host RuntimeHost profile における CM4 / Simulator / Crane の namespace 経路）
- 作業ブランチ: `task/issue-128-cm4-helper-removal`
- 指定 base / 開始 HEAD: `8193cb6500228c7dbb8e604994f13dc5e3d0ad8c`（設計 PR #132 の `design/issue-128-cm4-shared-namespace`）
- 開始時の状態: clean。branch と HEAD は依頼の指定値に一致。
- モデル: 起動要求は `gpt-6-luna` / `high`（親の `spawn_agent` 指定）。実際に適用されたモデル情報はこの実行環境から観測できないため `null`。
- 認可境界: 既存 image・タグ・digest、Dockerfile、container 内のソースを変更しない。Docker / Windows 実行、既存 I81-07D の再実行、UDP boundary relay の変更、PR / Issue 操作、merge は対象外。

## 判定

この作業環境で確認できた固定 Aspire.Hosting 13.5.4 の公開 `ContainerResource` モデルには、1つの container を network namespace owner とし、別の標準 container をその namespace follower として宣言する対応済み API route が見つからなかった。したがって製品コードは変更していない。これは「Docker で namespace 共有が不可能」とする結論ではなく、現在の固定 Aspire/DCP と標準 `ContainerResource` を使う対応済み route を確認できなかったという阻害である。

raw `--network container:<owner>` を `WithContainerRuntimeArgs` で追加する案は、公開モデルで共有モードを表すものではない。固定版 DCP は各標準 container に `DefaultAspireContainerNetwork` を設定し、別途収集した runtime args を `Spec.RunArgs` に設定する。既存ネットワーク指定を共有モードへ置き換える標準モデル上の経路は、参照した実装では確認できない。この構成を実装済みや runtime 成功と扱わない。

## 根拠

1. Aspire 13.5.4 の [`ContainerCreator.PrepareContainerNetworks`](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/ContainerCreator.cs#L2343-L2478) は、コンテナ資源の `Spec.Networks` に `DefaultAspireContainerNetwork` を設定する。また、独自 container network annotation は `Custom container networks are not supported yet.` として拒否する。
2. 同じ版の [`BuildRunArgsAsync` と呼出側](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/ContainerCreator.cs#L2641-L2667) は runtime argument annotation を `Spec.RunArgs` に設定する。ネットワーク生成コードが追加の runtime args により `Spec.Networks` を置き換える処理は、ここで参照した経路にない。
3. [`WithContainerRuntimeArgs`](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/ContainerResourceBuilderExtensions.cs#L3008-L3059) の API 説明は、引数を underlying container runtime の run command に「追加」する用途としている。共有 namespace を表す高水準モデル API とは説明されていない。
4. 既存 [`issue81-referee-driver-scope-20261022.md`](issue81-referee-driver-scope-20261022.md) は、13.5.4 DCP の default network と追加 `--network container:...` 指定が同じ生成物に残る静的懸念を記録している。DCP binary の引数解釈順や実 Docker 作成エラーは未確認で、モデル上の矛盾と実際の失敗原因を区別している。
5. [I81-07D 記録](../Tracker/Design/Testing/aspire-simulation-test-environment.md)では、標準 `ContainerResource` を使った既存の限定組成で follower の開始に失敗したが、Docker create error は保存されていない。3回目に owner 1世代と終了時の所有資源残存0が確認された。I81-07D は再実行しておらず、原因は未確定のまま保持する。旧 `--network host` conflict は別構成であり、本件の診断に用いていない。
6. 設計 PR #132 の [`issue-128-cm4-namespace-design-20261115.md`](issue-128-cm4-namespace-design-20261115.md) が示す CM4 / Simulator / Crane の loopback endpoint は候補設計の根拠であって、namespace 共有・UDP 通信・再生成挙動の実測ではない。

## 候補と実装の区別

- 候補: CM4 を namespace owner、Simulator と Crane を follower とし、固定 image 内の既存 loopback endpoint を保つ。条件が成立すれば、この profile から CM4 C helper の読み取り専用 mount と Crane launch overlay を撤去する。
- 実装: なし。CM4 registration、Simulator/Crane registration、helper mount / asset / startup、overlay publish / mount / destination は変更していない。
- 固定 image: pull、build、tag、digest、Dockerfile の変更なし。
- container source: Duck source / executable の追加配置なし。
- 他スライス: `Duck.Testing.UdpBoundaryRelay` は未変更。Windows RuntimeHost と Docker consumers の UDP boundary は別タスクのまま。

## 検証

- 実施: Git branch/status/HEAD 確認、既存設計・I81-07D・I81-07H 報告の読み取り、Aspire 13.5.4 の公開 API と DCP ソース確認。
- 未実施: product build、model test、実 Docker / DCP、Windows / FA780、UDP packet test。コード変更がなく、依頼で禁止された runtime topology 再試行もしないため実行していない。
- SDK 状態: 初期 `command -v dotnet` は見つからなかった。`/workspace/.duck-env` は存在するが、本作業では依存導入や SDK 設定変更をせず、build/test を試していない。
- `git diff --check`: exit 0。
- Markdown focused textlint: `npm run lint:md:text -- --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md` — exit 0。
- Markdown focused cspell: `npm run lint:md:spell -- --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md` — exit 1、2ファイルで186件。Markdown whitelist: `npm run lint:md:whitelist -- --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md` — exit 1。語彙許可設定は変更していない。
- 必須全体検査 `npm run lint:md` — exit 1。textlint段階は通過し、cspell段階で28文書中10文書に733件の未登録語が報告されたため、後段のwhitelist全体検査へ進まなかった。未登録語の許可一覧を無断で編集していない。
- `node_modules` は検査時に `/workspace/Duck/node_modules` への一時symlinkとして設置し、検査終了後に削除した。

## 未解決・次の境界

- DCP が標準 `ContainerResource` の既定ネットワークを除去・置換できる公開 route、または既存 Aspire/DCP 管理下で共有 namespace を表せる対応済み API の追加根拠が必要。
- I81-07D の既存失敗は再実行せず、根本原因未確定を維持する。新しい topology の Docker 実行・失敗再現も今回行っていない。
- 対応 route がない場合の次の設計判断は、標準 `ContainerResource` の範囲外となる DCP 統合または orchestration 経路を別途明示的に設計する必要がある。今回、内部 DCP モデルの書換えや非標準 custom resource を推測で採用していない。
- 実装可能な経路が確定するまで helper と overlay を除去しない。CM4 loopback、feedback、multicast fan-out、CM4 / Simulator / Crane の再生成追従、Windows-host RuntimeHost の双方向 packet path は未検証。
- Issue #128 は open のまま。I128-UDP-02 と I128-FINAL-03 の状態は変更していない。
