using Softone;
using System;
using System.Threading;
using System.Windows.Forms;
using WacomSignaturePdf.Forms;

namespace WacomSignaturePdf
{
    [WorksOn("PRSNIN")]
    public class Program : TXCode
    {
        private static MainForm _activeForm;

        public override void Initialize()
        {
            base.Initialize();
        }

        public override object ExecCommand(int Cmd)
        {
            if (Cmd != 4000500)
                return null;

            try
            {
                // If the form is already open, just bring it to the front
                if (_activeForm != null && !_activeForm.IsDisposed)
                {
                    _activeForm.Invoke(new Action(() =>
                    {
                        if (_activeForm.WindowState == FormWindowState.Minimized)
                            _activeForm.WindowState = FormWindowState.Normal;
                        _activeForm.Activate();
                    }));
                    return base.ExecCommand(Cmd);
                }

                string officialName = string.Empty;
                try
                {
                    var currentUserId = XSupport.ConnectionInfo.UserId;
                    var userResult = XSupport.GetSQLDataSet(
                        $"SELECT NAME FROM USERS WHERE USERS.USERS = {currentUserId}");
                    if (userResult != null && userResult.Count > 0)
                        officialName = userResult[0, "NAME"]?.ToString() ?? string.Empty;
                }
                catch { }

                var prsnTbl = XModule.GetTable("PRSN");
                if (prsnTbl == null || prsnTbl.Current == null)
                    return base.ExecCommand(Cmd);

                string personId = prsnTbl.Current["PRSN"]?.ToString() ?? string.Empty;
                string namePart = prsnTbl.Current["NAME"]?.ToString() ?? string.Empty;
                string name2Part = prsnTbl.Current["NAME2"]?.ToString() ?? string.Empty;
                string signerName = $"{namePart} {name2Part}".Trim();

                var thread = new Thread(() =>
                {
                    try
                    {
                        using (var form = new MainForm(personId, signerName, officialName))
                        {
                            _activeForm = form;
                            form.ShowDialog();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"DocumentSigner error:\n{ex}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        _activeForm = null;
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

            return base.ExecCommand(Cmd);
        }
    }
}