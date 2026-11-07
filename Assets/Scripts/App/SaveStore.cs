using System;
using System.IO;
using MartialTactics.Game;
using UnityEngine;

namespace MartialTactics.App
{
    /// <summary>진행 상황을 기기에 JSON 파일로 저장한다.</summary>
    public static class SaveStore
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static PlayerData Load()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonUtility.FromJson<PlayerData>(File.ReadAllText(FilePath)) ?? new PlayerData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"저장 파일을 읽지 못해 새로 시작합니다: {e.Message}");
            }
            return new PlayerData();
        }

        public static void Save(PlayerData data) => File.WriteAllText(FilePath, JsonUtility.ToJson(data));

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
