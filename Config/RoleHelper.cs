using System.Collections.Generic;

namespace WacomSignaturePdf.Config
{
    // Maps Softone user IDs to OfficialRole strings used in signature slot filtering.
    // Users not listed here see ALL official signatures (no role filtering).
    public static class RoleHelper
    {
        private static readonly Dictionary<int, string> _userRoles = new Dictionary<int, string>
        {
            {  13, "ADMIN"    },
            { 23, "HR"       },
            {   7, "DIR. EC." },
            { 111, "HR"       },
            { 108, "HR"       },
            { 110, "DIR. EC." },
            { 12001, "HR" },
            { 12000, "DIR. EC." },
        };

        public static string GetRole(int userId) =>
            _userRoles.TryGetValue(userId, out string role) ? role : string.Empty;
    }
}
