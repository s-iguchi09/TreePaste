using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreePaste.Models;

/// <summary>
/// ファイルツリーの各ノードを表すモデルクラス。
/// Model class representing each node in the file tree.
/// </summary>
public class FileTreeItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _fullPath = string.Empty;
    private bool _isDirectory;
    private bool _isPathFolder;
    private bool _isClipboardItem;

    /// <summary>
    /// 表示名。
    /// Display name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// ファイルまたはフォルダーのフルパス。
    /// Full path of the file or folder.
    /// </summary>
    public string FullPath
    {
        get => _fullPath;
        set { _fullPath = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// ディレクトリであるかどうかを示す値。
    /// Indicates whether this item is a directory.
    /// </summary>
    public bool IsDirectory
    {
        get => _isDirectory;
        set { _isDirectory = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// パスの途中フォルダーノード（クリップボードアイテムそのものではない）。
    /// Intermediate folder node in the path (not the clipboard item itself).
    /// </summary>
    public bool IsPathFolder
    {
        get => _isPathFolder;
        set { _isPathFolder = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// クリップボードから来た実際のアイテム。
    /// Actual item originating from the clipboard.
    /// </summary>
    public bool IsClipboardItem
    {
        get => _isClipboardItem;
        set { _isClipboardItem = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 子ノードのコレクション。
    /// Collection of child nodes.
    /// </summary>
    public ObservableCollection<FileTreeItem> Children { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// プロパティ変更通知を発火する。
    /// Raises the PropertyChanged notification.
    /// </summary>
    /// <param name="name">変更されたプロパティ名。 / Name of the changed property.</param>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
