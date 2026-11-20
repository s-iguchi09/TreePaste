using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
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
    private bool _checkStateRefreshQueued;
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

        var clipboardPaths = NormalizePaths(ClipboardHelper.GetClipboardFilePaths());

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

        // 共通の親から下をツリーにする（親が違う項目は途中のフォルダーを挟む）。
        // 浅い順に並んでいるので、コピーしたフォルダーの中にある項目は、すでにあるそのフォルダーのノードの中に入る。
        // Everything below the shared parent becomes the tree (intermediate folders are added for items with different parents).
        // Paths are ordered shallowest first, so an item inside a copied folder goes into that folder's existing node.
        for (int i = 0; i < clipboardPaths.Count; i++)
            AddToTree(clipboardPaths[i], partsList[i][commonLength..]);

        foreach (var item in RootItems)
            SubscribeToCheckedChanges(item);

        UpdateCanPaste();
        UpdatePreview();
    }

    /// <summary>
    /// クリップボードのパスを正規化し（".." などを解決し、末尾の区切りを取る）、重複を除いて浅い順に並べる。
    /// 同じ深さの中では元の並び順を保つ。
    /// Normalizes the clipboard paths (resolving ".." and the like, trimming the trailing separator),
    /// removes duplicates and orders them shallowest first, keeping the original order within the same depth.
    /// </summary>
    private static List<string> NormalizePaths(IEnumerable<string> paths)
    {
        return paths
            .Select(p =>
            {
                try { p = Path.GetFullPath(p); }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
                return p.Length > 3 ? p.TrimEnd(Path.DirectorySeparatorChar) : p;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length)
            .ToList();
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
    /// 同じ場所にすでに同じ名前のノードがあれば（コピーしたフォルダーの中身として読み込み済みなら）追加しない。
    /// フォルダーの中身を読めなかったときは、その中に明示的にコピーした項目だけを追加する。
    /// Adds a copied item to the tree. Creates intermediate folder nodes (shared by name) when the relative path is deeper than one level.
    /// If a node with the same name already exists at that spot (already loaded as part of a copied folder), it is not added again.
    /// If a folder's contents could not be read, only the items explicitly copied inside it are added.
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

        var leafName = SanitizeName(relativeParts.Length > 0 ? relativeParts[^1] : fullPath);
        if (siblings.Any(s => string.Equals(s.Name, leafName, StringComparison.OrdinalIgnoreCase)))
            return;

        var item = CreateFileTreeItem(fullPath);
        item.Name = leafName;
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
                : $"→ {_destinationPath}  ({files} directly, {renamed} renamed to avoid overwriting)";
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
                    CopyWithoutOverwrite(source, dest);
            };
        }
        else
        {
            // 貼り付けを始めた時点のチェック状態で計画を作る（コピー中にチェックを変えても影響しない）
            // Build the plan from the check states at the time the paste starts (changing checks during the copy has no effect)
            string baseDestination = GetBaseDestination(destination);
            var folders = new List<string>();
            var files = new List<(string Source, string Destination)>();
            foreach (var root in RootItems)
                PlanCheckedItems(root, Path.Combine(baseDestination, root.Name), folders, files);

            copy = () =>
            {
                foreach (var folder in folders)
                    Directory.CreateDirectory(folder);
                foreach (var (source, dest) in files)
                    File.Copy(source, dest, overwrite: true);
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
    /// 平らに貼り付けるときのコピー元とコピー先を決める。
    /// 貼り付け先にすでにある名前と、今回の貼り付けで使う名前は重ならないようにし、重なったら "name (2).ext" のように名前を変える。
    /// （上書きすると、貼り付け先にあるコピー元のファイルを先に壊してしまうことがあるため）
    /// Plans the sources and destinations for a flat paste.
    /// Names never collide with names already in the destination or used by this paste; collisions are renamed like "name (2).ext".
    /// (Overwriting could destroy a checked source located in the destination before it is copied.)
    /// </summary>
    /// <param name="destination">貼り付け先のフォルダー。 / Destination folder.</param>
    private List<(string Source, string Destination, bool Renamed)> PlanFlatten(string destination)
    {
        var plan = new List<(string, string, bool)>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 貼り付け先にすでにあるファイルとフォルダーの名前を先に押さえる
        // Reserve the names of files and folders that already exist in the destination
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(destination))
                usedNames.Add(Path.GetFileName(entry));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 読めなければ、コピーするときの上書きしない処理に任せる
            // If it cannot be read, rely on the no-overwrite handling at copy time
        }

        foreach (var file in RootItems.SelectMany(GetCheckedFiles))
        {
            string name = file.Name;
            bool renamed = false;
            if (!usedNames.Add(name))
            {
                int n = 2;
                do
                {
                    name = NumberedName(file.Name, n++);
                } while (!usedNames.Add(name));
                renamed = true;
            }
            plan.Add((file.FullPath, Path.Combine(destination, name), renamed));
        }

        return plan;
    }

    /// <summary>
    /// "name.ext" を "name (n).ext" にする。
    /// Turns "name.ext" into "name (n).ext".
    /// </summary>
    private static string NumberedName(string fileName, int n)
        => $"{Path.GetFileNameWithoutExtension(fileName)} ({n}){Path.GetExtension(fileName)}";

    private const int ERROR_FILE_EXISTS = 80;
    private const int ERROR_ALREADY_EXISTS = 183;

    /// <summary>
    /// 上書きせずにコピーする。いったん同じフォルダーの一時ファイル（この処理だけが使う名前）にコピーしてから、
    /// 上書きしない名前の変更で置き場所に移す。計画したあとに同じ名前ができていたら、空いている "name (n).ext" に移す。
    /// 失敗したときに消すのはこの処理が作った一時ファイルだけなので、ほかの処理が作ったファイルを消すことはない。
    /// Copies without overwriting. It first copies to a temporary file in the same folder (a name only this operation uses),
    /// then moves it into place with a non-overwriting rename. If the planned name has appeared since planning, it moves to the next free "name (n).ext".
    /// On failure it deletes only the temporary file it created, so it never deletes a file created by another process.
    /// </summary>
    private static void CopyWithoutOverwrite(string source, string destPath)
    {
        string folder = Path.GetDirectoryName(destPath)!;
        string originalName = Path.GetFileName(source);
        string tempPath = Path.Combine(folder, $".treepaste-{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(source, tempPath, overwrite: false);

            string target = destPath;
            int n = 2;
            while (true)
            {
                if (File.Exists(target) || Directory.Exists(target))
                {
                    target = Path.Combine(folder, NumberedName(originalName, n++));
                    continue;
                }

                try
                {
                    File.Move(tempPath, target, overwrite: false);
                    return;
                }
                catch (IOException ex) when ((ex.HResult & 0xFFFF) is ERROR_FILE_EXISTS or ERROR_ALREADY_EXISTS)
                {
                    // 確認したあとに同じ名前ができた
                    // The name appeared after the check
                    target = Path.Combine(folder, NumberedName(originalName, n++));
                }
            }
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
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
    /// チェックされたアイテムについて、作るフォルダーとコピーするファイルを再帰的に集める。
    /// フォルダーは親から順に並ぶので、先頭から作ればファイルの置き場所がそろう。
    /// Recursively collects the folders to create and the files to copy for checked items.
    /// Folders are listed parent first, so creating them in order prepares every file's location.
    /// </summary>
    /// <param name="item">コピー元の <see cref="FileTreeItem"/>。 / Source <see cref="FileTreeItem"/>.</param>
    /// <param name="destPath">コピー先のパス。 / Destination path.</param>
    /// <param name="folders">作るフォルダー。 / Folders to create.</param>
    /// <param name="files">コピーするファイル。 / Files to copy.</param>
    private static void PlanCheckedItems(FileTreeItem item, string destPath,
        List<string> folders, List<(string Source, string Destination)> files)
    {
        if (!HasAnyCheckedItem(item)) return;

        if (!item.IsDirectory)
        {
            string? parentDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(parentDir))
                folders.Add(parentDir);
            files.Add((item.FullPath, destPath));
            return;
        }

        folders.Add(destPath);
        foreach (var child in item.Children)
            PlanCheckedItems(child, Path.Combine(destPath, child.Name), folders, files);
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
    /// アイテムの IsChecked が変わったら、CanPaste と説明の更新を予約する。
    /// Schedules a CanPaste and preview refresh when an item's IsChecked changes.
    /// </summary>
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileTreeItem.IsChecked)) return;
        QueueCheckStateRefresh();
    }

    /// <summary>
    /// CanPaste と説明の更新を1回だけ予約する。フォルダーのチェックで子孫のチェックが一度に変わっても、
    /// 伝播が終わってから1回だけ計算し直す。入力より優先度が高いので、次のクリックやキー操作の前に終わる。
    /// Schedules a single CanPaste and preview refresh. Even when checking a folder changes all its descendants at once,
    /// they are recalculated only once after the propagation finishes. It runs at a higher priority than input,
    /// so it completes before the next click or key press.
    /// </summary>
    private void QueueCheckStateRefresh()
    {
        if (_checkStateRefreshQueued) return;
        _checkStateRefreshQueued = true;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _checkStateRefreshQueued = false;
            UpdateCanPaste();
            UpdatePreview();
        }, DispatcherPriority.DataBind);
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
