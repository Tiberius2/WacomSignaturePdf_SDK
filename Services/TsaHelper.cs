using System;
using System.Collections.Generic;
using System.Net.Http;

namespace WacomSignaturePdf.Services
{
    // RFC 3161 TSA (Time Stamp Authority) communication.
    // A missing timestamp is non-fatal — callers fall back to local time.
    internal static class TsaHelper
    {
        private const string TsaUrl = "https://freetsa.org/tsr";

        public static void TryGetTimestamp(string docHash, out byte[] tsaResponse, out DateTime? trustedAt)
        {
            tsaResponse = null;
            trustedAt = null;
            try
            {
                tsaResponse = RequestTimestamp(docHash);
                trustedAt = DateTime.UtcNow.ToLocalTime();
            }
            catch { }
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private static byte[] RequestTimestamp(string docHash)
        {
            byte[] tsq = BuildRequest(HexToBytes(docHash));

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
            {
                var content = new ByteArrayContent(tsq);
                content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/timestamp-query");

                var response = client.PostAsync(TsaUrl, content).Result;
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsByteArrayAsync().Result;
            }
        }

        private static byte[] BuildRequest(byte[] sha256Hash)
        {
            byte[] sha256Oid = { 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01,
                                 0x65, 0x03, 0x04, 0x02, 0x01, 0x05, 0x00 };
            byte[] hashOctet = Concat(new byte[] { 0x04, (byte)sha256Hash.Length }, sha256Hash);
            byte[] msgImp = WrapSeq(Concat(sha256Oid, hashOctet));
            byte[] version = { 0x02, 0x01, 0x01 };
            byte[] certReq = { 0x01, 0x01, 0xff };
            return WrapSeq(Concat(Concat(version, msgImp), certReq));
        }

        private static byte[] WrapSeq(byte[] content)
        {
            var r = new List<byte> { 0x30 };
            if (content.Length < 128)
            {
                r.Add((byte)content.Length);
            }
            else
            {
                var lb = new List<byte>();
                int n = content.Length;
                while (n > 0) { lb.Insert(0, (byte)(n & 0xff)); n >>= 8; }
                r.Add((byte)(0x80 | lb.Count));
                r.AddRange(lb);
            }
            r.AddRange(content);
            return r.ToArray();
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            var r = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, r, 0, a.Length);
            Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
            return r;
        }

        private static byte[] HexToBytes(string hex)
        {
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return b;
        }
    }
}
