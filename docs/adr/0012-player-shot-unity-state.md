# ADR-0012: PlayerShotの飛行・進行可否をUnityの有効状態へ統合

- Status: Partially Superseded
- Date: 2026-10-26
- Supersedes: [ADR-0010](0010-player-shot-lifecycle-and-events.md)の飛行状態と進行可否の表現のみ

- Partially superseded by: [ADR-0013](0013-hit-receivers-and-ufo-scoring.md)（命中契約・UFO撃破通知と得点順）

- Partially superseded by: [ADR-0014](0014-player-shot-results-and-scoring.md)（通常弾の結果通知・履歴所有・加点順）

## Context

Issue #87ではUnityが保持する状態と独自boolの二重管理をなくします。構成済みInactive PrefabからInstantiate→TryLaunchだけで発射できるAPI、入力受付と通知順は維持します。

## Decision

GameObject.activeSelfを飛行状態、MonoBehaviour.enabledを進行可否の正とします。PrefabとSceneの待機弾はInactiveで保存します。Rigidbody2D/Renderer参照はPrefabに保存し、Awakeや反復初期化に依存しません。非アクティブな親の下ではTryLaunchを拒否します。OnEnable/OnDisableでsimulated・描画・速度を導出します。PauseはActiveを保持してenabledのみ停止し、再開時に同じ飛行を続けます。

Invader.TryReceiveHit→Formation.NotifyDefeated→Driverは同期的な無効化・集計・状態反映だけで、Shotの命中処理へ再入しません。Ufo.TryReceiveHitも自身の解除だけです。Shield吸収にも弾へのコールバックはありません。そのため_resolvingHitを削除します。成立後にShotをInactiveへ戻しFlightEnded、UFOなら続けてUfoDefeatedを通知します。最終撃破の途中でenabled=falseになっても通知を完了します。ResetFlightは無通知です。

Playerは入力時点の同時1発制限に必要な射撃枠を所有します。発射成功時に占有し、Awakeで一度購読するFlightEndedで解放、OnDestroyで解除します。Pause中も購読・占有を保持し、Respawnの強制中断時だけ明示的に枠を解放します。pendingFireとrelease/再押下ゲートは従来どおりです。DebugCarpetBombはTryLaunchの成功で空き弾を使用し、成功時だけレーンを消費します。

## Consequences

PlayerShotの3独自boolと公開InFlightを廃止し、利用側の状態照会をなくします。購読者なし・複数インスタンスの発射も可能です。通常弾の履歴とUFO加点は引き続きDriverが購読し、B弾はUFO通知だけを購読します。旧ADRのイベント・参照構成とADR-0011の責務境界は維持します。
