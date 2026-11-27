# 全体受入fixture 安定化 実績・検証記録

状態: 一部対応済み。PR #76はDraftです。
親Issue: #75（#18配下）
追跡: #77（実装済み範囲）、#78（未着手の範囲）
PR: #76
作業ブランチ: `task/aspire-fullstack-fixture-stability`
実装コードHEAD: `96b70b648c9341a6c349bedc90182219e3afb8f7`
設計: [全体受入fixtureのチーム認識と審判状態の設計](../Tracker/Design/Testing/aspire-fullstack-fixture-stability-design.md)

## 実施結果

試験開始前にGame ControllerとCraneのチーム名を一致させ、readinessで両側の認識を確認する変更を実装しました。設定契約と受入条件はリンク先の設計文書に記載しています。

移動中のactive状態監視、専用の状態喪失失敗理由、審判履歴の記録、確認済みの試験専用AutoRef制御は未実装です。これらは子Issue #78で追跡しています。Issue #75の元の受入条件は削っていません。

## 検証記録

実装コードHEAD `96b70b648c9341a6c349bedc90182219e3afb8f7`でAppHost buildと全体受入用buildが成功しました。関連テスト54/54、Tracker.Tests全体447/447、比較モード全体受入2/2が成功しました。

受入ではHALT→PrepareKickoffYellow→NormalStartを確認し、Yellowロボットは112.08897 mm移動しました。5秒の比較測定はDuck 156 frames、TIGERs 812 frames、ER-Force 1000 frames、Vision 313 packets。評価1000回のP50は179.923 µs、P95は298.002 µsでした。終了後の対象containerは0件、AppHostは終了しました。

ビルド警告はNU1902（MessagePack 2.5.302）とASPIRE010（Aspire CLI bundle無効）です。追加のcapture_tracker_vision_inputs.pyはstraceにsudo認証を要求したため実行していません。

設計文書は通常のMarkdown対象である`Tracker/Design/Testing`に置き、既存package-lockと一致する依存で検査しました。textlint成功、CSpellは1文書・0件、Sudachi whitelistは違反0件、yomiyasuは100/100で指摘なしです。
