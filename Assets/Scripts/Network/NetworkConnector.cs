using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using UnityEngine;
using DevKit.Console;
using static NetworkPortManager;
using System.IO;

public class NetworkConnector
{
    private ConsoleUI consoleUI;

    #region 資料結構TCP、UDP
    public class UdpData : IDisposable
    {
        public UdpClient UdpClient;
        public CancellationTokenSource CancellationTokenSource = new();
        public string SourceData = string.Empty;
        private bool disposed = false;

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                CancellationTokenSource.Cancel();
                CancellationTokenSource.Dispose();
                UdpClient?.Close();
                UdpClient?.Dispose();
            }
        }
    }
    public void Init(ConsoleUI consoleUI)
    {
        this.consoleUI = consoleUI;
    }

    public class TCPData : IDisposable
{
    public TcpListener TcpListener;
    public CancellationTokenSource CancellationTokenSource = new();
    public string SourceData = string.Empty;
    private bool disposed = false;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();      
        TcpListener?.Stop();
        TcpListener?.Server?.Dispose(); 
    }
}

    #endregion

    #region 資料字典存取
    public enum ConnectionType { UDP, TCP }
    private readonly ConcurrentDictionary<int, UdpData> udpClients = new();
    private readonly ConcurrentDictionary<int, TCPData> tcpClients = new();

    #endregion

    #region 單獨新增 TCP 或 UDP 監聽器
    /// <summary>
    /// 單獨新增 TCP 或 UDP 監聽器
    /// </summary>
    /// <param name="remotePort">要新增的端口</param>
    /// <param name="connectionType">連接類型 (TCP 或 UDP)</param>
    public void AddPort(PortData portData)
    {
        if (portData.NetProtocol.Equals("UDP", StringComparison.OrdinalIgnoreCase))
        {
            AddUdpClient(portData);
        }
        else if (portData.NetProtocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
        {
            AddTcpListener(portData);
        }
        else
        {
            LogOnMainThread($"無法識別的連接類型: {portData.NetProtocol}", isError: true);
        }
    }

    /// <summary>
    /// 新增單個 TCP 監聽器
    /// </summary>
    /// <param name="remotePort">要新增的端口</param>
    private void AddTcpListener(PortData portData)
    {
        if (tcpClients.ContainsKey(portData.RemotePortDetails.Port))
        {
            LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 已經存在 TCP 監聽器。");
            return;
        }

        try
        {
            var tcpListener = new TcpListener(IPAddress.Any, portData.RemotePortDetails.Port);
            tcpListener.Start();
            var tcpData = new TCPData
            {
                TcpListener = tcpListener,
            };

            if (tcpClients.TryAdd(portData.RemotePortDetails.Port, tcpData))
            {
                LogOnMainThread($"在目標端口 {portData.RemotePortDetails.Port} 上啟動了 TCP 監聽器。");
                Task.Run(() => ListenForTcpClients(portData, tcpData));
            }
            else
            {
                tcpListener.Stop();
                LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 上的 TCP 監聽器已經存在。");
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 已經被使用。", isError: true);
        }
        catch (Exception ex)
        {
            LogOnMainThread($"初始化端口 {portData.RemotePortDetails.Port} 的 TCP 監聽器時發生錯誤: {ex.Message}", isError: true);
        }
    }

    /// <summary>
    /// 新增單個 UDP 客戶端
    /// </summary>
    /// <param name="remotePort">要新增的端口</param>
    private void AddUdpClient(PortData portData)
    {
        if (!udpClients.ContainsKey(portData.RemotePortDetails.Port))
        {
            try
            {
                var udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, portData.RemotePortDetails.Port));
                UdpData udpData = new() { UdpClient = udpClient };

                if (udpClients.TryAdd(portData.RemotePortDetails.Port, udpData))
                {
                    LogOnMainThread($"在端口 {portData.RemotePortDetails.Port} 上啟動了 UDP 客戶端。");
                    Task.Run(() => ReceiveUdpMessages(portData, udpData));
                }
                else
                {
                    udpClient.Close();
                    LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 上的 UDP 客戶端已經存在。");
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 已經被使用。", isError: true);
            }
            catch (Exception ex)
            {
                LogOnMainThread($"初始化端口 {portData.RemotePortDetails.Port} 的 UDP 客戶端時發生錯誤: {ex.Message}", isError: true);
            }
        }
    }
    #endregion

    #region 單獨停止指定端口

    /// <summary>
    /// 停止指定端口上的 TCP 或 UDP 客戶端，並刪除相關資料
    /// </summary>
    /// <param name="port">端口號</param>
    /// <param name="connectionType">連接類型，"TCP" 或 "UDP"</param>
    public void StopClient(int port, string connectionType)
    {
        try
        {
            if (connectionType.Equals("UDP", StringComparison.OrdinalIgnoreCase))
            {
                DisposeClientResources(port, udpClients, "UDP");
            }
            else if (connectionType.Equals("TCP", StringComparison.OrdinalIgnoreCase))
            {
                DisposeClientResources(port, tcpClients, "TCP");
            }
        }
        catch (Exception ex)
        {
            LogOnMainThread($"停止客戶端時出現錯誤: {ex.Message}", isError: true);
        }
    }
    private void DisposeClientResources<T>(int port, ConcurrentDictionary<int, T> clientDictionary, string protocol) where T : IDisposable
    {
        if (clientDictionary.TryRemove(port, out T clientData))
        {
            clientData.Dispose();
            LogOnMainThread($"已停止並刪除端口 {port} 上的 {protocol} 客戶端。");
        }
        else
        {
            LogOnMainThread($"無法找到端口 {port} 上的 {protocol} 客戶端。", isError: true);
        }
    }

    #endregion

    #region TCP 方法

    /// <summary>
    /// 監聽 TCP 客戶端
    /// </summary>
    /// <param name="tcpData">TCP 資料</param>
    /// <param name="remotePort">端口</param>
    private async Task ListenForTcpClients(PortData portData, TCPData tcpData)
    {
        try
        {
            while (!tcpData.CancellationTokenSource.Token.IsCancellationRequested)
            {
                await HandleTcpClientConnection(portData, tcpData);
            }
        }
        catch (OperationCanceledException)
        {
            LogOnMainThread("TCP 監聽器已取消。");
        }
        catch (Exception ex)
        {
            LogOnMainThread($"TCP 監聽器遇到錯誤: {ex.Message}", isError: true);
        }
        finally
        {
            tcpData.CancellationTokenSource?.Cancel();
            tcpData.CancellationTokenSource?.Dispose();

            if (tcpData.TcpListener != null)
            {
                tcpData.TcpListener.Stop();
                tcpData.TcpListener = null;
            }

            LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 的 TCP 監聽器已關閉。");
        }
    }
    private async Task HandleTcpClientConnection(PortData portData, TCPData tcpData)
    {
        try
        {
            var client = await tcpData.TcpListener.AcceptTcpClientAsync().WithCancellation(tcpData.CancellationTokenSource.Token);
            LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 上的客戶端已連接");
            await ReceiveTcpMessages(portData, client, tcpData);
        }
        catch (Exception ex) when (ex is OperationCanceledException ||
                                    ex is ObjectDisposedException ||
                                    (ex is SocketException se && se.SocketErrorCode == SocketError.Interrupted))
        {
            LogOnMainThread($"TCP 監聽器錯誤: {ex.Message}", isError: true);
        }
        catch (Exception ex)
        {
            LogOnMainThread($"接受 TCP 客戶端錯誤: {ex.Message}", isError: true);
        }
    }

    private async Task ReceiveTcpMessages(PortData portData, TcpClient client, TCPData tcpData)
    {
        var buffer = new byte[2048];
        using NetworkStream stream = client.GetStream();
        IPEndPoint remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        string sourceIP = remoteEndPoint?.Address.ToString();
        int sourcePort = remoteEndPoint?.Port ?? 0;
        while (!tcpData.CancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, tcpData.CancellationTokenSource.Token);
                if (bytesRead > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    tcpData.SourceData = string.Empty;
                    tcpData.SourceData = message;
                    LogOnMainThread($"TCP 收到訊息來自: IP {sourceIP}, Port {sourcePort}, 訊息: {message}");
                    await ForwardMessageToClient(tcpData, portData);
                }
            }
            catch (IOException ex)
            {
                LogOnMainThread($"TCP 連接中斷: {ex.Message}");
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        client.Close();
    }
    private async Task ForwardMessageToClient(TCPData tcpData, PortData portData)
    {
        if (string.IsNullOrEmpty(portData.TargetIP)) return; // Ensure target IP is valid

        TcpClient tcpClient = null;

        try
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(portData.TargetIP, portData.RemotePortDetails.Port);

            if (!tcpClient.Connected)
            {
                LogOnMainThread("連接未成功，無法轉發資料。", isError: true);
                return;
            }

            using NetworkStream targetStream = tcpClient.GetStream();
            byte[] messageBytes = Encoding.UTF8.GetBytes(tcpData.SourceData);
            await targetStream.WriteAsync(messageBytes, 0, messageBytes.Length);
            LogOnMainThread($"轉發資料到 {portData.TargetIP}:{portData.RemotePortDetails.Port}: {tcpData.SourceData}");
        }
        catch (SocketException ex)
        {
            LogOnMainThread($"轉發資料到目標電腦失敗: {ex.Message}", isError: true);
        }
        catch (Exception ex)
        {
            LogOnMainThread($"轉發資料時發生錯誤: {ex.Message}", isError: true);
        }
        finally
        {
            tcpClient?.Close();
        }
    }

    #endregion

    #region UDP 方法

    /// <summary>
    /// 接收 UDP 訊息
    /// </summary>
    /// <param name="udpData">UDP 資料</param>
    /// <param name="port">端口</param>
    private async Task ReceiveUdpMessages(PortData portData, UdpData udpData)
    {
        using (var sendClient = new UdpClient())
        {
            IPEndPoint sendEndPoint = new(IPAddress.Loopback, portData.LocalPortDetails.Port);

            while (!udpData.CancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await udpData.UdpClient.ReceiveAsync().WithCancellation(udpData.CancellationTokenSource.Token);
                    string message = Encoding.UTF8.GetString(result.Buffer);
                    int messageLength = result.Buffer.Length;
                    portData.COMReceived += messageLength;
                    udpData.SourceData = message;
                    LogOnMainThread($"UDP 收到來自 {result.RemoteEndPoint}, 資料: {message}, 資料總大小: {portData.COMReceived}");          

                    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                    await sendClient.SendAsync(messageBytes, messageBytes.Length, sendEndPoint);
                    portData.NetReceived += messageBytes.Length;
                    LogOnMainThread($"訊息已傳送到本地端口 {portData.LocalPortDetails.Port}, 資料: {message}, 資料總大小: {portData.NetReceived}");
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        portData.OnUpdate?.Invoke(portData);
                    });
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 上的 UDP 客戶端已關閉。 {ex.Message}", isError: true);
                }
                catch (OperationCanceledException)
                {
                    LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 上的 UDP 接收操作被取消。");
                }
                catch (Exception ex)
                {
                    LogOnMainThread($"接收 UDP 訊息時發生錯誤: {ex.Message}", isError: true);
                }
            }
        }
        udpData.Dispose();
        LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 上的 UDP 接收器已經關閉。");
    }

    #endregion

    #region 獲取資料來源
    /// <summary>
    /// 通過你要監聽哪個port來獲取原始資料
    /// 參數說明：port為要監聽的port，length資料長度，connectionType選擇TCP、UDP
    /// </summary>
    /// <param name="port"></param>
    /// <param name="length"></param>
    /// <param name="connectionType"></param>
    /// <returns></returns>
    public string GetSourceData(int port, int length, ConnectionType connectionType)
    {
        if (length <= 0)
        {
            Debug.LogWarning("Invalid length parameter. Length must be greater than 0.");
            return string.Empty;
        }

        string sourceData = string.Empty;

        if (connectionType == ConnectionType.UDP && udpClients.TryGetValue(port, out UdpData udpData))
        {
            lock (udpData)
            {
                sourceData = udpData.SourceData;
                udpData.SourceData = string.Empty;
            }
        }
        else if (connectionType == ConnectionType.TCP && tcpClients.TryGetValue(port, out TCPData tcpData))
        {
            lock (tcpData)
            {
                sourceData = tcpData.SourceData;
                tcpData.SourceData = string.Empty;
            }
        }

        if (!string.IsNullOrEmpty(sourceData))
        {
            return sourceData.Length > length ? sourceData.Substring(0, length) : sourceData;
        }

        return string.Empty; 
    }
    #endregion

    #region 反初始化
    /// <summary>
    /// 當應用程序退出時關閉客戶端
    /// </summary>
    private async Task ShutdownClientsAsync()
    {
        var udpDisposalTasks = new List<Task>();
        var tcpDisposalTasks = new List<Task>();

        foreach (var udpData in udpClients.Values)
        {
            udpDisposalTasks.Add(Task.Run(() => udpData.Dispose()));
        }

        foreach (var tcpData in tcpClients.Values)
        {
            tcpDisposalTasks.Add(Task.Run(() => tcpData.Dispose()));
        }

        await Task.WhenAll(udpDisposalTasks);
        await Task.WhenAll(tcpDisposalTasks);

        udpClients.Clear();
        tcpClients.Clear();

        Debug.Log("All clients successfully shut down.");
    }
    #endregion

    #region 日誌封裝
    private void LogOnMainThread(string message, bool isError = false)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            if (isError)
            {
                Debug.LogError(message);
            }
            else
            {
                Debug.Log(message);
            }
            consoleUI.AddLog(message);
        });
    }
    #endregion

    #region 退出應用
    public async void DeInit()
    {
        await ShutdownClientsAsync();
    }
    #endregion
}

public static class TaskExtensions
{
    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<object>();
        using (cancellationToken.Register(s => ((TaskCompletionSource<object>)s).TrySetResult(null), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task)) throw new OperationCanceledException(cancellationToken);
        }
        return await task;
    }
}
