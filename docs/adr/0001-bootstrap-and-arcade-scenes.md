# ADR-0001: Bootstrap + Arcadeの2Scene構成

- Status: Accepted
- Date: 2026-10-23

## Context

初期案ではBootstrap + Title + Gameの3Sceneを検討しました。一方、原作のAttract・Credit・開始・プレイ・終了は一台のアーケード筐体の連続した進行です。TitleとGameを分けると、共通表示やCreditの受け渡し、Attractデモと通常プレイのロジック共有が複雑になります。

## Decision

Bootstrapをアプリの起点、Arcadeを筐体全体として、2Scene構成を採用します。Attract〜GameOverはArcade内のGameStateで進めます。SceneはGameObject群の寿命が大きく変わる境界とし、細かな進行ごとには分割しません。

## Consequences

筐体内で連続する状態と表示を保ちやすくなります。Arcade内ではGameStateと各ゲーム要素の責務を分け、進行管理の肥大化に注意します。Bootstrapにゲーム中だけのオブジェクトを置きません。将来の永続サービスを置く方針と、実際にサービスが実装済みかは区別します。
