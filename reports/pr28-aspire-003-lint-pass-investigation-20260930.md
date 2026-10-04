# ASPIRE-003 文書用語が検査を通過した原因調査

日付: 2026-09-30
調査開始時 HEAD: `daf254ec5ab76ae88ae4f92f5e6f3d1438edc820`

## 対象

ASPIRE-003 レビューで指摘した `Tracker/Design/tasks-status.md:41-45` の一般英単語が、実装時の文書検査で阻止されなかった理由を調査した。

対象例は endpoint、interface、decode、commit、failure、success、focused test、host network、geometry、realism、run、artifact、resource、packet、comparison、match である。

## 結論

主因は、厳格な Markdown lint が成功したのではなく、実行環境不足で厳格な検査経路が起動できず、代替として標準英語辞書を使う直接 CSpell が実行されたことである。

直接 CSpell は上記の一般英単語を正しい英単語として許可する。本来の共有 CSpell wrapper と専用ホワイトリスト検査は、未登録の一般英単語も失敗させる設計である。

## 本来の検査経路

`package.json` の `lint:md` は次の三段である。

1. `lint:md:text`: textlint。
2. `lint:md:spell`: `.agents/skills/review-enforcer/scripts/run-cspell-markdown.js`。
3. `lint:md:whitelist`: `.agents/skills/review-enforcer/scripts/check-markdown-whitelist-sudachi.py`。

`tools/lint/README.md` は、共有 CSpell wrapper が `--no-default-configuration` を使い、標準英語辞書ではなく `tools/lint/markdown-whitelist.yaml` を基準にすることを明記している。

## 実行環境で欠けていたもの

調査時の Windows 作業環境では次を確認した。

- `xargs`: 存在しない。
- `.agents/skills/review-enforcer`: リポジトリ側リンクが存在しない。
- `node_modules/yaml`: 存在しない。
- Python の `yaml` module: 存在しない。
- 共有 skill 本体: `C:\Users\donabe\Project\CodexSkill\skills\review-enforcer` には存在する。

したがって、`npm run lint:md` は textlint 段階の `xargs` で停止し、共有 CSpell wrapper / Sudachi whitelist checker も通常のリポジトリ内パスから実行できない状態だった。

## 代替 CSpell が通した理由

実装時と同等の `npx cspell Tracker/Design/tasks-status.md --no-progress` を再実行した。

結果は `Sudachi` 5件と `Blazor` 1件だけが Unknown word になり、ASPIRE-003 で指摘した一般英単語は出なかった。

この直接実行は共有 wrapper の `--no-default-configuration` を経由しておらず、通常の英語辞書が有効であるため、一般英単語を綴りの正しい語として許可する。

## 厳格なホワイトリストならどうなるか

指摘した単語について、`tools/lint/markdown-whitelist.yaml` の `term` / `aliases` に単独完全一致する登録は 0 件だった。

`field geometry`、`packet capture`、`comparison panel` などの複合語登録は存在するが、共有 whitelist checker は登録値全体を正規化して保持し、登録済み複合語全体だけをマスクする。複合語の構成単語を全域で単独許可する実装ではない。

共有 checker と同じ英単語正規表現とインラインコード除外を 41〜45 行へ簡易再現すると、次が検査対象として残る。

- 41行: endpoint, interface, decode, commit, failure, success, focused, test
- 42行: host, network, geometry, realism, test, commit, failure, success
- 43行: run, packet, success, artifact, network
- 45行: resource, packet, comparison, match

したがって、専用 whitelist checker が正常に実行されていれば、これらは未登録英単語として失敗対象になる。

標準辞書を無効化した CSpell の最小再現でも、上記のうち run を除く 15 語が Unknown word になった。run についても専用 whitelist checker は `ENGLISH_RE` で抽出し、単独登録がないため失敗対象になる。

## バッククォートによる回避

共有 whitelist checker の `strip_markdown_noise` は fenced code と inline code を空白へ置換してから英単語を検査する。`cspell.config.jsonc` も `markdown-inline-code` を ignore 対象にする。

したがって、通常語をバッククォートで囲めば機械検査から除外される。この例外は識別子、コマンド、ファイルパス、画面表示名などのための仕様である。

`tools/lint/README.md` 自体が、通常文の英単語を検査から逃がす目的でコード記法や引用符を使うことを禁止し、その確認をレビューへ委ねている。つまり、過剰なバッククォート囲みは機械検査だけでは防止していない。

今回の ASPIRE-003 の 41〜45 行については、指摘した一般英単語の大半はバッククォート外にあり、今回の通過原因はバッククォート回避ではない。厳格 lint が実行されず、直接 CSpell へ置き換えられたことが直接原因である。

## textlint が止めなかった理由

`.textlintrc.json` の規則は、全角空白、未解決の TBD/WIP、PRH だけである。`tools/lint/prh.yml` は `rules: []` で、英語一般語の過多や英語密度を検出する規則はない。

そのため textlint は、この種の「ルー大柴現象」を判定する役割を持っていない。

## 対象外だった可能性

`tools/lint/markdown-targets.json` の除外一覧に `Tracker/Design/tasks-status.md` は含まれていない。設計台帳自体は本来の文書 lint 対象である。

## 原因の連鎖

1. Windows 環境に `xargs` がなく `npm run lint:md` が停止した。
2. `.agents/skills` の共有 skill リンクもなく、厳格 CSpell wrapper / whitelist checker を通常経路で起動できなかった。
3. 代替として直接 CSpell を使った。
4. 直接 CSpell は標準英語辞書を使うため、一般英単語を許可した。
5. 「厳格 lint が未実施なら完了にしない」という fail-closed な運用になっておらず、代替検査の結果で作業完了報告まで進んだ。
6. textlint に英語過多の規則はなく、バッククォート例外も人手レビュー依存である。

このため、今回の一般英単語は「厳格 lint を通過した」のではなく、「厳格 lint が実行されないまま、より緩い代替検査だけを通過した」と整理できる。
