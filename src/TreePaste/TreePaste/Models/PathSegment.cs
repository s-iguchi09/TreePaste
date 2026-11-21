using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreePaste.Models;

/// <summary>
/// パンくずに並べるコピー元フォルダーの1階層。
/// One level of the source folder path shown in the breadcrumb.
/// </summary>
public class PathSegment : INotifyPropertyChanged
{
    private bool _isSelected;

    /// <summary>
    /// 表示名（ドライブは "D:" のように表示する）。
    /// Display name (a drive is shown as "D:").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 貼り付け先に作るときのフォルダー名（ドライブの ":" は取り除く）。
    /// Folder name used when creating it at the destination (":" of a drive is removed).
    /// </summary>
    public required string FolderName { get; init; }

    /// <summary>
    /// パンくず内の位置（0 始まり）。
    /// Position in the breadcrumb (0-based).
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 先頭以外は前に区切り記号を表示する。
    /// Shows a separator before every segment except the first.
    /// </summary>
    public bool HasSeparator => Index > 0;

    /// <summary>
    /// この階層から貼り付ける起点として選ばれているかどうか。
    /// Indicates whether this level is selected as the paste starting point.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// プロパティ変更通知を発火する。
    /// Raises the PropertyChanged notification.
    /// </summary>
    /// <param name="name">変更されたプロパティ名。 / Name of the changed property.</param>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
