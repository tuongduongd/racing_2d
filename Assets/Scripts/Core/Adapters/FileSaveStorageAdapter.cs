using System.IO;

namespace RacingGame
{
    /// <summary>
    /// Concrete file-system adapter implementing ISaveStorage (Structural Adapter / DIP).
    /// </summary>
    public sealed class FileSaveStorageAdapter : ISaveStorage
    {
        public bool Exists(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public void WriteAllText(string path, string content)
        {
            if (string.IsNullOrEmpty(path)) return;

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, content);
        }

        public void Delete(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
