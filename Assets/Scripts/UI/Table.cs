using DevKit.Tool;
using System;
using Unity.VisualScripting;
using UnityEngine;
using static NetworkPortManager;

public class Table : MonoBehaviour
{
    public UICollector uiCollector;

    private string protocolType;
    private string remotePort;
    private string localPort;
    private string targetIP;
    private string receivedStatus;
    private int receivedCount;
    private int comReceivedCount;

    public event Action<string, PortData> OnDelete;
    public event Action<string, PortData> OnConnect;

    private void Start()
    {
        Subscribe();
    }

    public void Init(string protocolType, string remotePort, string localPort, string targetIP, int comReceivedCount, int receivedCount, string receivedStatus)
    {
        this.protocolType = protocolType;
        this.remotePort = remotePort;
        this.localPort = localPort;
        this.targetIP = targetIP;
        this.comReceivedCount = comReceivedCount;
        this.receivedCount = receivedCount;
        this.receivedStatus = receivedStatus;

        UpdateUI();
    }

    private void Subscribe()
    {
        if (uiCollector == null)
        {
            Debug.LogError("UICollector is not assigned.");
            return;
        }

        uiCollector.BindOnCheck(UIKey.table_Delete, () => HandleAction(OnDelete));
        uiCollector.BindOnCheck(UIKey.table_Connect, () => HandleAction(OnConnect));
    }

    private void HandleAction(Action<string, PortData> action)
    {
        action?.Invoke(protocolType, CreatePortData());
    }

    private void UpdateUI()
    {
        if (uiCollector == null)
        {
            Debug.LogError("UICollector is not assigned.");
            return;
        }

        SetValue(UIKey.table_prococolText, protocolType);
        SetValue(UIKey.table_remoteText, remotePort);
        SetValue(UIKey.table_localPortText, localPort);
        SetValue(UIKey.table_COMReceived, comReceivedCount.ToString());
        SetValue(UIKey.table_netReceived, receivedCount.ToString());
        SetValue(UIKey.table_ForwardTargetText, targetIP);
        SetValue(UIKey.table_netReceivedStatus, receivedStatus);

        if (protocolType.Equals("TCP Server", StringComparison.OrdinalIgnoreCase))
        {
            uiCollector.Deactive(UIKey.table_ConnectRoot);
        }
        else
        {
            uiCollector.Active(UIKey.table_ConnectRoot);
        }
    }

    private void SetValue(string uiKey, string content)
    {
        if (uiCollector != null)
        {
            uiCollector.SetText(uiKey, content);
        }
        else
        {
            Debug.LogError("UICollector is not assigned.");
        }
    }

    private PortData CreatePortData()
    {
        return new PortData
        {
            NetProtocol = protocolType,
            RemotePortDetails = new PortDetails { Port = remotePort },
            LocalPortDetails = new PortDetails { Port = localPort },
            TargetIP = targetIP,
            COMReceived = comReceivedCount,
            NetReceived = receivedCount,
            OnUpdate = null
        };
    }
}
