// Program.cs din WacomDebugRunner
using System;
using System.Windows.Forms;
using WacomSignaturePdf.Forms;

namespace WacomDebugRunner
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var mode = ShellForm.LoadLastMode();
            using (var shell = new ShellForm(
                personId: "76",
                signerName: "Tiberiu Test - Prezentare",
                officialName: "Admin Test",
                officialRole: "ADMIN",
                initialMode: mode))
            {
                Application.Run(shell);
            }
        }
    }
}