using System;
using WacomSignaturePdf.Services;

namespace WacomSignaturePdf.Models
{
    // Holds all state for the currently open document.
    // Disposed when the document is unloaded or the form closes.
    internal sealed class DocumentSession : IDisposable
    {
        public ResolvedTemplate Resolved { get; }
        public SignatureService Service { get; }
        public int SignatureCount { get; set; }

        public DocumentSession(ResolvedTemplate resolved, SignatureService service)
        {
            Resolved = resolved ?? throw new ArgumentNullException(nameof(resolved));
            Service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void Dispose() => Service.Dispose();
    }
}
