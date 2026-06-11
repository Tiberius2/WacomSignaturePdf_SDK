using System.Collections.Generic;

namespace WacomSignaturePdf.Models
{
    // Result of resolving a DocumentTemplate against a candidate folder.
    // PdfPath is both source and destination — signed in place.
    public class ResolvedTemplate
    {
        public DocumentTemplate Template { get; set; }
        public string PdfPath { get; set; }
        public string ArtifactsPath { get; set; }
        public List<SignatureSlot> Slots { get; set; }
    }
}