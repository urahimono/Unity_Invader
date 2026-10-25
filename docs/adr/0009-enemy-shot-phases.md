# ADR-0009: 敵弾だけをFormationの60Hz種類別phaseで進める

- Status: Accepted
- Date: 2026-10-25
- Extends: [ADR-0008](0008-component-gameplay.md)

## Context

Issue #77では3種類の敵弾の更新順と、他種の移動step countによるReload判定が必要です。弾ごとの独立FixedUpdate時計では原作の種類別phaseを表現できず、発射だけを秒数で制御しても同じ規則にはなりません。

## Decision

Formationは既存の発射元選択に加え、敵弾だけの60Hz蓄積時計と種類別phaseを所有します。選択したEnemyShotへ1stepを依頼し、EnemyShotは自身のRigidbody2D・移動step count・Shield/Player衝突と消費を所有します。Formationは必要なcount値のみを読み、位置・生死・Scoreを別モデルに複製しません。ScoreはDriverの読み取り専用値を参照します。

UnityのFixed Timestepは0.02秒を維持します。EnemyShotの独立時計を廃止し、物理stepの先頭で前stepの移動速度を保持位置へ戻してから、選択されたphaseのMovePositionを設定します。ゲーム全体の時計・PlayerShot・編隊移動・待機時間は60Hz化せず、汎用Managerや時刻基盤は追加しません。Fデバッグは編隊移動時計だけに残します。

## Consequences

発射試行と移動step countの前後関係を明示でき、3種類の既存EnemyShot衝突経路を共用できます。仮想tickとPhysics2Dの確定時刻は一致せず、ときに別phaseが同一物理stepへ量子化されます。ビーム別ISR・爆発待ち・初期phase/死亡/Wave持越し等との残る差は[敵弾仕様](../ENEMY_SHOTS.md)に記録し、原作同期済みとは扱いません。
