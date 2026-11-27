# Issue #80 設計レビュー・修正確認レポート

## 対象

- Issue: #80「Docker Desktop移行設計と現状調査」
- Draft PR: #83（#79・#80関連、追加PRなし）
- repository: ibis-ssl/Duck
- branch: task/aspire-docker-desktop-496
- base branch: design/issue18-aspire-test-orchestration
- 実装HEAD: b6cfbb04f833da366e12e2f6861dc8b20e629f2b
- レビュー対象: 上記HEADからの設計文書差分
- 最終レビュー対象文書SHA-256: 0963ac1a73ae655a06ee41263812753599353d6e74cf1f510d5b50c385a0156a
- レビュー方式: 通常設計レビューと同一レビュアーによる修正確認
- requested profile: gpt-6.1-sol high
- applied profile: 未取得（null）
- profile status: spawn_succeeded_profile_unverified

## 範囲と根拠

レビュー担当は、設計差分を現行の DockerContainerSpec、DockerContainerWrapper、所有者確認処理、起動管理資源、およびライフサイクル試験と照合した。Dockerデーモン操作や実パケット試験は行っていない。GitHubコネクタも使っていない。

対象設計はDocker Desktop 4.96を利用者報告として区別し、過去のLinux環境情報と初期 AddContainer / ホスト通信方式競合を履歴として扱う。UDP・マルチキャスト・Windows版Docker Desktopのホスト通信方式は実測前の未検証と明記する。

## 所見と修正確認

### ISSUE80-R1 — P2 — 取消後の候補IDと完全IDの扱い

**初回所見:** 取消後にコンテナを検索する経路について、元の設計差分は完全IDを再検証して後始末できると現行動作のように読めた。

**根拠:** DockerContainerSpec.CreateOwnerDiscoveryArguments は所有者等で絞った docker ps --all --quiet を構成するが、--no-trunc を指定しない。後続処理は検索出力をそのまま検査へ渡し、IsExactOwner は検索候補IDと検査結果IDの完全一致を要求する。既存の模擬試験は検索側と検査側に同じID文字列を返し、短縮IDと完全IDの差を検証していない。読み取り専用CLIヘルプでは --no-trunc がID省略を避ける指定と確認された。実Docker環境での後始末結果は未測定である。

**修正確認:** 同じレビュアーは設計文書SHA 0963ac1a73ae655a06ee41263812753599353d6e74cf1f510d5b50c385a0156a を確認した。現在の記述は、取消後の時間制限付き検索を実装の試行として記載し、短縮IDの可能性と完全ID一致条件を明示した。完全IDを用いた安全な取消後処理は現行実装・試験では未確認とし、完全IDと所有者を確定できない場合は後始末失敗として、他のコンテナに触れない境界を維持する。

#81には検索結果から完全IDを取得して所有者確認へ渡す方法の確立を割り当てた。#82には起動標準出力の完全IDと取消後検索の短縮IDの差を再現し、ID・所有者確認と他者コンテナ非変更を検証する条件を追加した。

**元の重大度:** P2。重大度変更なし。
**Disposition:** 修正確認済み、受理。レビュアーの最終判定は pass_with_held。追加の必須所見なし。#81/#82の実装・実測と現在HEADのCIは保留であり、本レビューで完了扱いにしない。

## 検証

検査対象の現在版はSHA-256 0963ac1a73ae655a06ee41263812753599353d6e74cf1f510d5b50c385a0156a。clean base文書SHA-256 895265aa4023163d88e2fc644c3ba817108f28d605e2f48eb399f139e48b1ded と、既存設定・ツールで比較した。

- textlint: current/baseとも指摘0、終了0。
- CSpell: current 13 / base 13、終了1。増分0。既存指摘が残るためpassではない。
- Sudachi whitelist: current 21 / base 21（英語14、カタカナ7）、終了1。増分0。既存指摘が残るためpassではない。
- Yomiyasu non-strict JSON: current/baseとも68/100、is_clean=false、同じ7件（警告6、情報1）。終了0を合格とは扱わない。
- build: dotnet build Duck.slnx --no-restore -p:EnableSourceControlManagerQueries=false 成功、警告2・エラー0。Gitの relativeworktrees 拡張を読む失敗を避けるためのコマンド限定プロパティであり、Git設定・プロジェクト設定の永続変更はない。
- git diff --check: 成功。
- テスト、Docker実測、パケット受入、現在HEADのCI確認: 未実施。

## 対象外・残作業

製品コード変更、Issue/Project更新、DockerやWSL設定変更、Engine停止・削除、context永続変更、依存導入、merge、deploy、branch削除は行っていない。#79〜#82のIssue状態を変更しておらず、#81/#82を完了扱いにしていない。PRはDraft #83のままである。

作成時点の状態は commit/push前である。通常commit/pushは親タスクで別途記録する。マージ許可は含まれない。
