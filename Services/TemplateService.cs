using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PdfiumViewer;
using WacomSignaturePdf.Models;

namespace WacomSignaturePdf.Services
{
    /// <summary>
    /// This service handles loading document templates, finding candidate folders, 
    /// and resolving templates against candidate folders to produce ResolvedTemplate instances ready for signing.
    /// </summary> 
    public static class TemplateService
    {
        // ── Load ──
        public static List<DocumentTemplate> LoadTemplates(string templatesDir)
        {
            if (!Directory.Exists(templatesDir))
                throw new DirectoryNotFoundException($"Templates directory not found: {templatesDir}");

            var templates = Directory
                .GetFiles(templatesDir, "*.json")
                .Select(f => JsonConvert.DeserializeObject<DocumentTemplate>(File.ReadAllText(f)))
                .Where(t => t != null)
                .ToList();

            if (templates.Count == 0)
                throw new InvalidOperationException($"No templates found in: {templatesDir}");

            return templates;
        }

        // ── Candidate folder ──
        public static string FindCandidateFolder(string workingRoot, string candidateId)
        {
            if (!Directory.Exists(workingRoot))
                throw new DirectoryNotFoundException($"Working root not found: {workingRoot}");

            candidateId = candidateId.Trim();

            var matches = Directory.GetDirectories(workingRoot)
                .Where(d =>
                {
                    string name = Path.GetFileName(d);
                    return name.StartsWith(candidateId + " - ", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith(candidateId + "-", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (matches.Count == 0)
                throw new InvalidOperationException(
                    $"No folder found for ID '{candidateId}' in '{workingRoot}'.");

            if (matches.Count > 1)
                throw new InvalidOperationException(
                    $"Multiple folders match ID '{candidateId}'. Resolve the duplicate.");

            return matches[0];
        }


        // Extract the candidate name from the folder name, which is expected to be in the format "ID - Name".
        public static string GetCandidateName(string candidateFolder)
        {
            string name = Path.GetFileName(candidateFolder);
            int idx = name.IndexOf(" - ", StringComparison.Ordinal);
            return idx >= 0 ? name.Substring(idx + 3).Trim() : name;
        }

        // ── Document Status ──
        public enum DocumentStatus
        {
            NotFound,
            Unsigned,
            PartialSigned,
            SignedUnsealed,
            SignedSealed
        }

        /// <summary>
        /// Lightweight status check — does not load the PDF or run compression.
        /// Used to show status badges in the document type dropdown.
        /// </summary>
        public static DocumentStatus GetDocumentStatus(DocumentTemplate template, string candidateFolder)
        {
            if (string.IsNullOrWhiteSpace(candidateFolder)) return DocumentStatus.NotFound;

            string pdfPath = Path.Combine(candidateFolder, template.FileSystemBlock.InputFileName);

            if (File.Exists(pdfPath))
            {
                var state = SignatureService.ReadSigningState(pdfPath);

                if (state == null || state.Slots == null || !state.Slots.Any(s => s.Signed))
                    return DocumentStatus.Unsigned;

                // Check if all required slots are signed
                var requiredIds = new HashSet<int>(
                    template.Signatures.Where(s => s.Required).Select(s => s.SignatureId));

                bool allRequiredSigned = requiredIds.Count == 0
                    || requiredIds.All(id => state.Slots.Any(s => s.SignatureId == id && s.Signed));

                return allRequiredSigned
                    ? DocumentStatus.SignedUnsealed
                    : DocumentStatus.PartialSigned;
            }

            // Check for _Semnat version
            string noExt = Path.GetFileNameWithoutExtension(template.FileSystemBlock.InputFileName);
            string ext = Path.GetExtension(template.FileSystemBlock.InputFileName);
            string semnatPath = Path.Combine(candidateFolder, noExt + "_Semnat" + ext);

            if (File.Exists(semnatPath))
                return SignatureService.IsDocumentSealed(semnatPath)
                    ? DocumentStatus.SignedSealed
                    : DocumentStatus.SignedUnsealed;

            return DocumentStatus.NotFound;
        }

        // ── Resolve ──
        // Resolves a DocumentTemplate against a candidate folder,
        // producing a ResolvedTemplate with actual file paths and resolved signature slots.
        public static ResolvedTemplate Resolve(DocumentTemplate template, string candidateFolder, string signerName, string officialName = "")
        {
            string pdfPath = Path.Combine(candidateFolder, template.FileSystemBlock.InputFileName);
            string artifactsPath = Path.Combine(candidateFolder, "SignatureArtifacts"); // we have to save these artifacts in a db later on

            if (!File.Exists(pdfPath))
            {
                // Check if the _Semnat version exists — means it was already finalized.
                string noExt = Path.GetFileNameWithoutExtension(template.FileSystemBlock.InputFileName);
                string ext = Path.GetExtension(template.FileSystemBlock.InputFileName);
                string semnatName = noExt + "_Semnat" + ext;
                string semnatPath = Path.Combine(candidateFolder, semnatName);

                if (File.Exists(semnatPath))
                {
                    bool sealed_ = SignatureService.IsDocumentSealed(semnatPath); // Check if the _Semnat file is digitally sealed (DocMDP / locked after signing) in Adobe.
                    if (sealed_)
                        throw new DocumentAlreadyFinalizedException(
                            $"Documentul \"{template.FileSystemBlock.InputFileName}\" a fost finalizat, semnat și sigilat.\n\n" +
                            $"Fișierul se găsește la:\n{semnatPath}");
                    else
                        throw new DocumentSignedNotSealedException(
                            $"Documentul \"{template.FileSystemBlock.InputFileName}\" a fost semnat prin aplicație " +
                            $"dar NU a fost sigilat digital în Adobe.\n\n" +
                            $"Deschideți fișierul în Adobe Acrobat și aplicați semnătura digitală cu opțiunea " +
                            $"\"Lock document after signing\" pentru a finaliza procesul.\n\n" +
                            $"Fișierul se găsește la:\n{semnatPath}",
                            semnatPath);
                }
                throw new FileNotFoundException(
                    $"Document '{template.FileSystemBlock.InputFileName}' not found in:\n{candidateFolder}");
            }

            PdfCompressor.CompressInPlace(pdfPath);


            // After we check the file exists, we can load it to get the page count for resolving {{LastPage}} in signature slots and the other items.
            int lastPage;
            using (var doc = PdfDocument.Load(pdfPath))
                lastPage = doc.PageCount;

            var slots = template.Signatures.Select(s => new SignatureSlot
            {
                SignatureId = s.SignatureId,
                SignerName = s.SignerName,
                ResolvedSignerName = s.SignerName
                    .Replace("{{SignerName}}", signerName)
                    .Replace("{{OfficialName}}", officialName ?? string.Empty),
                Reason = s.Reason,
                Page = s.Page,
                Party = s.Party,
                ResolvedPage = s.Page.Trim().Equals("{{LastPage}}", StringComparison.OrdinalIgnoreCase)
                                         ? lastPage
                                         : int.Parse(s.Page),
                Location = s.Location,
                Required = s.Required,
                Biometric = s.Biometric
            }).ToList();

            return new ResolvedTemplate
            {
                Template = template,
                PdfPath = pdfPath,
                ArtifactsPath = artifactsPath,
                Slots = slots
            };
        }
    }

    /// <summary>
    /// Thrown when the document to load has already been finalized (renamed to _Semnat)
    /// AND digitally sealed in Adobe (DocMDP / locked after signing).
    /// </summary>
    public class DocumentAlreadyFinalizedException : Exception
    {
        public DocumentAlreadyFinalizedException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when the document has been signed via the app (renamed to _Semnat)
    /// but has NOT been digitally sealed in Adobe. The user must still open it
    /// in Adobe Acrobat and apply a digital signature with "Lock document after signing".
    /// </summary>
    public class DocumentSignedNotSealedException : Exception
    {
        public string SemnatPath { get; }
        public DocumentSignedNotSealedException(string message, string semnatPath) : base(message)
        {
            SemnatPath = semnatPath;
        }
    }
}