namespace RacingGame
{
    /// <summary>
    /// Storage boundary abstraction (DIP) allowing pure domain logic to persist without direct System.IO coupling in tests.
    /// </summary>
    public interface ISaveStorage
    {
        bool Exists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string content);
        void Delete(string path);
    }
}
