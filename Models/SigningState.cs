using System;
using System.Collections.Generic;

namespace WacomSignaturePdf.Models
{
    public class SigningState
    {
        // SHA-256 of the original clean PDF — computed once, reused across all machines for audit consistency.
        public string OriginalDocumentHash { get; set; }

        // "Template" or "FreeForm" — prevents opening a Template document in FreeForm mode.
        public string Source { get; set; }

        // Finalized documents cannot be reopened.
        public bool Finalized { get; set; }

        // Original filename before the _InProces suffix — used to locate the backup in "Documente In Original".
        public string OriginalFileName { get; set; }

        public List<SigningStateEntry> Slots { get; set; } = new List<SigningStateEntry>();
    }

    public class SigningStateEntry
    {
        public int SignatureId { get; set; }
        public string Party { get; set; }
        public string SignerName { get; set; }
        public string Reason { get; set; }
        public bool Signed { get; set; }

        // Populated only when Signed = true
        public DateTime? SignedAt { get; set; }
        public string MachineName { get; set; }
        public string ActualSignerName { get; set; }

        // FreeForm only — null for Template documents
        public FreeFormSlotGeometry FreeForm { get; set; }
    }

    // Geometry and config for a free-form signature slot, stored in signing-state.json.
    public class FreeFormSlotGeometry
    {
        public int Page { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float W { get; set; }
        public float H { get; set; }
        public string OfficialRole { get; set; }
        public bool Required { get; set; }
        public bool Biometric { get; set; }
    }
}