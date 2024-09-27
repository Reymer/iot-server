using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevKit.Console;
using DevKit.Tool;
using Newtonsoft.Json;
using UnityEngine;

public class NetworkPortManager
{
    private static readonly Lazy<NetworkPortManager> instance = new(() => new NetworkPortManager());

    public static NetworkPortManager Instance => instance.Value;

    private readonly string filePath;
    private ConsoleUI consoleUI;
    private readonly NetworkConnector networkConnector = new();
    public readonly Dictionary<int, PortData> tcpServers = new();
    public readonly Dictionary<int, PortData> udpPorts = new();
    public readonly Dictionary<int, PortData> tcpClients = new();
    private readonly List<PortData> portDataList = new();

    public event Action<PortData> PortDataUpdated;

    private NetworkPortManager()
    {
        filePath = Path.Combine(Application.dataPath, "portData.json");
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("檔案路徑未設定！");
        }
    }

    public void Init()
    {
        consoleUI = GameObject.FindObjectOfType<ConsoleUI>(true);
        networkConnector.Init(consoleUI);
    }

    public class PortData
    {
        public string NetProtocol { get; set; }
        public PortDetails LocalPortDetails { get; set; }
        public PortDetails RemotePortDetails { get; set; }
        public string TargetIP { get; set; }
        [JsonIgnore]
        public Action<PortData> OnUpdate { get; set; }
        public int COMReceived { get; set; } = 0;
        public int NetReceived { get; set; } = 0;
    }

    public class PortDetails
    {
        public int Port { get; set; }
        public string Description { get; set; }
    }

    public PortData AddPortData(string protocolType, int remotePort, int localPort, string target)
    {
        var data = new PortData
        {
            NetProtocol = protocolType,
            LocalPortDetails = new PortDetails { Port = localPort },
            RemotePortDetails = new PortDetails { Port = remotePort },
            TargetIP = target,
            COMReceived = 0,
            NetReceived = 0,
            OnUpdate = OnUpdate 
        };

        AddPortToDictionary(data);
        portDataList.Add(data);
        networkConnector.AddPort(data);
        SavePortDataToFile();
        return data;
    }

    private void AddPortToDictionary(PortData data)
    {
        if (data.NetProtocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
        {
            tcpServers[data.RemotePortDetails.Port] = data;
            Debug.Log($"Added TCP Server Port: {data.RemotePortDetails.Port}");
        }
        else if (data.NetProtocol.Equals("UDP", StringComparison.OrdinalIgnoreCase))
        {
            udpPorts[data.RemotePortDetails.Port] = data; // Assuming all UDP ports are treated similarly
            Debug.Log($"Added UDP Port: {data.RemotePortDetails.Port}");
        }
    }


    public bool IsPortUnique(string protocolType, int port)
    {
        if (protocolType.Equals("TCP", StringComparison.OrdinalIgnoreCase))
        {
            return !tcpServers.ContainsKey(port);
        }
        else if (protocolType.Equals("UDP", StringComparison.OrdinalIgnoreCase))
        {
            return !udpPorts.ContainsKey(port);
        }

        return false;
    }


    public void RemovePortData(string netProtocol, int remotePort)
    {
        PortData dataToRemove = GetPortData(netProtocol, remotePort);

        if (dataToRemove == null)
        {
            Debug.LogWarning($"找不到協定為 {netProtocol}，遠端端口為 {remotePort} 的資料。");
            return;
        }

        Debug.Log($"Removing {netProtocol} Port: {remotePort}");

        // 移除事件訂閱
        dataToRemove.OnUpdate -= OnUpdate;

        RemovePortFromDictionary(netProtocol, remotePort);

        bool removedFromList = portDataList.Remove(dataToRemove);
        if (!removedFromList)
        {
            Debug.LogWarning($"未能成功從清單中移除協定為 {netProtocol}，遠端端口為 {remotePort} 的資料。");
        }
        networkConnector.StopClient(dataToRemove.RemotePortDetails.Port, netProtocol);
        SavePortDataToFile();
    }

    private PortData GetPortData(string netProtocol, int remotePort)
    {
        return netProtocol.Equals("TCP", StringComparison.OrdinalIgnoreCase) ? tcpServers.GetValueOrDefault(remotePort) :
               netProtocol.Equals("UDP", StringComparison.OrdinalIgnoreCase) ? udpPorts.GetValueOrDefault(remotePort) : null;
    }

    private void RemovePortFromDictionary(string netProtocol, int remotePort)
    {
        if (netProtocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
        {
            if (tcpServers.Remove(remotePort))
            {
                Debug.Log($"Successfully removed TCP Port: {remotePort}");
            }
            else
            {
                Debug.LogWarning($"TCP Port: {remotePort} not found for removal.");
            }
        }
        else if (netProtocol.Equals("UDP", StringComparison.OrdinalIgnoreCase))
        {
            if (udpPorts.Remove(remotePort))
            {
                Debug.Log($"Successfully removed UDP Port: {remotePort}");
            }
            else
            {
                Debug.LogWarning($"UDP Port: {remotePort} not found for removal.");
            }
        }
    }

    private void OnUpdate(PortData data)
    {
        Debug.Log($"OnUpdate triggered for port: {data.RemotePortDetails.Port}, COMReceived: {data.COMReceived}");
        PortDataUpdated?.Invoke(data);
    }

    public void RefreshAndRecreateTables(PortTablePrefabManager prefabManager, UICollector uiCollector)
    {
        InstantiateTables(prefabManager, uiCollector);
    }

    public void InstantiateTables(PortTablePrefabManager prefabManager, UICollector uiCollector)
    {
        foreach (var portData in portDataList)
        {
            prefabManager.InstantiatePortTable(uiCollector, portData);
        }
    }

    public void AddPortsToNetwork()
    {
        var allPorts = tcpServers.Values.Concat(udpPorts.Values).ToList();

        foreach (var portData in allPorts)
        {
            networkConnector.AddPort(portData);
        }
    }

    public void LoadFromJson()
    {
        try
        {
            var loadedData = JsonFileHandler.LoadFromJson<PortData>(filePath);
            if (loadedData == null || !loadedData.Any())
            {
                consoleUI?.AddLog("載入的資料為空。");
                return;
            }
            portDataList.Clear();
            tcpServers.Clear();
            udpPorts.Clear();

            foreach (var data in loadedData)
            {
                data.OnUpdate += OnUpdate;
                portDataList.Add(data);
                AddPortToDictionary(data);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load data from JSON: {ex.Message}");
            consoleUI?.AddLog("無法載入資料，請檢查文件的格式和路徑。");
        }
    }
    public void DeInit()
    {
        networkConnector.DeInit();
        foreach (var portData in tcpServers.Values.Concat(udpPorts.Values))
        {
            portData.OnUpdate -= OnUpdate;
        }
        PortDataUpdated = null;
        SavePortDataToFile();
    }
    private void SavePortDataToFile()
    {
        try
        {
            JsonFileHandler.SaveToJson(filePath, portDataList);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save port data to JSON: {ex.Message}");
            consoleUI?.AddLog("無法保存端口資料。");
        }
    }
}
