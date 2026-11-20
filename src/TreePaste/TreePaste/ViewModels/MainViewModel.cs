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
    private bool _isProcessing;
    private bool _canPaste;
    private bool _isFlatten;
    private string _previewText = string.Empty;
    private readonly List<FileTreeItem> _subscribedItems = new();

    /// <summary>
    /// コピー処理中かどうかを示す。プログレスバーの表示制御に使用する。
    /// Indicates whether a copy operation is in progress. Used to control the progress bar visibility.
    /// </summary>
    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            _isProcessing = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// 貼り付け可能かどうかを示す。チェックされているアイテムが1つ以上ある場合に true。
    /// Indicates whether pasting is possible. True when at least one item is checked.
    /// </summary>
    public bool CanPaste
    {
        get => _canPaste;
        private set
        {
            if (_canPaste == value) return;
            _canPaste = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// ツリービューのルートアイテムコレクション（コピーした項目）。
    /// Root item collection for the tree view (the copied items).
    /// </summary>
    public ObservableCollection<FileTreeItem> RootItems { get; } = new();

    /// <summary>
    /// コピー元フォルダーのパンくず。
    /// Breadcrumb of the source folder.
    /// </summary>
    public ObservableCollection<PathSegment> Segments { get; } = new();

    /// <summary>
    /// チェックしたファイルを階層を無視して貼り付け先の直下に貼り付けるかどうか。
    /// パンくずの階層選択とは排他。
    /// Whether to paste checked files directly into the destination, ignoring the hierarchy.
    /// Mutually exclusive with the breadcrumb level selection.
    /// </summary>
    public bool IsFlatten
    {
        get => _isFlatten;
        set
        {
            if (_isFlatten == value) return;
            _isFlatten = value;
            OnPropertyChanged();
            if (value)
                ClearSegmentSelection();
            UpdatePreview();
        }
    }

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
    /// 現在の選択で貼り付けたときに作られる場所の説明。
    /// Description of where items will be created with the current selection.
    /// </summary>
    public string PreviewText
    {
        get => _previewText;
        private set { _previewText = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// キャンセルボタンのコマンド。
    /// Command for the cancel button.
    /// </summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>
    /// 貼り付けボタンのコマンド。
    /// Command for the paste button.
    /// </summary>
    public RelayCommand PasteCommand { get; }

    /// <summary>
    /// コンストラクター。コマンドを初期化する。
    /// Constructor. Initializes commands.
    /// </summary>
    public MainViewModel()
    {
        CancelCommand = new RelayCommand(OnCancel, () => !IsProcessing);
        PasteCommand = new RelayCommand(OnPaste, () => !IsProcessing && CanPaste);
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

    // ━━━ ツリーとパンくずの構築 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// クリップボードのファイルパスを読み込み、パンくずとツリーを作る。
    /// Reads file paths from the clipboard and builds the breadcrumb and the tree.
    /// </summary>
    private void LoadClipboardFiles()
    {
        Clear();

        var clipboardPaths = ClipboardHelper.GetClipboardFilePaths()
            .Select(p => p.Length > 3 ? p.TrimEnd(Path.DirectorySeparatorChar) : p)
            .ToList();

        if (clipboardPaths.Count == 0)
        {
            UpdatePreview();
            MessageBox.Show("No files in clipboard.",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var partsList = clipboardPaths
            .Select(p => p.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        // すべての項目に共通する親フォルダーをパンくずにする
        // The parent folder shared by all items becomes the breadcrumb
        int commonLength = GetCommonParentLength(partsList);
        var commonParts = partsList[0][..commonLength];
        for (int i = 0; i < commonParts.Length; i++)
        {
            var segment = new PathSegment
            {
                Name = commonParts[i],
                FolderName = SanitizeName(commonParts[i]),
                Index = i
            };
            segment.PropertyChanged += OnSegmentPropertyChanged;
            Segments.Add(segment);
        }

        // 共通の親から下をツリーにする（親が違う項目は途中のフォルダーを挟む）
        // Everything below the shared parent becomes the tree (intermediate folders are added for items with different parents)
        for (int i = 0; i < clipboardPaths.Count; i++)
            AddToTree(clipboardPaths[i], partsList[i][commonLength..]);

        foreach (var item in RootItems)
            SubscribeToCheckedChanges(item);

        UpdateCanPaste();
        UpdatePreview();
    }

    /// <summary>
    /// すべての項目に共通する親フォルダーのセグメント数を返す。
    /// Returns the number of segments of the parent folder shared by all items.
    /// </summary>
    private static int GetCommonParentLength(List<string[]> partsList)
    {
        int length = partsList.Min(p => p.Length - 1);
        for (int i = 0; i < length; i++)
        {
            var name = partsList[0][i];
            if (partsList.Any(p => !string.Equals(p[i], name, StringComparison.OrdinalIgnoreCase)))
                return i;
        }
        return Math.Max(length, 0);
    }

    /// <summary>
    /// コピーした項目をツリーに追加する。相対パスが2段以上なら途中のフォルダーノードを作る（同名は共有）。
    /// Adds a copied item to the tree. Creates intermediate folder nodes (shared by name) when the relative path is deeper than one level.
    /// </summary>
    /// <param name="fullPath">コピーした項目のフルパス。 / Full path of the copied item.</param>
    /// <param name="relativeParts">共通の親からの相対セグメント。 / Segments relative to the shared parent.</param>
    private void AddToTree(string fullPath, string[] relativeParts)
    {
        var siblings = RootItems;
        for (int i = 0; i < relativeParts.Length - 1; i++)
        {
            var name = SanitizeName(relativeParts[i]);
            var folder = siblings.FirstOrDefault(s =>
                s.IsDirectory && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (folder == null)
            {
                // 項目のパスから必要な段数だけ上がったフォルダーのパス
                // Path of the folder reached by going up the required number of levels from the item
                string folderPath = fullPath;
                for (int up = 0; up < relativeParts.Length - 1 - i; up++)
                    folderPath = Path.GetDirectoryName(folderPath) ?? folderPath;

                folder = new FileTreeItem { Name = name, FullPath = folderPath, IsDirectory = true };
                siblings.Add(folder);
            }
            siblings = folder.Children;
        }

        var item = CreateFileTreeItem(fullPath);
        item.Name = SanitizeName(relativeParts.Length > 0 ? relativeParts[^1] : item.Name);
        siblings.Add(item);
    }

    /// <summary>
    /// 指定パスから <see cref="FileTreeItem"/> を再帰的に生成する。
    /// Recursively creates a <see cref="FileTreeItem"/> from the specified path.
    /// </summary>
    /// <param name="fullPath">対象のフルパス。 / Full path of the target file or directory.</param>
    /// <returns>生成した <see cref="FileTreeItem"/>。 / The created <see cref="FileTreeItem"/>.</returns>
    private static FileTreeItem CreateFileTreeItem(string fullPath)
    {
        bool isDirectory = Directory.Exists(fullPath);
        var name = Path.GetFileName(fullPath);
        var item = new FileTreeItem
        {
            Name = string.IsNullOrEmpty(name) ? fullPath : name,
            FullPath = fullPath,
            IsDirectory = isDirectory
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
    /// ファイル名に使えない文字（ドライブの ":" など）を取り除く。
    /// Removes characters that cannot be used in file names (such as ":" of a drive).
    /// </summary>
    private static string SanitizeName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(name.Where(c => !invalidChars.Contains(c)));
    }

    // ━━━ 貼り付け方の選択 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 選ばれている階層。選ばれていなければ null。
    /// The selected level, or null when none is selected.
    /// </summary>
    private PathSegment? SelectedSegment => Segments.FirstOrDefault(s => s.IsSelected);

    /// <summary>
    /// 階層の選択が変わったとき、ほかの階層と Flatten を外す（ラジオボタンと同じ動き）。
    /// When a level gets selected, deselects the other levels and Flatten (radio-button behavior).
    /// </summary>
    private void OnSegmentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PathSegment.IsSelected) || sender is not PathSegment changed)
            return;

        if (changed.IsSelected)
        {
            foreach (var other in Segments)
            {
                if (!ReferenceEquals(other, changed))
                    other.IsSelected = false;
            }
            IsFlatten = false;
        }

        UpdatePreview();
    }

    /// <summary>
    /// 選ぶ階層を左右に動かす。左端より左には行かず、右端の右は「選択なし」。
    /// Moves the selected level left or right. It stops at the left end; moving right past the right end clears the selection.
    /// </summary>
    /// <param name="direction">-1 で左（上の階層）、+1 で右。 / -1 moves left (up a level), +1 moves right.</param>
    public void MoveSelection(int direction)
    {
        if (IsProcessing || Segments.Count == 0) return;

        var current = SelectedSegment;
        if (direction < 0)
        {
            int index = current == null ? Segments.Count - 1 : Math.Max(current.Index - 1, 0);
            Segments[index].IsSelected = true;
        }
        else if (current != null)
        {
            if (current.Index == Segments.Count - 1)
                current.IsSelected = false;
            else
                Segments[current.Index + 1].IsSelected = true;
        }
    }

    /// <summary>
    /// Flatten を切り替える。
    /// Toggles Flatten.
    /// </summary>
    public void ToggleFlatten()
    {
        if (IsProcessing) return;
        IsFlatten = !IsFlatten;
    }

    /// <summary>
    /// すべての階層の選択を外す。
    /// Deselects all levels.
    /// </summary>
    private void ClearSegmentSelection()
    {
        foreach (var segment in Segments)
            segment.IsSelected = false;
    }

    /// <summary>
    /// 選ばれている階層から右のフォルダーを付けた貼り付け先のフォルダーを返す。
    /// Returns the destination folder with the folders from the selected level to the right appended.
    /// </summary>
    private string GetBaseDestination(string destination)
    {
        var selected = SelectedSegment;
        if (selected == null)
            return destination;

        var folders = Segments.Skip(selected.Index).Select(s => s.FolderName);
        return Path.Combine([destination, .. folders]);
    }

    /// <summary>
    /// 作られる場所の説明を更新する。
    /// Updates the description of where items will be created.
    /// </summary>
    private void UpdatePreview()
    {
        if (string.IsNullOrEmpty(_destinationPath) || RootItems.Count == 0)
        {
            PreviewText = string.Empty;
            return;
        }

        if (!CanPaste)
        {
            PreviewText = "No items checked.";
            return;
        }

        if (IsFlatten)
        {
            var plan = PlanFlatten(_destinationPath);
            int renamed = plan.Count(p => p.Renamed);
            string files = plan.Count == 1 ? "1 file" : $"{plan.Count} files";
            PreviewText = renamed == 0
                ? $"→ {_destinationPath}  ({files} directly)"
                : $"→ {_destinationPath}  ({files} directly, {renamed} renamed for duplicate names)";
            return;
        }

        PreviewText = $"→ {Path.Combine(GetBaseDestination(_destinationPath), "…")}";
    }

    // ━━━ 貼り付け ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 現在の選択に従って、チェックした項目を貼り付け先へコピーする。
    /// Copies the checked items to the destination according to the current selection.
    /// </summary>
    private async void OnPaste()
    {
        if (string.IsNullOrEmpty(_destinationPath) || !CanPaste)
            return;

        string destination = _destinationPath;
        Action copy;
        if (IsFlatten)
        {
            var plan = PlanFlatten(destination);
            copy = () =>
            {
                Directory.CreateDirectory(destination);
                foreach (var (source, dest, _) in plan)
                    File.Copy(source, dest, overwrite: true);
            };
        }
        else
        {
            string baseDestination = GetBaseDestination(destination);
            var roots = RootItems.ToList();
            copy = () =>
            {
                foreach (var root in roots)
                    CopyCheckedItems(root, Path.Combine(baseDestination, root.Name));
            };
        }

        try
        {
            IsProcessing = true;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            await Task.Run(copy);

            MessageBox.Show("Copied successfully.",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            OnHide();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error while copying:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
            Mouse.OverrideCursor = null;
        }
    }

    /// <summary>
    /// 平らに貼り付けるときのコピー元とコピー先を決める。同じ名前が重なったら "name (2).ext" のように名前を変える。
    /// Plans the sources and destinations for a flat paste. Duplicate names are renamed like "name (2).ext".
    /// </summary>
    /// <param name="destination">貼り付け先のフォルダー。 / Destination folder.</param>
    private List<(string Source, string Destination, bool Renamed)> PlanFlatten(string destination)
    {
        var plan = new List<(string, string, bool)>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in RootItems.SelectMany(GetCheckedFiles))
        {
            string name = file.Name;
            bool renamed = false;
            if (!usedNames.Add(name))
            {
                string stem = Path.GetFileNameWithoutExtension(file.Name);
                string ext = Path.GetExtension(file.Name);
                int n = 2;
                do
                {
                    name = $"{stem} ({n++}){ext}";
                } while (!usedNames.Add(name));
                renamed = true;
            }
            plan.Add((file.FullPath, Path.Combine(destination, name), renamed));
        }

        return plan;
    }

    /// <summary>
    /// チェックされたファイル（フォルダーは除く）を深さに関係なく列挙する。
    /// Enumerates checked files (excluding folders) at any depth.
    /// </summary>
    private static IEnumerable<FileTreeItem> GetCheckedFiles(FileTreeItem item)
    {
        if (!item.IsDirectory)
        {
            if (item.IsChecked == true)
                yield return item;
            yield break;
        }

        foreach (var child in item.Children)
        {
            foreach (var file in GetCheckedFiles(child))
                yield return file;
        }
    }

    /// <summary>
    /// チェックされたアイテムのみを再帰的にコピーする。
    /// Recursively copies only checked items.
    /// </summary>
    /// <param name="item">コピー元の <see cref="FileTreeItem"/>。 / Source <see cref="FileTreeItem"/>.</param>
    /// <param name="destPath">コピー先のパス。 / Destination path.</param>
    private static void CopyCheckedItems(FileTreeItem item, string destPath)
    {
        if (!HasAnyCheckedItem(item)) return;

        if (!item.IsDirectory)
        {
            string? parentDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(parentDir))
                Directory.CreateDirectory(parentDir);
            File.Copy(item.FullPath, destPath, overwrite: true);
            return;
        }

        Directory.CreateDirectory(destPath);
        foreach (var child in item.Children)
            CopyCheckedItems(child, Path.Combine(destPath, child.Name));
    }

    // ━━━ チェック状態の監視 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// ツリーアイテムとその子孫の IsChecked 変更を購読する。
    /// Subscribes to IsChecked changes for a tree item and all its descendants.
    /// </summary>
    /// <param name="item">購読対象のアイテム。 / Item to subscribe.</param>
    /// <param name="parent">親アイテム。 / Parent item.</param>
    private void SubscribeToCheckedChanges(FileTreeItem item, FileTreeItem? parent = null)
    {
        item.Parent = parent;
        item.PropertyChanged += OnItemPropertyChanged;
        _subscribedItems.Add(item);
        foreach (var child in item.Children)
            SubscribeToCheckedChanges(child, item);
    }

    /// <summary>
    /// 購読中のすべてのアイテムから IsChecked 変更の購読を解除する。
    /// Unsubscribes from IsChecked changes for all currently subscribed items.
    /// </summary>
    private void UnsubscribeFromCheckedChanges()
    {
        foreach (var item in _subscribedItems)
            item.PropertyChanged -= OnItemPropertyChanged;
        _subscribedItems.Clear();
    }

    /// <summary>
    /// アイテムの IsChecked が変わったら CanPaste と説明を更新する。
    /// Updates CanPaste and the preview when an item's IsChecked changes.
    /// </summary>
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileTreeItem.IsChecked)) return;
        UpdateCanPaste();
        UpdatePreview();
    }

    /// <summary>
    /// CanPaste を再計算する。チェックされたアイテムが1つ以上あれば true。
    /// Recalculates CanPaste. True if at least one checked item exists.
    /// </summary>
    private void UpdateCanPaste()
    {
        CanPaste = RootItems.Any(HasAnyCheckedItem);
    }

    /// <summary>
    /// ノードまたはその子孫にチェックされたアイテムがあるかどうかを返す。
    /// Returns whether the node or any descendant is checked.
    /// </summary>
    /// <param name="item">探索するノード。 / Node to search.</param>
    private static bool HasAnyCheckedItem(FileTreeItem item)
    {
        if (item.Children.Count == 0) return item.IsChecked == true;
        return item.Children.Any(HasAnyCheckedItem);
    }

    // ━━━ 表示の終了 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// ツリー・パンくず・選択をすべて消す。
    /// Clears the tree, the breadcrumb and the selection.
    /// </summary>
    private void Clear()
    {
        UnsubscribeFromCheckedChanges();
        RootItems.Clear();
        foreach (var segment in Segments)
            segment.PropertyChanged -= OnSegmentPropertyChanged;
        Segments.Clear();
        _isFlatten = false;
        OnPropertyChanged(nameof(IsFlatten));
        CanPaste = false;
    }

    /// <summary>
    /// 画面が非表示になる際にツリーをクリアして購読を解除する。
    /// Clears the tree and unsubscribes from events when the window is about to be hidden.
    /// </summary>
    public void OnHide()
    {
        Clear();
        PreviewText = string.Empty;
    }

    /// <summary>
    /// キャンセル操作を実行し、ツリーをクリアして購読を解除した後、ウィンドウを閉じるよう通知する。
    /// Executes the cancel operation, clears the tree, unsubscribes from events, and notifies the view to close the window.
    /// </summary>
    private void OnCancel()
    {
        OnHide();
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
