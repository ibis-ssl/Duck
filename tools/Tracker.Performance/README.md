# Duck の追跡処理を固定入力で計測する

既存の SSL-Vision 記録を使い、変更の前後で同じ入力と設定を再生する。準備するものは .NET SDK 10.0、利用権限のある gzip 圧縮済みの `SSL_LOG_FILE` v1、RuntimeHost の JSON 設定ファイル。記録本体は手元に保管する。

## 実行手順

リポジトリの最上位で `pwsh` から実行する。`archive` は手元の記録の保存場所に置き換え、結果のファイル名は実験ごとに変える。

```powershell
$archive = 'C:\path\to\recording.log.gz'
$settings = 'Tracker/Tracker.RuntimeHost/appsettings.json'
$revision = (git rev-parse HEAD).Trim()
$result = Join-Path ([IO.Path]::GetTempPath()) 'duck-tracker-performance-before.json'

dotnet build tools/Tracker.Performance/Tracker.Performance.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw '計測ツールのビルドに失敗しました。' }

dotnet tools/Tracker.Performance/bin/Release/net10.0/Tracker.Performance.dll `
  --archive $archive --settings $settings --profile sim --source-revision $revision > $result
if ($LASTEXITCODE -ne 0) { throw '計測ツールの標準エラーを確認してください。' }
```

開始は記録内で最初に現れる 種別番号 4（`SSL_WrapperPacket`）の受信時刻。終了時刻ちょうどの入力は除外する。既定は `--duration-seconds 30`、`--warmup-seconds 5`、`--iterations 7`。暖機に同じ区間を最低 3 回かつ 5 秒以上再生する。`--profile` を省略すると、指定したファイルの有効な設定を使う。`--help` で引数を確認できる。

種別番号 4 の受信時刻が区間の終わりに達するか、記録単位の境界でファイルの終端に達するまで読む。`input.reachedWindowEnd` が `false` なら、終了時刻以降の 種別番号 4 は見つからなかった。ファイル全体の SHA-256 は、選択範囲外の圧縮バイト列も含めて計算する。記録単位の長さは 16,777,216 バイト以下とし、種別番号 4 の空の通信データ、読み取り範囲内の 種別番号 4 同士の受信時刻の逆行や途中で切れた記録単位はエラーとする。

結果の `source.sourceRevision` は引数の値。各アセンブリのビルド構成、版、識別子も保存する。JSON に入力の保存場所と元の通信データは出力しない。

## 計測の範囲

各再生は新しい追跡状態で始まる。指定した RuntimeHost 設定の周期を仮想時刻で進め、最後の入力を取り込む通常周期で終了する。同梱の設定は 16 ms 周期。これは進行の間隔であり、処理時間の目標を評価するには実稼働で別途測定する。

同じ周期の入力で、バッファに残った最新値は記録順に処理する。バッファの並べ替えに使う時刻は 100 ns 単位で、直前の値以下になる場合は直前の値から 1 刻み進める。周期の割り当てと仮想遅延には元の受信時刻を使う。並べ替え時刻の調整は入力の復号・保存時間の外で行う。

| 出力項目 | 計測範囲 |
| --- | --- |
| `measurements[].cycleMicroseconds` | `ProcessLatestPacket` の呼び出しから、最新入力の取り出し、追跡、配信用パケット生成、主記憶上での protobuf 直列化を経て、処理が戻るまで。空の周期も含む。 |
| `measurements[].receiveMicroseconds` | 入力 1 件の protobuf 復号から、`StorePacket` の複製・保存まで。 |
| `measurements[].virtualCommitLagMilliseconds` | 確定した追跡結果に含まれる検出のうち最後に届いた入力の記録受信時刻から、確定した仮想周期までの差。 |
| `measurements[].cycleAllocatedBytes`、`receiveAllocatedBytes` | 各区間で再生スレッドが割り当てた総バイト数。 |
| `measurements[].wallMilliseconds`、`processCpuMilliseconds`、`gcCollectionsByGeneration` | 1 回の再生を通した経過時間、プロセス CPU 時間、世代 0・1・2 の回収回数。周期外で行う件数集計も含む。 |

時間の分位点は各再生の p50、p95、p99 と最大値。各周期・入力で時間と割り当てを観測するための小さな負荷がある。保存済み記録の展開、出力ハッシュ計算、UDP の送受信、OS の実時間待機は、正式な計測の範囲外。プロセス CPU 時計の粒度は OS に依存する。CPU の標本を採取する場合は独立した再生で行う。

同じ設定と入力で行う 2 回の品質確認は正式な時間計測から分離する。配信順に直列化済みバイト列を連結した SHA-256 と件数を比較する。`counts` はカメラごとの最新値への置換、検出を含まない入力の置換、遅着による破棄、フィールド形状変更による保留破棄、再生末尾の保留、確定、配信を数える。再生末尾に追加の確定処理は行わない。

確定または配信が 0 件の場合、CLI は `comparisonEligible` を `false` とし、標準エラーに理由を出して終了値 1 を返す。調査用の件数とハッシュは JSON に残る。比較には終了値が 0 で `comparisonEligible` が `true` の結果を使う。

## 変更前後の照合

前後とも計測ツールを含む該当コミットを展開した作業場所で `-c Release` を指定してビルドする。結果の `source.tool`、`source.core`、`source.runtimeHost` それぞれで、`configuration` が `Release` であり、`informationalVersion` に含まれるコミットと、測定対象として引数に指定した完全なコミット識別子が一致することを確認する。指定した `source.sourceRevision` は照合のための入力値。実行したアセンブリの識別には、埋め込まれた版と構成も照合する。

いずれかの版や構成が不一致なら、その結果を破棄し、測定対象のコミットに対応する変更のない作業場所で `-c Release` を指定して再ビルドする。未コミットの変更がある場合や、版にコミットが埋め込まれない環境では、対象コミット、追加・変更したファイル、実際に実行した各アセンブリのハッシュを対応付けた明細が必要になる。

計算機、ビルド構成、区間、暖機と反復条件をそろえ、入力と設定のハッシュ、カメラ数、入力件数・確定件数・出力件数、出力ハッシュを照合する。処理時間、割り当て、CPU 時間、世代別の回収回数を記録し、周期の各分位点は再生ごとに比較する。

## 既存記録との照合例

課題 147 の手元の実記録で用いた比較条件を示す。記録本体は添付していない。

| 条件 | 値 |
| --- | --- |
| 圧縮記録 SHA-256 | `F1521FE40977CD97E2E886C6056BBB779C849C397A61A090A0003751CCA380FF` |
| 区間の連結入力 SHA-256 | `ED03D2EF7771A2AA17CC9D0A8034D821A56CFB035A0D430B50BD0B90071DD023` |
| RuntimeHost 設定 SHA-256 | `0ECC3BC8A16224C62FBFB7CE85E2CAF238F713A044C9F3A29477BEAE09D108D1` |
| 入力 | 先頭 30 秒、1,876 件、1 カメラ、1 入力あたり最大 22 ロボットと 1 ボール |
| 設定 | `sim`、周期 16 ms、並べ替え窓 10 ms、統合窓 20 ms |
| 確定と配信 | 908 件。遅着破棄 0 件、最新値の置換 143 件、末尾の保留 1 件 |
| 連結出力 SHA-256 | `330BA050FA6C52A393F68A9FD8DFD079AEF9D489304108C7987CF97D2A4DE420` |

## 外部トラッカーと比較する場合

同じ SSL-Vision 記録から同じ区間を実時間の UDP 入力として再送し、同じ計算機とネットワーク条件で実行する。既存の比較用構成の接続先は [AppHost](../../Testing/Duck.Testing.AppHost/Program.cs) と [比較構成の CI](../../.github/workflows/dotnet-test.yml) を参照。共通の送受信計測で入力・出力件数、欠落、出力間隔を記録し、CPU は同じ区間の追跡プロセスを対象にする。

比較対象は TIGERs Mannheim の Sumatra と ER-Force のトラッカーで、入力は SSL-Vision の検出パケット。出力は各送信元の `TrackerWrapperPacket` として識別する。Duck の仮想周期内の分位点は Duck 内の変更前後の比較に使う。外部との同一条件の実測、実時間での過負荷、多カメラ負荷、追跡精度はこの再生では未検証。[Sumatra](https://github.com/TIGERs-Mannheim/Sumatra)・[ER-Force](https://github.com/robotics-erlangen/framework)・[RoboCup SSL Game Controller](https://github.com/RoboCup-SSL/ssl-game-controller)
