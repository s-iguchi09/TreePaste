// 画面に出す文言。英語は Strings.resx、日本語は Strings.ja.resx に書く。
// User-facing strings. English lives in Strings.resx, Japanese in Strings.ja.resx.
using System.Globalization;
using System.Resources;

namespace TreePaste.Properties;

/// <summary>
/// 画面に出す文言を OS の表示言語に合わせて返す。
/// Returns user-facing strings in the OS display language.
/// </summary>
public static class Strings
{
    private static ResourceManager? _resourceManager;

    /// <summary>文言を読む ResourceManager。 / The ResourceManager that reads the strings.</summary>
    public static ResourceManager ResourceManager
        => _resourceManager ??= new ResourceManager("TreePaste.Properties.Strings", typeof(Strings).Assembly);

    /// <summary>
    /// 使う言語。null なら現在の UI カルチャ。
    /// Culture to use. null means the current UI culture.
    /// </summary>
    public static CultureInfo? Culture { get; set; }

    /// <summary>Copy to:</summary>
    public static string CopyToLabel => ResourceManager.GetString("CopyToLabel", Culture)!;

    /// <summary>Paste from:  (Alt+← / Alt+→ to choose a level, Alt+F to flatten)</summary>
    public static string PasteFromLabel => ResourceManager.GetString("PasteFromLabel", Culture)!;

    /// <summary>Create folders from this level</summary>
    public static string SegmentToolTip => ResourceManager.GetString("SegmentToolTip", Culture)!;

    /// <summary>Flatten</summary>
    public static string FlattenButton => ResourceManager.GetString("FlattenButton", Culture)!;

    /// <summary>Paste checked files directly into the destination, ignoring folders (Alt+F)</summary>
    public static string FlattenToolTip => ResourceManager.GetString("FlattenToolTip", Culture)!;

    /// <summary>Files in clipboard:</summary>
    public static string FilesInClipboardLabel => ResourceManager.GetString("FilesInClipboardLabel", Culture)!;

    /// <summary>Paste</summary>
    public static string PasteButton => ResourceManager.GetString("PasteButton", Culture)!;

    /// <summary>Cancel</summary>
    public static string CancelButton => ResourceManager.GetString("CancelButton", Culture)!;

    /// <summary>Loading...</summary>
    public static string Loading => ResourceManager.GetString("Loading", Culture)!;

    /// <summary>Desktop (Explorer not found)</summary>
    public static string DesktopFallback => ResourceManager.GetString("DesktopFallback", Culture)!;

    /// <summary>No files in clipboard.</summary>
    public static string NoFilesInClipboard => ResourceManager.GetString("NoFilesInClipboard", Culture)!;

    /// <summary>No items checked.</summary>
    public static string NoItemsChecked => ResourceManager.GetString("NoItemsChecked", Culture)!;

    /// <summary>1 file</summary>
    public static string FileCountOne => ResourceManager.GetString("FileCountOne", Culture)!;

    /// <summary>{0} files</summary>
    public static string FileCountMany => ResourceManager.GetString("FileCountMany", Culture)!;

    /// <summary>{0} directly</summary>
    public static string FlattenPreview => ResourceManager.GetString("FlattenPreview", Culture)!;

    /// <summary>{0} directly, {1} renamed to avoid overwriting</summary>
    public static string FlattenPreviewRenamed => ResourceManager.GetString("FlattenPreviewRenamed", Culture)!;

    /// <summary>Copied successfully.</summary>
    public static string CopySucceeded => ResourceManager.GetString("CopySucceeded", Culture)!;

    /// <summary>Error while copying: {0}</summary>
    public static string CopyFailed => ResourceManager.GetString("CopyFailed", Culture)!;

    /// <summary>Source and destination are the same file: {0}</summary>
    public static string SameFileError => ResourceManager.GetString("SameFileError", Culture)!;

    /// <summary>Failed to register hotkey (Ctrl+Alt+V).</summary>
    public static string HotkeyFailed => ResourceManager.GetString("HotkeyFailed", Culture)!;

    /// <summary>Show (Ctrl+Alt+V)</summary>
    public static string TrayShow => ResourceManager.GetString("TrayShow", Culture)!;

    /// <summary>Exit</summary>
    public static string TrayExit => ResourceManager.GetString("TrayExit", Culture)!;

    /// <summary>Info</summary>
    public static string InfoTitle => ResourceManager.GetString("InfoTitle", Culture)!;

    /// <summary>Success</summary>
    public static string SuccessTitle => ResourceManager.GetString("SuccessTitle", Culture)!;

    /// <summary>Error</summary>
    public static string ErrorTitle => ResourceManager.GetString("ErrorTitle", Culture)!;
}
