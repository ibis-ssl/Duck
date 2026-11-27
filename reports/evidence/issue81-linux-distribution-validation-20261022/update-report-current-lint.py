from pathlib import Path
import hashlib
p=Path('/home/donabe/Duck-issue81-aspire-desktop-bridge/reports/issue81-linux-distribution-implementation-20261022.md')
text=p.read_text(encoding='utf-8')
old=text

def replace_once(before, after, label):
    global text
    n=text.count(before)
    if n != 1:
        raise SystemExit(f'{label}: expected one exact match, found {n}')
    text=text.replace(before, after, 1)

def section(s, heading):
    a=s.index('## '+heading+'\n')
    b=s.find('\n## ', a+4)
    return s[a:b if b>=0 else len(s)]

dispatch_before=section(text,'Dispatch profile（親所有）')
replace_once('- Windows Node+既存cache junctionのfocused textlint/CSpell/Sudachi/Yomiyasu raw commandと差分は `/tmp/issue81-referee-driver-host-design-sync-20261022/final/` を参照。全体文書gateは新たに実行していない。', '- 現04A設計docのsource/config/runtime/dictionary同一性とbaseline・追加12行の比較結果、及びcurrent-source lint rawは `C:\\Users\\donabe\\AppData\\Local\\Temp\\issue81-04a-evidence-20261022\\04a-lint-source-context.json`、`04a-lint-runtime-context.txt`、`04a-lint-results-summary.txt` と同rootの `text-current.*` / `spell-*.{command.txt,stdout.log,stderr.log,exit-code}` / `yomiyasu-04a-*.{command.txt,stdout.log,stderr.log,exit-code}` / `sudachi-04a-*-captured.*` に保存。実行は既存Windows Node v24.20.0 / npm 11.19.0 と既存cache junctionで、依存導入なし。全体文書gateは新たに実行していない。\n- 以前の `/tmp/issue81-referee-driver-host-design-sync-20261022/final/` 等を指す38件の `prior-lint-reference.sha256` は03Cの歴史的参照のみで、04Aの結果根拠には使わない。Sudachiの最初のstdinなしcaptureはraw保持の上invalid扱いとし、stdinを渡した `*-captured.*` のみを有効結果に使う。', '04A current lint reference')
replace_once('- 全ログ・command・exit・source hashは `C:\\Users\\donabe\\AppData\\Local\\Temp\\issue81-04a-evidence-20261022` に保存。150ファイル/39,594,738 bytesのinventory manifest `evidence-files.sha256` SHA-256は `dbac0c6d7a21f6c23124d33ca2139bf6abc6f8c7be69d6addde0357e69f5c675`。gzip source archiveはTemp原本のみ、展開コピーとbinlog重複は除外。', '- 全ログ・command・exit・source hashは `C:\\Users\\donabe\\AppData\\Local\\Temp\\issue81-04a-evidence-20261022` に保存。217ファイル/39,620,424 bytes。inventory manifest `evidence-files.sha256` SHA-256 `5e3a8d76ae8ac1aa3aa12fc23d0d47d524d4283a5b0789620b2197153d15cf12`。ZIP拡張子・PK magicは0件、gzip source archiveはTemp原本のみ、展開コピーとbinlog重複は除外。', 'inventory line')
replace_once('- focused設計docの既知結果はtextlint 0、CSpellは既存 `Tailscale` line 296 のみでexit 1、Sudachiは既存10件（TCP 9、Tailscale 1）でexit 1、Yomiyasu strictはexit 0 / score 100。全文gateは既知のCSpell 232件/5文書、Sudachi 890件/5文書の不合格状態を維持。', '- 現04A設計docはbaseline `4e3099e...` とcurrent mirror/source `fdff6ec...` をSHA比較し完全一致を確認（current 36,178 bytes / `98424b1d...`）。diffの追加12行は1,632 bytes / `b0f4978b...`。textlint currentはexit 0。CSpell baseline/currentは同じ `Tailscale` 1件だけ（line 284/296）でexit 1、追加12行はexit 0。Yomiyasu strict baseline/current/追加行は全てexit 0、score 100、clean、findings 0。Sudachiのstdinあり正規captureではbaseline/currentが各10件（TCP 9、Tailscale 1）でexit 1、追加行は0件/exit 0。既存source/configとSudachiPy 0.6.11 / SudachiDict-core 20260428 / system.dic SHAを `04a-lint-results-summary.txt` に記録。全文gateは既知のCSpell 232件/5文書、Sudachi 890件/5文書の不合格状態を維持。', '04A findings')
replace_once('- 既存source lint evidenceはWSL `/tmp/issue81-referee-driver-host-design-sync-20261022/` と `/tmp/issue81-referee-driver-host-review-preparation-validation-20261022/` に保存し、SHA参照manifestを作成した。歴史的な最初の直接UNC失敗は現存する保存rawから発見できなかったため、その詳細を再構成せず欠落として記録。', '- 現04Aのsource/config/runtime/dictionaryとlint結果のcommand/stdout/stderr/exitをTemp evidence root内に保存し、baseline・追加12行との差分結果と照合した。prior-lint-reference.sha256の38件は旧03C歴史参照であり、04Aの検証根拠として扱わない。歴史的な最初の直接UNC失敗は保存rawから発見できておらず、その詳細を再構成せず欠落として記録。', 'historical evidence')
if section(text,'Dispatch profile（親所有）') != dispatch_before:
    raise SystemExit('parent Dispatch changed; refusing write')
if text==old: raise SystemExit('no changes')
p.write_text(text,encoding='utf-8')
print('report_updated=true')
print('dispatch_preserved=true')
print('report_sha256='+hashlib.sha256(text.encode()).hexdigest())
