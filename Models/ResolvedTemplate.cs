using System.Collections.Generic;

namespace WacomSignaturePdf.Models
{
    /// <summary>
    /// Result of resolving a DocumentTemplate against a candidate folder.
    /// All paths are absolute. All slot variables are substituted.
    /// </summary>
    public class ResolvedTemplate
    {
        public DocumentTemplate Template { get; set; }
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public string ArtifactsPath { get; set; }
        public List<SignatureSlot> Slots { get; set; }
    }
}