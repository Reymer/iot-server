using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public static class JsonFileHandler
{
    public static void SaveToJson<T>(string filePath, List<T> data)
    {
        try
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"儲存到 JSON 時發生錯誤: {ex.Message}");
        }
    }

    public static List<T> LoadFromJson<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"檔案 {filePath} 不存在。");
            return new List<T>();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"從 JSON 載入資料時發生錯誤: {ex.Message}");
            return new List<T>();
        }
    }
}
