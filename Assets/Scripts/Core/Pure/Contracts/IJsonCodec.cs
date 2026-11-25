namespace RacingGame
{
    /// <summary>
    /// Serialization boundary abstraction (DIP) decoupling pure core rules from UnityEngine.JsonUtility.
    /// </summary>
    public interface IJsonCodec
    {
        string Serialize<T>(T value, bool prettyPrint = true);
        T Deserialize<T>(string json);
    }
}
