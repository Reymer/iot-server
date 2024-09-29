using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
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
    public readonly Dictionary<string, PortData> tcpServers = new();
    public readonly Dictionary<string, PortData> tcpClients = new();
    public readonly Dictionary<string, PortData> udpPorts = new();
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
        public int COMReceived { get; set; } = 0;
        public int NetReceived { get; set; } = 0;
        [JsonIgnore]
        public Action<PortData> OnUpdate { get; set; }
    }

    public class PortDetails
    {
        public string Port { get; set; }
        public string Description { get; set; }
    }

    public PortData AddPortData(string protocolType, string remotePort, string localPort, string target)
    {
        var data = new PortData
        {
            NetProtocol = protocolType,
            LocalPortDetails = new PortDetails { Port = localPort },
            RemotePortDetails = new PortDetails { Port = remotePort},
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
        if (data.NetProtocol.Equals("TCP Server", StringComparison.OrdinalIgnoreCase))
        {
            tcpServers[data.LocalPortDetails.Port] = data;
        }
        else if (data.NetProtocol.Equals("TCP Client", StringComparison.OrdinalIgnoreCase))
        {
            tcpClients[data.RemotePortDetails.Port] = data;
        }
        else if (data.NetProtocol.Equals("UDP", StringComparison.OrdinalIgnoreCase))
        {
            udpPorts[data.RemotePortDetails.Port] = data;
        }
    }

    public bool IsPortUnique(string protocolType, string remotePort, string localPort)
    {
        if (protocolType.Equals("TCP Server", StringComparison.OrdinalIgnoreCase))
        {
            return !tcpServers.ContainsKey(localPort); 
        }
        else if (protocolType.Equals("TCP Client", StringComparison.OrdinalIgnoreCase))
        {
            return !tcpClients.ContainsKey(remotePort);
        }
        else if (protocolType.Equals("UDP", StringComparison.OrdinalIgnoreCase))
        {
            return !udpPorts.ContainsKey(remotePort);
        }

        return false;
    }

    public void RemovePortData(string netProtocol, PortData portData)
    {
        PortData dataToRemove = GetPortData(netProtocol, portData);

        if (dataToRemove == null)
        {
            Debug.LogWarning($"未能找到協定為 {netProtocol} 的端口資料，無法移除。");
            return;
        }

        string portToRemove = (netProtocol.Equals("TCP Server", StringComparison.OrdinalIgnoreCase))
            ? dataToRemove.LocalPortDetails.Port
            : dataToRemove.RemotePortDetails.Port;


        Debug.Log($"Removing {netProtocol} Port: {portToRemove}");
        dataToRemove.OnUpdate -= OnUpdate;
        RemovePortFromDictionary(netProtocol, portToRemove);

        bool removedFromList = portDataList.Remove(dataToRemove);
        if (!removedFromList)
        {
            Debug.LogWarning($"未能成功從清單中移除協定為 {netProtocol}，端口為 {portToRemove} 的資料。");
        }
        networkConnector.StopClient(portToRemove, netProtocol);

        SavePortDataToFile();
    }

    public void ConnectPort(string netProtocol, PortData portData)
    {
        networkConnector.AddPort(portData);
    }


    private PortData GetPortData(string netProtocol, PortData portData)
    {
        if (portData == null)
        {
            throw new ArgumentNullException(nameof(portData), "PortData cannot be null.");
        }

        PortData resultPortData = null;

        if (netProtocol.Equals("TCP Server", StringComparison.OrdinalIgnoreCase))
        {
            resultPortData = tcpServers.GetValueOrDefault(portData.LocalPortDetails.Port);
        }
        else if (netProtocol.Equals("UDP", StringComparison.OrdinalIgnoreCase))
        {
            resultPortData = udpPorts.GetValueOrDefault(portData.RemotePortDetails.Port);
        }
        else if (netProtocol.Equals("TCP Client", StringComparison.OrdinalIgnoreCase))
        {
            resultPortData = tcpClients.GetValueOrDefault(portData.RemotePortDetails.Port);
        }

        return resultPortData;
    }


    private void RemovePortFromDictionary(string netProtocol, string port)
    {
        if (netProtocol.Equals("TCP Server", StringComparison.OrdinalIgnoreCase))
        {
            if (tcpServers.Remove(port))
            {
                Debug.Log($"Successfully removed TCP Server on Local Port: {port}");
            }
            else
            {
                Debug.LogWarning($"TCP Server Local Port: {port} not found for removal.");
            }
        }
        else if (netProtocol.Equals("TCP Client", StringComparison.OrdinalIgnoreCase))
        {
            if (tcpClients.Remove(port))
            {
                Debug.Log($"Successfully removed TCP Client on Remote Port: {port}");
            }
            else
            {
                Debug.LogWarning($"TCP Client Remote Port: {port} not found for removal.");
            }
        }
        else if (netProtocol.Equals("UDP", StringComparison.OrdinalIgnoreCase))
        {
            if (udpPorts.Remove(port))
            {
                Debug.Log($"Successfully removed UDP Port: {port}");
            }
            else
            {
                Debug.LogWarning($"UDP Port: {port} not found for removal.");
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
        var allPorts = tcpServers.Values.Concat(udpPorts.Values).Concat(tcpClients.Values).ToList();

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
            tcpClients.Clear();

            foreach (var data in loadedData)
            {
                data.OnUpdate += OnUpdate;
                portDataList.Add(data);
                AddPortToDictionary(data);
            }
        }
        catch (Exception ex)
        {
            consoleUI?.AddLog("無法載入資料，請檢查文件的格式和路徑。" + ex);
        }
    }

    public void DeInit()
    {
        networkConnector.DeInit();
        foreach (var portData in tcpServers.Values.Concat(udpPorts.Values).Concat(tcpClients.Values))
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
            Debug.Log("無法保存端口資料。" + ex);
        }
    }
}
