# Duck #55 / PR #123 独立最終レビュー報告

## 判定

**pass_with_held** — 対象 HEAD の実装に必須レビュー指摘は残っていない。修正後の Aspire DCP が自動継承した PATH で Docker runtime を起動する実動確認は未実施として保留する。この報告は #55 全体の実通信受入完了やマージ承認を意味しない。

## 対象とレビュー担当

- Issue / PR: [Duck #55](https://github.com/ibis-ssl/Duck/issues/55) / [PR #123](https://github.com/ibis-ssl/Duck/pull/123)
- branch: task/issue55-aspire-007a
- base: design/issue18-aspire-test-orchestration
- reviewed implementation HEAD: 1fa137457aa1a33430b04999f18fd2949b057bf6
- 今回の限定差分: 8203b25ff78bd731d4d337fc847badb3262d49ba..1fa137457aa1a33430b04999f18fd2949b057bf6、5ファイル
- reviewer: /root/issue55_independent_final_review_dcf2。前回と同一の独立レビュー担当で、実装・通常修正確認には参加していない。
- requested profile: 本人指定の GPT-6.1 sol / high。実際に適用されたモデル・reasoning はランタイムから確認できず unknown。
- review identity / reservation: issue55-pr123-dcf2b122-20261112。予約 owner は review-enforcer。予約済み報告先はこのファイル。
- 通常修正確認: /root/issue55_normal_fix_verification_8203b25 が同じ5ファイルを確認し pass_with_held。実装担当とは別の担当。今回の independent reviewer と同様、実適用モデルは確認できない。

## 変更と根本原因

親の PowerShell では Docker CLI が PATH から解決できず、ユーザー領域の既定 Docker Desktop CLI は存在していた。AppHost は executable を絶対パスで解決して Docker Engine identity を取得できる一方、Aspire DCP は docker を PATH から探索する。最初の起動ログには Container runtime 'docker' could not be found があり、DCP の資源が作られなかった。

HEAD 1fa1374 は、解決済み Docker executable の親ディレクトリを AppHost プロセスだけの PATH 先頭へ加え、DCP 子プロセスに継承させる。User / Machine PATH、Docker 設定、認証、権限は変更しない。明示 executable、bare command、path 重複、区切り文字を扱う focused test と設計書の説明を追加した。

## 独立レビューの指摘履歴

- IFR55-001 / P2 — resolved: 成功した比較結果から source UUID、endpoint、source 別受信数を後から確認できない問題。HEAD 8203b25 で、同一 health 応答の source と diagnostics を成功結果 JSON に保持する変更を確認した。今回の PATH 差分はその経路を変更しない。
- IFR55-002 / P2 — withdrawn / closed by existing evidence: AutoRef の入力・Duck 側 publish 無効・観測された fresh tracker 出力を既存同一 run の証拠で照合し、後続の supplemental evidence 拒否だけで earlier evidence を無効にしないと判断した。元の P2 を保持し、重大度再分類はしていない。追加の送信方向・件数・完全な process 帰属を条件にしていない。
- NET-009 — held から closed: 既存の第二 AppHost 拒否実行、occupied UDP/TCP/IPv6 socket tests、共有 multicast 除外のテスト、制御 port の適用確認を用いた。一般的な resource recreation を新しい完了条件に加えていない。
- 今回の PATH 修正: 最小性、起動順、process-only の変更範囲、明示 executable と bare command、区切り文字・重複・OS別パス比較、設計・報告の一致を確認。新しい必須指摘なし。

## 検証

- local focused test: dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~DockerIdentitySettingsResolverTests' — 設計更新後に build 成功、8 passed / 0 failed、exit 0。保存ログは %TEMP%\issue55-dockerpath-fix-8203b25-20261112\focused-test-after-design.log。これは commit 前の dirty workspace における実行で、CI 実行とは別。
- exact-HEAD CI: [run 43311557559](https://github.com/ibis-ssl/Duck/actions/runs/43311557559)、HEAD 1fa1374 に一致。1,431 passed / 4 skipped / 0 failed、Linux publish 成功。ASPIRE-NET-002 と full-stack Linux acceptance は skipped。
- git diff --check: pass。
- 通常修正確認と independent bounded closure は読み取りのみ。reviewer はテスト、Docker 起動、コード変更、投稿をしていない。

## 実動確認の限界

修正後の AppHost/DCP で自動 PATH 継承が成功した実 Docker acceptance は未確認。先の二回目の試行では Docker context と Engine ID も手動指定しており、自動 identity 解決の証拠には使えない。VSTest は Starting を表示したが、完了結果 JSON / exit-code artifact はなく、その試行を受入成功とは扱わない。既存の manual-override AppHost とその9コンテナは未操作で、試験証拠として使用していない。実 DCP child の環境 block 自体を直接読んだ証拠もない。

既存の試合 run 43133171207 は Crane/YELLOW 1–0 TIGERs/BLUE、最大試合時間まで進行した記録である。本人はこの結果について問題ないと述べている。停止時刻や POST_GAME 未到達を新しい完了条件にしない。過去 HEAD の run を今回の HEAD の runtime acceptance に付け替えていない。

## 報告 attestation の境界

- technical verdict は reviewed implementation HEAD 1fa137457aa1a33430b04999f18fd2949b057bf6 に付く。
- このファイルを含む後続コミットは報告のみの管理上の attestation とする。first parent は上記 HEAD、変更対象はこの予約済みファイルだけとし、実装・設計・workflow・設定・tracking・handoff は変更しない。
- attestation の SHA はこのファイル内には含めず、commit 後に外部の進捗報告で示す。
- 後続の別 commit があればこの判定は移転しない。実装変更には通常の検証と同じ独立 reviewer による bounded closure が必要。
- PR は OPEN / Draft。マージしていない。PR 本文・コメントは変更していない。
