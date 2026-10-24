# ADR-0005: PlayingControllerが入力と共通時計を所有する

- Status: Accepted
- Date: 2026-10-23

## Context

[PR #13](https://github.com/urahimono/Unity_Invader/pull/13) でPlayerControllerがArcadeGameplaySimulation全体をAdvanceする構造を見直しました。Playerの表示担当がFormation・Collision・Scoreまで含むゲーム全体の時計を所有すると、表示の有効・無効とゲーム進行が結び付き、後続のWaveClear等もPlayer表示側へ集中してしまいます。

## Decision

専用のPlayingControllerをUnity側の入力・共通時計担当とします。ArcadeGameplaySimulationを進める実行時の入口を一つにし、PlayerControllerとInvaderFormationControllerは表示同期担当にします。

## Consequences

表示Controllerとゲーム進行の責務が分かれ、WaveClearなどの進行をPlayerControllerへ押し込まずに済みます。表示側を無効にしても時計は独立し、複数の表示担当が重複してモデルを進めることを防ぎます。入力・時間の供給と表示同期の実行順は接続側で保証する必要があります。現在の所有関係・更新順は [ARCHITECTURE.md](../ARCHITECTURE.md) を参照します。
