using System.IO;
using System.Text;

namespace CyberWall.Common.IO;

/// <summary>
/// Writes state files without leaving them truncated. A crash or a concurrent write
/// during a plain <see cref="File.WriteAllText(string, string)"/> can leave a half
/// written JSON file behind, which silently discards every stored rule or setting.
/// </summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string contents, Encoding? encoding = null)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        File.WriteAllText(temp, contents, encoding ?? new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    public static void WriteAllBytes(string path, byte[] contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        File.WriteAllBytes(temp, contents);
        File.Move(temp, path, overwrite: true);
    }
}
