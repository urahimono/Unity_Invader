# ADR-0004: Unity依存コードとゲームルールを分離する

- Status: Accepted
- Date: 2026-10-23

## Context

ゲームルールをScene参照やMonoBehaviour、Transform、物理コールバックへ埋め込むと、表示設定と仕様が混ざり、ルール単体の検証・調整が難しくなります。

## Decision

可能な範囲でゲームルールを通常C#に置きます。MonoBehaviour・Input System・Scene・Sprite・Audio・GameObject生成はUnity側に置き、専用の入力状態と論理モデルを介して接続します。Unity PhysicsやTransformをゲームルールの基準にしません。

## Consequences

Sceneや描画から独立したロジック検証が容易になります。一方、Unity側との接続・表示同期は別途検証が必要です。境界のためだけに汎用フレームワークを導入せず、このゲームに必要な薄い接続に留めます。この方針は、現在すべてのState進行処理がUnity非依存という意味ではありません。
