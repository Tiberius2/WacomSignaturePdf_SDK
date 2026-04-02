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
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using WacomSignaturePdf.Models;
using wgssSTU;

namespace WacomSignaturePdf.Services
{
    public class SignatureService : IDisposable
    {
        #region Fields & Constants

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

        private const int CompositeWidth = 800;
        private const int CompositeHeight = 480;
        private const int CompositeLineH = 40;
        private const int CompositeMetaMargin = 14;
        private const int CompositeSepGap = 16;
        private static readonly int CompositeInkHeight =
            CompositeHeight - (CompositeLineH * 3) - CompositeMetaMargin - CompositeSepGap; // 330px
        private const string StateFileName = "signing-state.json";
        private const string OriginalsFolder = "Originally Generated Documents";
        private const bool SaveArtifactsToDisk = false;

        private const string WacomLicence = "eyJhbGciOiJSUzUxMiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiI3YmM5Y2IxYWIxMGE0NmUxODI2N2E5MTJkYTA2ZTI3NiIsImV4cCI6MjE0NzQ4MzY0NywiaWF0IjoxNTYwOTUwMjcyLCJyaWdodHMiOlsiU0lHX1NES19DT1JFIiwiU0lHQ0FQVFhfQUNDRVNTIl0sImRldmljZXMiOlsiV0FDT01fQU5ZIl0sInR5cGUiOiJwcm9kIiwibGljX25hbWUiOiJTaWduYXR1cmUgU0RLIiwid2Fjb21faWQiOiI3YmM5Y2IxYWIxMGE0NmUxODI2N2E5MTJkYTA2ZTI3NiIsImxpY191aWQiOiJiODUyM2ViYi0xOGI3LTQ3OGEtYTlkZS04NDlmZTIyNmIwMDIiLCJhcHBzX3dpbmRvd3MiOltdLCJhcHBzX2lvcyI6W10sImFwcHNfYW5kcm9pZCI6W10sIm1hY2hpbmVfaWRzIjpbXX0.ONy3iYQ7lC6rQhou7rz4iJT_OJ20087gWz7GtCgYX3uNtKjmnEaNuP3QkjgxOK_vgOrTdwzD-nm-ysiTDs2GcPlOdUPErSp_bcX8kFBZVmGLyJtmeInAW6HuSp2-57ngoGFivTH_l1kkQ1KMvzDKHJbRglsPpd4nVHhx9WkvqczXyogldygvl0LRidyPOsS5H2GYmaPiyIp9In6meqeNQ1n9zkxSHo7B11mp_WXJXl0k1pek7py8XYCedCNW5qnLi4UCNlfTd6Mk9qz31arsiWsesPeR9PN121LBJtiPi023yQU8mgb9piw_a-ccciviJuNsEuRDN3sGnqONG3dMSA";

        #endregion

        #region Constructor

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

            // The backup lives in "Originally Generated Documents" next to the PDF.
            // Created once on the first machine — subsequent machines just read the
            // hash from the existing backup (or from the embedded signing state).
            string originalsDir = Path.Combine(Path.GetDirectoryName(pdfPath), OriginalsFolder);
            string backupPath = Path.Combine(originalsDir, Path.GetFileName(pdfPath));

            var existingState = ReadSigningState(pdfPath);

            if (!File.Exists(backupPath))
            {
                // First machine — PDF is still clean. Back it up before any writes.
                Directory.CreateDirectory(originalsDir);
                File.Copy(pdfPath, backupPath, overwrite: false);
            }

            _originalBackupPath = backupPath;

            // Hash is always taken from the backup — never from the working PDF.
            _originalDocHash = !string.IsNullOrEmpty(existingState?.OriginalDocumentHash)
                ? existingState.OriginalDocumentHash
                : ComputeHash(backupPath);
        }

        #endregion

        #region Public API

        /// <summary>True when at least one signature was captured in this session.</summary>
        public bool HasNewCaptures => _placements.Count > 0;

        /// <summary>The final renamed path after Finalize() or FinalizeFromState().</summary>
        public string FinalizedPath { get; private set; }

        public bool CaptureAndEmbed(
            int signatureId, string party,
            string signerName, string reason,
            int page, float x, float y, float width, float height,
            bool isImputernicire = false)
        {
            DateTime capturedAt = DateTime.Now;

            SigObj sigObj = CaptureSignature(signerName, reason, _originalDocHash, capturedAt);

            string rawPath = RenderRawSignature(sigObj);
            string transparentPath = MakeTransparent(rawPath);

            TsaHelper.TryGetTimestamp(_originalDocHash, out byte[] tsaResponse, out DateTime? trustedAt);

            string compositePath = BuildCompositeImage(transparentPath, signerName, reason, capturedAt, trustedAt, isImputernicire);
            string fssPath = SaveFssTemp(sigObj);
            string artifactDir = SaveArtifactsToDisk
                ? SaveArtifacts(signerName, reason, capturedAt,
                      sigObj.SigText, compositePath, rawPath, fssPath, tsaResponse, trustedAt)
                : string.Empty;

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

        #endregion

        #region Document Verification

        /// <summary>
        /// Verifies the backup original against the stored hash.
        /// Returns false if the original has been tampered with.
        /// We have to check this somewhere before finalization to avoid writing signatures into a tampered document.
        /// </summary>
        public bool VerifyOriginalIntegrity() =>
            File.Exists(_originalBackupPath) &&
            string.Equals(ComputeHash(_originalBackupPath), _originalDocHash,
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Checks whether a PDF has been digitally signed and locked in Adobe
        /// (i.e. "Lock document after signing" / DocMDP certification).
        /// Returns true if the document contains a certification signature that
        /// prevents further modifications.
        /// </summary>
        public static bool IsDocumentSealed(string pdfPath)
        {
            if (!File.Exists(pdfPath)) return false;

            try
            {
                byte[] pdfBytes = File.ReadAllBytes(pdfPath);
                using (var ms = new MemoryStream(pdfBytes))
                using (var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import))
                {
                    var catalog = doc.Internals.Catalog;

                    // Check /Perms/DocMDP — certification signature
                    if (catalog.Elements.ContainsKey("/Perms"))
                    {
                        var perms = ResolveDict(catalog.Elements["/Perms"]);
                        if (perms != null && perms.Elements.ContainsKey("/DocMDP"))
                            return true;
                    }

                    // Check /AcroForm/SigFlags bit 2 (AppendOnly)
                    if (catalog.Elements.ContainsKey("/AcroForm"))
                    {
                        var acroForm = ResolveDict(catalog.Elements["/AcroForm"]);
                        if (acroForm != null && acroForm.Elements.ContainsKey("/SigFlags"))
                        {
                            int flags = 0;
                            try
                            {
                                var sigFlagsItem = acroForm.Elements["/SigFlags"];
                                if (sigFlagsItem is PdfReference sigRef)
                                    sigFlagsItem = sigRef.Value;
                                flags = int.Parse(sigFlagsItem.ToString());
                            }
                            catch { }

                            // Bit 2 = AppendOnly → document is locked after signing
                            if ((flags & 2) != 0)
                                return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>Resolves a PdfItem that may be a direct dict or a PdfReference to one.</summary>
        private static PdfDictionary ResolveDict(PdfSharp.Pdf.PdfItem item)
        {
            if (item is PdfDictionary dict) return dict;
            if (item is PdfReference r && r.Value is PdfDictionary refDict) return refDict;
            return null;
        }

        #endregion

        #region Wacom Capture

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
        #endregion

        #region Image Pipeline

        private string RenderRawSignature(SigObj sigObj)
        {
            string rawPath = TempFile("raw", "png");
            sigObj.RenderBitmap(
                rawPath,
                CompositeWidth, CompositeInkHeight,
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

        // Draws the transparent signature on a white background and adds metadata text below.
        private string BuildCompositeImage(
            string transparentSigPath, string signerName, string reason,
            DateTime capturedAt, DateTime? trustedAt, bool isImputernicire = false)
        {
            string outputPath = TempFile("composite", "png");

            int metaY = CompositeHeight - (CompositeLineH * 3) - CompositeMetaMargin;
            int separatorY = metaY - CompositeSepGap;

            using (var composite = new Bitmap(CompositeWidth, CompositeHeight, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(composite))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
                g.Clear(Color.White);

                // Signature ink — rendered natively at CompositeInkHeight so no vertical squish
                using (var fs = new FileStream(transparentSigPath, FileMode.Open, FileAccess.Read))
                using (var sig = Image.FromStream(fs))
                    g.DrawImage(sig, 0, 0, CompositeWidth, CompositeInkHeight);

                // Separator line
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var sepPen = new Pen(Color.FromArgb(80, 80, 80), 2.5f))
                    g.DrawLine(sepPen, 6, separatorY, CompositeWidth - 6, separatorY);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;

                // Metadata text
                using (var font = new Font("Calibri", 36f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(Color.FromArgb(40, 40, 40)))
                {
                    string dateStr = trustedAt.HasValue
                        ? trustedAt.Value.ToString("dd.MM.yyyy HH:mm:ss") + " (TSA-verified)"
                        : capturedAt.ToString("dd.MM.yyyy HH:mm:ss") + $" ({capturedAt:zzz})";

                    string nameLabel = isImputernicire
                        ? $"Name: (Imputernicit) {signerName}"
                        : $"Name: {signerName}";

                    g.DrawString(nameLabel, font, brush, 10, metaY);
                    g.DrawString($"Reason: {reason}", font, brush, 10, metaY + CompositeLineH);
                    g.DrawString($"Date: {dateStr}", font, brush, 10, metaY + CompositeLineH * 2);
                }

                // Imputernicire marker — top-right corner, above separator
                if (isImputernicire)
                {
                    using (var markerFont = new Font("Calibri", 72f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var markerBrush = new SolidBrush(Color.Black))
                    {
                        SizeF sz = g.MeasureString("*", markerFont);
                        g.DrawString("*", markerFont, markerBrush, CompositeWidth - sz.Width - 6, 2f);
                    }
                }

                using (var pen = new Pen(Color.FromArgb(160, 160, 160), 1f))
                    g.DrawRectangle(pen, 0, 0, CompositeWidth - 1, CompositeHeight - 1);

                composite.Save(outputPath, ImageFormat.Png);
            }

            return outputPath;
        }

        private string SaveFssTemp(SigObj sigObj)
        {
            string fssPath = TempFile("sig", "fss");
            File.WriteAllBytes(fssPath, Convert.FromBase64String(sigObj.SigText));
            return fssPath;
        }

        #endregion

        #region Artifacts

        // Saves all relevant artifacts (composite image, raw signature, FSS data, metadata JSON, TSA response) to a timestamped folder.
        // We might have to save these to a db later on
        private string SaveArtifacts(
            string signerName, string reason, DateTime capturedAt,
            string sigText, string compositePath, string rawPath, string tempFssPath,
            byte[] tsaResponse, DateTime? trustedAt)
        {
            string folder = $"{capturedAt:yyyyMMdd_HHmmss}_{Sanitize(signerName)}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            string dir = Path.Combine(_artifactsRootDir, folder);
            Directory.CreateDirectory(dir);

            //File.Copy(compositePath, Path.Combine(dir, "signature.png"), overwrite: true);
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

        #endregion

        #region PDF Writing

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

        #endregion

        #region Signing State


        // Reads the embedded signing state from the PDF, if present.
        // This is used to preserve signature metadata across multiple signing sessions on different machines,
        // and to verify the integrity of the original document.
        public static SigningState ReadSigningState(string pdfPath)
        {
            if (!File.Exists(pdfPath)) return null;

            try
            {
                byte[] pdfBytes = File.ReadAllBytes(pdfPath);
                using (var ms = new MemoryStream(pdfBytes))
                using (var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import))
                {
                    var nameArray = PdfAttachmentHelper.GetEmbeddedFilesArray(doc);
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


        // Embeds the signing state into the PDF as an attachment. This includes metadata for all signature slots,
        // both those signed in this session and those signed in previous sessions (loaded from the existing embedded state).
        private void AttachSigningState(PdfDocument document)
        {
            // Slots signed in this session
            var sessionIds = new HashSet<int>(_placements.Select(p => p.SignatureId));

            // Slots already signed in a previous session (loaded from the embedded state)
            var previousState = ReadSigningState(_pdfPath);
            var previousSigned = previousState?.Slots?
                .Where(e => e.Signed && !sessionIds.Contains(e.SignatureId))
                .ToDictionary(e => e.SignatureId)
                ?? new Dictionary<int, SigningStateEntry>();

            var state = new SigningState
            {
                OriginalDocumentHash = _originalDocHash,
                Slots = _allSlots.Select(s =>
                {
                    if (sessionIds.Contains(s.SignatureId))
                    {
                        // Signed this session — use fresh capture data
                        var placement = _placements.First(p => p.SignatureId == s.SignatureId);
                        return new SigningStateEntry
                        {
                            SignatureId = s.SignatureId,
                            Party = s.Party ?? string.Empty,
                            SignerName = placement.Capture.SignerName,
                            Reason = s.Reason,
                            Signed = true,
                            SignedAt = placement.Capture.CapturedAt,
                            MachineName = Environment.MachineName
                        };
                    }

                    if (previousSigned.TryGetValue(s.SignatureId, out var prev))
                    {
                        // Signed in a previous session — preserve that entry exactly
                        return new SigningStateEntry
                        {
                            SignatureId = prev.SignatureId,
                            Party = prev.Party,
                            SignerName = prev.SignerName,
                            Reason = prev.Reason,
                            Signed = true,
                            SignedAt = prev.SignedAt,
                            MachineName = prev.MachineName
                        };
                    }

                    // Not yet signed
                    return new SigningStateEntry
                    {
                        SignatureId = s.SignatureId,
                        Party = s.Party ?? string.Empty,
                        SignerName = s.ResolvedSignerName,
                        Reason = s.Reason,
                        Signed = false,
                        SignedAt = null,
                        MachineName = null
                    };
                }).ToList()
            };

            PdfAttachmentHelper.AttachFile(document, StateFileName, "Document Signing State",
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(state, Formatting.Indented)));
        }


        // Removes the signing state attachment from the PDF. Called on finalization to clean up the file.
        private static void RemoveStateAttachment(PdfDocument document)
        {
            var nameArray = PdfAttachmentHelper.GetEmbeddedFilesArray(document);
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

        #endregion

        #region PDF Attachments

        private static void AttachFssAndMetadata(PdfDocument document, SignaturePlacement p)
        {
            if (string.IsNullOrEmpty(p.FssPath) || !File.Exists(p.FssPath)) return;

            string safeName = Sanitize(p.Capture.SignerName);
            string safeReason = Sanitize(p.Capture.Reason);

            PdfAttachmentHelper.AttachFile(document,
                $"{safeName}_{safeReason}_Sig#{p.SignatureId}.fss",
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

            PdfAttachmentHelper.AttachFile(document,
                $"md_{safeName}_{safeReason}_Sig#{p.SignatureId}.json",
                $"Signature Metadata — {p.Capture.SignerName}",
                Encoding.UTF8.GetBytes(metaJson));
        }

        #endregion

        #region Utilities

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

        #endregion

        #region Dispose

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }

        #endregion
    }
}