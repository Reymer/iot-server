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

    #region 資料結構 TCP Client、TCP Server、UDP
    public class UdpData : IDisposable
    {
        public UdpClient udpClient;
        public CancellationTokenSource CancellationTokenSource = new();
        public string SourceData = string.Empty;
        private bool disposed = false;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CancellationTokenSource.Cancel();
            udpClient?.Close();
            udpClient?.Dispose();
            CancellationTokenSource.Dispose();
        }
    }

    public class TCPServerData : IDisposable
    {
        public TcpListener TcpListener;
        public CancellationTokenSource CancellationTokenSource = new();
        public string SourceData = string.Empty;
        private bool disposed = false;
        private readonly object lockObj = new();

        public void Dispose()
        {
            lock (lockObj)
            {
                if (disposed) return;
                disposed = true;

                if (CancellationTokenSource != null && !CancellationTokenSource.IsCancellationRequested)
                {
                    CancellationTokenSource.Cancel();
                    CancellationTokenSource.Dispose();
                }

                TcpListener?.Stop();
                TcpListener?.Server?.Dispose();
            }
        }
    }

    public class TCPClinetData : IDisposable
    {
        public TcpClient tcpClient;
        public CancellationTokenSource CancellationTokenSource = new();
        private bool disposed = false;
        private readonly object lockObj = new();
        public string remotePort = string.Empty;
        public string remoteIP = string.Empty;
        public bool IsStopped { get; set; } = false;
        public void Dispose()
        {
            lock (lockObj)
            {
                if (disposed) return;
                disposed = true;

                if (CancellationTokenSource != null && !CancellationTokenSource.IsCancellationRequested)
                {
                    CancellationTokenSource.Cancel();
                    CancellationTokenSource.Dispose();
                }
                tcpClient.Close();
                tcpClient.Dispose();
            }
        }
    }
    public void Init(ConsoleUI consoleUI)
    {
        this.consoleUI = consoleUI;
    }   

    #endregion

    #region 資料字典存取
    public enum ConnectionType { UDP, TCP }
    private readonly ConcurrentDictionary<string, UdpData> udpClients = new();
    private readonly ConcurrentDictionary<string, TCPServerData> tcpServerdatas = new();
    private readonly ConcurrentDictionary<string, TCPClinetData> tcpClientdatas = new();

    #endregion

    #region 單獨新增 TCP 或 UDP 監聽器
    /// <summary>
    /// 單獨新增 TCP 或 UDP 監聽器
    /// </summary>
    /// <param name="remotePort">要新增的端口</param>
    /// <param name="connectionType">連接類型 (TCP 或 UDP)</param>
    public void AddPort(PortData portData)
    {
        switch (portData.NetProtocol.ToUpperInvariant())
        {
            case "UDP":
                AddUdpClient(portData);
                break;
            case "TCP CLIENT":
                AddTcpClient(portData);
                break;
            case "TCP SERVER":
                AddTcpListener(portData);
                break;
            default:
                LogOnMainThread($"無法識別的連接類型: {portData.NetProtocol}", isError: true);
                break;
        }
    }

    private async void AddTcpClient(PortData portData)
    {
        if (tcpClientdatas.TryGetValue(portData.RemotePortDetails.Port, out var tcpClientData))
        {
            // 如果TCP客户端已经存在且已连接，直接返回
            if (tcpClientData.tcpClient != null && tcpClientData.tcpClient.Connected)
            {
                LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 的 TCP 客戶端已經連接。");
                return;
            }
            else
            {
                LogOnMainThread($"端口 {portData.RemotePortDetails.Port} 的 TCP 客戶端尚未連接或已斷開，正在重新連接...");
            }
        }
        else
        {
            // 创建新的TCP客户端数据
            tcpClientData = new TCPClinetData
            {
                remoteIP = portData.TargetIP,
                remotePort = portData.RemotePortDetails.Port,
                CancellationTokenSource = new CancellationTokenSource()  // 支持取消操作
            };
            tcpClientdatas.TryAdd(portData.RemotePortDetails.Port, tcpClientData);
            LogOnMainThread($"在端口 {portData.RemotePortDetails.Port} 上啟動了新的 TCP 客戶端。");
        }

        try
        {
            var tcpClient = new TcpClient();

            // 检查是否请求了取消操作
            var token = tcpClientData.CancellationTokenSource.Token;
            if (token.IsCancellationRequested)
            {
                LogOnMainThread($"TCP 客戶端連接已被取消，端口 {portData.RemotePortDetails.Port}");
                return;
            }

            // 异步连接，支持取消
            var connectTask = tcpClient.ConnectAsync(portData.TargetIP, int.Parse(portData.RemotePortDetails.Port));

            // 等待连接并支持取消
            await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, token));

            // 如果任务因为取消而结束
            if (token.IsCancellationRequested)
            {
                LogOnMainThread($"TCP 客戶端已連接但立即取消，端口 {portData.RemotePortDetails.Port}");
                tcpClient.Close();  // 关闭连接
                return;
            }

            // 如果连接任务因失败抛出异常
            if (connectTask.IsFaulted)
            {
                throw connectTask.Exception ?? new Exception("Unknown connection failure.");
            }

            // 连接成功
            tcpClientData.tcpClient = tcpClient;
            LogOnMainThread($"已連接到 TCP 客戶端: {portData.TargetIP}:{portData.RemotePortDetails.Port}");
        }
        catch (OperationCanceledException)
        {
            LogOnMainThread($"TCP 客戶端連接被取消，端口 {portData.RemotePortDetails.Port}");
        }
        catch (Exception ex)
        {
            // 捕获异常，报告连接失败
            LogOnMainThread($"TCP 客戶端 {portData.TargetIP}:{portData.RemotePortDetails.Port} 連接失敗: {ex.Message}。");
            LogOnMainThread($"TCP 客戶端 {portData.TargetIP}:{portData.RemotePortDetails.Port} 無法連接。請稍後重試。");
        }
    }

    /// <summary>
    /// 新增單個 TCP 監聽器
    /// </summary>
    /// <param name="remotePort">要新增的端口</param>
    private void AddTcpListener(PortData portData)
    {
        if (tcpServerdatas.ContainsKey(portData.LocalPortDetails.Port))
        {
            LogOnMainThread($"端口 {portData.LocalPortDetails.Port} 已經存在 TCP 監聽器。");
            return;
        }
        try
        {
            var tcpListener = new TcpListener(IPAddress.Any, int.Parse(portData.LocalPortDetails.Port));
            tcpListener.Start();
            var tcpServerData = new TCPServerData
            {
                TcpListener = tcpListener,
                CancellationTokenSource = new CancellationTokenSource()
            };
            if (tcpServerdatas.TryAdd(portData.LocalPortDetails.Port, tcpServerData))
            {
                LogOnMainThread($"在端口 {portData.LocalPortDetails.Port} 上啟動了 TCP 伺服器端。");
                Task.Run(() => ListenForTcpClients(portData, tcpServerData));
            }
            else
            {
                tcpListener.Stop();
                LogOnMainThread($"端口 {portData.LocalPortDetails.Port} 上的 TCP 監聽器已經存在。");
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            LogOnMainThread($"端口 {portData.LocalPortDetails.Port} 已經被使用。", isError: true);
        }
        catch (Exception ex)
        {
            LogOnMainThread($"初始化端口 {portData.LocalPortDetails.Port} 的 TCP 監聽器時發生錯誤: {ex.Message}", isError: true);
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
                var udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, int.Parse(portData.RemotePortDetails.Port)));
                UdpData udpData = new() { udpClient = udpClient };

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
    public void StopClient(string port, string connectionType)
    {
        try
        {
            if (connectionType.Equals("UDP", StringComparison.OrdinalIgnoreCase))
            {
                DisposeClientResources(port, udpClients, "UDP");
            }
            else if (connectionType.Equals("TCP Server", StringComparison.OrdinalIgnoreCase))
            {
                DisposeClientResources(port, tcpServerdatas, "TCP");
            }
            else if (connectionType.Equals("TCP Client", StringComparison.OrdinalIgnoreCase))
            {
                if (tcpClientdatas.TryGetValue(port, out TCPClinetData tcpClientData))
                {
                    tcpClientData.IsStopped = true;
                }

                DisposeClientResources(port, tcpClientdatas, "TCP");
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"停止客戶端時出現錯誤: {ex.Message}");
        }
    }
    private void DisposeClientResources<T>(string port, ConcurrentDictionary<string, T> clientDictionary, string protocol) where T : IDisposable
    {
        if (clientDictionary.TryRemove(port, out T clientData))
        {
            if (clientData is UdpData udpData)
            {
                udpData.CancellationTokenSource.Cancel();
                LogOnMainThread($"已刪除 UDP 端口 {port} 上的 {protocol}。");
            }
            else if (clientData is TCPServerData tcpServerData)
            {
                tcpServerData.CancellationTokenSource.Cancel();
                LogOnMainThread($"已刪除 TCP 伺服器端口 {port} 上的 {protocol}。");
            }
            else if (clientData is TCPClinetData tcpClientData)
            {
                tcpClientData.CancellationTokenSource.Cancel();
                LogOnMainThread($"已刪除 TCP 客戶端端口 {port} 上的 {protocol}。");
            }

            clientData.Dispose();
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
    /// <param name="tcpServerData">TCP 資料</param>
    /// <param name="remotePort">端口</param>
    private async Task ListenForTcpClients(PortData portData, TCPServerData tcpServerData)
    {
        try
        {
            while (!tcpServerData.CancellationTokenSource.Token.IsCancellationRequested)
            {
                await HandleTcpClientConnection(portData, tcpServerData);
            }
        }
        catch (OperationCanceledException)
        {
            LogOnMainThread("TCP 監聽器已取消。");
        }
        catch (ObjectDisposedException)
        {
            LogOnMainThread("TCP 監聽器已被處置。");
        }
        catch (Exception ex)
        {
            LogOnMainThread($"TCP 監聽器遇到錯誤: {ex.Message}", isError: true);
        }
        finally
        {
            lock (tcpServerData)
            {
                if (tcpServerData != null && !tcpServerData.CancellationTokenSource.IsCancellationRequested)
                {
                    tcpServerData.CancellationTokenSource.Cancel();
                }
                tcpServerData.Dispose();
            }
        }
    }
    private async Task HandleTcpClientConnection(PortData portData, TCPServerData tcpData)
    {
        try
        {
            tcpData.CancellationTokenSource.Token.ThrowIfCancellationRequested();
            var client = await tcpData.TcpListener.AcceptTcpClientAsync().WithCancellation(tcpData.CancellationTokenSource.Token);
            await ReceiveTcpMessages(portData, client, tcpData);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("TCP 監聽器已取消");
        }
        catch (ObjectDisposedException)
        {
            Debug.Log("TCP 監聽器已被釋放");
        }
        catch (SocketException se) when (se.SocketErrorCode == SocketError.Interrupted)
        {
            Debug.Log($"Socket 中斷: {se.Message}");
        }
        catch (Exception ex)
        {
            Debug.Log($"接受 TCP 客戶端錯誤: {ex.Message}");
        }
    }


    private async Task ReceiveTcpMessages(PortData portData, TcpClient client, TCPServerData tcpServerData)
    {
        IPEndPoint remoteEndPoint = client.Client.LocalEndPoint as IPEndPoint;
        string sourceIP = remoteEndPoint?.Address.ToString();
        int sourcePort = remoteEndPoint?.Port ?? 0;

        try
        {
            var buffer = new byte[2048];
            using NetworkStream stream = client.GetStream();

            while (!tcpServerData.CancellationTokenSource.Token.IsCancellationRequested)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, tcpServerData.CancellationTokenSource.Token);
                if (bytesRead > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    tcpServerData.SourceData = message;
                    LogOnMainThread($"TCP 收到訊息來自: IP {sourceIP}, Port {sourcePort}, 訊息: {message}");
                    if (tcpClientdatas.Count > 0)
                    {
                        SendMessageToAllClients(portData, tcpServerData);
                    }
                    else
                    {
                        LogOnMainThread("沒有可用的 TCP 客戶端，無法發送訊息。");
                    }
                }
            }
        }
        catch (IOException ex)
        {
            LogOnMainThread($"接收 TCP 訊息時出現錯誤: {ex.Message}", isError: true);
        }
        finally
        {
            // Ensure the client is closed when done
            client.Close();
            LogOnMainThread($"TCP 客戶端已關閉: IP {sourceIP}, Port {sourcePort}");
        }
    }

    private void SendMessageToAllClients(PortData portData, TCPServerData tcpServerData)
    {
        if (!tcpClientdatas.ContainsKey(portData.LocalPortDetails.Port))
        {
            Debug.Log($"沒有可用的 TCP 客戶端!");
            return;
        }

        var tcpClient = tcpClientdatas[portData.LocalPortDetails.Port];
        if (tcpClient.tcpClient.Connected)
        {
            try
            {
                var buffer = Encoding.UTF8.GetBytes(tcpServerData.SourceData);
                NetworkStream stream = tcpClient.tcpClient.GetStream();
                stream.Write(buffer, 0, buffer.Length);
                LogOnMainThread($"訊息已傳送到客戶端: {tcpClient.tcpClient.Client.RemoteEndPoint}");
            }
            catch (Exception ex)
            {
                LogOnMainThread($"發送訊息到 TCP 客戶端時出現錯誤: {ex.Message}", isError: true);
            }
        }
        else
        {
            LogOnMainThread($"TCP 客戶端已斷開連接: {tcpClient.tcpClient.Client.RemoteEndPoint}", isError: true);
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
            IPEndPoint sendEndPoint = new(IPAddress.Loopback, int.Parse(portData.LocalPortDetails.Port));

            while (!udpData.CancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await udpData.udpClient.ReceiveAsync().WithCancellation(udpData.CancellationTokenSource.Token);
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
    public string GetSourceData(string port, int length, ConnectionType connectionType)
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
        else if (connectionType == ConnectionType.TCP && tcpServerdatas.TryGetValue(port, out TCPServerData tcpData))
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
        var tcpServerDisposalTasks = new List<Task>();
        var tcpClientDisposalTasks = new List<Task>();

        foreach (var udpData in udpClients.Values)
        {
            udpDisposalTasks.Add(Task.Run(() => udpData.Dispose()));
        }

        foreach (var tcpServerData in tcpServerdatas.Values)
        {
            tcpServerDisposalTasks.Add(Task.Run(() => tcpServerData.Dispose()));
        }

        foreach(var tcpClientData in tcpClientdatas.Values)
        {
            tcpClientDisposalTasks.Add(Task.Run(() => tcpClientData.Dispose()));
        }

        await Task.WhenAll(udpDisposalTasks);
        await Task.WhenAll(tcpServerDisposalTasks);
        await Task.WhenAll(tcpClientDisposalTasks);

        udpClients.Clear();
        tcpServerdatas.Clear();
        tcpClientdatas.Clear();

        Debug.Log("All clients successfully shut down.");
    }
    #endregion

    #region 日誌封裝
    private void LogOnMainThread(string message, bool isError = false)
    {
        string formattedMessage = FormatLogMessage(message, isError);

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            if (isError)
            {
                Debug.LogError(formattedMessage);
            }
            else
            {
                Debug.Log(formattedMessage);
            }
            consoleUI.AddLog(formattedMessage);
        });
    }

    /// <summary>
    /// 格式化日誌訊息
    /// </summary>
    /// <param name="message">原始訊息</param>
    /// <param name="isError">是否為錯誤訊息</param>
    /// <returns>格式化後的訊息</returns>
    private string FormatLogMessage(string message, bool isError)
    {
        string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string errorLabel = isError ? "[Error]" : "[Info]";
        return $"[{timeStamp}] {errorLabel} {message}";
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
