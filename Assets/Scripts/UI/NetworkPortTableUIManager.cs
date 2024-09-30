using DevKit.Tool;
using UnityEngine;
using DevKit.Console;
using System;
using static NetworkPortManager;
public class NetworkPortTableUIManager : MonoBehaviour
{
    private PortTablePrefabManager prefabManager;
    private UICollector uiCollector;
    private NetworkSettingsUI networkSettingUI;
    private ConsoleUI consoleUI;

    public void Init()
    {
        consoleUI = GameObject.FindObjectOfType<ConsoleUI>(true);
        prefabManager = GameObject.FindObjectOfType<PortTablePrefabManager>();
        networkSettingUI = GameObject.FindObjectOfType<NetworkSettingsUI>();
        uiCollector = GetComponent<UICollector>();
        networkSettingUI.Confirm += OnConfirm;
        NetworkPortManager.Instance.PortDataUpdated += OnUpdate;
        NetworkPortManager.Instance.LoadFromJson();
        NetworkPortManager.Instance.InstantiateTables(prefabManager, uiCollector);
        NetworkPortManager.Instance.AddPortsToNetwork();
    }

    private void OnConfirm(string protocolType, string remotePort, string localPort, string targetIP)
    {
        if (NetworkPortManager.Instance.IsPortUnique(protocolType, remotePort, localPort))
        {
            var portData = NetworkPortManager.Instance.AddPortData(protocolType, remotePort, localPort, targetIP);
            prefabManager.InstantiatePortTable(uiCollector, portData);
        }
        else
        {
            consoleUI.AddLog($"端口號: {remotePort} 已經存在，請選擇另一個端口號。");
        }
    }

    public void OnRemove(string netProtocol, PortData portData)
    {
        prefabManager.RefreshAndRecreateTables(uiCollector);
        NetworkPortManager.Instance.RemovePortData(netProtocol, portData);
        NetworkPortManager.Instance.RefreshAndRecreateTables(prefabManager, uiCollector);
    }

    public void OnConnect(string netProtocol, PortData portData)
    {
        prefabManager.RefreshAndRecreateTables(uiCollector);
        NetworkPortManager.Instance.ConnectPort(netProtocol, portData);
        NetworkPortManager.Instance.RefreshAndRecreateTables(prefabManager, uiCollector);
    }


    public void OnUpdate(PortData portData)
    {
        prefabManager.RefreshAndRecreateTables(uiCollector);
        NetworkPortManager.Instance.RefreshAndRecreateTables(prefabManager, uiCollector);
    }

    public void DeInit()
    {
        if (networkSettingUI != null)
        {
            networkSettingUI.Confirm -= OnConfirm;
            NetworkPortManager.Instance.PortDataUpdated -= OnUpdate;
        }
    }
}
