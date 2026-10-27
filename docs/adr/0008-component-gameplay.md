# ADR-0008: PlayingをComponent・Physics2D中心へ移行する

- Status: Partially Superseded（PlayerShotの飛行状態・結果通知のみ[ADR-0010](0010-player-shot-lifecycle-and-events.md)で更新）
- Date: 2026-10-25
- Supersedes: [ADR-0002](0002-logical-coordinate-system.md)の座標変換・位置の正、[ADR-0003](0003-60hz-logical-tick.md)、[ADR-0004](0004-unity-game-logic-boundary.md)、[ADR-0005](0005-playing-controller-owns-clock.md)、[ADR-0007](0007-push-display-and-encapsulation.md)

- Partially superseded by: [ADR-0014](0014-player-shot-results-and-scoring.md)（最終撃破の得点・WaveClear確定順）

## Context

[Issue #57](https://github.com/urahimono/Unity_Invader/issues/57)でGameplaySimulationの廃止と本編PlayingのComponent/Physics2D移行が合意されました。独立PoCの考え方を本編の既存素材・入力・Score・Waveへ適用します。旧Push規約と独立モデルを新Componentへ併存させると、位置・生死・表示の正が二重化します。

## Decision

DriverはState・Score・Waveと待機を所有します。Playerは入力と移動・単一弾の発射、Shotは自身のTrigger検出と命中後の消費、Invaderは自身の被弾・撃破・表示、Formationは55体の配置・集団移動・生存数・通知集計を担当します。通知はInvader→Formation→Driverの用途を絞ったメソッドで行い、共通イベント基盤やInterfaceは導入しません。

Transform/Rigidbody2DとGameObject有効状態を位置・生死の正とし、GameplaySimulation、旧モデル、表示スナップショットと専用表示Controllerを削除します。Component間の必要な参照保持は認め、所有する可変Component・管理コレクションを返す公開Getterや連鎖アクセスの禁止は維持します。

Playerの入力取得はUpdate、移動/発射とFormation周期はFixedUpdate、Shotの移動・接触検出はUnity Physics2Dを使います。ProjectSettingsは変更せず、速度を60/240px毎秒、Formation周期を20/60秒へ換算します。論理画面224×256と従来の寸法・素材比率を維持し、1px＝0.01 Unity単位にします。ADR-0002の論理画面・寸法の基準は維持し、位置の正とUnityへの倍率は本ADRで更新します。

命中の受理中は弾側で処理中ガードを立て、個体は無効化後に1回だけ通知します。弾は受理後に無効化され、同時接触でも最大1体です。物理通知順序による左右優先は保証しません。非PlayingとDriver無効時は入力・移動・表示・接触を停止し、位置・Shot占有・生死を保持します。次Waveでも55体とShotを再利用します。

## Consequences

Hierarchy/Inspectorで個体・Collider・弾を追えるようになります。旧tick順序と独自矩形の厳密な境界・優先順位を保証する構造ではなくなり、物理stepと描画FPS別の操作・接触を実機で確認する必要があります。旧モデル依存のEditor検証は削除し、接続チェックと限定的なPlay手順へ置き換えます。

Scene・PrefabはEditor APIで移行します。ライセンス制約で実行できない場合は、移行未適用とPlay未検証を明記し、ローカルUnityで移行・確認してから評価します。本編に敵弾・被弾・Shield等は追加しません。ADR-0007等の本文は過去の判断として残し、Statusと後継リンクのみ更新します。
