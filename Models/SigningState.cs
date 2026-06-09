using System;
using System.Collections.Generic;

namespace WacomSignaturePdf.Models
{
    public class SigningState
    {
        /// <summary>
        /// SHA-256 of the original clean PDF — computed once on the first machine
        /// and reused by all subsequent machines for audit consistency.
        /// </summary>
        public string OriginalDocumentHash { get; set; }

        /// <summary>
        /// Sursa fluxului de semnare: "Template" sau "FreeForm".
        /// Folosit pentru a preveni deschiderea unui document Template in modul FreeForm.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// True daca documentul a fost finalizat si exportat.
        /// Documentele finalizate nu pot fi redeschise in aplicatie.
        /// </summary>
        public bool Finalized { get; set; }

        /// <summary>
        /// Numele original al fișierului sursă (fără sufix _InProces).
        /// Folosit pentru a localiza backup-ul din Documente In Original.
        /// </summary>
        public string OriginalFileName { get; set; }

        public List<SigningStateEntry> Slots { get; set; } = new List<SigningStateEntry>();
    }


    // Represents the signing state of a single signature slot, as reported by the client machines.
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

        // FreeForm-only: coordonatele si configuratia slotului
        // Null pentru documentele Template
        public FreeFormSlotGeometry FreeForm { get; set; }
    }

    /// <summary>
    /// Geometria si configuratia unui slot de semnatura libera.
    /// Stocata in signing-state.json in loc de freeform-slots.json separat.
    /// </summary>
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