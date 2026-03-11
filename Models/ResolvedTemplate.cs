using System.Collections.Generic;

namespace WacomSignaturePdf.Models
{
    /// <summary>
    /// Result of resolving a DocumentTemplate against a candidate folder.
    /// PdfPath is both the source and destination — we sign in place.
    /// </summary>
    public class ResolvedTemplate
    {
        public DocumentTemplate Template { get; set; }
        public string PdfPath { get; set; }
        public string ArtifactsPath { get; set; }
        public List<SignatureSlot> Slots { get; set; }
    }
}