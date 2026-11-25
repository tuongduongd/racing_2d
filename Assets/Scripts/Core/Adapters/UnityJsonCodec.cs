using System;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// UnityEngine.JsonUtility adapter implementing IJsonCodec and throwing SaveDataCorruptedException on invalid payloads.
    /// </summary>
    public sealed class UnityJsonCodec : IJsonCodec
    {
        public string Serialize<T>(T value, bool prettyPrint = true)
        {
            return JsonUtility.ToJson(value, prettyPrint);
        }

        public T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new SaveDataCorruptedException(string.Empty, "JSON content is empty or whitespace.");
            }

            try
            {
                T result = JsonUtility.FromJson<T>(json);
                if (result == null)
                {
                    throw new SaveDataCorruptedException(string.Empty, $"JSON deserialization for {typeof(T).Name} returned null.");
                }
                return result;
            }
            catch (ArgumentException ex)
            {
                throw new SaveDataCorruptedException(string.Empty, $"Invalid JSON format for {typeof(T).Name}: {ex.Message}", ex);
            }
        }
    }
}
