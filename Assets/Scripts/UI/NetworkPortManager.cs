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

    private readonly string filePath; // JSON 文件的路徑
    private ConsoleUI consoleUI; // 控制台 UI
    private readonly NetworkConnector networkConnector = new(); // 網路連接器
    public readonly Dictionary<int, PortData> tcpPorts = new(); // 儲存 TCP 端口資料的字典
    public readonly Dictionary<int, PortData> udpPorts = new(); // 儲存 UDP 端口資料的字典
    private readonly List<PortData> portDataList = new(); // 儲存所有端口資料的清單

    public event Action<PortData> PortDataUpdated; // 端口資料更新事件

    private NetworkPortManager()
    {
        // 設定 JSON 文件的路徑
        filePath = Path.Combine(Application.dataPath, "portData.json");
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("檔案路徑未設定！");
        }
    }

    public void Init()
    {
        consoleUI = GameObject.FindObjectOfType<ConsoleUI>(true); // 找到控制台 UI
        networkConnector.Init(consoleUI); // 初始化網路連接器
        //PortDataUpdated += HandlePortDataUpdated; // 註冊事件處理器
    }

    public class PortData
    {
        public string NetProtocol { get; set; } // 協定類型 (TCP/UDP)
        public PortDetails LocalPortDetails { get; set; } // 本地端口詳情
        public PortDetails RemotePortDetails { get; set; } // 遠端端口詳情
        public string TargetIP { get; set; }
        [JsonIgnore]
        public Action<PortData> OnUpdate { get; set; } // 更新時的回調函數
        public int COMReceived { get; set; } = 0; // 接收到的 COM 數據量
        public int NetReceived { get; set; } = 0; // 接收到的網路數據量
    }

    public class PortDetails
    {
        public int Port { get; set; } // 端口號
        public string Description { get; set; } // 端口描述
    }

    public PortData AddPortData(string protocolType, int remotePort, int localPort, string target)
    {
        // 建立新的 PortData 物件
        var data = new PortData
        {
            NetProtocol = protocolType,
            LocalPortDetails = new PortDetails { Port = localPort },
            RemotePortDetails = new PortDetails { Port = remotePort },
            TargetIP = target,
            COMReceived = 0,
            NetReceived = 0,
            OnUpdate = data => OnUpdate(data) // 設定更新回調
        };

        // 將物件加入字典和清單
        AddPortToDictionary(data); // 將端口資料加入對應的字典
        portDataList.Add(data); // 將端口資料加入清單
        networkConnector.AddPort(data); // 通知網路連接器新增端口
        JsonFileHandler.SaveToJson(filePath, portDataList); // 將資料保存到 JSON 文件中
        return data; // 返回新增的端口資料
    }

    private void AddPortToDictionary(PortData data)
    {
        // 根據協定類型將資料加入對應的字典
        if (data.NetProtocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
        {
            tcpPorts[data.RemotePortDetails.Port] = data; // 添加 TCP 端口資料
            Debug.Log($"Added TCP Port: {data.RemotePortDetails.Port}");
        }
        else if (data.NetProtocol.Equals("UDP", StringComparison.OrdinalIgnoreCase))
        {
            udpPorts[data.RemotePortDetails.Port] = data; // 添加 UDP 端口資料
            Debug.Log($"Added UDP Port: {data.RemotePortDetails.Port}");
        }
    }

    public bool IsPortUnique(string protocolType, int port)
    {
        // 檢查端口是否唯一，根據協定類型進行判斷
        return protocolType.Equals("TCP", StringComparison.OrdinalIgnoreCase) ? !tcpPorts.ContainsKey(port) :
               protocolType.Equals("UDP", StringComparison.OrdinalIgnoreCase) && !udpPorts.ContainsKey(port);
    }

    public void RemovePortData(string netProtocol, int remotePort)
    {
        // 取得要移除的資料
        PortData dataToRemove = GetPortData(netProtocol, remotePort);

        // 檢查是否成功取得資料
        if (dataToRemove == null)
        {
            Debug.LogWarning($"找不到協定為 {netProtocol}，遠端端口為 {remotePort} 的資料。");
            return;
        }

        // 日誌記錄移除的端口
        Debug.Log($"Removing {netProtocol} Port: {remotePort}");

        // 取消事件訂閱，確保不再接收更新事件
        dataToRemove.OnUpdate -= OnUpdate;

        // 從字典中移除資料，並檢查是否成功移除
        RemovePortFromDictionary(netProtocol, remotePort);

        // 從清單中移除資料，並檢查是否成功移除
        bool removedFromList = portDataList.Remove(dataToRemove);
        if (!removedFromList)
        {
            Debug.LogWarning($"未能成功從清單中移除協定為 {netProtocol}，遠端端口為 {remotePort} 的資料。");
        }

        // 停止對應的網路連接
        networkConnector.StopClient(dataToRemove.RemotePortDetails.Port, netProtocol);

        // 將更新後的資料保存到 JSON 文件中
        JsonFileHandler.SaveToJson(filePath, portDataList);

        // 在控制台輸出已移除的記錄
        consoleUI.AddLog($"已移除 協定: {netProtocol}, 遠端端口: {remotePort}");
    }

    private PortData GetPortData(string netProtocol, int remotePort)
    {
        // 根據協定類型取得端口資料
        return netProtocol.Equals("TCP", StringComparison.OrdinalIgnoreCase) ? tcpPorts.GetValueOrDefault(remotePort) :
               netProtocol.Equals("UDP", StringComparison.OrdinalIgnoreCase) ? udpPorts.GetValueOrDefault(remotePort) : null;
    }

    private void RemovePortFromDictionary(string netProtocol, int remotePort)
    {
        // 根據協定類型移除對應的端口資料
        if (netProtocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
        {
            if (tcpPorts.Remove(remotePort))
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
        // 當端口資料更新時觸發的事件
        Debug.Log($"OnUpdate triggered for port: {data.RemotePortDetails.Port}, COMReceived: {data.COMReceived}");
        PortDataUpdated?.Invoke(data); // 觸發事件
    }

    public void RefreshAndRecreateTables(PortTablePrefabManager prefabManager, UICollector uiCollector)
    {
        InstantiateTables(prefabManager, uiCollector);
    }

    public void InstantiateTables(PortTablePrefabManager prefabManager, UICollector uiCollector)
    {
        //var allPorts = tcpPorts.Values.Concat(udpPorts.Values).ToList();

        foreach (var portData in portDataList)
        {
            prefabManager.InstantiatePortTable(uiCollector, portData);
        }
    }

    public void AddPortsToNetwork()
    {
        // 將所有端口添加到網路
        var allPorts = tcpPorts.Values.Concat(udpPorts.Values).ToList();

        foreach (var portData in allPorts)
        {
            networkConnector.AddPort(portData); // 通知網路連接器添加端口
        }
    }

    public void LoadFromJson()
    {
        // 從 JSON 文件中加載端口資料
        var loadedData = JsonFileHandler.LoadFromJson<PortData>(filePath);

        if (loadedData.Any())
        {
            portDataList.Clear(); // 清空現有端口資料
            tcpPorts.Clear(); // 清空 TCP 端口字典
            udpPorts.Clear(); // 清空 UDP 端口字典

            foreach (var data in loadedData)
            {
                data.OnUpdate += OnUpdate;  // 確保事件重新訂閱
                portDataList.Add(data); // 將加載的資料加入清單
                AddPortToDictionary(data); // 將資料加入字典
            }
        }
        else
        {
            Debug.LogWarning("載入的資料為空。");
        }
    }

    public void DeInit()
    {
        // 去初始化
        networkConnector.DeInit();
        foreach (var portData in tcpPorts.Values.Concat(udpPorts.Values))
        {
            portData.OnUpdate -= OnUpdate; // 取消事件訂閱
        }
        PortDataUpdated = null; // 清空事件
        JsonFileHandler.SaveToJson(filePath, portDataList); // 保存當前資料到 JSON 文件
    }

    private void HandlePortDataUpdated(PortData data)
    {
        // 當端口資料更新時的處理函數
        JsonFileHandler.SaveToJson(filePath, portDataList); // 保存更新後的資料到 JSON 文件
        Debug.Log($"PortDataUpdated: 協定: {data.NetProtocol}, 遠端端口: {data.RemotePortDetails.Port}, 總消息長度: {data.COMReceived}");
    }
}
