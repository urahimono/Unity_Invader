# ADR-0006: GameplayDriverへゲーム全体の実行制御を統合する

- Status: Superseded by [ADR-0007](0007-push-display-and-encapsulation.md)
- Date: 2026-10-24

## Context

従来はGameplayDriverが入力・時計・State遷移を実行し、別のMonoBehaviourであるGameStateControllerが現在StateとArcadeGameplaySimulationを保持していました。ゲーム全体の進行を誰が所有するかが分かりにくく、表示Controllerも別の保持Componentを経由していました。

## Decision

GameplayDriverがCurrentState、ChangeState、Simulationを持ち、入力取得・Updateからの時間進行・WaveClear検出・暫定1秒待機・次Wave開始・開発用Playing自動開始を担当します。GameStateControllerは統合して削除し、代替のStateManager等は追加しません。

ArcadeGameplaySimulationはGameplaySimulationへ改名します。通常C#による60Hz tick、Player・Shot・Formation・命中・Score・Waveのルールは変更せず、GameState制御やUnity Input Systemは移しません。

PlayerControllerとInvaderFormationControllerはGameplayDriverのCurrentStateとSimulationを参照し、LateUpdateで表示を同期します。独自にゲームロジックを進めません。Sceneでは既存GameplayDriverを保持し、旧ComponentのState値と表示参照を移し、進行管理オブジェクトをGameplayへ改名します。

## Consequences

ゲーム全体の実行入口とState・Simulationの所有者がGameplayDriverへ揃います。Unity側の実行制御とUnity非依存のゲームルールの境界は維持します。待機やState遷移のさらなる分離、新機能、ゲーム挙動・数値・入力仕様の変更は行いません。

[ADR-0005](0005-playing-controller-owns-clock.md) の「入力・共通時計を一つの実行入口に置き、表示側から分離する」判断を拡張するもので、その判断は置き換えません。ADR-0005の当時の説明を保持し、現在名の補足だけを追従させます。現在構造は [Architecture](../ARCHITECTURE.md) を参照してください。
