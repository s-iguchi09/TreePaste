# TreePaste

Language: [English](#english) | [日本語](#japanese)

Website: [English](https://s-iguchi09.github.io/apps/treepaste.html) | [日本語](https://s-iguchi09.github.io/ja/apps/treepaste.html)

---

<a id="english"></a>
## Overview (English)

TreePaste is a tool that pastes files copied to the clipboard while preserving the original folder structure.  
You can choose the folder level from a breadcrumb of the source path and paste from any level you choose.

### Features

- Resides in the **system tray** on startup and runs in the background
- Press **Ctrl+Alt+V** at any time to bring up the window
- Displays files and folders copied to the clipboard as a tree, with check boxes to choose what to paste
- Shows the source path as a breadcrumb; select a level to recreate the folders from that level (click it again to deselect and paste the items as they are)
- **Flatten** pastes the checked files directly into the destination, ignoring folders (it never overwrites: names that are duplicated or already in the destination get " (2)" etc.)
- Shows where the items will be created before pasting
- Keyboard: **Alt+← / Alt+→** to choose a level, **Alt+F** to toggle Flatten, **Enter** to paste, **Esc** to cancel
- Automatically detects the path of the active Explorer window and sets it as the paste destination
- Falls back to the Desktop if no Explorer window is found
- The UI is shown in Japanese or English, following the Windows display language

### How to Use

1. Launch TreePaste (it will reside in the system tray)
2. Copy the files you want to paste in Explorer (Ctrl+C)
3. Open the destination folder in Explorer
4. Press **Ctrl+Alt+V** to bring up the window
5. Check the items to paste, and if needed select the starting level in the breadcrumb or turn on Flatten
6. Click **Paste** (or press Enter)

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
dotnet publish src/TreePaste/TreePaste/TreePaste.csproj -c Release -p:PublishProfile=win-x64.pubxml
```

This uses the same publish profile as the release workflow and produces a self-contained single-file executable in `src/TreePaste/TreePaste/bin/Release/net10.0-windows/publish/win-x64/`.

### Related Article

- [Fixing a WPF Tray ContextMenu That Does Not Close on Focus Loss](https://s-iguchi09.github.io/articles/wpf-tray-contextmenu-close-on-focus-loss/) — an issue solved while building TreePaste

### License

[MIT License](LICENSE)

---

<a id="japanese"></a>
## 概要 (Japanese)

クリップボードにコピーしたファイルを、フォルダー構成を維持したままペーストするツールです。  
コピー元のパスをパンくずで表示し、任意の階層からペーストできます。

### 機能

- 起動するとタスクトレイに常駐し、バックグラウンドで動作
- **Ctrl+Alt+V** でいつでもウィンドウを呼び出せる
- クリップボードにコピーされたファイル・フォルダーをツリー表示し、チェックボックスでペーストする項目を選択可能
- コピー元のパスをパンくずで表示し、階層を選ぶとその階層からフォルダーを作ってペースト（もう一度押すと選択が外れ、そのままペースト）
- **フラット化** でチェックしたファイルを階層を無視してペースト先の直下にペースト（上書きはせず、重なる名前やペースト先にすでにある名前には「 (2)」などを付けて保存）
- ペーストする前に、作られる場所を表示
- キー操作：**Alt+← / Alt+→** で階層を選択、**Alt+F** でフラット化の切り替え、**Enter** でペースト、**Esc** でキャンセル
- アクティブなエクスプローラーウィンドウのパスを自動検出し、ペースト先として設定
- エクスプローラーが見つからない場合はデスクトップをペースト先に設定
- 画面の表示言語は Windows の表示言語に合わせて日本語・英語を自動で切り替え

### 使い方

1. TreePaste を起動（タスクトレイに常駐）
2. ペーストしたいファイルをエクスプローラーでコピー（Ctrl+C）
3. エクスプローラーでペースト先フォルダーを開く
4. **Ctrl+Alt+V** でウィンドウを呼び出す
5. ペーストする項目にチェックを付け、必要ならパンくずで起点の階層を選ぶかフラット化をオンにする
6. **貼り付け** を押す（または Enter）

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
dotnet publish src/TreePaste/TreePaste/TreePaste.csproj -c Release -p:PublishProfile=win-x64.pubxml
```

リリースのワークフローと同じ発行プロファイルを使い、自己完結型の単一ファイルの実行ファイルを `src/TreePaste/TreePaste/bin/Release/net10.0-windows/publish/win-x64/` に出力します。

### 関連記事

- [WPF タスクトレイの ContextMenu がフォーカス移動で閉じない問題の解消方法](https://s-iguchi09.github.io/ja/articles/wpf-tray-contextmenu-close-on-focus-loss/) — TreePaste の開発中に解決した問題

### ライセンス

[MIT License](LICENSE)
