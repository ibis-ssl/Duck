# Issue #52 Planner 既定値の継続記録

## 対象

- Issue: `ibis-ssl/Duck#52` / PR #119（Draft、base: `task/issue50-aspire-006g`）
- 作業ブランチ: `task/issue52-aspire-006g2`
- 作業開始時 HEAD: `0a711049464f5fb73403bce7c35ca85c8a60edcd`
- HEAD は変えていない。既存の未コミット変更を保持したまま作業した。
- 進捗記録を加える前の実装・テスト差分の Git blob fingerprint: `982332fac94c04637e9befb3637a263dd4fbca0c`

## 今回の範囲

再起動後の引継ぎを確認し、Match 起動時に Crane の既定 Planner を明示上書きしないようにした。Match で Planner が `rvo2` の場合は `PLANNER` 環境変数と `planner:=` 起動引数を省き、Crane 自身の launch 設定にある既定値を使う。明示された別 Planner は従来どおり環境変数と起動引数で渡す。CM4 と mode-4 packet adapter の選択は Match モードに基づく既存条件を保ち、Planner 選択に結び付けなかった。

この変更は Crane の動作を失敗回避のために変えるものではない。実対戦の停止原因を直したことも意味しない。既存の8ファイルの差分は維持し、今回の変更を同じ作業ツリーに加えた。Python は実行していない。

## 変更と検証

- `Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs`: Match の `rvo2` 既定時は Planner override を渡さず、明示的な別 Planner では従来の設定経路を使う。
- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs`: Match 既定で Planner override がないことと、明示設定が保たれることを確認する。
- 実装担当が差分と上記2ファイルの責務分離を確認した。これは実装確認であり、通常レビュー・独立レビューの判定ではない。
- `git diff --check`: 成功。
- `dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~MatchMode|FullyQualifiedName~BaseSimulationResourceRegistrationTests" --logger "console;verbosity=minimal"`: 最終実行は17 passed、0 failed、0 skipped。
- 最初の実行ではテストがコマンド文字列の旧表現を期待して1件失敗した。起動引数を環境変数参照のまま維持する実装に直し、上記の最終実行で通過した。
- 全 .NET テスト `dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj -c Release --no-restore --logger "console;verbosity=minimal"`: **1,343 passed、8 failed、2 skipped**。8件は Windows 実行で native Linux verifier が必要という動的 skip 要求を失敗として返した。Linux 実行を代替していないため、全体ゲート成功とは扱わない。
- 実行中に NuGet vulnerability feed への接続不可、MessagePack 2.5.302 の既知 advisory、既存の Aspire/nullable/analyzer 警告が出た。テスト実行は完了したが、これらの警告を解消したとは扱わない。

## 現在の受入・レビュー状態

- 現在の未コミット差分に対する CI はない。直近の exact-head CI run `42425293153` は、差分追加前の HEAD `0a71104` に対して成功した。
- 直近の full-stack run `42421208991` は別 HEAD `c6a2beb` で失敗し、DCP 上の Match Controller／Simulator／CM4／Crane／Duck がそろわず、Vision record が0件だった。CM4 helper を含む `0a71104` と今回の差分では full-stack を実行していない。
- 両チームの実動作、match 完了、11003 の送信元一意性、今回の Planner 既定経路を使った Linux 実受入は未確認。
- 今回の差分に対する通常レビュー・独立レビューは未実施。以前のレビュー結果をこの差分の判定へ流用しない。
- PR #119 は Draft のまま。Issue #52 の受入条件が満たされていないため、親ブランチへの統合は行わない。

## 次の作業

現在の差分を実装担当の通常レビューに回し、必要な修正と exact-head CI を終えてから、CM4 helper の起動確認を含む最新 HEAD の受入を行う。受入ログで DCP 起動と mode-4 adapter の動作を確認し、審判指令・両チームの動作・送信元一意性・記録完了を個別に判定する。失敗または未確認の項目は成功扱いしない。
