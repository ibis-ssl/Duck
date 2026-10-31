from pathlib import Path
import hashlib
p=Path("/home/donabe/Duck-issue81-aspire-desktop-bridge/reports/issue81-linux-distribution-implementation-20261022.md")
text=p.read_text(encoding="utf-8")
old=text

def section(name, body):
    global text
    start=text.index("## "+name+"\n")
    end=text.find("\n## ", start+4)
    if end < 0: end=len(text)
    text=text[:start]+"## "+name+"\n\n"+body.strip()+"\n\n"+text[end+1:]

section("実行コマンド", """
- `bash -n scripts/tests/verify-linux-publish-outputs.sh` は exit 0。スクリプトは mode 100755、SHA-256 `adfe7cd88a943a93bf59afc515b71f178b05d4255761280548d07ecd1cc2704f`。
- `gh run view 39867225445 -R ibis-ssl/Duck --json ...` を実行前確認と完了確認に使用。初回の通常権限実行は api.github.com:443 の socket access error。親の承認済み経路 `login:false + require_escalated` で読み直し、attempt 1 が fdff6ec の終端failureであることを確認した。
- 承認された再実行は `gh run rerun 39867225445 --failed -R ibis-ssl/Duck` の一度だけ。exit 0。実行後のstatus readと `gh run view ... --log --job 120136505328` も exit 0、attempt 2 の終端結果を保存した。
- attempt 2 の標準試験は 776 passed / 0 failed / 0 skipped。後続 `scripts/tests/verify-linux-publish-outputs.sh` は RuntimeHost、DebugHost、RefereeDriver.Host を各 `LinuxContainer` profile で発行し、`Linux publish output contract passed for all three managed applications.` を出した。
- CIのsource archiveから得た対象4ファイルと、Python `subprocess.run(["git","show",ref+":"+path], check=True)` で baseline `4e3099e75c4afab8ba1f11c67500ebe29f17fed3` / candidate `fdff6ec8e5851a4a7d1d20d0dad4f69503b926a9` を比較。exit 0、test fixture / wrapper / launch options / test csproj は全てbyte-equal。過去のprocess substitution比較ログは空blobを含むため失敗証拠として保持し、比較根拠には使用しない。
- Windows Node+既存cache junctionのfocused textlint/CSpell/Sudachi/Yomiyasu raw commandと差分は `/tmp/issue81-referee-driver-host-design-sync-20261022/final/` を参照。全体文書gateは新たに実行していない。
- 親所有trackingの新規行は2ファイル/2行を抽出して個別確認。最終textlint/CSpell/Sudachiは各ファイル exit 0。断片SHA-256は tasks `4801438f2f4a9a1386e9a5ae8953a62541d2b56f5db0c8ce5f439538f7f1fd58`、phases `a350e836b82627bb51e6c7e77143dfc0fa732f4f7a10e17a2fad000ef06d134e`。
- 全ログ・command・exit・source hashは `C:\\Users\\donabe\\AppData\\Local\\Temp\\issue81-04a-evidence-20261022` に保存。inventory manifest/hashを含む。
""")
section("対象ファイル", """
- `Tracker/Tracker.RuntimeHost/Properties/PublishProfiles/LinuxContainer.pubxml`
- `Tracker/Tracker.DebugHost/Properties/PublishProfiles/LinuxContainer.pubxml`
- `Testing/Duck.Testing.RefereeDriver.Host/Properties/PublishProfiles/LinuxContainer.pubxml`
- `scripts/tests/verify-linux-publish-outputs.sh`
- `Tracker/Design/Testing/aspire-simulation-test-environment.md`
- 親所有の `Tracker/Design/tasks-status.md` と `Tracker/Design/phases-status.md` は読み取り・changed-line lintのみ。今回このworkerからは編集していない。
""")
section("指摘事項", """
- aaa903d / CI 39865903670 で最初に測定された構成RedはRuntimeHost Linux publish profileの欠落だけ。標準776件成功、publish check exit 1。
- fdff6ec / CI 39867225445 attempt 1 は標準試験775 passed / 1 failed / 0 skipped。`DockerContainerWrapperLifecycleTests.LogFollowerReapTimeoutReturnsFailureAndStillCleansContainer` が期待1・実際0で失敗し、publish stepは未到達。artifact `12463689590` を保持。
- attempt 2 は同じheadで標準776件成功後、3アプリの実publish出力契約も成功。初回の終了処理試験失敗は再現しなかった。原因は未確定であり、タイミング依存は推測に留める。
- packet-flowとfull-stack CI jobは両attemptでskip。04Bの外部成果物digest/Debug専用構成は未着手。
- focused設計docの既知結果はtextlint 0、CSpellは既存 `Tailscale` line 296 のみでexit 1、Sudachiは既存10件（TCP 9、Tailscale 1）でexit 1、Yomiyasu strictはexit 0 / score 100。全文gateは既知のCSpell 232件/5文書、Sudachi 890件/5文書の不合格状態を維持。
- tracking changed-line Sudachi初回は追加語 `アプリ` でexit 1。親が語を置換し、最新2行の最終再検査は3 lint全てexit 0。
""")
section("結果", """
- 04Aの3つのLinux配布profileは実際の遠隔 `dotnet publish` と固定期待値の出力検査に結び付き、CI attempt 2で3アプリ全てpassした。
- 固定先行試験scriptは編集前後同一hash・実行modeで、期待値は緩和していない。標準 `dotnet test` も776件成功。
- exact CI source archive hashは `candidate-source-hashes.txt` と `source.tar.gz` に記録。展開したソース重複はhash記録後に削除、archive原本はTempに保持。
- 既存source lint evidenceはWSL `/tmp/issue81-referee-driver-host-design-sync-20261022/` と `/tmp/issue81-referee-driver-host-review-preparation-validation-20261022/` に保存し、SHA参照manifestを作成した。歴史的な最初の直接UNC失敗は現存する保存rawから発見できなかったため、その詳細を再構成せず欠落として記録。
""")
section("リスク", """
- attempt 1 のlifecycle failureはattempt 2で再現しなかったが、原因解明には至っていない。成功した再実行を原因修正の証拠にしない。
- passは標準776件と3アプリのmanaged publish output contractに限る。packet-flow、full-stack、実機、通常review、独立最終reviewは別途未完了。container imageの取得・build・起動はしていない。
- 全文文書lintは既知の未解消baseline findingで失敗を保つ。図の最終視認も未完了。
""")
if "## Dispatch profile（親所有）" not in old or "## Dispatch profile（親所有）" not in text:
    raise SystemExit("Dispatch heading missing")

def extract_dispatch(s):
    a=s.index("## Dispatch profile（親所有）\n")
    b=s.find("\n## ",a+4)
    return s[a:b if b>=0 else len(s)]
if extract_dispatch(old)!=extract_dispatch(text):
    raise SystemExit("parent Dispatch changed; refusing write")
if old==text:
    raise SystemExit("no changes to report")
p.write_text(text,encoding="utf-8")
print("report_updated=true")
print("dispatch_sha256="+hashlib.sha256(extract_dispatch(text).encode()).hexdigest())
print("file_sha256="+hashlib.sha256(text.encode()).hexdigest())
