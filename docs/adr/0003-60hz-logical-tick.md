# ADR-0003: 描画と分離した60Hz論理tick

- Status: Accepted
- Date: 2026-10-23

## Context

描画フレームごとの移動やdeltaTimeによる連続移動だけでは、FormationやShotのstep挙動を調整しにくく、描画FPSとゲーム速度が結び付きやすくなります。

## Decision

ゲームルールは60Hzの論理tickを基準に進めます。render frameとgame tickを分離し、経過時間に応じて必要なtickを処理します。描画は最新の論理状態を表示します。

## Consequences

描画FPSに依存せずゲーム速度を扱い、FormationやShotのstepを調整しやすくなります。1描画フレームで複数tick進む場合も、相互作用をtick単位で評価する必要があります。入力の取り込みとtickの境界、停止・再開時の蓄積時間に注意します。60Hzの採用は原作の全挙動がframe-perfectに再現済みという意味ではありません。
