using DevKit.Tool;
using System;
using UnityEngine;
using static NetworkPortManager;

public class Table : MonoBehaviour
{
    public UICollector uiCollector;

    private string protocolType;
    private string remotePort;
    private string localPort;
    private int receivedCount;
    private int comReceivedCount;
    private string receivedStatus;
    private string targetIP;
    public Action<string, PortData> delete;

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
        SetValue();
    }

    private void Subscribe()
    {
        if (uiCollector != null)
        {
            uiCollector.BindOnCheck(UIKey.table_Delete, () => Delete(protocolType, new PortData
            {
                NetProtocol = protocolType,
                RemotePortDetails = new PortDetails { Port = remotePort },
                LocalPortDetails = new PortDetails { Port = localPort },
                TargetIP = targetIP,
                COMReceived = comReceivedCount,
                NetReceived = receivedCount,
                OnUpdate = null
            }));
        }
    }

    public void SetValue()
    {
        if (uiCollector != null)
        {
            SetValue(UIKey.table_prococolText, protocolType);
            SetValue(UIKey.table_remoteText, remotePort.ToString());
            SetValue(UIKey.table_localPortText, localPort.ToString());
            SetValue(UIKey.table_COMReceived, comReceivedCount.ToString());
            SetValue(UIKey.table_netReceived, receivedCount.ToString());
            SetValue(UIKey.table_ForwardTargetText, targetIP);
            SetValue(UIKey.table_netReceivedStatus, receivedStatus);
        }
    }

    public void Delete(string protocolType, PortData portData)
    {
        delete?.Invoke(protocolType, portData);
    }

    public void SetValue(string uiKey, string content)
    {
        if (uiCollector != null)
        {
            uiCollector.SetText(uiKey, content);
        }
    }
}
