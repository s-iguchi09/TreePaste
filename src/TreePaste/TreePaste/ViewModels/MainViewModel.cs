using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using TreePaste.Models;
using TreePaste.Services;
using MessageBox = System.Windows.MessageBox;

namespace TreePaste.ViewModels;

/// <summary>
/// メインウィンドウのViewModel。クリップボードのファイルツリー表示とコピー操作を管理する。
/// Main ViewModel for the main window. Manages clipboard file tree display and copy operations.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private string _destinationPathText = "Loading...";
    private string? _destinationPath;

    /// <summary>
    /// ツリービューのルートアイテムコレクション。
    /// Root item collection for the tree view.
    /// </summary>
    public ObservableCollection<FileTreeItem> RootItems { get; } = new();

    /// <summary>
    /// UIに表示するコピー先パスのテキスト。
    /// Text of the destination path displayed in the UI.
    /// </summary>
    public string DestinationPathText
    {
        get => _destinationPathText;
        set { _destinationPathText = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// キャンセルボタンのコマンド。
    /// Command for the cancel button.
    /// </summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>
    /// パスフォルダーのクリックコマンド。
    /// Command executed when a path folder node is clicked.
    /// </summary>
    public RelayCommand<FileTreeItem> PathFolderClickCommand { get; }

    /// <summary>
    /// コンストラクター。コマンドを初期化する。
    /// Constructor. Initializes commands.
    /// </summary>
    public MainViewModel()
    {
        CancelCommand = new RelayCommand(OnCancel);
        PathFolderClickCommand = new RelayCommand<FileTreeItem>(OnPathFolderClick);
    }

    /// <summary>
    /// アクティブなエクスプローラーのパスを取得し、クリップボードからファイルツリーを読み込む。
    /// Retrieves the active Explorer path and loads the file tree from the clipboard.
    /// </summary>
    public void LoadFromClipboard()
    {
        _destinationPath = ExplorerHelper.GetActiveExplorerPath();

        if (string.IsNullOrEmpty(_destinationPath))
        {
            DestinationPathText = "Desktop(Explorer not found)";
            _destinationPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }
        else
        {
            DestinationPathText = _destinationPath;
        }

        LoadClipboardFiles();
    }

    /// <summary>
    /// クリップボードのファイルパスを読み込み、ルートアイテムに変換してコレクションへ追加する。
    /// Reads file paths from the clipboard, converts them to tree items, and adds them to the root collection.
    /// </summary>
    private void LoadClipboardFiles()
    {
        RootItems.Clear();

        var clipboardPaths = ClipboardHelper.GetClipboardFilePaths();

        if (clipboardPaths.Count == 0)
        {
            MessageBox.Show("No files in clipboard.",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var filePath in clipboardPaths)
        {
            var chain = BuildPathChain(filePath);
            RootItems.Add(chain);
        }
    }

    /// <summary>
    /// ファイルパスをルートから末端まで入れ子のツリーノードに変換する。
    /// 例: C:\Foo\Bar\file.txt
    ///   └─ [Foo (IsPathFolder)]
    ///       └─ [Bar (IsPathFolder)]
    ///            └─ [file.txt (IsClipboardItem)]
    /// Converts a full file path into nested tree nodes from root to leaf.
    /// e.g. C:\Foo\Bar\file.txt → [Foo (IsPathFolder)] → [Bar (IsPathFolder)] → [file.txt (IsClipboardItem)]
    /// </summary>
    /// <param name="fullPath">変換対象のフルパス。 / Full path to convert.</param>
    /// <returns>ルートの <see cref="FileTreeItem"/>。 / The root <see cref="FileTreeItem"/>.</returns>
    private FileTreeItem BuildPathChain(string fullPath)
    {
        var parts = fullPath.Split(Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);

        var invalidChars = Path.GetInvalidFileNameChars();
        string SanitizeName(string name) =>
            string.Concat(name.Select(c => invalidChars.Contains(c) ? '_' : c));

        FileTreeItem? root = null;
        FileTreeItem? current = null;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            string folderPath;
            string nodeName;
            if (i == 0)
            {
                // ドライブレターまたはネットワークドライブのルート
                // Drive letter or network drive root
                folderPath = parts[0] + Path.DirectorySeparatorChar;
                nodeName = SanitizeName(parts[0]);
            }
            else
            {
                folderPath = parts[0] + Path.DirectorySeparatorChar +
                    string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(1).Take(i).ToArray());
                nodeName = SanitizeName(parts[i]);
            }

            var node = new FileTreeItem
            {
                Name = nodeName,
                FullPath = folderPath,
                IsDirectory = true,
                IsPathFolder = true,
                IsClipboardItem = false
            };

            if (root == null)
            {
                root = node;
                current = node;
            }
            else
            {
                current!.Children.Add(node);
                current = node;
            }
        }

        bool isDir = Directory.Exists(fullPath);
        var leafNode = new FileTreeItem
        {
            Name = SanitizeName(parts[^1]),
            FullPath = fullPath,
            IsDirectory = isDir,
            IsPathFolder = false,
            IsClipboardItem = true
        };

        if (isDir)
        {
            try
            {
                foreach (var sub in Directory.GetFileSystemEntries(fullPath))
                    leafNode.Children.Add(CreateFileTreeItem(sub));
            }
            catch { }
        }

        if (current != null)
            current.Children.Add(leafNode);
        else
            root = leafNode;

        return root!;
    }

    /// <summary>
    /// 指定パスから <see cref="FileTreeItem"/> を再帰的に生成する。
    /// Recursively creates a <see cref="FileTreeItem"/> from the specified path.
    /// </summary>
    /// <param name="fullPath">対象のフルパス。 / Full path of the target file or directory.</param>
    /// <returns>生成した <see cref="FileTreeItem"/>。 / The created <see cref="FileTreeItem"/>.</returns>
    private FileTreeItem CreateFileTreeItem(string fullPath)
    {
        bool isDirectory = Directory.Exists(fullPath);
        var item = new FileTreeItem
        {
            Name = Path.GetFileName(fullPath) ?? fullPath,
            FullPath = fullPath,
            IsDirectory = isDirectory,
            IsPathFolder = false,
            IsClipboardItem = true
        };

        if (isDirectory)
        {
            try
            {
                foreach (var sub in Directory.GetFileSystemEntries(fullPath))
                    item.Children.Add(CreateFileTreeItem(sub));
            }
            catch { }
        }

        return item;
    }

    /// <summary>
    /// パスフォルダーノードがクリックされたときに呼び出され、対応するファイルをコピー先へコピーする。
    /// Called when a path folder node is clicked; copies the corresponding file to the destination.
    /// </summary>
    /// <param name="folderItem">クリックされたフォルダーアイテム。 / The clicked folder item.</param>
    private void OnPathFolderClick(FileTreeItem? folderItem)
    {
        if (folderItem == null || string.IsNullOrEmpty(_destinationPath))
            return;

        try
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            var leaf = FindClipboardLeaf(folderItem);
            if (leaf == null)
                return;

            // BuildPathChain は folderItem.FullPath を parts[0..i] の形で構築しているため、
            // セグメント数（RemoveEmptyEntries）= folderItem が leaf.FullPath 内の何番目のセグメントか を表す。
            // この深さを使って leaf.FullPath からフォルダ名を先頭に含む相対パスを再構築する。
            // ※ Path.GetRelativePath / Path.GetFullPath はUNCパスを不完全に保存したノードのFullPathでは
            //   正しく機能しないため、パス文字列への依存を避けてセグメント配列で処理する。
            string[] folderParts = folderItem.FullPath.Split(Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries);
            string[] leafParts = leaf.FullPath.Split(Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries);

            // folderItem が何番目のセグメントか（0始まり）
            int startIndex = folderParts.Length - 1;

            string relativePath;
            if (startIndex >= 0 && startIndex < leafParts.Length)
            {
                // leaf.FullPath の startIndex 以降のセグメントを結合して相対パスを作成する
                // 先頭セグメントの ":" を除去
                string[] relParts = leafParts[startIndex..];
                relParts[0] = relParts[0].Replace(":", "");
                relativePath = Path.Combine(relParts);
            }
            else
            {
                // 範囲外の場合はファイル名のみにフォールバック
                relativePath = Path.GetFileName(leaf.FullPath) ?? leaf.Name;
            }

            string destPath = Path.Combine(_destinationPath, relativePath);

            ClipboardHelper.CopyFileOrDirectory(leaf.FullPath, destPath);

            MessageBox.Show($"Copied successfully.\n{destPath}",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error while copying:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    /// <summary>
    /// ノードを再帰的に探索し、最初の <see cref="FileTreeItem.IsClipboardItem"/> が true のノードを返す。
    /// Recursively searches the node tree and returns the first node where <see cref="FileTreeItem.IsClipboardItem"/> is true.
    /// </summary>
    /// <param name="node">探索を開始するノード。 / The node from which to start the search.</param>
    /// <returns>見つかったリーフノード。見つからない場合は null。 / The found leaf node, or null if not found.</returns>
    private static FileTreeItem? FindClipboardLeaf(FileTreeItem node)
    {
        if (node.IsClipboardItem)
            return node;
        foreach (var child in node.Children)
        {
            var found = FindClipboardLeaf(child);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// キャンセル操作を実行し、ウィンドウを閉じるよう通知する。
    /// Executes the cancel operation and notifies the view to close the window.
    /// </summary>
    private void OnCancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// ウィンドウを Hide するよう View に通知するイベント。
    /// Event that notifies the View to hide the window.
    /// </summary>
    public event EventHandler? CloseRequested;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// プロパティ変更通知を発火する。
    /// Raises the PropertyChanged notification.
    /// </summary>
    /// <param name="name">変更されたプロパティ名。 / Name of the changed property.</param>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
