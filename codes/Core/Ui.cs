using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RealEstate.Models;

namespace RealEstate.Core
{
    public static class Ui
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        public static readonly (string Key, string Name)[] Kinds = { ("house", "House"), ("townhouse", "Townhouse"), ("condo", "Condo"), ("loft", "Loft") };

        public static readonly (string Key, string Name)[] ListingStatuses = { ("draft", "Draft"), ("active", "For sale"), ("pending", "Under offer"), ("sold", "Sold"), ("rented", "Rented"), ("off", "Off market") };

        public static readonly (string Key, string Name)[] LeadKinds = { ("viewing", "Viewing request"), ("question", "Question"), ("valuation", "Valuation"), ("contact", "Message") };

        public static readonly (string Key, string Name)[] LeadStatuses = { ("new", "New"), ("contacted", "Contacted"), ("booked", "Viewing booked"), ("closed", "Closed") };

        public static readonly (string Key, string Name)[] ViewingStatuses = { ("booked", "Booked"), ("done", "Done"), ("noshow", "No-show"), ("cancelled", "Cancelled") };

        public static string Enc(string? v)
        {
            return System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Attr(string? v)
        {
            return System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Js(string? v)
        {
            return (v ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "&quot;").Replace("<", "\\x3c").Replace("\n", " ").Replace("\r", string.Empty);
        }

        public static string Clip(string? value, int max)
        {
            string v = (value ?? string.Empty).Trim();
            return v.Length > max ? v.Substring(0, max) : v;
        }

        public static string Plural(int n, string one, string many)
        {
            return n.ToString("#,##0", Inv) + " " + (n == 1 ? one : many);
        }

        public static string Iso(DateTime d)
        {
            return d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);
        }

        public static string Ago(DateTime d)
        {
            TimeSpan t = DateTime.UtcNow - d.ToUniversalTime();
            if (t.TotalMinutes < 1)
            {
                return "Just now";
            }
            if (t.TotalMinutes < 60)
            {
                int m = (int)t.TotalMinutes;
                return m + (m == 1 ? " min ago" : " mins ago");
            }
            if (t.TotalHours < 24)
            {
                int h = (int)t.TotalHours;
                return h + (h == 1 ? " hour ago" : " hours ago");
            }
            return d.ToUniversalTime().ToString("d MMM yyyy", Inv);
        }

        public static string Time(DateTime d, string mode = "ago")
        {
            DateTime u = DateTime.SpecifyKind(d, DateTimeKind.Utc);
            return "<time datetime=\"" + Iso(u) + "\" data-t=\"" + mode + "\">" + (mode == "ago" ? Ago(u) : u.ToString("d MMMM yyyy, HH:mm 'UTC'", Inv)) + "</time>";
        }

        public static string Initials(string name)
        {
            string[] parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "?";
            }
            string a = parts[0].Substring(0, 1);
            string b = parts.Length > 1 ? parts[^1].Substring(0, 1) : string.Empty;
            return (a + b).ToUpperInvariant();
        }

        public static string Hue(string id)
        {
            int h = 0;
            foreach (char c in id ?? string.Empty)
            {
                h = (h * 31 + c) % 360;
            }
            return h.ToString(Inv);
        }

        public static string Avatar(string p, Staff? u)
        {
            string name = u?.Name ?? "Staff";
            return "<span class=\"" + p + "-av\" style=\"--h:" + Hue(u?.Id ?? name) + "\" aria-hidden=\"true\">" + Enc(Initials(name)) + "</span>";
        }

        public static string Face(string p, TeamMember? a, string cls = "")
        {
            string c = p + "-face" + (cls != string.Empty ? " " + cls : string.Empty);
            if (a != null && a.Photo != string.Empty)
            {
                return "<img class=\"" + c + "\" src=\"" + Attr(a.Photo) + "\" alt=\"\" loading=\"lazy\" width=\"480\" height=\"600\">";
            }
            return "<span class=\"" + c + " " + p + "-noface\" style=\"--h:" + Hue(a?.Id ?? "x") + "\" aria-hidden=\"true\">" + Enc(Initials(a?.Name ?? "?")) + "</span>";
        }

        public static string Pager(string p, int page, int size, int total, string one, string many, string js)
        {
            if (total == 0)
            {
                return string.Empty;
            }
            int pages = Math.Max(1, (total + size - 1) / size);
            return "<div class=\"" + p + "-pager\"><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page - 1) + ")\"" + (page <= 1 ? " disabled" : string.Empty) + ">&larr; Previous</button><span>Page " + page + " of " + pages + " &middot; " + Plural(total, one, many) + "</span><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page + 1) + ")\"" + (page >= pages ? " disabled" : string.Empty) + ">Next &rarr;</button></div>";
        }

        public static int PageOf(string value, int size, int total)
        {
            int pages = Math.Max(1, (total + size - 1) / size);
            return Math.Clamp(int.TryParse(value, out int n) ? n : 1, 1, pages);
        }

        public static Dictionary<string, Staff> StaffMap(List<Staff> list)
        {
            Dictionary<string, Staff> d = new Dictionary<string, Staff>();
            foreach (Staff s in list)
            {
                d[s.Id] = s;
            }
            return d;
        }

        public static string Money(decimal v)
        {
            return (v < 0 ? "&minus;$" : "$") + Math.Abs(v).ToString(v == Math.Floor(v) ? "#,##0" : "#,##0.00", Inv);
        }

        public static string Whole(decimal v)
        {
            return (v < 0 ? "&minus;$" : "$") + Math.Round(Math.Abs(v), 0).ToString("#,##0", Inv);
        }

        public static string Short(decimal v)
        {
            if (v >= 1000000m)
            {
                return "$" + (v / 1000000m).ToString(v % 1000000m == 0 ? "0" : "0.0#", Inv) + "M";
            }
            if (v >= 10000m)
            {
                return "$" + Math.Round(v / 1000m).ToString("0", Inv) + "K";
            }
            return "$" + v.ToString("#,##0", Inv);
        }

        public static string Num(int v)
        {
            return v.ToString("#,##0", Inv);
        }

        public static string Baths(decimal b)
        {
            return b.ToString(b == Math.Floor(b) ? "0" : "0.#", Inv);
        }

        public static string Beds(int b)
        {
            return b == 0 ? "Studio" : Plural(b, "bed", "beds");
        }

        public static string Day(DateTime d, bool year = true)
        {
            return d.ToString(year ? "ddd d MMM yyyy" : "ddd d MMM", Inv);
        }

        public static string LongDay(DateTime d)
        {
            return d.ToString("dddd, d MMMM yyyy", Inv);
        }

        public static string Ymd(DateTime d)
        {
            return d.ToString("yyyy-MM-dd", Inv);
        }

        public static DateTime? ParseDay(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd", Inv, DateTimeStyles.None, out DateTime d) ? DateTime.SpecifyKind(d.Date, DateTimeKind.Unspecified) : null;
        }

        public static DateTime? ParseStart(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd'T'HH:mm", Inv, DateTimeStyles.None, out DateTime d) ? DateTime.SpecifyKind(d, DateTimeKind.Unspecified) : null;
        }

        public static string Stamp(DateTime d)
        {
            return d.ToString("yyyy-MM-dd'T'HH:mm", Inv);
        }

        public static string Hm(SiteData s, DateTime d)
        {
            return d.ToString(s.Settings.Clock24 ? "HH:mm" : "h:mm tt", Inv);
        }

        public static string HmText(SiteData s, string hm)
        {
            TimeSpan? t = Store.Hm(hm);
            return t == null ? string.Empty : Hm(s, DateTime.Today.Add(t.Value));
        }

        public static string Thumb(string full)
        {
            return full.Contains("/full/", StringComparison.Ordinal) ? full.Replace("/full/", "/thumbs/") : full;
        }

        public static string Pic(string src, string alt, string cls, bool thumb = false, bool eager = false)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            return "<img class=\"" + cls + "\" src=\"" + Attr(thumb ? Thumb(src) : src) + "\" alt=\"" + Attr(alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + " width=\"" + (thumb ? "640" : "1600") + "\" height=\"" + (thumb ? "427" : "1067") + "\">";
        }

        public static string CleanPhone(string? v)
        {
            return Regex.Replace(Clip(v, 30), "[^0-9+()\\-. ]", string.Empty).Trim();
        }

        public static string Digits(string? v)
        {
            return Regex.Replace(v ?? string.Empty, "[^0-9]", string.Empty);
        }

        public static bool Email(string v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");
        }

        public static bool SafeColor(string v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^#[0-9a-fA-F]{6}$");
        }

        public static string Opts(IEnumerable<(string Value, string Text)> items, string cur)
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string v, string t) in items)
            {
                sb.Append("<option value=\"" + Attr(v) + "\"" + (v == cur ? " selected" : string.Empty) + ">" + Enc(t) + "</option>");
            }
            return sb.ToString();
        }

        public static string DataList(string id, IEnumerable<string> values)
        {
            StringBuilder sb = new StringBuilder("<datalist id=\"" + id + "\">");
            foreach (string v in values.Where(x => x != string.Empty).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append("<option value=\"" + Attr(v) + "\"></option>");
            }
            sb.Append("</datalist>");
            return sb.ToString();
        }

        public static string NameOf((string Key, string Name)[] list, string key)
        {
            foreach ((string k, string n) in list)
            {
                if (k == key)
                {
                    return n;
                }
            }
            return key;
        }

        public static string StatusText(Property l)
        {
            if (l.Status == "active")
            {
                return l.Deal == "rent" ? "For rent" : "For sale";
            }
            return NameOf(ListingStatuses, l.Status);
        }

        public static string StatusPill(string p, Property l)
        {
            return "<span class=\"" + p + "-st " + p + "-st-" + l.Status + "\">" + StatusText(l) + "</span>";
        }

        public static string Pill(string p, (string Key, string Name)[] list, string key)
        {
            return "<span class=\"" + p + "-st " + p + "-st-" + key + "\">" + Enc(NameOf(list, key)) + "</span>";
        }

        public static string PriceText(Property l)
        {
            return Money(l.Price) + (l.Deal == "rent" ? "<small>/mo</small>" : string.Empty);
        }

        public static string PricePlain(Property l)
        {
            return "$" + l.Price.ToString("#,##0", Inv) + (l.Deal == "rent" ? "/mo" : string.Empty);
        }

        public static bool Public(Property l)
        {
            return l.Status is "active" or "pending" or "sold" or "rented";
        }

        public static bool Live(Property l)
        {
            return l.Status is "active" or "pending";
        }

        public static Models.Area? AreaOf(SiteData s, string key)
        {
            return s.Areas.FirstOrDefault(a => a.Key == key);
        }

        public static string AreaName(SiteData s, string key)
        {
            return AreaOf(s, key)?.Name ?? key;
        }

        public static TeamMember? AgentOf(SiteData s, string id)
        {
            return s.Agents.FirstOrDefault(a => a.Id == id);
        }

        public static string AgentName(SiteData s, string id)
        {
            return AgentOf(s, id)?.Name ?? "Unassigned";
        }

        public static Property? ListingOf(SiteData s, string id)
        {
            return s.Listings.FirstOrDefault(x => x.Id == id);
        }

        public static string KindName(string k)
        {
            return NameOf(Kinds, k);
        }

        public static (DateTime? From, DateTime? To) Period(DateTime today, string period, string from, string to, bool future = false)
        {
            return period switch
            {
                "today" => (today, today),
                "7" => future ? (today, today.AddDays(6)) : (today.AddDays(-6), today),
                "30" => future ? (today, today.AddDays(29)) : (today.AddDays(-29), today),
                "range" => (ParseDay(from), ParseDay(to)),
                _ => (null, null)
            };
        }

        public static bool InPeriod(DateTime d, DateTime? from, DateTime? to)
        {
            return (from == null || d.Date >= from.Value) && (to == null || d.Date <= to.Value);
        }

        public static string PeriodBar(string p, string js, string cur, string from, string to, bool future = false)
        {
            (string k, string n)[] chips = { ("today", "Today"), ("7", future ? "Next 7 days" : "7 days"), ("30", future ? "Next 30 days" : "30 days"), ("all", "All") };
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-period\"><div class=\"" + p + "-chips\">");
            foreach ((string k, string n) in chips)
            {
                sb.Append("<button type=\"button\" class=\"" + p + "-chip" + (k == cur ? " " + p + "-act" : string.Empty) + "\" onclick=\"" + js + "('" + k + "')\">" + n + "</button>");
            }
            sb.Append("</div><label class=\"" + p + "-pf\"><span>From</span><input type=\"date\" id=\"" + p + "-from\" value=\"" + Attr(cur == "range" ? from : string.Empty) + "\" onchange=\"" + js + "('range')\"></label><label class=\"" + p + "-pf\"><span>To</span><input type=\"date\" id=\"" + p + "-to\" value=\"" + Attr(cur == "range" ? to : string.Empty) + "\" onchange=\"" + js + "('range')\"></label><input type=\"hidden\" id=\"" + p + "-per\" value=\"" + Attr(cur) + "\"></div>");
            return sb.ToString();
        }

        public static List<string> Images(string folder)
        {
            string dir = Path.Combine(Store.Root, "images", folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir))
            {
                return new List<string>();
            }
            return Directory.GetFiles(dir).Where(f => Regex.IsMatch(f, "\\.(webp|jpg|jpeg|png)$", RegexOptions.IgnoreCase)).Select(f => "images/" + folder + "/" + Path.GetFileName(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();
        }

        public static bool SafeImage(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png)$") && !src!.Contains("..") && File.Exists(Path.Combine(Store.Root, src.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static List<string> SplitLines(string? v, int max, int each)
        {
            return (v ?? string.Empty).Split('\n').Select(x => Clip(x, each)).Where(x => x != string.Empty).Take(max).ToList();
        }

        public static string Words(SiteData s, string text)
        {
            return text.Replace("{agent}", s.Settings.AgentWord.ToLowerInvariant()).Replace("{Agent}", s.Settings.AgentWord).Replace("{agents}", s.Settings.AgentsWord.ToLowerInvariant()).Replace("{Agents}", s.Settings.AgentsWord);
        }
    }
}
