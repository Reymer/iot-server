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
            Debug.LogError($"�x�s�� JSON �ɵo�Ϳ��~: {ex.Message}");
        }
    }

    public static List<T> LoadFromJson<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"�ɮ� {filePath} ���s�b�C");
            return new List<T>();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"�q JSON ���J��Ʈɵo�Ϳ��~: {ex.Message}");
            return new List<T>();
        }
    }
}
