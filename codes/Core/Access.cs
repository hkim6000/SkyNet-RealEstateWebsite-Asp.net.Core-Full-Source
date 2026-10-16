using System.Security.Cryptography;
using System.Text;
using RealEstate.Models;

namespace RealEstate.Core
{
    public sealed class Right
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Page { get; init; } = string.Empty;
        public string Extra { get; init; } = string.Empty;
        public bool AdminOnly { get; init; }
        public bool NoEdit { get; init; }
    }

    public static class Access
    {
        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        public static readonly Right[] Rights =
        {
            new Right { Key = "listings", Name = "Listings", Page = "AdminListings", Extra = "Publish" },
            new Right { Key = "leads", Name = "Leads", Page = "AdminLeads", Extra = "Assign" },
            new Right { Key = "viewings", Name = "Viewings", Page = "AdminViewings" },
            new Right { Key = "areas", Name = "Neighborhoods", Page = "AdminAreas" },
            new Right { Key = "agents", Name = "Agents", Page = "AdminAgents" },
            new Right { Key = "activity", Name = "Activity log", Page = "AdminActivity", NoEdit = true },
            new Right { Key = "settings", Name = "Settings", Page = "AdminSettings", AdminOnly = true },
            new Right { Key = "users", Name = "Users", Page = "AdminUsers", AdminOnly = true }
        };

        public static readonly Dictionary<string, string> AgentDefaults = new Dictionary<string, string> { { "listings", "VE" }, { "leads", "VE" }, { "viewings", "VE" }, { "areas", "V" } };

        public static Right? RightOf(string key)
        {
            return Rights.FirstOrDefault(a => a.Key == key);
        }

        public static string RightName(SiteData site, Right a)
        {
            return a.Key == "agents" ? site.Settings.AgentsWord : a.Name;
        }

        public static string Level(Staff u, string area)
        {
            if (u.Admin)
            {
                return "VEX";
            }
            Right? a = RightOf(area);
            if (a == null || a.AdminOnly)
            {
                return string.Empty;
            }
            string p = u.Perms.TryGetValue(area, out string? v) ? v ?? string.Empty : string.Empty;
            string r = string.Empty;
            if (p.Contains('V') || p.Contains('E') || p.Contains('X'))
            {
                r += "V";
            }
            if (p.Contains('E') && !a.NoEdit)
            {
                r += "E";
            }
            if (p.Contains('X') && p.Contains('E') && a.Extra != string.Empty)
            {
                r += "X";
            }
            return r;
        }

        public static bool Can(Staff u, string area, char letter)
        {
            return Level(u, area).Contains(letter);
        }

        public static string Clean(string area, string value)
        {
            Right? a = RightOf(area);
            if (a == null || a.AdminOnly)
            {
                return string.Empty;
            }
            string v = value ?? string.Empty;
            string r = string.Empty;
            if (v.Contains('V') || v.Contains('E'))
            {
                r = "V";
            }
            if (v.Contains('E') && !a.NoEdit)
            {
                r += "E";
                if (v.Contains('X') && a.Extra != string.Empty)
                {
                    r += "X";
                }
            }
            return r;
        }

        public static string NewPassword()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 12; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    sb.Append('-');
                }
                sb.Append(Letters[RandomNumberGenerator.GetInt32(Letters.Length)]);
            }
            return sb.ToString();
        }

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] dk = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(dk);
        }

        public static bool Verify(string password, string stored)
        {
            string[] parts = (stored ?? string.Empty).Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out int iter) || iter < 10000 || iter > 1000000)
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] want = Convert.FromBase64String(parts[3]);
                byte[] got = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iter, HashAlgorithmName.SHA256, want.Length);
                return CryptographicOperations.FixedTimeEquals(got, want);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
