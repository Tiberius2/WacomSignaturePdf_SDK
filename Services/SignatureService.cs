using FlSigCaptLib;
using FLSIGCTLLib;
using Newtonsoft.Json;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using WacomSignaturePdf.Models;

namespace WacomSignaturePdf.Services
{
    public class SignatureService : IDisposable
    {
        private readonly string _pdfPath;
        private readonly string _artifactsRootDir;
        private readonly string _tempDir;
        private readonly List<SignatureSlot> _allSlots;
        private readonly List<SignaturePlacement> _placements = new List<SignaturePlacement>();

        /// <summary>
        /// Hash of the original clean PDF — computed from the backup copy in
        /// "Originally Generated Documents", which is never modified.
        /// All signature metadata files reference this same hash.
        /// </summary>
        private readonly string _originalDocHash;

        /// <summary>Absolute path to the untouched backup copy.</summary>
        private readonly string _originalBackupPath;

        private int _writtenCount = 0;

        private const string TsaUrl = "https://freetsa.org/tsr";
        private const int CompositeWidth = 800;
        private const int CompositeHeight = 480;
        private const string StateFileName = "signing-state.json";
        private const string OriginalsFolder = "Originally Generated Documents";

        private const string WacomLicence = "eyJhbGciOiJSUzUxMiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiI3YmM5Y2IxYWIxMGE0NmUxODI2N2E5MTJkYTA2ZTI3NiIsImV4cCI6MjE0NzQ4MzY0NywiaWF0IjoxNTYwOTUwMjcyLCJyaWdodHMiOlsiU0lHX1NES19DT1JFIiwiU0lHQ0FQVFhfQUNDRVNTIl0sImRldmljZXMiOlsiV0FDT01fQU5ZIl0sInR5cGUiOiJwcm9kIiwibGljX25hbWUiOiJTaWduYXR1cmUgU0RLIiwid2Fjb21faWQiOiI3YmM5Y2IxYWIxMGE0NmUxODI2N2E5MTJkYTA2ZTI3NiIsImxpY191aWQiOiJiODUyM2ViYi0xOGI3LTQ3OGEtYTlkZS04NDlmZTIyNmIwMDIiLCJhcHBzX3dpbmRvd3MiOltdLCJhcHBzX2lvcyI6W10sImFwcHNfYW5kcm9pZCI6W10sIm1hY2hpbmVfaWRzIjpbXX0.ONy3iYQ7lC6rQhou7rz4iJT_OJ20087gWz7GtCgYX3uNtKjmnEaNuP3QkjgxOK_vgOrTdwzD-nm-ysiTDs2GcPlOdUPErSp_bcX8kFBZVmGLyJtmeInAW6HuSp2-57ngoGFivTH_l1kkQ1KMvzDKHJbRglsPpd4nVHhx9WkvqczXyogldygvl0LRidyPOsS5H2GYmaPiyIp9In6meqeNQ1n9zkxSHo7B11mp_WXJXl0k1pek7py8XYCedCNW5qnLi4UCNlfTd6Mk9qz31arsiWsesPeR9PN121LBJtiPi023yQU8mgb9piw_a-ccciviJuNsEuRDN3sGnqONG3dMSA";
        
        // ── Constructor ───────────────────────────────────────────────────────────

        public SignatureService(string pdfPath, string artifactsRootDir, List<SignatureSlot> allSlots)
        {
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("PDF not found.", pdfPath);

            _pdfPath = pdfPath;
            _artifactsRootDir = artifactsRootDir;
            _allSlots = allSlots ?? throw new ArgumentNullException(nameof(allSlots));
            _tempDir = Path.Combine(Path.GetTempPath(), "WacomSig_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_tempDir);
            Directory.CreateDirectory(_artifactsRootDir);

            // ── Original backup ───────────────────────────────────────────────────
            // The backup lives in "Originally Generated Documents" next to the PDF.
            // Created once on the first machine — subsequent machines just read the
            // hash from the existing backup (or from the embedded signing state).
            string originalsDir = Path.Combine(Path.GetDirectoryName(pdfPath), OriginalsFolder);
            string backupPath = Path.Combine(originalsDir, Path.GetFileName(pdfPath));

            var existingState = ReadSigningState(pdfPath);

            if (!File.Exists(backupPath))
            {
                // First machine — PDF is still clean. Back it up now, before any writes.
                Directory.CreateDirectory(originalsDir);
                File.Copy(pdfPath, backupPath, overwrite: false);
            }

            _originalBackupPath = backupPath;

            // Hash is always taken from the backup — never from the working PDF.
            // If the backup didn't exist yet we just created it from the clean file,
            // so the hash is correct either way.
            _originalDocHash = !string.IsNullOrEmpty(existingState?.OriginalDocumentHash)
                ? existingState.OriginalDocumentHash
                : ComputeHash(backupPath);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public bool CaptureAndEmbed(
            int signatureId, string party,
            string signerName, string reason,
            int page, float x, float y, float width, float height)
        {
            DateTime capturedAt = DateTime.Now;

            SigObj sigObj = CaptureSignature(signerName, reason, _originalDocHash, capturedAt);

            string rawPath = RenderRawSignature(sigObj);
            string transparentPath = MakeTransparent(rawPath);

            byte[] tsaResponse = null;
            DateTime? trustedAt = null;
            TryGetTsaTimestamp(_originalDocHash, out tsaResponse, out trustedAt);

            string compositePath = BuildCompositeImage(transparentPath, signerName, reason, capturedAt, trustedAt);
            string fssPath = SaveFssTemp(sigObj);
            string artifactDir = SaveArtifacts(signerName, reason, capturedAt,
                                       sigObj.SigText, compositePath, rawPath, fssPath, tsaResponse, trustedAt);

            _placements.Add(new SignaturePlacement
            {
                SignatureId = signatureId,
                Party = party,
                Capture = new SignatureCapture
                {
                    SigText = sigObj.SigText,
                    DocumentHash = _originalDocHash,
                    SignerName = signerName,
                    Reason = reason,
                    CapturedAt = capturedAt,
                    TrustedAt = trustedAt,
                    ArtifactDir = artifactDir,
                    ImagePath = compositePath
                },
                Page = page,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                FssPath = fssPath
            });

            return true;
        }

        public void SaveIntermediate()
        {
            if (_placements.Count == 0) return;
            WriteSignaturesToPdf(includeState: true);
        }

        public void SaveProgress()
        {
            WriteSignaturesToPdf(includeState: true);
        }

        /// <summary>True when at least one signature was captured in this session.</summary>
        public bool HasNewCaptures => _placements.Count > 0;

        /// <summary>The final renamed path after Finalize() or FinalizeFromState().</summary>
        public string FinalizedPath { get; private set; }

        public List<SignatureCapture> Finalize(bool openAfterSave = true)
        {
            if (_placements.Count == 0)
                throw new InvalidOperationException("No signatures captured.");

            WriteSignaturesToPdf(includeState: false);
            FinalizedPath = RenameToSemnat(_pdfPath);

            if (openAfterSave)
                Process.Start(new ProcessStartInfo { FileName = FinalizedPath, UseShellExecute = true });

            return _placements.Select(p => p.Capture).ToList();
        }

        /// <summary>
        /// Called when all signatures were loaded from a previous session and no new
        /// captures were made this session. Strips the signing-state attachment and
        /// renames the file to _Semnat.
        /// </summary>
        public string FinalizeFromState()
        {
            string tempOut = _pdfPath + ".tmp";
            byte[] pdfBytes = File.ReadAllBytes(_pdfPath);

            using (var ms = new MemoryStream(pdfBytes))
            using (var document = PdfReader.Open(ms, PdfDocumentOpenMode.Modify))
            {
                RemoveStateAttachment(document);
                document.Save(tempOut);
            }

            if (File.Exists(_pdfPath)) File.Delete(_pdfPath);
            File.Move(tempOut, _pdfPath);

            FinalizedPath = RenameToSemnat(_pdfPath);
            return FinalizedPath;
        }

        private static string RenameToSemnat(string path)
        {
            string dir = Path.GetDirectoryName(path);
            string noExt = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            string dest = Path.Combine(dir, noExt + "_Semnat" + ext);
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(path, dest);
            return dest;
        }

        public static SigningState ReadSigningState(string pdfPath)
        {
            if (!File.Exists(pdfPath)) return null;

            try
            {
                byte[] pdfBytes = File.ReadAllBytes(pdfPath);
                using (var ms = new MemoryStream(pdfBytes))
                using (var doc = PdfReader.Open(ms, PdfDocumentOpenMode.ReadOnly))
                {
                    var nameArray = GetEmbeddedFilesArray(doc);
                    if (nameArray == null) return null;

                    for (int i = 0; i + 1 < nameArray.Elements.Count; i += 2)
                    {
                        var key = nameArray.Elements[i] as PdfString;
                        if (key == null || key.Value != StateFileName) continue;

                        var fileSpecRef = nameArray.Elements[i + 1] as PdfReference;
                        var fileSpec = fileSpecRef?.Value as PdfDictionary;
                        if (fileSpec == null || !fileSpec.Elements.ContainsKey("/EF")) continue;

                        var efRef = fileSpec.Elements["/EF"] as PdfReference;
                        var efDict = efRef?.Value as PdfDictionary;
                        if (efDict == null || !efDict.Elements.ContainsKey("/F")) continue;

                        var streamRef = efDict.Elements["/F"] as PdfReference;
                        var streamDict = streamRef?.Value as PdfDictionary;
                        if (streamDict?.Stream == null) continue;

                        string json = Encoding.UTF8.GetString(streamDict.Stream.Value);
                        return JsonConvert.DeserializeObject<SigningState>(json);
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Verifies the backup original against the stored hash.
        /// Returns false if the original has been tampered with.
        /// </summary>
        public bool VerifyOriginalIntegrity() =>
            File.Exists(_originalBackupPath) &&
            string.Equals(ComputeHash(_originalBackupPath), _originalDocHash,
                StringComparison.OrdinalIgnoreCase);

        // ── Wacom Capture ─────────────────────────────────────────────────────────

        private static SigObj CaptureSignature(
            string signerName, string reason, string docHash, DateTime capturedAt)
        {
            var sigCtl = new SigCtl { Licence = WacomLicence };

            var dc = (DynamicCapture)Activator.CreateInstance(
                          Type.GetTypeFromProgID("Florentis.DynamicCapture"));
            var res = dc.Capture(sigCtl, signerName, reason, null, null);

            switch (res)
            {
                case DynamicCaptureResult.DynCaptOK: break;
                case DynamicCaptureResult.DynCaptCancel:
                    throw new OperationCanceledException("Signature cancelled by user.");
                case DynamicCaptureResult.DynCaptPadError:
                    throw new InvalidOperationException("Signing device error. Check STU-540 connection.");
                default:
                    throw new InvalidOperationException($"Capture failed: {res}");
            }

            SigObj sigObj = (SigObj)sigCtl.Signature;
            sigObj.set_ExtraData("DocumentHash", docHash);
            sigObj.set_ExtraData("SignedBy", signerName);
            sigObj.set_ExtraData("Reason", reason);
            sigObj.set_ExtraData("CapturedAt", capturedAt.ToString("o"));

            return sigObj;
        }

        // ── Image Pipeline ────────────────────────────────────────────────────────

        private string RenderRawSignature(SigObj sigObj)
        {
            string rawPath = TempFile("raw", "png");
            sigObj.RenderBitmap(
                rawPath,
                CompositeWidth, CompositeHeight,
                "image/png",
                0.5f,
                0x8B0000,
                0xffffff,
                5.0f, 5.0f,
                RBFlags.RenderOutputFilename | RBFlags.RenderColor32BPP | RBFlags.RenderEncodeData);
            return rawPath;
        }

        private string MakeTransparent(string inputPath)
        {
            string outputPath = TempFile("transparent", "png");
            const int threshold = 240;

            Bitmap src;
            using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
                src = new Bitmap(fs);

            var result = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    Color px = src.GetPixel(x, y);
                    result.SetPixel(x, y,
                        px.R >= threshold && px.G >= threshold && px.B >= threshold
                            ? Color.Transparent : px);
                }

            src.Dispose();
            result.Save(outputPath, ImageFormat.Png);
            result.Dispose();

            return outputPath;
        }

        private string BuildCompositeImage(
            string transparentSigPath, string signerName, string reason,
            DateTime capturedAt, DateTime? trustedAt)
        {
            string outputPath = TempFile("composite", "png");

            using (var composite = new Bitmap(CompositeWidth, CompositeHeight, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(composite))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                g.Clear(Color.White);

                using (var fs = new FileStream(transparentSigPath, FileMode.Open, FileAccess.Read))
                using (var sig = Image.FromStream(fs))
                    g.DrawImage(sig, 0, 0, CompositeWidth, CompositeHeight);

                using (var font = new Font("Calibri", 40f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(Color.Black))
                {
                    int metaX = 8;
                    int lineH = 55;
                    int metaY = CompositeHeight - (lineH * 3) - 20;

                    string dateStr = trustedAt.HasValue
                        ? trustedAt.Value.ToString("M/d/yyyy h:mm:ss tt") + " (TSA-verified)"
                        : capturedAt.ToString("M/d/yyyy h:mm:ss tt") + $" ({capturedAt:zzz})";

                    g.DrawString($"Name: {signerName}", font, brush, metaX, metaY);
                    g.DrawString($"Reason: {reason}", font, brush, metaX, metaY + lineH);
                    g.DrawString($"Date: {dateStr}", font, brush, metaX, metaY + lineH * 2);
                }

                using (var pen = new Pen(Color.FromArgb(160, 160, 160), 1f))
                    g.DrawRectangle(pen, 0, 0, CompositeWidth - 1, CompositeHeight - 1);

                composite.Save(outputPath, ImageFormat.Png);
            }

            return outputPath;
        }

        // ── FSS Temp ──────────────────────────────────────────────────────────────

        private string SaveFssTemp(SigObj sigObj)
        {
            string fssPath = TempFile("sig", "fss");
            File.WriteAllBytes(fssPath, Convert.FromBase64String(sigObj.SigText));
            return fssPath;
        }

        // ── Artifacts ─────────────────────────────────────────────────────────────

        private string SaveArtifacts(
            string signerName, string reason, DateTime capturedAt,
            string sigText, string compositePath, string rawPath, string tempFssPath,
            byte[] tsaResponse, DateTime? trustedAt)
        {
            string folder = $"{capturedAt:yyyyMMdd_HHmmss}_{Sanitize(signerName)}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            string dir = Path.Combine(_artifactsRootDir, folder);
            Directory.CreateDirectory(dir);

            File.Copy(compositePath, Path.Combine(dir, "signature.png"), overwrite: true);
            File.Copy(rawPath, Path.Combine(dir, "signature_raw.png"), overwrite: true);
            File.WriteAllBytes(Path.Combine(dir, "signature.fss"), Convert.FromBase64String(sigText));

            var meta = new
            {
                SignerName = signerName,
                Reason = reason,
                CapturedAt = capturedAt.ToString("o"),
                TrustedAt = trustedAt?.ToString("o"),
                TsaVerified = trustedAt.HasValue,
                OriginalDocumentHash = _originalDocHash,
                OriginalDocumentPath = _originalBackupPath,
                MachineName = Environment.MachineName,
                AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            };
            File.WriteAllText(
                Path.Combine(dir, "metadata.json"),
                JsonConvert.SerializeObject(meta, Formatting.Indented),
                Encoding.UTF8);

            if (tsaResponse != null && tsaResponse.Length > 0)
                File.WriteAllBytes(Path.Combine(dir, "timestamp.tsr"), tsaResponse);

            return dir;
        }

        // ── PDF Write (in-place) ──────────────────────────────────────────────────

        private void WriteSignaturesToPdf(bool includeState)
        {
            string tempOut = _pdfPath + ".tmp";
            byte[] pdfBytes = File.ReadAllBytes(_pdfPath);

            using (var ms = new MemoryStream(pdfBytes))
            using (var document = PdfReader.Open(ms, PdfDocumentOpenMode.Modify))
            {
                for (int i = _writtenCount; i < _placements.Count; i++)
                {
                    var p = _placements[i];

                    if (p.Page < 1 || p.Page > document.PageCount)
                        throw new ArgumentOutOfRangeException(
                            $"Slot references page {p.Page} but PDF has {document.PageCount} pages.");

                    PdfPage pdfPage = document.Pages[p.Page - 1];

                    using (XGraphics gfx = XGraphics.FromPdfPage(pdfPage))
                    using (XImage image = XImage.FromFile(p.Capture.ImagePath))
                    {
                        double drawY = pdfPage.Height.Point - p.Y - p.Height;
                        gfx.DrawImage(image, p.X, drawY, p.Width, p.Height);
                    }

                    AttachFssAndMetadata(document, p);
                }

                RemoveStateAttachment(document);
                if (includeState)
                    AttachSigningState(document);

                document.Save(tempOut);
            }

            if (File.Exists(_pdfPath)) File.Delete(_pdfPath);
            File.Move(tempOut, _pdfPath);

            _writtenCount = _placements.Count;
        }

        // ── Signing State ─────────────────────────────────────────────────────────

        private void AttachSigningState(PdfDocument document)
        {
            var signedIds = new HashSet<int>(_placements.Select(p => p.SignatureId));

            var state = new SigningState
            {
                OriginalDocumentHash = _originalDocHash,
                Slots = _allSlots.Select(s =>
                {
                    bool signed = signedIds.Contains(s.SignatureId);
                    var placement = signed ? _placements.First(p => p.SignatureId == s.SignatureId) : null;
                    return new SigningStateEntry
                    {
                        SignatureId = s.SignatureId,
                        Party = s.Party ?? string.Empty,
                        SignerName = signed ? placement.Capture.SignerName : s.ResolvedSignerName,
                        Reason = s.Reason,
                        Signed = signed,
                        SignedAt = signed ? placement.Capture.CapturedAt : (DateTime?)null,
                        MachineName = signed ? Environment.MachineName : null
                    };
                }).ToList()
            };

            AttachFile(document, StateFileName, "Document Signing State",
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(state, Formatting.Indented)));
        }

        private static void RemoveStateAttachment(PdfDocument document)
        {
            var nameArray = GetEmbeddedFilesArray(document);
            if (nameArray == null) return;

            for (int i = 0; i + 1 < nameArray.Elements.Count; i += 2)
            {
                var key = nameArray.Elements[i] as PdfString;
                if (key == null || key.Value != StateFileName) continue;

                nameArray.Elements.RemoveAt(i + 1);
                nameArray.Elements.RemoveAt(i);
                return;
            }
        }

        // ── PDF Attachments ───────────────────────────────────────────────────────

        private static void AttachFssAndMetadata(PdfDocument document, SignaturePlacement p)
        {
            if (string.IsNullOrEmpty(p.FssPath) || !File.Exists(p.FssPath)) return;

            string safeName = Sanitize(p.Capture.SignerName);

            AttachFile(document,
                $"signature_{safeName}_#{p.SignatureId}.fss",
                $"Signature FSS Data — {p.Capture.SignerName} — {p.Capture.CapturedAt:u}",
                File.ReadAllBytes(p.FssPath));

            string metaJson = JsonConvert.SerializeObject(new
            {
                SignatureId = p.SignatureId,
                SignerName = p.Capture.SignerName,
                Reason = p.Capture.Reason,
                Party = p.Party,
                CapturedAt = p.Capture.CapturedAt.ToString("o"),
                TrustedAt = p.Capture.TrustedAt?.ToString("o"),
                OriginalDocumentHash = p.Capture.DocumentHash,
                MachineName = Environment.MachineName,
                AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            }, Formatting.Indented);

            AttachFile(document,
                $"metadata_{safeName}_#{p.SignatureId}.json",
                $"Signature Metadata — {p.Capture.SignerName}",
                Encoding.UTF8.GetBytes(metaJson));
        }

        private static void AttachFile(PdfDocument document, string filename, string description, byte[] data)
        {
            var efStream = new PdfDictionary(document);
            efStream.Elements["/Type"] = new PdfName("/EmbeddedFile");
            efStream.Elements["/Length"] = new PdfInteger(data.Length);
            efStream.CreateStream(data);
            document.Internals.AddObject(efStream);

            var efDict = new PdfDictionary(document);
            efDict.Elements["/F"] = efStream.Reference;
            document.Internals.AddObject(efDict);

            var fileSpec = new PdfDictionary(document);
            fileSpec.Elements["/Type"] = new PdfName("/Filespec");
            fileSpec.Elements["/F"] = new PdfString(filename);
            fileSpec.Elements["/UF"] = new PdfString(filename);
            fileSpec.Elements["/Desc"] = new PdfString(description);
            fileSpec.Elements["/EF"] = efDict.Reference;
            document.Internals.AddObject(fileSpec);

            PdfDictionary catalog = document.Internals.Catalog;

            if (!catalog.Elements.ContainsKey("/Names"))
                catalog.Elements["/Names"] = new PdfDictionary(document);
            var namesDict = catalog.Elements["/Names"] as PdfDictionary;
            if (namesDict == null) return;

            if (!namesDict.Elements.ContainsKey("/EmbeddedFiles"))
                namesDict.Elements["/EmbeddedFiles"] = new PdfDictionary(document);
            var embeddedFiles = namesDict.Elements["/EmbeddedFiles"] as PdfDictionary;
            if (embeddedFiles == null) return;

            if (!embeddedFiles.Elements.ContainsKey("/Names"))
                embeddedFiles.Elements["/Names"] = new PdfArray(document);
            var nameArray = embeddedFiles.Elements["/Names"] as PdfArray;
            if (nameArray == null) return;

            nameArray.Elements.Add(new PdfString(filename));
            nameArray.Elements.Add(fileSpec.Reference);
        }

        // ── TSA ───────────────────────────────────────────────────────────────────

        private static void TryGetTsaTimestamp(
            string docHash, out byte[] tsaResponse, out DateTime? trustedAt)
        {
            tsaResponse = null;
            trustedAt = null;
            try
            {
                tsaResponse = RequestTsaTimestamp(docHash);
                trustedAt = DateTime.UtcNow.ToLocalTime();
            }
            catch { }
        }

        private static byte[] RequestTsaTimestamp(string docHash)
        {
            byte[] tsq = BuildTimestampRequest(HexToBytes(docHash));

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

        private static byte[] BuildTimestampRequest(byte[] sha256Hash)
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

        // ── Utilities ─────────────────────────────────────────────────────────────

        private static PdfArray GetEmbeddedFilesArray(PdfDocument document)
        {
            PdfDictionary catalog = document.Internals.Catalog;
            if (!catalog.Elements.ContainsKey("/Names")) return null;

            var namesDict = catalog.Elements["/Names"] as PdfDictionary;
            if (namesDict == null || !namesDict.Elements.ContainsKey("/EmbeddedFiles")) return null;

            var embeddedFiles = namesDict.Elements["/EmbeddedFiles"] as PdfDictionary;
            if (embeddedFiles == null || !embeddedFiles.Elements.ContainsKey("/Names")) return null;

            return embeddedFiles.Elements["/Names"] as PdfArray;
        }

        private static string ComputeHash(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs))
                    .Replace("-", "").ToLowerInvariant();
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 30 ? name.Substring(0, 30) : name;
        }

        private string TempFile(string prefix, string ext) =>
            Path.Combine(_tempDir, $"{prefix}_{Guid.NewGuid():N}.{ext}");

        // ── Dispose ───────────────────────────────────────────────────────────────

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }
    }
}