using Softone;
using System;
using System.Threading;
using System.Windows.Forms;
using WacomSignaturePdf.Config;
using WacomSignaturePdf.Forms;

namespace WacomSignaturePdf
{
    [WorksOn("GENERAL")]
    public class S1 : TXCode
    {
        public static XSupport xSupp;
        public override void Initialize() { base.Initialize(); xSupp = XSupport; }
    }

    [WorksOn("PRSNIN")]
    public class Program : TXCode
    {
        public override object ExecCommand(int command)
        {
            if (command != 4000500) return null;

            try
            {
                // Bring existing window to front if already open
                if (ShellForm.Instance != null && !ShellForm.Instance.IsDisposed)
                {
                    ShellForm.Instance.Invoke(new Action(() =>
                    {
                        if (ShellForm.Instance.WindowState == FormWindowState.Minimized)
                            ShellForm.Instance.WindowState = FormWindowState.Normal;
                        ShellForm.Instance.Activate();
                    }));
                    return base.ExecCommand(command);
                }

                string officialName = string.Empty;
                string officialRole = string.Empty;
                try
                {
                    if (int.TryParse(XSupport.ConnectionInfo.UserId.ToString(), out int userId))
                    {
                        var result = XSupport.GetSQLDataSet($"SELECT NAME FROM USERS WHERE USERS.USERS = {userId}");
                        if (result?.Count > 0)
                            officialName = result[0, "NAME"]?.ToString() ?? string.Empty;
                        officialRole = RoleHelper.GetRole(userId);
                    }
                }
                catch { }

                var personTable = XModule.GetTable("PRSN");
                if (personTable?.Current == null) return base.ExecCommand(command);

                string personId = personTable.Current["PRSN"]?.ToString() ?? string.Empty;
                string signerName = $"{personTable.Current["NAME"]} {personTable.Current["NAME2"]}".Trim();

                var thread = new Thread(() =>
                {
                    try
                    {
                        using (var shell = new ShellForm(personId, signerName, officialName, officialRole,
                                                         ShellForm.LoadLastMode()))
                            shell.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"DocumentSigner error:\n{ex}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
            }
            catch (Exception ex)
            {
                XSupport.Warning($"DocumentSigner ExecCommand error: {ex}");
            }

            return base.ExecCommand(command);
        }
    }
}