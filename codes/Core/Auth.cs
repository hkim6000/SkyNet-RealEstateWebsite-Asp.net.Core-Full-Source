using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RealEstate.Models;

namespace RealEstate.Core
{
    public sealed class Ctx
    {
        public Session S { get; set; } = new Session();
        public Staff U { get; set; } = new Staff();
    }

    public static class Auth
    {
        private static readonly JsonSerializerOptions ReadOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private static readonly SemaphoreSlim SessGate = new SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, List<DateTime>> Fails = new Dictionary<string, List<DateTime>>();

        private static string SessionFile => Path.Combine(Store.Root, "temp", "admin", "sessions.json");

        private static async Task<List<Session>> ReadSessions()
        {
            if (!File.Exists(SessionFile))
            {
                return new List<Session>();
            }
            try
            {
                return JsonSerializer.Deserialize<List<Session>>(await File.ReadAllTextAsync(SessionFile), ReadOpts) ?? new List<Session>();
            }
            catch (JsonException)
            {
                return new List<Session>();
            }
        }

        private static async Task WriteSessions(List<Session> list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionFile) ?? Store.Root);
            string tmp = SessionFile + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(list, WriteOpts));
            File.Move(tmp, SessionFile, true);
        }

        public static bool Locked(string login)
        {
            lock (Fails)
            {
                string k = login.ToLowerInvariant();
                if (!Fails.TryGetValue(k, out List<DateTime>? list))
                {
                    return false;
                }
                list.RemoveAll(f => f < DateTime.UtcNow.AddMinutes(-5));
                return list.Count >= 6;
            }
        }

        public static void Fail(string login)
        {
            lock (Fails)
            {
                string k = login.ToLowerInvariant();
                if (Fails.Count > 2000)
                {
                    Fails.Clear();
                }
                if (!Fails.TryGetValue(k, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Fails[k] = list;
                }
                list.Add(DateTime.UtcNow);
            }
        }

        public static async Task<string> Open(string user)
        {
            string tok = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                list.RemoveAll(x => x.Exp < DateTime.UtcNow);
                list.Add(new Session { Tok = tok, User = user, Exp = DateTime.UtcNow.AddHours(8) });
                await WriteSessions(list.OrderByDescending(x => x.Exp).Take(1000).ToList());
            }
            finally
            {
                SessGate.Release();
            }
            return tok;
        }

        public static async Task Close(string tok)
        {
            string t = Store.CleanId(tok);
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                if (list.RemoveAll(x => x.Tok == t) > 0)
                {
                    await WriteSessions(list);
                }
            }
            finally
            {
                SessGate.Release();
            }
        }

        public static async Task CloseUser(string user, string keep)
        {
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                if (list.RemoveAll(x => x.User == user && x.Tok != keep) > 0)
                {
                    await WriteSessions(list);
                }
            }
            finally
            {
                SessGate.Release();
            }
        }

        public static async Task<Ctx?> Check(string? tokValue)
        {
            string tok = Store.CleanId(tokValue);
            if (tok == string.Empty)
            {
                return null;
            }
            Session? s;
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                int before = list.Count;
                list.RemoveAll(x => x.Exp < DateTime.UtcNow);
                s = list.FirstOrDefault(x => x.Tok == tok);
                bool slide = s != null && s.Exp < DateTime.UtcNow.AddHours(7);
                if (s != null && slide)
                {
                    s.Exp = DateTime.UtcNow.AddHours(8);
                }
                if (list.Count != before || slide)
                {
                    await WriteSessions(list);
                }
            }
            finally
            {
                SessGate.Release();
            }
            if (s == null)
            {
                return null;
            }
            List<Staff> staff = await Store.LoadStaff();
            Staff? u = staff.FirstOrDefault(x => x.Id == s.User && x.Active);
            return u == null ? null : new Ctx { S = s, U = u };
        }

        public static string Mine(Ctx c)
        {
            return c.U.Own && !c.U.Admin && c.U.AgentId != string.Empty ? c.U.AgentId : string.Empty;
        }

        public static string Menu(string p, string page, Ctx c, SiteData site)
        {
            string own = Mine(c);
            DateTime today = Store.Today(site);
            List<(string Key, string Name, int Count)> items = new List<(string, string, int)> { ("Admin", "Dashboard", 0) };
            foreach (Right a in Access.Rights)
            {
                if (!Access.Can(c.U, a.Key, 'V'))
                {
                    continue;
                }
                int cnt = a.Key switch
                {
                    "leads" => site.Leads.Count(x => x.Status == "new" && (own == string.Empty || x.Agent == own || x.Agent == string.Empty)),
                    "viewings" => site.Viewings.Count(x => x.Status == "booked" && x.Start.Date == today && (own == string.Empty || x.Agent == own)),
                    _ => 0
                };
                items.Add((a.Page, Access.RightName(site, a), cnt));
            }
            if (!Access.Can(c.U, "activity", 'V'))
            {
                items.Add(("AdminActivity", "My activity", 0));
            }
            items.Add(("AdminAccount", "My account", 0));
            StringBuilder sb = new StringBuilder();
            foreach ((string k, string n, int cnt) in items)
            {
                sb.Append("<li><a class=\"" + p + "-al" + (k == page ? " " + p + "-on" : string.Empty) + "\" href=\"" + k + "\">" + Ui.Enc(n) + (cnt > 0 ? "<i class=\"" + p + "-cnt\">" + cnt.ToString(CultureInfo.InvariantCulture) + "</i>" : string.Empty) + "</a></li>");
            }
            return sb.ToString();
        }

        public static string Who(string p, Ctx c)
        {
            return Ui.Avatar(p, c.U) + "<span class=\"" + p + "-who-t\"><b>" + Ui.Enc(c.U.Name) + "</b><small>" + Ui.Enc(c.U.Title) + "</small></span>";
        }
    }
}
