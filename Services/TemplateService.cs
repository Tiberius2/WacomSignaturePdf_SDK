using Newtonsoft.Json;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WacomSignaturePdf.Models;

namespace WacomSignaturePdf.Services
{
    public static class TemplateService
    {
        // ── Load ──────────────────────────────────────────────────────────────────

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

        // ── Candidate Folder ──────────────────────────────────────────────────────

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
                throw new InvalidOperationException($"No folder found for ID '{candidateId}' in '{workingRoot}'.");

            if (matches.Count > 1)
                throw new InvalidOperationException($"Multiple folders match ID '{candidateId}'. Resolve the duplicate.");

            return matches[0];
        }

        public static string GetCandidateName(string candidateFolder)
        {
            string name = Path.GetFileName(candidateFolder);
            int idx = name.IndexOf(" - ", StringComparison.Ordinal);
            return idx >= 0 ? name.Substring(idx + 3).Trim() : name;
        }

        // ── Folder Filtering ──────────────────────────────────────────────────────

        // Returns true if the folder contains at least one unsigned slot for the given role.
        // Used to filter the folder dropdown in FilterMyOnly mode.
        public static bool FolderHasPendingForRole(
            string folderPath, List<DocumentTemplate> templates, string officialRole)
        {
            if (!Directory.Exists(folderPath) || templates == null) return false;

            foreach (var template in templates)
            {
                var roleSlotIds = new HashSet<int>(
                    template.Signatures
                        .Where(s => s.Party == "Official" && (string.IsNullOrEmpty(s.OfficialRole) || s.OfficialRole == officialRole))
                        .Select(s => s.SignatureId));

                if (roleSlotIds.Count == 0) continue;

                IEnumerable<string> pdfPaths;

                if (template.FileSystemBlock.IsMultiDocument)
                {
                    pdfPaths = Directory.GetFiles(folderPath, template.FileSystemBlock.FilePattern)
                        .Select(f =>
                        {
                            string nameNoExt = Path.GetFileNameWithoutExtension(f);
                            if (nameNoExt.EndsWith("_Semnat", StringComparison.OrdinalIgnoreCase))
                                nameNoExt = nameNoExt.Substring(0, nameNoExt.Length - 7);
                            return Path.Combine(folderPath, nameNoExt + ".pdf");
                        })
                        .Distinct(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    string resolved = FindInputFilePath(folderPath, template.FileSystemBlock.InputFileName);
                    pdfPaths = resolved != null
                        ? new[] { resolved }
                        : Array.Empty<string>();
                }

                foreach (var pdfPath in pdfPaths)
                {
                    if (!File.Exists(pdfPath)) continue;

                    var state = SignatureService.ReadSigningState(pdfPath);

                    if (state == null || state.Slots == null || !state.Slots.Any(s => s.Signed))
                        return true; // no signatures yet — all role slots are pending

                    var signedIds = new HashSet<int>(state.Slots.Where(s => s.Signed).Select(s => s.SignatureId));
                    if (roleSlotIds.Any(id => !signedIds.Contains(id)))
                        return true;
                }
            }

            return false;
        }

        // ── Document Status ───────────────────────────────────────────────────────

        public enum DocumentStatus { NotFound, Unsigned, PartialSigned, SignedUnsealed, SignedSealed }

        // For multi-doc templates returns the aggregate (worst) status across all matching files.
        public static DocumentStatus GetDocumentStatus(DocumentTemplate template, string candidateFolder)
        {
            if (string.IsNullOrWhiteSpace(candidateFolder)) return DocumentStatus.NotFound;

            if (template.FileSystemBlock.IsMultiDocument)
            {
                var files = GetMatchingFiles(template, candidateFolder);
                if (files.Count == 0) return DocumentStatus.NotFound;

                bool anyUnsigned = files.Any(f => f.Status == DocumentStatus.Unsigned);
                bool anySigned = files.Any(f => f.Status == DocumentStatus.PartialSigned
                                               || f.Status == DocumentStatus.SignedUnsealed
                                               || f.Status == DocumentStatus.SignedSealed);

                if (anyUnsigned && anySigned) return DocumentStatus.PartialSigned;
                if (anyUnsigned) return DocumentStatus.Unsigned;
                if (files.Any(f => f.Status == DocumentStatus.PartialSigned)) return DocumentStatus.PartialSigned;
                if (files.Any(f => f.Status == DocumentStatus.SignedUnsealed)) return DocumentStatus.SignedUnsealed;
                return DocumentStatus.SignedSealed;
            }

            // Single-file template
            string inputFileName = template.FileSystemBlock.InputFileName;

            if (!inputFileName.Contains('*'))
            {
                // Exact filename — original behaviour
                return GetSingleFileStatus(template, Path.Combine(candidateFolder, inputFileName));
            }

            // Wildcard InputFileName (e.g. "GDPR_*.pdf")
            string resolved = FindInputFilePath(candidateFolder, inputFileName);
            if (resolved != null)
                return GetSingleFileStatus(template, resolved);

            // No working file found — check for a _Semnat variant via wildcard
            // e.g. "GDPR_*.pdf" → "GDPR_*_Semnat.pdf"
            string semnatPattern = inputFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? inputFileName.Substring(0, inputFileName.Length - 4) + "_Semnat.pdf"
                : inputFileName + "_Semnat";

            string[] semnatMatches = Directory.GetFiles(candidateFolder, semnatPattern);
            if (semnatMatches.Length == 1)
                return SignatureService.IsDocumentSealed(semnatMatches[0])
                    ? DocumentStatus.SignedSealed
                    : DocumentStatus.SignedUnsealed;
            if (semnatMatches.Length > 1)
                return DocumentStatus.SignedUnsealed; // ambiguous but signed

            return DocumentStatus.NotFound;
        }

        // Returns all document instances for a multi-file template.
        // Normalises _Semnat variants back to their canonical name.
        public static List<(string FilePath, DocumentStatus Status)> GetMatchingFiles(
            DocumentTemplate template, string candidateFolder)
        {
            if (!template.FileSystemBlock.IsMultiDocument || !Directory.Exists(candidateFolder))
                return new List<(string, DocumentStatus)>();

            string pattern = template.FileSystemBlock.FilePattern;
            var allMatches = Directory.GetFiles(candidateFolder, pattern);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<(string, DocumentStatus)>();

            foreach (var filePath in allMatches.OrderBy(f => f))
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(filePath);
                if (nameNoExt.EndsWith("_Semnat", StringComparison.OrdinalIgnoreCase))
                    nameNoExt = nameNoExt.Substring(0, nameNoExt.Length - 7);

                string canonical = Path.Combine(candidateFolder, nameNoExt + ".pdf");
                if (!seen.Add(canonical)) continue;

                var status = GetSingleFileStatus(template, canonical);
                if (status != DocumentStatus.NotFound)
                    result.Add((canonical, status));
            }

            return result;
        }

        private static DocumentStatus GetSingleFileStatus(DocumentTemplate template, string pdfPath)
        {
            if (File.Exists(pdfPath))
            {
                var state = SignatureService.ReadSigningState(pdfPath);

                if (state == null || state.Slots == null || !state.Slots.Any(s => s.Signed))
                    return DocumentStatus.Unsigned;

                var requiredIds = new HashSet<int>(
                    template.Signatures.Where(s => s.Required).Select(s => s.SignatureId));

                bool allRequiredSigned = requiredIds.Count == 0
                    || requiredIds.All(id => state.Slots.Any(s => s.SignatureId == id && s.Signed));

                return allRequiredSigned ? DocumentStatus.SignedUnsealed : DocumentStatus.PartialSigned;
            }

            // Check for _Semnat version (finalized and renamed)
            string noExt = Path.GetFileNameWithoutExtension(pdfPath);
            string ext = Path.GetExtension(pdfPath);
            string dir = Path.GetDirectoryName(pdfPath) ?? "";
            string semnatPath = Path.Combine(dir, noExt + "_Semnat" + ext);

            if (File.Exists(semnatPath))
                return SignatureService.IsDocumentSealed(semnatPath)
                    ? DocumentStatus.SignedSealed
                    : DocumentStatus.SignedUnsealed;

            return DocumentStatus.NotFound;
        }

        // ── Input File Resolution ─────────────────────────────────────────────────

        /// <summary>
        /// Resolves an InputFileName that may contain '*' to an exact path.
        /// - No wildcard → returns the combined path as-is (may not exist yet).
        /// - Wildcard → searches folder; returns the single match, or null if none found.
        ///   Throws if more than one match (ambiguous).
        /// </summary>
        // strict=true : throws if 2+ files found (used in Resolve when user actively loads)
        // strict=false: returns most recently modified file (used in status checks / folder populate)
        private static string FindInputFilePath(string folder, string inputFileName, bool strict = false)
        {
            if (!inputFileName.Contains('*'))
                return Path.Combine(folder, inputFileName);

            // Exclude finalized (_Semnat) files — they are handled separately
            string[] matches = Directory.GetFiles(folder, inputFileName)
                .Where(f => !Path.GetFileNameWithoutExtension(f)
                                .EndsWith("_Semnat", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matches.Length == 0) return null;
            if (matches.Length == 1) return matches[0];

            if (strict)
                throw new InvalidOperationException(
                    $"Gasite {matches.Length} documente cu pattern-ul '{inputFileName}' in folderul candidatului.\n Acest tip de document trebuie sa fie unic in dosarul personal.\n" +
                    $"Verificati dosarul personal si eliminati duplicatele.");

            // Non-strict: best guess for status display — most recently modified
            return matches.OrderByDescending(File.GetLastWriteTime).First();
        }

        // ── Resolve ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves a template against a candidate folder into a <see cref="ResolvedTemplate"/> ready for signing.
        /// For multi-doc templates, <paramref name="specificFilePath"/> must be provided.
        /// </summary>
        public static ResolvedTemplate Resolve(
            DocumentTemplate template,
            string candidateFolder,
            string signerName,
            string officialName = "",
            string specificFilePath = null)
        {
            string pdfPath;
            if (template.FileSystemBlock.IsMultiDocument)
            {
                pdfPath = specificFilePath
                    ?? throw new ArgumentException("Multi-document template requires a specific file path.", nameof(specificFilePath));
            }
            else
            {
                string inputFileName = template.FileSystemBlock.InputFileName;

                if (!inputFileName.Contains('*'))
                {
                    pdfPath = Path.Combine(candidateFolder, inputFileName);
                }
                else
                {
                    // Wildcard — must resolve to exactly one file when loading
                    pdfPath = FindInputFilePath(candidateFolder, inputFileName, strict: true);

                    if (pdfPath == null)
                    {
                        string semnatPattern = inputFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                            ? inputFileName.Substring(0, inputFileName.Length - 4) + "_Semnat.pdf"
                            : inputFileName + "_Semnat";

                        string[] semnatMatches = Directory.GetFiles(candidateFolder, semnatPattern);

                        if (semnatMatches.Length == 1)
                        {
                            string sp = semnatMatches[0];
                            if (SignatureService.IsDocumentSealed(sp))
                                throw new DocumentAlreadyFinalizedException(
                                    $"Documentul a fost finalizat, semnat \u015fi sigilat.\n\nFi\u015fierul:\n{sp}");
                            else
                                throw new DocumentSignedNotSealedException(
                                    $"Documentul a fost semnat dar NU sigilat digital \xc3\xaen Adobe.\n\nFi\u015fierul:\n{sp}", sp);
                        }

                        throw new FileNotFoundException(
                            $"Niciun document g\xc4\x83sit cu pattern-ul '{inputFileName}' \xc3\xaen:\n{candidateFolder}");
                    }
                }
            }

            if (!File.Exists(pdfPath))
            {
                string noExt = Path.GetFileNameWithoutExtension(pdfPath);
                string ext = Path.GetExtension(pdfPath);
                string dir = Path.GetDirectoryName(pdfPath) ?? candidateFolder;
                string semnatPath = Path.Combine(dir, noExt + "_Semnat" + ext);
                string displayName = Path.GetFileName(pdfPath);

                if (File.Exists(semnatPath))
                {
                    if (SignatureService.IsDocumentSealed(semnatPath))
                        throw new DocumentAlreadyFinalizedException(
                            $"Documentul \"{displayName}\" a fost finalizat, semnat si sigilat.\n\nFisierul se gaseste la:\n{semnatPath}");
                    else
                        throw new DocumentSignedNotSealedException(
                            $"Documentul \"{displayName}\" a fost semnat prin aplicatie dar NU a fost sigilat digital in Adobe.\n\n" +
                            $"Deschideti fisierul in Adobe Acrobat si aplicati semnatura digitala cu optiunea " +
                            $"\"Lock document after signing\" pentru a finaliza procesul.\n\nFisierul se gaseste la:\n{semnatPath}",
                            semnatPath);
                }

                throw new FileNotFoundException($"Document '{displayName}' not found in:\n{candidateFolder}");
            }

            PdfCompressor.CompressInPlace(pdfPath);

            int lastPage;
            using (var doc = PdfDocument.Load(pdfPath))
                lastPage = doc.PageCount;

            var slots = template.Signatures.Select(s => new SignatureSlot
            {
                SignatureId = s.SignatureId,
                SignerName = s.SignerName,
                ResolvedSignerName = s.SignerName
                    .Replace("{{SignerName}}", signerName)
                    .Replace("{{OfficialName}}", officialName ?? ""),
                Reason = s.Reason,
                Page = s.Page,
                Party = s.Party,
                OfficialRole = s.OfficialRole,
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
                Slots = slots
            };
        }
    }

    public class DocumentAlreadyFinalizedException : Exception
    {
        public DocumentAlreadyFinalizedException(string message) : base(message) { }
    }

    public class DocumentSignedNotSealedException : Exception
    {
        public string SemnatPath { get; }
        public DocumentSignedNotSealedException(string message, string semnatPath) : base(message)
            => SemnatPath = semnatPath;
    }
}