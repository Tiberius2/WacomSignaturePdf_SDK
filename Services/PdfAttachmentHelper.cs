using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;

namespace WacomSignaturePdf.Services
{
    /// <summary>
    /// Low-level helpers for embedding and locating files inside a PDF's
    /// /Names → /EmbeddedFiles attachment tree.
    /// </summary>
    internal static class PdfAttachmentHelper
    {
        /// <summary>
        /// Embeds <paramref name="data"/> as a named file attachment in the PDF document.
        /// </summary>
        public static void AttachFile(
            PdfDocument document, string filename, string description, byte[] data)
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

        /// <summary>
        /// Returns the /Names array from the document's /EmbeddedFiles tree,
        /// or null if no attachments exist.
        /// </summary>
        public static PdfArray GetEmbeddedFilesArray(PdfDocument document)
        {
            PdfDictionary catalog = document.Internals.Catalog;
            if (!catalog.Elements.ContainsKey("/Names")) return null;

            var namesDict = catalog.Elements["/Names"] as PdfDictionary;
            if (namesDict == null || !namesDict.Elements.ContainsKey("/EmbeddedFiles")) return null;

            var embeddedFiles = namesDict.Elements["/EmbeddedFiles"] as PdfDictionary;
            if (embeddedFiles == null || !embeddedFiles.Elements.ContainsKey("/Names")) return null;

            return embeddedFiles.Elements["/Names"] as PdfArray;
        }
    }
}