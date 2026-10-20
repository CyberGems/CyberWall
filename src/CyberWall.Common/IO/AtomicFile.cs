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

        var enc = encoding ?? new UTF8Encoding(false);
        var temp = path + ".tmp";
        File.WriteAllText(temp, contents, enc);
        try
        {
            File.Move(temp, path, overwrite: true);
        }
        catch (IOException)
        {
            // The destination is open in another process, so it cannot be replaced.
            // The content is written in place instead of being left in the temporary
            // file, otherwise the update would be silently lost.
            try
            {
                File.WriteAllText(path, contents, enc);
            }
            catch
            {
                // The temporary file is kept: it still holds the content.
                return;
            }
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
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
