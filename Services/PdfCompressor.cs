using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Diagnostics;
using System.IO;

namespace WacomSignaturePdf.Services
{
    internal static class PdfCompressor
    {
        public static void CompressInPlace(string pdfPath)
        {
            // Skip if document already has signing state attached (signatures present)
            if (HasSigningState(pdfPath))
                return;

            string tempPath = pdfPath + ".compressed.tmp";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "gswin32c.exe",
                    Arguments = $"-sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS=/ebook " +
                                $"-dNOPAUSE -dQUIET -dBATCH " +
                                $"-sOutputFile=\"{tempPath}\" \"{pdfPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    if (proc.ExitCode != 0)
                        throw new InvalidOperationException(
                            $"Ghostscript exit {proc.ExitCode}: {stderr}");
                }

                var original = new FileInfo(pdfPath);
                var compressed = new FileInfo(tempPath);

                if (compressed.Exists && compressed.Length < original.Length)
                {
                    File.Delete(pdfPath);
                    File.Move(tempPath, pdfPath);
                }
                else
                {
                    if (compressed.Exists) File.Delete(tempPath);
                }
            }
            catch
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private static bool HasSigningState(string pdfPath)
        {
            try
            {
                using (var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import))
                {
                    var nameArray = PdfAttachmentHelper.GetEmbeddedFilesArray(doc);
                    if (nameArray == null) return false;

                    for (int i = 0; i + 1 < nameArray.Elements.Count; i += 2)
                    {
                        var key = nameArray.Elements[i] as PdfString;
                        if (key != null && key.Value == "signing-state.json")
                            return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}