using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RedlineDiagnostics.Obd
{
    /// <summary>The reassembled payload from one ECU (starts with the response service byte, e.g. 0x41).</summary>
    public sealed class EcuReply
    {
        public int EcuId;
        public List<byte> Data = new List<byte>();
        public int ExpectedLength = -1;
        public bool IsNegative => Data.Count > 0 && Data[0] == 0x7F;
        public byte NegativeCode => Data.Count >= 3 ? Data[2] : (byte)0;
    }

    /// <summary>Parsed reply of one request, possibly from several ECUs (functional addressing).</summary>
    public sealed class ObdResponse
    {
        public string Raw = "";
        public bool NoData;
        public bool Error;
        public string ErrorText = "";
        public readonly List<EcuReply> Replies = new List<EcuReply>();

        public bool Ok => !Error && !NoData && Replies.Count > 0;
        public EcuReply First => Replies.Count > 0 ? Replies[0] : null;

        /// <summary>Returns the data bytes following the positive response header, or null.</summary>
        public byte[] Payload(byte expectedService, int headerBytesAfterService)
        {
            foreach (var r in Replies)
            {
                if (r.Data.Count == 0 || r.IsNegative) continue;
                if (r.Data[0] != expectedService) continue;
                int skip = 1 + headerBytesAfterService;
                if (r.Data.Count < skip) return new byte[0];
                return r.Data.Skip(skip).ToArray();
            }
            return null;
        }

        public bool AnyPositive => Replies.Any(r => r.Data.Count > 0 && !r.IsNegative);
        public bool AnyNegative => Replies.Any(r => r.IsNegative);

        // ------------------------------------------------------------------ parsing

        /// <summary>Parses raw ELM327 text (headers on, spaces off) into per-ECU payloads.</summary>
        public static ObdResponse Parse(string raw, ObdProtocol protocol, bool headersOn)
        {
            var resp = new ObdResponse { Raw = raw ?? "" };
            var lines = (raw ?? "").Replace("\n", "\r").Split(new[] { '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

            var frames = new Dictionary<int, EcuReply>();
            var order = new List<int>();

            foreach (var line in lines)
            {
                var up = line.ToUpperInvariant();
                if (up.StartsWith("SEARCHING") || up.StartsWith("BUS INIT") || up == "OK") continue;
                if (up.Contains("NO DATA")) { resp.NoData = true; continue; }
                if (up.Contains("UNABLE TO CONNECT") || up.Contains("CAN ERROR") || up.Contains("BUS ERROR") ||
                    up.Contains("BUS BUSY") || up.Contains("DATA ERROR") || up.Contains("FB ERROR") || up.Contains("LV RESET") ||
                    up.Contains("STOPPED") || up == "?" || up.Contains("ERR"))
                {
                    resp.Error = true;
                    resp.ErrorText = line;
                    continue;
                }
                var hex = up.Replace(" ", "");
                if (hex.Length == 0 || !hex.All(IsHex)) continue;

                int ecuId = 0;
                string dataHex;
                if (!headersOn)
                {
                    // "0: 49 02 01 .." lines or plain data
                    int colon = line.IndexOf(':');
                    if (colon >= 0 && colon <= 2) dataHex = up.Substring(colon + 1).Replace(" ", "");
                    else dataHex = hex;
                    Append(frames, order, 0, HexToBytes(dataHex), -1);
                    continue;
                }

                bool can29 = ObdProtocolInfo.IsCan29(protocol);
                bool can11 = ObdProtocolInfo.IsCan(protocol) && !can29;
                if (protocol == ObdProtocol.Auto || protocol == ObdProtocol.Unknown)
                {
                    // heuristic: 11-bit CAN frames have an odd number of hex characters (3 id chars + bytes)
                    if (hex.Length % 2 == 1) can11 = true;
                    else if (hex.Length >= 18 && (hex.StartsWith("18DA") || hex.StartsWith("18DB"))) can29 = true;
                }

                if (can11 || can29)
                {
                    int idLen = can11 ? 3 : 8;
                    if (hex.Length < idLen + 2) continue;
                    ecuId = int.Parse(hex.Substring(0, idLen), NumberStyles.HexNumber);
                    var bytes = HexToBytes(hex.Substring(idLen));
                    if (bytes.Length == 0) continue;
                    // Strip a possible trailing DLC byte? ELM does not add one. ISO-TP PCI handling:
                    int pci = bytes[0] >> 4;
                    if (pci == 0)
                    {
                        int len = bytes[0] & 0x0F;
                        var data = bytes.Skip(1).Take(len).ToArray();
                        Append(frames, order, ecuId, data, -1);
                    }
                    else if (pci == 1)
                    {
                        if (bytes.Length < 2) continue;
                        int len = ((bytes[0] & 0x0F) << 8) | bytes[1];
                        var data = bytes.Skip(2).ToArray();
                        Append(frames, order, ecuId, data, len);
                    }
                    else if (pci == 2)
                    {
                        var data = bytes.Skip(1).ToArray();
                        Append(frames, order, ecuId, data, -1);
                    }
                    else
                    {
                        // flow control or unknown, ignore
                    }
                }
                else
                {
                    // Legacy: 3 header bytes, data, 1 checksum byte
                    var bytes = HexToBytes(hex);
                    if (bytes.Length < 5) continue;
                    ecuId = bytes[2];
                    var data = bytes.Skip(3).Take(bytes.Length - 4).ToArray();
                    Append(frames, order, ecuId, data, -1);
                }
            }

            foreach (var id in order)
            {
                var r = frames[id];
                if (r.ExpectedLength > 0 && r.Data.Count > r.ExpectedLength)
                    r.Data.RemoveRange(r.ExpectedLength, r.Data.Count - r.ExpectedLength);
                resp.Replies.Add(r);
            }
            if (resp.Replies.Count == 0 && !resp.Error) resp.NoData = true;
            return resp;
        }

        private static void Append(Dictionary<int, EcuReply> frames, List<int> order, int ecuId, byte[] data, int expected)
        {
            EcuReply r;
            if (!frames.TryGetValue(ecuId, out r))
            {
                r = new EcuReply { EcuId = ecuId };
                frames[ecuId] = r;
                order.Add(ecuId);
            }
            if (expected > 0) r.ExpectedLength = expected;
            r.Data.AddRange(data);
        }

        private static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');

        public static byte[] HexToBytes(string hex)
        {
            if (hex.Length % 2 == 1) hex = hex.Substring(0, hex.Length - 1);
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber);
            return b;
        }

        public static string BytesToHex(IEnumerable<byte> bytes, string sep = " ")
        {
            return string.Join(sep, bytes.Select(b => b.ToString("X2")));
        }
    }

    public enum DtcState { Stored, Pending, Permanent }

    public sealed class DtcRecord
    {
        public string Code;
        public DtcState State;
        public string ModuleId;

        public override string ToString() => Code + " (" + State + ")";
    }

    /// <summary>Decodes DTC bytes from SAE J1979 (modes 03/07/0A) and UDS (service 19 02).</summary>
    public static class DtcParser
    {
        public static string DecodePair(byte a, byte b)
        {
            char system;
            switch ((a >> 6) & 0x03)
            {
                case 0: system = 'P'; break;
                case 1: system = 'C'; break;
                case 2: system = 'B'; break;
                default: system = 'U'; break;
            }
            var sb = new StringBuilder();
            sb.Append(system);
            sb.Append(((a >> 4) & 0x03).ToString());
            sb.Append((a & 0x0F).ToString("X"));
            sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        /// <summary>Payload after the 0x43/0x47/0x4A service byte.</summary>
        public static List<string> ParseJ1979(byte[] payload)
        {
            var list = new List<string>();
            if (payload == null || payload.Length == 0) return list;
            int start = 0;
            // On CAN the first byte is the DTC count; on legacy protocols pairs follow directly.
            if (payload.Length % 2 == 1) start = 1;
            for (int i = start; i + 1 < payload.Length; i += 2)
            {
                if (payload[i] == 0 && payload[i + 1] == 0) continue;
                var code = DecodePair(payload[i], payload[i + 1]);
                if (!list.Contains(code)) list.Add(code);
            }
            return list;
        }

        /// <summary>Payload after 0x59 0x02: availability mask then 4-byte records (3 DTC bytes + status).</summary>
        public static List<DtcRecord> ParseUds(byte[] payload, string moduleId)
        {
            var list = new List<DtcRecord>();
            if (payload == null || payload.Length < 5) return list;
            for (int i = 1; i + 3 < payload.Length; i += 4)
            {
                byte status = payload[i + 3];
                bool confirmed = (status & 0x08) != 0;
                bool pending = (status & 0x04) != 0;
                bool testFailed = (status & 0x01) != 0;
                if (!confirmed && !pending && !testFailed) continue;
                var code = DecodePair(payload[i], payload[i + 1]);
                list.Add(new DtcRecord
                {
                    Code = code,
                    State = confirmed || testFailed ? DtcState.Stored : DtcState.Pending,
                    ModuleId = moduleId
                });
            }
            return list;
        }
    }
}
