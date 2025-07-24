using System.IO;
using System;
using System.Linq;

static class Extensions
{
    #region DirectoryInfo
    internal static DirectoryInfo Combine(this DirectoryInfo dir, params string[] paths)
    {
        if (dir == null) throw new ArgumentNullException(nameof(dir));
        var final = dir.FullName;
        foreach (var path in paths)
        {
            final = Path.Combine(final, path ?? string.Empty);
        }
        return new DirectoryInfo(final);
    }
    internal static bool IsEmpty(this DirectoryInfo directory)
    {
        if (directory == null) throw new ArgumentNullException(nameof(directory));
        return !Directory.EnumerateFileSystemEntries(directory.FullName).Any();
    }
    #endregion
    #region FileInfo

    internal static FileInfo CombineFile(this DirectoryInfo dir, params string[] paths)
    {
        if (dir == null) throw new ArgumentNullException(nameof(dir));
        var final = dir.FullName;
        foreach (var path in paths)
        {
            final = Path.Combine(final, path ?? string.Empty);
        }
        return new FileInfo(final);
    }
    internal static FileInfo Combine(this FileInfo file, params string[] paths)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        var final = file.DirectoryName ?? string.Empty;
        foreach (var path in paths)
        {
            final = Path.Combine(final, path ?? string.Empty);
        }
        return new FileInfo(final);
    }
    internal static string FileNameWithoutExtension(this FileInfo file)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        return Path.GetFileNameWithoutExtension(file.Name);
    }
    internal static bool WriteAllText(this FileInfo file, string content)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        if (file.Directory != null && !file.Directory.Exists) file.Directory.Create();
        try
        {
            File.WriteAllText(file.FullName, content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to file {file.FullName}: {ex.Message}");
            return false;
        }
        return true;
    }
    internal static string ReadAllText(this FileInfo file)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        if (file.Directory != null && !file.Directory.Exists) file.Directory.Create();
        if (!file.Exists) return string.Empty;
        return File.ReadAllText(file.FullName);
    }
    #endregion
}