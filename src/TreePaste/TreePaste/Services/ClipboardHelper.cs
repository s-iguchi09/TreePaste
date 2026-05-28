using System.Collections.Specialized;
using System.IO;
using Clipboard = System.Windows.Clipboard;

namespace TreePaste.Services;

/// <summary>
/// クリップボードのファイルパス取得とファイルコピー操作を提供するユーティリティクラス。
/// Utility class providing clipboard file path retrieval and file copy operations.
/// </summary>
internal static class ClipboardHelper
{
    /// <summary>
    /// クリップボードに含まれるファイルパスのリストを取得する。
    /// Retrieves the list of file paths contained in the clipboard.
    /// </summary>
    /// <returns>ファイルパスのリスト。クリップボードにファイルがない場合は空のリスト。 / List of file paths; empty list if no files in clipboard.</returns>
    public static List<string> GetClipboardFilePaths()
    {
        var paths = new List<string>();

        if (Clipboard.ContainsFileDropList())
        {
            StringCollection files = Clipboard.GetFileDropList();
            foreach (string? file in files)
            {
                if (!string.IsNullOrEmpty(file))
                {
                    paths.Add(file);
                }
            }
        }

        return paths;
    }

    /// <summary>
    /// コピー元ディレクトリ配下のすべてのディレクトリ・ファイルパスをメモリに収集して返す。
    /// ディスクの読み取りはこの関数内でのみ行う。
    /// Collects all directory and file paths under the source into memory and returns them.
    /// All disk reads are performed exclusively in this function.
    /// </summary>
    /// <param name="rootPath">収集するルートディレクトリ（フルパス）。 / Root directory to collect from (full path).</param>
    /// <returns>収集したディレクトリパスとファイルパスのタプル。 / Tuple of collected directory paths and file paths.</returns>
    private static (List<string> Dirs, List<string> Files) CollectAllEntries(string rootPath)
    {
        var dirs = new List<string>();
        var files = new List<string>();
        var queue = new Queue<string>();
        queue.Enqueue(rootPath);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (string file in Directory.GetFiles(current))
                files.Add(file);
            foreach (string dir in Directory.GetDirectories(current))
            {
                dirs.Add(dir);
                queue.Enqueue(dir);
            }
        }

        return (dirs, files);
    }

    /// <summary>
    /// 収集済みのエントリリストをコピー先へ書き出す。
    /// ディスクの書き込みはこの関数内でのみ行う。
    /// Writes the collected entries to the destination.
    /// All disk writes are performed exclusively in this function.
    /// </summary>
    /// <param name="fullSource">コピー元ルートのフルパス。 / Full path of the source root.</param>
    /// <param name="fullDest">コピー先ルートのフルパス。 / Full path of the destination root.</param>
    /// <param name="dirs">収集済みディレクトリパスのリスト。 / Collected directory paths.</param>
    /// <param name="files">収集済みファイルパスのリスト。 / Collected file paths.</param>
    private static void WriteEntries(string fullSource, string fullDest, List<string> dirs, List<string> files)
    {
        Directory.CreateDirectory(fullDest);

        foreach (string dir in dirs)
        {
            string rel = Path.GetRelativePath(fullSource, dir);
            Directory.CreateDirectory(Path.Combine(fullDest, rel));
        }

        foreach (string file in files)
        {
            string rel = Path.GetRelativePath(fullSource, file);
            File.Copy(file, Path.Combine(fullDest, rel), overwrite: true);
        }
    }

    /// <summary>
    /// ディレクトリをコピーする。収集フェーズと書き込みフェーズを完全に分離しているため、
    /// コピー先がコピー元の配下にあっても無限ループにならない。
    /// Copies a directory. Collection and write phases are completely separated,
    /// so no infinite loop occurs even if the destination is inside the source.
    /// </summary>
    /// <param name="sourcePath">コピー元ディレクトリのパス。 / Source directory path.</param>
    /// <param name="destinationPath">コピー先ディレクトリのパス。 / Destination directory path.</param>
    public static void CopyDirectoryRecursive(string sourcePath, string destinationPath)
    {
        string fullSource = Path.GetFullPath(sourcePath);
        string fullDest = Path.GetFullPath(destinationPath);

        // フェーズ1: ディスクを読んでコピー対象をすべてメモリに収集する。書き込みは一切しない。
        // Phase 1: Read disk and collect all entries into memory. No writes at all.
        var (dirs, files) = CollectAllEntries(fullSource);

        // フェーズ2: 収集済みリストだけを使って書き込む。ディスクを読みに行かない。
        // Phase 2: Write using only the collected lists. No disk reads at all.
        WriteEntries(fullSource, fullDest, dirs, files);
    }

    /// <summary>
    /// ファイルまたはディレクトリをコピーする。ディレクトリの場合は再帰的にコピーする。
    /// Copies a file or directory. Directories are copied recursively.
    /// </summary>
    /// <param name="sourcePath">コピー元のパス。 / Source path.</param>
    /// <param name="destinationPath">コピー先のパス。 / Destination path.</param>
    public static void CopyFileOrDirectory(string sourcePath, string destinationPath)
    {
        if (File.Exists(sourcePath))
        {
            if (Path.GetFullPath(sourcePath) == Path.GetFullPath(destinationPath))
                throw new InvalidOperationException($"Source and destination are the same file:\n{sourcePath}");

            string? parentDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(parentDir))
                Directory.CreateDirectory(parentDir);

            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
        else if (Directory.Exists(sourcePath))
        {
            CopyDirectoryRecursive(sourcePath, destinationPath);
        }
    }
}
