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
    }
}