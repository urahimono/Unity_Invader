# Unity_Invader

Unityを使用してSpace Invadersの挙動を再現する個人開発・学習プロジェクトです。参考プレイ動画を基準に忠実な再現を目指します。

元ゲームから抽出した画像・音声・動画などの素材は含みません。これらの素材をリポジトリへ追加しない方針です。

- Unity Project Root: `Invader/`
- Unity: `6000.6.4f1`
- 現在の状態: Bootstrap → Arcadeの起動経路とGameStateの土台を実装しています。Player等のゲーム内容は未実装です。

Unity Hubから `Invader/` を指定して、上記バージョンのEditorで開いてください。

`Assets/Scenes/Bootstrap.unity` を開いてPlayすると、`Arcade.unity` に切り替わります。
Arcadeの `GameState` オブジェクトにある `GameStateController` が現在Stateを保持します。
初期値の `Attract` は土台としての仮置きで、原作の起動ルールを確定するものではありません。

後続コードは `UnityInvader.GameStateController.CurrentState` で現在Stateを参照し、
`ChangeState(GameState nextState)` で変更できます。`UnityInvader.GameState` は
`Attract` / `PlayerReady` / `Playing` / `PlayerDead` / `WaveClear` / `GameOver` の6値です。
各Stateの処理、自動遷移、入力、Player / Invader / Shot、Score / Wave、tickはまだ実装していません。

Sceneは `Assets/Editor/ArcadeSceneSetup.cs` のUnity Editor APIで生成しています。
`Unity Invader > Create Initial Scenes` はSceneが未作成の場合のみ使用できます。
既存Sceneの上書きを防ぐため、作成済みのプロジェクトで再実行するとエラーになります。

開発はIssueを起点に、`develop` から `codex/` ブランチを作成し、検証後に `develop` 向けDraft PRでレビューします。作業ルールは [AGENTS.md](AGENTS.md) と [Invader/AGENTS.md](Invader/AGENTS.md) を参照してください。
