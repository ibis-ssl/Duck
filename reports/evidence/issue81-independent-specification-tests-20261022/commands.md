# 操作・初回実行計画

実施済みはSkill/許可pack読取、許可単一report読取、新規worker作成・保存・hashだけ。
全exec_commandは shell=powershell, login=false, sandbox_permissions=require_escalated。
WSL経路は wsl.exe -d Ubuntu -- python3 または許可単一reportへのcat。
製品repoの列挙・ソース・差分・既存テスト・成果物・ILは読んでいない。

1. 指定Skill5文書のsha256sum/cat試行はPowerShell/native引用により空パスとなり失敗。ファイル未読。
2. Pythonのliteral path listに修正し、指定Skill5文書のread_text/read_bytes/hashlib.sha256を実施。
3. /tmp/issue81-independent-spec-tests-input-20261022/ だけをrglobし21fileをhash。design2/api10/metadata2/proto7を全文読取。設計は行番号付き分割で切落としを補完。
4. 唯一許可されたreports/issue81-independent-specification-tests-20261022.mdをcatし、親所有prefix/Dispatchを確認。
5. Python stdin scriptをPowerShellのliteral here-stringからwsl python3 -へ渡し新規worker内のテスト・文書を作成。
6. 独立時計sourceだけをread_textし、日本語XMLコメントの保存を確認。
7. public-delta api.txtのread/hash要求は自動承認レビュー拒否。コマンド未実行、0byte読取。再試行・迂回なし。
8. 許可元packとSkillのread_bytes/hashlib.sha256をledgerに保存。自作source/期待値/matrixをmanifestへ記録。
9. 親所有reportの5子欄だけ更新。ビルド・実行の結果は未実行と記載。

初回実行予定（未実行、凍結照合と親指示後のみ）:

    dotnet test /tmp/issue81-independent-spec-tests-worker-20261022/IndependentSpecificationTests.csproj --no-restore --configuration Debug --logger 'trx;LogFileName=initial.trx' --results-directory /tmp/issue81-independent-spec-tests-worker-20261022/results/initial -p:ProductRoot=/home/donabe/Duck-issue81-aspire-desktop-bridge

assetsファイルは現時点で作成していない。親による既存キャッシュのみのoffline assets構成可否確認が前提。
新規依存install・ネットワーク/auth/環境変更・製品編集・ホスト起動・commit/push/GitHub操作は禁止。
失敗結果は製品実装担当へ戻し、同じ凍結テストで再実行する。テスト自体の誤り訂正には設計根拠・新旧版hashを残す。
