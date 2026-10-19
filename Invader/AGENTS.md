# Unity作業ルール

- Unity Project Rootはリポジトリ内の `Invader/`（このファイルのあるディレクトリ）。
- 使用Unityバージョンは `6000.6.4f1`。正確なrevisionは `ProjectSettings/ProjectVersion.txt` を参照する。
- UnityやPackageのバージョンを無断で変更しない。Packageの追加も依頼範囲内に限る。
- `Assets/`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/` と必要な `.meta` をGit管理する。
- アセットと `.meta` は一緒に追加・移動・削除し、既存GUIDを不用意に変更しない。
- `Library/`、`Temp/`、`Logs/`、`Obj/`、`UserSettings/`、IDEの自動生成ファイルを管理対象にしない。追加前に `.gitignore` の適用を確認する。
- `.unity` / `.prefab` のYAML直接編集は原則避ける。Scene / Prefabを機械生成する場合はUnity Editor APIを優先する。直接編集が必要な場合は理由と検証方法を説明する。
- MonoBehaviourを必要以上に増やさず、Unity依存コードとゲームロジックの責務を意識する。
- 将来使うかもしれないという理由だけで抽象化せず、Space Invadersの再現に必要な設計に留める。
- 既存のSampleSceneやテンプレート素材を依頼なく変更・削除しない。
- Unity Editorでの起動・コンパイル・動作確認を行ったかを報告する。Git上の検証だけでUnity動作確認済みとしない。
