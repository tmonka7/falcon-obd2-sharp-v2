using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RedlineDiagnostics.Obd
{
    /// <summary>
    /// A byte pipe to an ELM327-compatible adapter. Commands are ASCII lines terminated by CR and every
    /// reply ends with the '&gt;' prompt character.
    /// </summary>
    public interface IObdTransport : IDisposable
    {
        string Name { get; }
        bool IsOpen { get; }
        void Open();
        void Close();
        /// <summary>Sends a command and returns the raw reply (without the trailing prompt).</summary>
        string SendCommand(string command, int timeoutMs);
    }

    public sealed class ObdTransportException : Exception
    {
        public ObdTransportException(string message) : base(message) { }
        public ObdTransportException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Shared read loop for stream-like transports.</summary>
    internal static class PromptReader
    {
        public static string ReadUntilPrompt(Func<int> readByte, int timeoutMs)
        {
            var sb = new StringBuilder();
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                int b;
                try { b = readByte(); }
                catch (TimeoutException) { continue; }
                if (b < 0)
                {
                    Thread.Sleep(2);
                    continue;
                }
                if (b == '>') return sb.ToString();
                if (b == 0) continue;
                sb.Append((char)b);
            }
            if (sb.Length == 0) throw new ObdTransportException("Timeout waiting for adapter response.");
            return sb.ToString();
        }
    }

    /// <summary>USB or Bluetooth SPP adapters exposed as a COM port.</summary>
    public sealed class SerialTransport : IObdTransport
    {
        private readonly string _port;
        private readonly int _baud;
        private SerialPort _sp;

        public SerialTransport(string port, int baud)
        {
            _port = port;
            _baud = baud;
        }

        public string Name => _port;
        public bool IsOpen => _sp != null && _sp.IsOpen;

        public void Open()
        {
            if (string.IsNullOrWhiteSpace(_port)) throw new ObdTransportException("No serial port configured.");
            try
            {
                _sp = new SerialPort(_port, _baud, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 200,
                    WriteTimeout = 1000,
                    NewLine = "\r",
                    DtrEnable = true,
                    RtsEnable = true,
                    Handshake = Handshake.None
                };
                _sp.Open();
                _sp.DiscardInBuffer();
            }
            catch (Exception ex) when (!(ex is ObdTransportException))
            {
                throw new ObdTransportException("Cannot open " + _port + ": " + ex.Message, ex);
            }
        }

        public void Close()
        {
            try { _sp?.Close(); } catch { }
            _sp = null;
        }

        public string SendCommand(string command, int timeoutMs)
        {
            if (!IsOpen) throw new ObdTransportException("Serial port is not open.");
            try
            {
                _sp.DiscardInBuffer();
                _sp.Write(command + "\r");
            }
            catch (Exception ex)
            {
                throw new ObdTransportException("Write failed: " + ex.Message, ex);
            }
            return PromptReader.ReadUntilPrompt(() =>
            {
                try { return _sp.ReadByte(); }
                catch (TimeoutException) { return -1; }
            }, timeoutMs);
        }

        public void Dispose() => Close();

        public static string[] AvailablePorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames();
                Array.Sort(ports, (a, b) =>
                {
                    int na, nb;
                    bool ia = int.TryParse(a.Replace("COM", ""), out na);
                    bool ib = int.TryParse(b.Replace("COM", ""), out nb);
                    return ia && ib ? na.CompareTo(nb) : string.CompareOrdinal(a, b);
                });
                return ports;
            }
            catch { return new string[0]; }
        }
    }

    /// <summary>WiFi adapters (default 192.168.0.10:35000).</summary>
    public sealed class TcpTransport : IObdTransport
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;

        public TcpTransport(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public string Name => _host + ":" + _port;
        public bool IsOpen => _client != null && _client.Connected;

        public void Open()
        {
            try
            {
                _client = new TcpClient();
                var ar = _client.BeginConnect(_host, _port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(4000)) throw new ObdTransportException("Connection to " + Name + " timed out.");
                _client.EndConnect(ar);
                _client.NoDelay = true;
                _stream = _client.GetStream();
                _stream.ReadTimeout = 200;
            }
            catch (Exception ex) when (!(ex is ObdTransportException))
            {
                throw new ObdTransportException("Cannot connect to " + Name + ": " + ex.Message, ex);
            }
        }

        public void Close()
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
        }

        public string SendCommand(string command, int timeoutMs)
        {
            if (!IsOpen) throw new ObdTransportException("Not connected.");
            try
            {
                while (_stream.DataAvailable) _stream.ReadByte();
                var bytes = Encoding.ASCII.GetBytes(command + "\r");
                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush();
            }
            catch (Exception ex)
            {
                throw new ObdTransportException("Write failed: " + ex.Message, ex);
            }
            return PromptReader.ReadUntilPrompt(() =>
            {
                try
                {
                    if (!_stream.DataAvailable) { Thread.Sleep(5); return -1; }
                    return _stream.ReadByte();
                }
                catch (IOException) { return -1; }
            }, timeoutMs);
        }

        public void Dispose() => Close();
    }
}
