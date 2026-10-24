# Architecture Decision Records

ADR（Architecture Decision Record）は、重要なArchitecture判断と、その理由・結果を残す記録です。小さな実装判断すべてをADR化しません。現在構造は [ARCHITECTURE.md](../ARCHITECTURE.md)、長期原則は [PROJECT_PRINCIPLES.md](../PROJECT_PRINCIPLES.md) を読みます。

## 運用

- 連番のファイルにStatus / Date / Context / Decision / Consequencesを記載し、設計変更のレビューと合わせて追加します。
- AcceptedのADRは原則として過去の判断記録として保持します。判断を変える場合は旧ADRを黙って書き換えず、新ADRでSupersedeし、旧ADRのStatusをSupersededとして後継へのリンクを付けます。
- 初期5件はPhase 3までに確定した判断を今回正式に文書化したものです。Dateはリポジトリへの記録日（2026-10-23）で、過去の議論・決定日を推定したものではありません。

## 初期ADR

| ADR | 判断 |
| --- | --- |
| [0001](0001-bootstrap-and-arcade-scenes.md) | Bootstrap + Arcadeの2Scene構成 |
| [0002](0002-logical-coordinate-system.md) | 224×256論理座標 |
| [0003](0003-60hz-logical-tick.md) | 60Hz論理tick |
| [0004](0004-unity-game-logic-boundary.md) | Unity依存コードとゲームルールの分離 |
| [0005](0005-playing-controller-owns-clock.md) | PlayingController（現 GameplayDriver）による入力・共通時計の所有 |

## 後続ADR

| ADR | 判断 |
| --- | --- |
| [0006](0006-gameplay-driver-owns-execution-state.md) | GameplayDriverへState・Simulationの保持と実行制御を統合 |
| [0007](0007-push-display-and-encapsulation.md) | Driverから表示値をPushし、可変モデルを所有者内部に閉じる（0006をSupersede） |

## 初期記録の根拠

[Issue #15](https://github.com/urahimono/Unity_Invader/issues/15) とNotionの [Unity設計](https://www.notion.so/3ff4f337d5bf814eaa3dd6871eab5160) の確定判断を、Phase 3完了コードと照合しました。Notionには古い暫定案と未実装の責務図も含まれます。現行構造はコードを優先し、ADRには採用理由だけを要約しています。ADR-0005は [PR #13](https://github.com/urahimono/Unity_Invader/pull/13) のレビュー対応を根拠にしています。各ADRは過去会話や外部資料を開かなくても判断の要旨を読めるようにします。
