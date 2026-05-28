# TreePaste

Language: [English](#english) | [日本語](#japanese)

---

<a id="english"></a>
## Overview (English)

TreePaste is a tool that pastes files copied to the clipboard while preserving the original folder structure.  
You can select the folder hierarchy level from a tree view and paste from any level you choose.

### Features

- Resides in the **system tray** on startup and runs in the background
- Press **Ctrl+Alt+V** at any time to bring up the window
- Displays files and folders copied to the clipboard as a tree
- Click on a folder node in the tree to select the paste destination level
- Automatically detects the path of the active Explorer window and sets it as the paste destination
- Falls back to the Desktop if no Explorer window is found

### How to Use

1. Launch TreePaste (it will reside in the system tray)
2. Copy the files you want to paste in Explorer (Ctrl+C)
3. Open the destination folder in Explorer
4. Press **Ctrl+Alt+V** to bring up the window
5. Select the paste starting point from the folder hierarchy shown in the tree view
6. Execute the paste

### Requirements

| Item | Details |
|------|---------|
| OS | Windows 10 / 11 (x64) |
| .NET | .NET 10 |

### Build

```
dotnet build src/TreePaste/TreePaste.slnx
```

### Publish

```
dotnet publish src/TreePaste/TreePaste/TreePaste.csproj -c Release -r win-x64
```

Output is placed in `src/publish/win-x64/`.

### License

[MIT License](LICENSE)

---

<a id="japanese"></a>
## 概要 (Japanese)

クリップボードにコピーしたファイルを、フォルダー構成を維持したままペーストするツールです。  
ツリー表示でフォルダー階層を選択し、任意の階層からペーストできます。

### 機能

- 起動するとタスクトレイに常駐し、バックグラウンドで動作
- **Ctrl+Alt+V** でいつでもウィンドウを呼び出せる
- クリップボードにコピーされたファイル・フォルダーをツリー表示
- ツリー上のフォルダー階層をクリックして、ペースト先の起点を選択可能
- アクティブなエクスプローラーウィンドウのパスを自動検出し、ペースト先として設定
- エクスプローラーが見つからない場合はデスクトップをペースト先に設定

### 使い方

1. TreePaste を起動（タスクトレイに常駐）
2. ペーストしたいファイルをエクスプローラーでコピー（Ctrl+C）
3. エクスプローラーでペースト先フォルダーを開く
4. **Ctrl+Alt+V** でウィンドウを呼び出す
5. ツリービューに表示されたフォルダー階層から、ペースト起点を選択
6. ペーストを実行

### 動作環境

| 項目 | 内容 |
|------|------|
| OS | Windows 10 / 11 (x64) |
| .NET | .NET 10 |

### ビルド

```
dotnet build src/TreePaste/TreePaste.slnx
```

### 公開（発行）

```
dotnet publish src/TreePaste/TreePaste/TreePaste.csproj -c Release -r win-x64
```

発行成果物は `src/publish/win-x64/` に出力されます。

### ライセンス

[MIT License](LICENSE)
