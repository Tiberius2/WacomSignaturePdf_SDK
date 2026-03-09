using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PdfiumViewer;
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

        // ── Candidate folder ──────────────────────────────────────────────────────

        /// <summary>
        /// Finds the candidate folder matching "ID - Name" or "ID-Name" pattern.
        /// Throws if zero or more than one match is found.
        /// </summary>
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

        /// <summary>
        /// Extracts the candidate name from "ID - Name" folder naming convention.
        /// </summary>
        public static string GetCandidateName(string candidateFolder)
        {
            string name = Path.GetFileName(candidateFolder);
            int idx = name.IndexOf(" - ", StringComparison.Ordinal);
            return idx >= 0 ? name.Substring(idx + 3).Trim() : name;
        }

        // ── Resolve ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves a template against a specific candidate folder.
        /// Substitutes {{SignerName}}, {{OfficialName}}, and {{LastPage}} in all slots.
        /// </summary>
        public static ResolvedTemplate Resolve(
            DocumentTemplate template,
            string candidateFolder,
            string signerName,
            string officialName = "")
        {
            string inputPath = Path.Combine(candidateFolder, template.FileSystemBlock.InputFileName);
            string outputPath = Path.Combine(candidateFolder, template.FileSystemBlock.OutputFileName);
            string artifactsPath = Path.Combine(candidateFolder, "SignatureArtifacts");

            if (!File.Exists(inputPath))
                throw new FileNotFoundException(
                    $"Document '{template.FileSystemBlock.InputFileName}' not found in:\n{candidateFolder}");

            int lastPage;
            using (var doc = PdfDocument.Load(inputPath))
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
                InputPath = inputPath,
                OutputPath = outputPath,
                ArtifactsPath = artifactsPath,
                Slots = slots
            };
        }
    }
}