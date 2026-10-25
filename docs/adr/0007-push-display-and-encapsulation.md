# ADR-0007: 表示データをDriverからPushし、可変モデルを所有者内部に閉じる

- Status: Superseded by [ADR-0008](0008-component-gameplay.md)
- Date: 2026-10-25
- Supersedes: [ADR-0006](0006-gameplay-driver-owns-execution-state.md)

## Context

ADR-0006で実行制御をGameplayDriverへ統合しましたが、表示ControllerがDriverの公開GetterからSimulation・Body・Shot・Formationを辿り、所有者の可変モデル実体を取得する構成が残りました。[Issue #46](https://github.com/urahimono/Unity_Invader/issues/46) は、[PR #47](https://github.com/urahimono/Unity_Invader/pull/47) で確定したカプセル化・依存方向の原則を実装へ適用します。

## Decision

GameplayDriverはState・入力・GameplaySimulationと両表示Controllerを所有し、Updateでゲーム進行、LateUpdateで表示値の生成・配信を行います。初期表示はStartでも配信します。CurrentState・Simulationの公開GetterとChangeStateのpublic APIは廃止し、表示ControllerからDriverやモデルへの参照・検索をなくします。Interfaceで参照先を隠す代替も導入しません。

表示には可変モデルを含まないreadonly structを渡します。Formationの55個分はDriverと表示側の固定バッファを再利用します。Viewは受信値を保持して描画へ反映し、Disableで隠し、再Enableでキャッシュから復帰します。Driver停止時には最終表示値を配信し、以後ゲーム進行を停止します。

GameplaySimulation・PlayerUnit・InvaderFormation・単独検証用InvaderFormationSimulationも、所有する可変オブジェクトを返しません。必要な観測はまとまった不変の値で行い、更新・命中・撃破・Score加算は所有者内部の既存責務に残します。

検証操作・観測はAssets/Editorと、必要最小限の#if UNITY_EDITORの入口へ分離します。観測スナップショットのSlot要素も不変の値として複製し、可変モデルを返しません。通常ランタイムからEditor専用APIは呼びません。

## Consequences

Driver→Viewの一方向依存となり、表示コードがゲームモデルを更新できなくなります。通常フレームの配信に新規配列やListを作らず、既存55 Rendererを再利用します。値スナップショットで検証するため、旧設計の共有参照同一性テストは、観測値と表示の一致・状態保持・独立したスナップショットの確認へ置き換えます。

ADR-0006のState・Simulation所有者をDriverへ統合する判断は維持し、公開APIと表示側からの参照を許していた部分を本ADRで置き換えます。[ADR-0005](0005-playing-controller-owns-clock.md) の単一の入力・共通時計という判断も維持します。60Hz、tick順序、移動・Shot・Collision・Score・WaveClear待機・入力仕様・数値は変更しません。新しいManager・イベント基盤・汎用Interfaceは追加しません。
