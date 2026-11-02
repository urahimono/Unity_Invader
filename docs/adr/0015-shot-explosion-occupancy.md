# ADR-0015: 弾の即時結果通知と爆発完了の枠解放を分離する

- Status: Accepted
- Date: 2026-11-03
- Supersedes: ADR-0012の飛行のみをActiveとする表現・FlightEndedによる即時枠解放、およびADR-0014の命中時Inactive化・枠解放のみ

## Context

Issue #143ではROMの爆発中の弾枠占有を再現します。結果通知を爆発完了まで遅らせると、最終撃破のUpdate補完で加点前にWaveClearへ進むおそれがあります。通常弾履歴とUFO得点・B弾の共通結果経路を維持する必要があります。

## Decision

FlightEnded(HitTargetType)を飛行結果確定の即時・一度だけの通知として維持します。Playerは履歴を加算してShotResolvedを発行し、Driverは加点後に全滅を判定します。B弾は既存経路を利用し通常弾履歴に加算しません。

GameObject.activeSelfは飛行と爆発の占有、MonoBehaviour.enabledは進行可否です。弾自身の爆発残量で飛行と待機を区別し、物理・表示を導出します。PlayerShotは16/60秒の待機後にInactive化してBecameReusableを発行し、Playerが枠だけを解放します。占有の照会や内部Component参照をDriverへ公開しません。EnemyShotは既存の種類別phaseによる4回の更新で待機し、StepCountを完了まで保持して既存Reload・UFO共有枠を維持します。

ResetFlightは爆発も即時・無通知で中断し、表示を復元します。Pause/Disableでは残量を保持し、停止時間を取り戻しません。次Waveの通常弾占有保持、死亡復帰・新ゲーム・B弾回収と敵弾の次Wave回収は既存どおりです。

## Consequences

得点確定と再発射可否の時点を分離でき、結果通知を二重発行せず原作の発射テンポへ近づけられます。待機完了通知が一つ増え、直接所有するPlayerが生成時から破棄時まで購読します。Inactiveは再使用可能、Activeは飛行または爆発になり、残量は新しい状態として必要です。Unityの0.02秒更新・敵弾phaseによる近似、簡易Renderer表示、精密な画像・SE・画素判定の未実装は[敵弾資料](../ENEMY_SHOTS.md#爆発待機と通知issue-143)へ記録します。
