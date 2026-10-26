# ADR-0014: Shield命中受付と移動区間検索の分離

- Status: Accepted
- Date: 2026-10-27
- Partially supersedes: [ADR-0013](0013-hit-receivers-and-ufo-scoring.md)のComponent単独の命中引数とShield例外のみ

## Context

Issue #91では弾からShield.TryAbsorbShotへの具体型依存を除去する。ShieldのColliderは破壊状態に追従しないため、通常のTrigger侵入だけでは穴通過や移動区間途中の1pxを判定できない。

## Decision

読み取り専用HitInfoはSource、矩形中心のStart/End、Size、Directionだけを持つ。Directionは移動量ゼロでも上下を表す。PlayerShotは下端アンカーから高さ半分上、EnemyShotは既存Rigidbody2D中心を使う。全受信側がIHitReceiver.TryReceiveHit(in HitInfo)を共有する。

ProjectileHitDetectorは静的な内部ヘルパーとし、通常Triggerを転送する一方、ISweptHitReceiverはTriggerから除外する。移動区間のOverlapBox検索後、候補への副作用のない距離照会で最短の1件だけに共通命中受付を呼ぶ。選択中に破壊せず、Shieldは受付時に既存画素探索を再実行してCarveする。画素位置のキャッシュや新しい可変状態は持たない。現行の鉛直弾・回転なしShieldに限定する。

## Consequences

弾はShieldを知らず、Shieldは画素・破壊・Texture・復元を引き続き所有する。自弾の予測区間、敵弾の種類別位相の移動区間、通常のTrigger・Layer制限・UFO通知とFlightEndedの順序は維持する。距離照会と受付で選択したShieldの画素走査が2回になるが、候補検索の追加や画素配列の割り当てはない。通常Triggerとsweptのグローバルな優先順位は導入しない。InvaderのShield侵食は今回の対象外。
