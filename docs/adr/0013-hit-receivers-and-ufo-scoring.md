# ADR-0013: 命中受理の統一とUFO自身からの撃破通知

- Status: Partially Superseded
- Date: 2026-10-27
- Supersedes: ADR-0010/0011/0012のPlayerShot.UfoDefeatedによる通知・得点順のみ

- Partially superseded by: [ADR-0014](0014-player-shot-results-and-scoring.md)（通常弾の結果通知・履歴所有・加点順）

## Context

Issue #89ではPlayerShotからInvader/Ufoの具体型判定と得点固有の通知を除きます。既存IHitReceiverはEnemyShotも利用するため、対象制限なしで実装を広げると友軍誤射が発生します。通常弾終了後のUFO得点表選択とB弾の履歴除外は維持します。

## Decision

IHitReceiver.TryReceiveHitへ命中元Componentを1引数追加します。PlayerShot/EnemyShotはthisを渡すだけです。InvaderとPlayerは命中元を使わず、Ufoは自身をInactiveにしてDefeated(source)を発行します。FormationはInitializeで一度購読し、OnDestroyで解除、既存Driverへ転送します。Ufoは上位管理者の参照を保持しません。Invaderの既存Formation通知は維持します。

UFO通知は弾の終了より先に届きます。Driverは命中元と保存済みの通常弾を参照比較し、通常弾なら履歴+1を得点添字だけに使います。実際の履歴を先行更新したり別に保持せず、通常弾FlightEndedでのみ1回進めます。B弾には補正も履歴加算もありません。Shot.UfoDefeatedとDriver/B弾の購読は廃止します。

Player(8)、Invader/UFO(9)、PlayerShot(10)、EnemyShot(11)のレイヤーを保存し、Physics2DでPlayerShot↔PlayerとEnemyShot↔Invader/UFOを除外します。これにより受信側も弾の具体型を識別する必要がありません。その他の組合せ・ShieldのDefaultとピクセル/移動区間判定は変更しません。Prefab/SceneはEditor APIで設定し、新規生成コードも同じ割当てを使います。

## Consequences

弾は命中受理の問い合わせと自身の終了通知だけを担当します。UFO退出・強制回収・Waveリセットは撃破イベントを発行せず加点しません。終了回数・得点値は維持しますが、UFO加点はFlightEndedより前になります。Pause中も購読を維持し、最終Invader撃破時の正常終了通知を完了します。Layer設定が誤射防止の契約であり、Editor設定検証と実Physics2D接触で確認します。汎用HitContext・新規Managerは導入しません。
