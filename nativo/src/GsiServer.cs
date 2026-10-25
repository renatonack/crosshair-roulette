using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CrosshairRoulette
{
    // A tiny HTTP endpoint on 127.0.0.1 that CS2 POSTs its game state to.
    // Payloads without our token are dropped. Payload fires on a worker thread.
    sealed class GsiServer : IDisposable
    {
        public event Action<Dictionary<string, object>> Payload;
        public string Error { get; private set; }

        TcpListener listener;
        volatile bool running;
        string token;

        public bool Start(int port, string authToken)
        {
            token = authToken;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
            }
            catch (SocketException e)
            {
                Error = e.SocketErrorCode == SocketError.AddressAlreadyInUse
                    ? $"A porta {port} já está em uso por outro programa."
                    : e.Message;
                return false;
            }
            running = true;
            new Thread(Loop) { IsBackground = true, Name = "GSI" }.Start();
            return true;
        }

        void Loop()
        {
            while (running)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { if (!running) return; continue; }
                ThreadPool.QueueUserWorkItem(_ => Handle(client));
            }
        }

        static int IndexOf(byte[] buf, int len, byte[] pattern)
        {
            for (int i = 0; i + pattern.Length <= len; i++)
            {
                int j = 0;
                while (j < pattern.Length && buf[i + j] == pattern[j]) j++;
                if (j == pattern.Length) return i;
            }
            return -1;
        }

        static readonly byte[] HeaderEnd = Encoding.ASCII.GetBytes("\r\n\r\n");
        static readonly byte[] Ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");

        void Handle(TcpClient client)
        {
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = 3000;
                    var stream = client.GetStream();
                    var buf = new byte[16384];
                    var data = new MemoryStream();
                    int headerEnd = -1;
                    while (headerEnd < 0)
                    {
                        int n = stream.Read(buf, 0, buf.Length);
                        if (n <= 0 || data.Length > (1 << 20)) return;
                        data.Write(buf, 0, n);
                        headerEnd = IndexOf(data.GetBuffer(), (int)data.Length, HeaderEnd);
                    }
                    string head = Encoding.ASCII.GetString(data.GetBuffer(), 0, headerEnd);
                    var m = Regex.Match(head, @"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase);
                    int length = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                    int bodyStart = headerEnd + 4;
                    while (data.Length - bodyStart < length && data.Length < (4 << 20))
                    {
                        int n = stream.Read(buf, 0, buf.Length);
                        if (n <= 0) break;
                        data.Write(buf, 0, n);
                    }
                    // CS2 only needs a 2xx back; answer before doing any work
                    stream.Write(Ok, 0, Ok.Length);
                    if (!head.StartsWith("POST")) return;

                    int bodyLen = (int)Math.Min(length, data.Length - bodyStart);
                    var json = Json.Parse(Encoding.UTF8.GetString(data.GetBuffer(), bodyStart, bodyLen));
                    if (json == null) return;
                    if (!string.IsNullOrEmpty(token) && Json.Str(Json.Get(json, "auth"), "token", null) != token) return;
                    Payload?.Invoke(json);
                }
            }
            catch
            {
                /* a broken request never takes the server down */
            }
        }

        public void Dispose()
        {
            running = false;
            try { listener?.Stop(); } catch { }
        }
    }
}
