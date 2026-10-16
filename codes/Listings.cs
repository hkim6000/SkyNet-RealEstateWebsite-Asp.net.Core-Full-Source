using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class Listings : WebPage
    {
        private sealed class View
        {
            public string Title { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Main { get; set; } = string.Empty;
            public string Nav { get; set; } = string.Empty;
        }

        private string Q(string key, int max = 80)
        {
            return Ui.Clip(QueryValue(key), max);
        }

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            View v = await Render(site);
            HtmlDoc.SetTitle((v.Title != string.Empty ? v.Title + " | " : string.Empty) + site.Settings.Name);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", v.Desc != string.Empty ? v.Desc : site.Settings.Tagline);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_name}", Ui.Enc(site.Settings.Name))
                .Replace("{plhd_town}", Ui.Enc(site.Settings.Town))
                .Replace("{plhd_tagline}", Ui.Enc(site.Settings.Tagline))
                .Replace("{plhd_addr}", Ui.Enc(site.Settings.Address))
                .Replace("{plhd_phone}", Ui.Enc(site.Settings.Phone))
                .Replace("{plhd_tel}", Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)))
                .Replace("{plhd_email}", Ui.Enc(site.Settings.Email))
                .Replace("{plhd_license}", Ui.Enc(site.Settings.License))
                .Replace("{plhd_agents}", Ui.Enc(site.Settings.AgentsWord))
                .Replace("{plhd_today}", TodayLine(site))
                .Replace("{plhd_areas}", AreaLinks(site))
                .Replace("{plhd_theme}", site.Settings.Theme == "dark" ? "dark" : "light")
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ls-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "ls", v.Nav, false))
                .Replace("{plhd_drawer}", Nav(site, "ls", v.Nav, true))
                .Replace("{plhd_main}", v.Main);
        }

        private static string Nav(SiteData site, string p, string on, bool drawer)
        {
            (string Href, string Key, string Name)[] links = { ("Listings?deal=sale", "buy", "Buy"), ("Listings?deal=rent", "rent", "Rent"), ("Sell", "sell", "Sell"), ("Mortgage", "mortgage", "Mortgage"), ("Agents", "agents", site.Settings.AgentsWord), ("Contact", "contact", "Contact") };
            StringBuilder sb = new StringBuilder();
            if (drawer)
            {
                sb.Append("<a href=\"Home\"" + (on == "home" ? " class=\"" + p + "-on\"" : string.Empty) + ">Home</a>");
            }
            foreach ((string href, string k, string n) in links)
            {
                sb.Append("<a href=\"" + href + "\"" + (k == on ? " class=\"" + p + "-on\" aria-current=\"page\"" : string.Empty) + ">" + Ui.Enc(n) + "</a>");
            }
            if (drawer)
            {
                sb.Append("<a href=\"Listings?view=map\">Map search</a><a href=\"Listings?saved=1\">Saved homes</a>");
            }
            return sb.ToString();
        }

        private static string AreaLinks(SiteData site)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Models.Area a in site.Areas.Where(x => x.Active).OrderBy(x => x.Order))
            {
                sb.Append("<a href=\"Listings?area=" + Uri.EscapeDataString(a.Key) + "\">" + Ui.Enc(a.Name) + "</a>");
            }
            return sb.ToString();
        }

        private static string TodayLine(SiteData site)
        {
            DateTime now = Store.Now(site);
            OpenDay? d = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)now.DayOfWeek);
            TimeSpan? from = d != null && d.Open ? Store.Hm(d.Start) : null;
            TimeSpan? to = d != null && d.Open ? Store.Hm(d.End) : null;
            if (from != null && to != null && now.TimeOfDay >= from.Value && now.TimeOfDay < to.Value)
            {
                return "<i></i>Office open today until " + Ui.HmText(site, d!.End);
            }
            if (from != null && now.TimeOfDay < from.Value)
            {
                return "<i class=\"ls-shut\"></i>Office opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"ls-shut\"></i>Office closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"ls-shut\"></i>Office closed";
        }

        private static string HoursRows(SiteData site, string p)
        {
            int today = (int)Store.Today(site).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? Ui.HmText(site, o.Start) + " &ndash; " + Ui.HmText(site, o.End) : "Closed";
                sb.Append("<span class=\"" + p + "-hrow" + (d == today ? " " + p + "-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Subscribe()
        {
            ApiResponse response = new ApiResponse();
            string mail = Ui.Clip(GetDataValue("nlmail"), 120);
            if (!Ui.Email(mail))
            {
                response.SetElementContents("ls-e-nlmail", "Enter an email address like name@example.com.");
                return response;
            }
            bool added = await Store.AddSubscriber(mail);
            response.ExecuteScript("ListingsJs.subscribed('" + (added ? "Thanks. You will hear from us when new homes are listed." : "You are already on the list.") + "');");
            return response;
        }

        public async Task<ApiResponse> Heart()
        {
            ApiResponse response = new ApiResponse();
            string id = Ui.Clip(GetDataValue("hid"), 20);
            bool on = GetDataValue("hon") == "1";
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Property? l = site.Listings.FirstOrDefault(x => x.Id == id && Ui.Public(x));
                if (l != null)
                {
                    l.Saves = Math.Max(0, l.Saves + (on ? 1 : -1));
                    await Store.SaveSite(site);
                }
            }
            return response;
        }

        private static string Facts(Property l)
        {
            return "<span>" + (l.Beds == 0 ? "Studio" : l.Beds + " bd") + "</span><span>" + Ui.Baths(l.Baths) + " ba</span><span>" + Ui.Num(l.Sqft) + " sq ft</span>";
        }

        private static string FactsPlain(Property l)
        {
            return (l.Beds == 0 ? "Studio" : l.Beds + " bd") + " · " + Ui.Baths(l.Baths) + " ba · " + Ui.Num(l.Sqft) + " sq ft";
        }

        private static OpenHouse? NextOpen(Property l, DateTime today)
        {
            return l.Status == "active" ? l.Opens.Where(o => o.Date.Date >= today).OrderBy(o => o.Date).FirstOrDefault() : null;
        }

        private static (string Cls, string Text) Badge(SiteData site, Property l, DateTime today)
        {
            if (l.Status is "pending" or "sold" or "rented")
            {
                return (l.Status, Ui.StatusText(l));
            }
            OpenHouse? o = NextOpen(l, today);
            if (o != null && o.Date.Date <= today.AddDays(7))
            {
                return ("open", "Open " + (o.Date.Date == today ? "today" : o.Date.ToString("ddd", Ui.Inv)) + " " + Ui.HmText(site, o.Start).Replace(":00", string.Empty));
            }
            if (l.Listed.Date >= today.AddDays(-7))
            {
                return ("new", "New");
            }
            if (l.History.Count > 1 && l.History[^1].Event == "Price cut")
            {
                return ("cut", "Price cut");
            }
            return (string.Empty, string.Empty);
        }

        private static string Card(SiteData site, Property l, DateTime today)
        {
            string href = "Listing?id=" + Uri.EscapeDataString(l.Id);
            (string cls, string text) = Badge(site, l, today);
            string meta = l.Deal == "rent" ? (l.Status == "rented" ? "Rented" : l.Available != null && l.Available.Value.Date > today ? "Available " + l.Available.Value.ToString("MMM d", Ui.Inv) : "Available now") : Ui.KindName(l.Kind) + (l.Status == "active" ? " &middot; " + Math.Max(1, (today - l.Listed.Date).Days) + (Math.Max(1, (today - l.Listed.Date).Days) == 1 ? " day" : " days") + " on market" : string.Empty);
            return "<article class=\"ls-hc\" data-lid=\"" + Ui.Attr(l.Id) + "\"><a class=\"ls-hc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(l.Photos.FirstOrDefault() ?? string.Empty, string.Empty, string.Empty, true) + (text != string.Empty ? "<span class=\"ls-badge ls-b-" + cls + "\">" + text + "</span>" : string.Empty) + "<span class=\"ls-pics\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 8h3l1.6-2.5h6.8L17 8h3v11H4z\"/><circle cx=\"12\" cy=\"13.2\" r=\"3.4\"/></svg>" + l.Photos.Count + "</span></a><button type=\"button\" class=\"ls-heart\" data-heart=\"" + Ui.Attr(l.Id) + "\" aria-pressed=\"false\" aria-label=\"Save this home\" onclick=\"ListingsJs.heart(this)\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg></button><div class=\"ls-hc-b\"><span class=\"ls-price\">" + Ui.PriceText(l) + "</span><span class=\"ls-hc-f\">" + Facts(l) + "</span><span class=\"ls-hc-a\"><a href=\"" + href + "\">" + Ui.Enc(l.Street) + "</a></span><span class=\"ls-hc-m\"><span>" + Ui.Enc(Ui.AreaName(site, l.Area)) + "</span><span>" + meta + "</span></span></div></article>";
        }

        private static string Pins(SiteData site, IEnumerable<Property> list)
        {
            var items = list.Select(l => new { i = l.Id, x = Math.Round(l.X, 1), y = Math.Round(l.Y, 1), p = Ui.Short(l.Price) + (l.Deal == "rent" ? "/mo" : string.Empty), r = Ui.PricePlain(l), f = FactsPlain(l), a = l.Street + ", " + Ui.AreaName(site, l.Area), t = Ui.Thumb(l.Photos.FirstOrDefault() ?? string.Empty), h = "Listing?id=" + Uri.EscapeDataString(l.Id), c = "ls-p-" + (l.Status == "active" ? l.Deal : l.Status) });
            return JsonSerializer.Serialize(items);
        }

        private static string AgentLine(SiteData site, TeamMember a, string p)
        {
            return "<div class=\"" + p + "-acard\">" + Ui.Face(p, a) + "<div><b>" + Ui.Enc(a.Name) + "</b><small>" + Ui.Enc(a.Title) + "</small></div></div>";
        }

        private static string MapHtml(string p, string id, string cls, IEnumerable<Models.Area> labels, string pins, string extra)
        {
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-map " + cls + "\" id=\"" + id + "\" data-pins=\"" + Ui.Attr(pins) + "\" tabindex=\"-1\"><div class=\"" + p + "-mlayer\"><img src=\"images/map/ashby-harbor.svg\" alt=\"Map of Ashby Harbor\" width=\"1600\" height=\"1200\" draggable=\"false\">");
            foreach (Models.Area a in labels)
            {
                sb.Append("<span class=\"" + p + "-mlab\" style=\"left:" + (a.X / 10).ToString("0.##", Ui.Inv) + "%;top:" + (a.Y / 7).ToString("0.##", Ui.Inv) + "%\"><i style=\"background:" + (Ui.SafeColor(a.Color) ? a.Color : "#B5562F") + "\"></i>" + Ui.Enc(a.Name) + "</span>");
            }
            sb.Append("<div class=\"" + p + "-pins\"></div></div><div class=\"" + p + "-mctl\"><button type=\"button\" aria-label=\"Zoom in\" onclick=\"ListingsJs.zoom('" + id + "',1.4)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Zoom out\" onclick=\"ListingsJs.zoom('" + id + "',0.714)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Show all\" onclick=\"ListingsJs.fit('" + id + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5\"/></svg></button></div>" + extra + "</div>");
            return sb.ToString();
        }

        private sealed class Filter
        {
            public string Deal { get; set; } = "sale";
            public string Text { get; set; } = string.Empty;
            public decimal Min { get; set; }
            public decimal Max { get; set; }
            public int Beds { get; set; }
            public decimal Baths { get; set; }
            public string Kind { get; set; } = string.Empty;
            public List<string> Areas { get; set; } = new List<string>();
            public int Sqft { get; set; }
            public List<string> Feats { get; set; } = new List<string>();
            public bool Open { get; set; }
            public bool Uo { get; set; } = true;
            public bool Sold { get; set; }
            public string Sort { get; set; } = "new";
            public double[]? Box { get; set; }
            public bool Saved { get; set; }
            public List<string> Ids { get; set; } = new List<string>();
            public int Page { get; set; } = 1;
            public string View { get; set; } = "split";
        }

        private sealed class Result
        {
            public string Head { get; set; } = string.Empty;
            public string Cards { get; set; } = string.Empty;
            public string Pager { get; set; } = string.Empty;
            public string Pins { get; set; } = "[]";
            public int Count { get; set; }
            public int Page { get; set; } = 1;
        }

        private const int Size = 18;

        private static readonly (string Key, string Name)[] Feats = { ("garage", "Garage"), ("garden", "Garden"), ("pool", "Pool"), ("waterfront", "Waterfront"), ("fireplace", "Fireplace"), ("balcony", "Balcony"), ("parking", "Parking"), ("laundry", "In-unit laundry"), ("office", "Home office") };

        private static readonly (string Key, string Name)[] Sorts = { ("new", "Newest"), ("plow", "Price, low to high"), ("phigh", "Price, high to low"), ("big", "Largest first"), ("ppsf", "Price per sq ft") };

        private static readonly decimal[] SalePrices = { 200000, 300000, 400000, 500000, 600000, 700000, 800000, 900000, 1000000, 1250000, 1500000, 2000000 };

        private static readonly decimal[] RentPrices = { 1000, 1500, 2000, 2500, 3000, 3500, 4000, 5000, 6000 };

        private static decimal Dec(string v)
        {
            return decimal.TryParse(v, NumberStyles.Number, Ui.Inv, out decimal d) && d > 0 ? Math.Min(d, 100000000m) : 0;
        }

        private static int Int(string v)
        {
            return int.TryParse(v, NumberStyles.Integer, Ui.Inv, out int n) ? n : 0;
        }

        private static Filter Parse(SiteData site, Func<string, string> get)
        {
            Filter f = new Filter();
            f.Deal = get("deal") == "rent" ? "rent" : "sale";
            f.Text = Regex.Replace(Ui.Clip(get("q"), 60), "\\s+", " ").Trim();
            f.Min = Dec(get("min"));
            f.Max = Dec(get("max"));
            if (f.Max > 0 && f.Min > f.Max)
            {
                (f.Min, f.Max) = (f.Max, f.Min);
            }
            f.Beds = Math.Clamp(Int(get("beds")), 0, 6);
            f.Baths = Math.Clamp(Dec(get("baths")), 0, 6);
            f.Kind = Ui.Kinds.Any(k => k.Key == get("kind")) ? get("kind") : string.Empty;
            f.Areas = get("area").Split(',').Where(a => site.Areas.Any(x => x.Active && x.Key == a)).Distinct().ToList();
            f.Sqft = Math.Clamp(Int(get("sqft")), 0, 20000);
            f.Feats = get("feat").Split(',').Where(x => Feats.Any(k => k.Key == x)).Distinct().ToList();
            f.Open = get("open") == "1";
            f.Uo = get("uo") != "0";
            f.Sold = get("sold") == "1";
            f.Sort = Sorts.Any(s => s.Key == get("sort")) ? get("sort") : "new";
            string[] bx = get("bx").Split(',');
            if (bx.Length == 4 && bx.All(b => double.TryParse(b, NumberStyles.Float, Ui.Inv, out _)))
            {
                double[] v = bx.Select(b => double.Parse(b, NumberStyles.Float, Ui.Inv)).ToArray();
                if (v[2] > v[0] && v[3] > v[1] && v[0] > -400 && v[1] > -300 && v[2] < 1400 && v[3] < 1000)
                {
                    f.Box = v;
                }
            }
            f.Saved = get("saved") == "1";
            f.Ids = get("ids").Split(',').Where(x => Regex.IsMatch(x, "^[A-Za-z0-9-]{1,20}$")).Distinct().Take(200).ToList();
            f.Page = Math.Max(1, Int(get("page")));
            f.View = get("view") is "map" or "list" ? get("view") : "split";
            return f;
        }

        private static bool HasFeat(Property l, string key)
        {
            return key switch
            {
                "parking" => l.Parking > 0,
                "laundry" => l.Features.Contains("In-unit laundry"),
                "office" => l.Features.Contains("Home office"),
                _ => l.Features.Any(x => x.Equals(key, StringComparison.OrdinalIgnoreCase))
            };
        }

        private static bool TextHit(SiteData site, Property l, string text)
        {
            string hay = (l.Id + " " + l.Title + " " + l.Street + " " + Ui.AreaName(site, l.Area) + " " + l.Style + " " + Ui.KindName(l.Kind)).ToLowerInvariant();
            return text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => hay.Contains(w, StringComparison.Ordinal));
        }

        private static List<Property> Match(SiteData site, Filter f, DateTime today)
        {
            IEnumerable<Property> q = site.Listings.Where(Ui.Public);
            if (f.Saved)
            {
                return q.Where(l => f.Ids.Contains(l.Id)).OrderBy(l => f.Ids.IndexOf(l.Id)).ToList();
            }
            q = q.Where(l => l.Deal == f.Deal);
            q = q.Where(l => l.Status == "active" || (l.Status == "pending" && f.Uo) || (l.Status is "sold" or "rented" && f.Sold));
            if (f.Text != string.Empty)
            {
                q = q.Where(l => TextHit(site, l, f.Text));
            }
            if (f.Min > 0)
            {
                q = q.Where(l => l.Price >= f.Min);
            }
            if (f.Max > 0)
            {
                q = q.Where(l => l.Price <= f.Max);
            }
            if (f.Beds > 0)
            {
                q = q.Where(l => l.Beds >= f.Beds);
            }
            if (f.Baths > 0)
            {
                q = q.Where(l => l.Baths >= f.Baths);
            }
            if (f.Kind != string.Empty)
            {
                q = q.Where(l => l.Kind == f.Kind);
            }
            if (f.Areas.Count > 0)
            {
                q = q.Where(l => f.Areas.Contains(l.Area));
            }
            if (f.Sqft > 0)
            {
                q = q.Where(l => l.Sqft >= f.Sqft);
            }
            foreach (string k in f.Feats)
            {
                q = q.Where(l => HasFeat(l, k));
            }
            if (f.Open)
            {
                q = q.Where(l => NextOpen(l, today) != null);
            }
            if (f.Box != null)
            {
                double[] b = f.Box;
                q = q.Where(l => l.X >= b[0] && l.X <= b[2] && l.Y >= b[1] && l.Y <= b[3]);
            }
            IOrderedEnumerable<Property> o = q.OrderBy(l => l.Status == "active" ? 0 : l.Status == "pending" ? 1 : 2);
            o = f.Sort switch
            {
                "plow" => o.ThenBy(l => l.Price),
                "phigh" => o.ThenByDescending(l => l.Price),
                "big" => o.ThenByDescending(l => l.Sqft),
                "ppsf" => o.ThenBy(l => l.Sqft > 0 ? l.Price / l.Sqft : l.Price),
                _ => o.ThenByDescending(l => l.Listed)
            };
            return o.ThenBy(l => l.Id, StringComparer.Ordinal).ToList();
        }

        private static string PriceOpts(string deal, decimal cur, bool min)
        {
            StringBuilder sb = new StringBuilder("<option value=\"\"" + (cur <= 0 ? " selected" : string.Empty) + ">" + (min ? "No min" : "No max") + "</option>");
            foreach (decimal v in deal == "rent" ? RentPrices : SalePrices)
            {
                sb.Append("<option value=\"" + v.ToString("0", Ui.Inv) + "\"" + (v == cur ? " selected" : string.Empty) + ">" + Ui.Short(v) + (deal == "rent" ? "/mo" : string.Empty) + "</option>");
            }
            if (cur > 0 && !(deal == "rent" ? RentPrices : SalePrices).Contains(cur))
            {
                sb.Append("<option value=\"" + cur.ToString("0", Ui.Inv) + "\" selected>" + Ui.Short(cur) + "</option>");
            }
            return sb.ToString();
        }

        private static string Chip(string text, string key, string value)
        {
            return "<button type=\"button\" class=\"ls-chip ls-fchip\" onclick=\"ListingsJs.drop('" + key + "','" + Ui.Js(value) + "')\" aria-label=\"Remove " + Ui.Attr(text) + "\">" + Ui.Enc(text) + "<b aria-hidden=\"true\">&times;</b></button>";
        }

        private static List<string> Chips(SiteData site, Filter f)
        {
            List<string> c = new List<string>();
            string suffix = f.Deal == "rent" ? "/mo" : string.Empty;
            if (f.Text != string.Empty)
            {
                c.Add(Chip("“" + f.Text + "”", "q", string.Empty));
            }
            if (f.Min > 0 || f.Max > 0)
            {
                c.Add(Chip(f.Min > 0 && f.Max > 0 ? Ui.Short(f.Min) + " – " + Ui.Short(f.Max) + suffix : f.Min > 0 ? Ui.Short(f.Min) + suffix + " and up" : "Up to " + Ui.Short(f.Max) + suffix, "price", string.Empty));
            }
            if (f.Beds > 0)
            {
                c.Add(Chip(f.Beds + "+ beds", "beds", string.Empty));
            }
            if (f.Baths > 0)
            {
                c.Add(Chip(Ui.Baths(f.Baths) + "+ baths", "baths", string.Empty));
            }
            if (f.Kind != string.Empty)
            {
                c.Add(Chip(Ui.KindName(f.Kind), "kind", string.Empty));
            }
            foreach (string a in f.Areas)
            {
                c.Add(Chip(Ui.AreaName(site, a), "area", a));
            }
            if (f.Sqft > 0)
            {
                c.Add(Chip(Ui.Num(f.Sqft) + "+ sq ft", "sqft", string.Empty));
            }
            foreach (string k in f.Feats)
            {
                c.Add(Chip(Ui.NameOf(Feats, k), "feat", k));
            }
            if (f.Open)
            {
                c.Add(Chip("Open houses", "open", string.Empty));
            }
            if (f.Sold)
            {
                c.Add(Chip(f.Deal == "rent" ? "Including rented" : "Including sold", "sold", string.Empty));
            }
            if (!f.Uo && f.Deal == "sale")
            {
                c.Add(Chip("No homes under offer", "uo", string.Empty));
            }
            if (f.Box != null)
            {
                c.Add(Chip("Map area", "bx", string.Empty));
            }
            return c;
        }

        private static Result Results(SiteData site, Filter f, DateTime today)
        {
            List<Property> all = Match(site, f, today);
            Result r = new Result { Count = all.Count };
            r.Page = Ui.PageOf(f.Page.ToString(Ui.Inv), Size, all.Count);
            List<Property> page = all.Skip((r.Page - 1) * Size).Take(Size).ToList();
            List<string> chips = Chips(site, f);
            string title = f.Saved ? "Saved homes" : f.Deal == "rent" ? "Homes for rent" : "Homes for sale";
            string where = f.Areas.Count > 0 ? string.Join(", ", f.Areas.Select(a => Ui.AreaName(site, a))) : site.Settings.Town;
            string count = Ui.Plural(all.Count, f.Saved ? "saved home" : "home", f.Saved ? "saved homes" : "homes") + (f.Saved ? string.Empty : " &middot; " + Ui.Enc(f.Box != null ? "in the map area" : where));
            StringBuilder h = new StringBuilder("<div class=\"ls-rtop\"><div><h1>" + title + "</h1><p>" + count + "</p></div><label class=\"ls-sort\"><span>Sort</span><select id=\"ls-sort\" onchange=\"ListingsJs.go(1)\">" + Ui.Opts(Sorts.Select(s => (s.Key, s.Name)), f.Sort) + "</select></label></div>");
            if (!f.Saved && chips.Count > 0)
            {
                h.Append("<div class=\"ls-chips ls-fchips\">" + string.Join(string.Empty, chips) + (chips.Count > 1 ? "<button type=\"button\" class=\"ls-clear\" onclick=\"ListingsJs.clearAll()\">Clear all</button>" : string.Empty) + "</div>");
            }
            if (f.Saved && all.Count > 0)
            {
                h.Append("<div class=\"ls-chips ls-fchips\"><a class=\"ls-chip\" href=\"Listings\">&larr; Back to search</a><button type=\"button\" class=\"ls-clear\" onclick=\"ListingsJs.clearSaved()\">Remove all saved homes</button></div>");
            }
            r.Head = h.ToString();
            if (all.Count == 0)
            {
                r.Cards = f.Saved
                    ? "<div class=\"ls-empty\"><h2>No saved homes yet</h2><p>Tap the heart on any home to keep it here. Saved homes stay in this browser.</p><a class=\"ls-btn ls-btn-accent\" href=\"Listings\">Browse homes</a></div>"
                    : "<div class=\"ls-empty\"><h2>No homes match all of these filters</h2><p>Remove a filter above, or widen the price range. New homes come on the market every week.</p><button type=\"button\" class=\"ls-btn ls-btn-ghost\" onclick=\"ListingsJs.clearAll()\">Clear all filters</button></div>";
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                foreach (Property l in page)
                {
                    sb.Append(Card(site, l, today));
                }
                r.Cards = sb.ToString();
            }
            r.Pager = Ui.Pager("ls", r.Page, Size, all.Count, "home", "homes", "ListingsJs.go");
            r.Pins = Pins(site, all);
            return r;
        }

        private static string Sel(string id, string label, string opts, string cls = "")
        {
            return "<label class=\"ls-q" + (cls != string.Empty ? " " + cls : string.Empty) + "\"><span>" + label + "</span><select id=\"ls-" + id + "\" data-f=\"" + id + "\">" + opts + "</select></label>";
        }

        private static string Bar(SiteData site, Filter f)
        {
            StringBuilder sb = new StringBuilder("<section class=\"ls-bar\" id=\"ls-bar\" aria-label=\"Search filters\"><div class=\"ls-seg\" role=\"group\" aria-label=\"Buy or rent\"><button type=\"button\" id=\"ls-d-sale\" class=\"" + (f.Deal == "sale" ? "ls-act" : string.Empty) + "\" aria-pressed=\"" + (f.Deal == "sale" ? "true" : "false") + "\" onclick=\"ListingsJs.deal('sale')\">Buy</button><button type=\"button\" id=\"ls-d-rent\" class=\"" + (f.Deal == "rent" ? "ls-act" : string.Empty) + "\" aria-pressed=\"" + (f.Deal == "rent" ? "true" : "false") + "\" onclick=\"ListingsJs.deal('rent')\">Rent</button></div>");
            sb.Append("<form class=\"ls-find\" role=\"search\" onsubmit=\"ListingsJs.go(1);return false\"><label class=\"ls-sr\" for=\"ls-q\">Street, neighborhood or listing number</label><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"11\" cy=\"11\" r=\"6.5\"/><path d=\"M16 16l4.5 4.5\"/></svg><input type=\"search\" id=\"ls-q\" maxlength=\"60\" placeholder=\"Street, area or ID\" value=\"" + Ui.Attr(f.Text) + "\" autocomplete=\"off\"></form>");
            sb.Append("<div class=\"ls-fwrap\" id=\"ls-fwrap\"><div class=\"ls-fhead\"><b>Filters</b><button type=\"button\" class=\"ls-ibtn\" aria-label=\"Close filters\" onclick=\"ListingsJs.filters(false)\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><div class=\"ls-quick\">");
            sb.Append(Sel("min", "Min price", PriceOpts(f.Deal, f.Min, true)));
            sb.Append(Sel("max", "Max price", PriceOpts(f.Deal, f.Max, false)));
            sb.Append(Sel("beds", "Beds", Ui.Opts(new[] { ("", "Any beds"), ("1", "1+ beds"), ("2", "2+ beds"), ("3", "3+ beds"), ("4", "4+ beds"), ("5", "5+ beds") }, f.Beds > 0 ? f.Beds.ToString(Ui.Inv) : string.Empty)));
            sb.Append(Sel("kind", "Home type", "<option value=\"\">Any type</option>" + Ui.Opts(Ui.Kinds.Select(k => (k.Key, k.Name)), f.Kind)));
            sb.Append("</div><div class=\"ls-more\" id=\"ls-more\"><div class=\"ls-mgrid\">");
            sb.Append(Sel("baths", "Baths", Ui.Opts(new[] { ("", "Any baths"), ("1", "1+ baths"), ("1.5", "1.5+ baths"), ("2", "2+ baths"), ("3", "3+ baths") }, f.Baths > 0 ? Ui.Baths(f.Baths) : string.Empty), "ls-qbath"));
            sb.Append(Sel("sqft", "Size", Ui.Opts(new[] { ("", "Any size"), ("750", "750+ sq ft"), ("1000", "1,000+ sq ft"), ("1500", "1,500+ sq ft"), ("2000", "2,000+ sq ft"), ("2500", "2,500+ sq ft"), ("3000", "3,000+ sq ft") }, f.Sqft > 0 ? f.Sqft.ToString(Ui.Inv) : string.Empty)));
            sb.Append("</div><fieldset class=\"ls-fs\"><legend>Neighborhoods</legend><div class=\"ls-checks\">");
            foreach (Models.Area a in site.Areas.Where(x => x.Active).OrderBy(x => x.Order))
            {
                sb.Append("<label class=\"ls-ck\"><input type=\"checkbox\" name=\"ls-area\" value=\"" + Ui.Attr(a.Key) + "\"" + (f.Areas.Contains(a.Key) ? " checked" : string.Empty) + "><i style=\"background:" + (Ui.SafeColor(a.Color) ? a.Color : "#B5562F") + "\"></i>" + Ui.Enc(a.Name) + "</label>");
            }
            sb.Append("</div></fieldset><fieldset class=\"ls-fs\"><legend>Must have</legend><div class=\"ls-checks\">");
            foreach ((string k, string n) in Feats)
            {
                sb.Append("<label class=\"ls-ck\"><input type=\"checkbox\" name=\"ls-feat\" value=\"" + k + "\"" + (f.Feats.Contains(k) ? " checked" : string.Empty) + ">" + n + "</label>");
            }
            sb.Append("</div></fieldset><fieldset class=\"ls-fs\"><legend>Show</legend><div class=\"ls-checks\"><label class=\"ls-ck\"><input type=\"checkbox\" id=\"ls-open\"" + (f.Open ? " checked" : string.Empty) + ">Open houses only</label><label class=\"ls-ck\" id=\"ls-l-uo\"" + (f.Deal == "rent" ? " hidden" : string.Empty) + "><input type=\"checkbox\" id=\"ls-uo\"" + (f.Uo ? " checked" : string.Empty) + ">Homes under offer</label><label class=\"ls-ck\"><input type=\"checkbox\" id=\"ls-sold\"" + (f.Sold ? " checked" : string.Empty) + "><span id=\"ls-l-sold\">" + (f.Deal == "rent" ? "Recently rented" : "Recently sold") + "</span></label></div></fieldset><div class=\"ls-ffoot\"><button type=\"button\" class=\"ls-btn ls-btn-ghost\" onclick=\"ListingsJs.clearAll()\">Clear all</button><button type=\"button\" class=\"ls-btn ls-btn-accent\" id=\"ls-showbtn\" onclick=\"ListingsJs.filters(false)\">Show homes</button></div></div></div>");
            sb.Append("<button type=\"button\" class=\"ls-btn ls-btn-ghost ls-mbtn\" id=\"ls-mbtn\" aria-expanded=\"false\" aria-controls=\"ls-fwrap\" onclick=\"ListingsJs.filters()\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M4 7h10M18 7h2M4 17h4M12 17h8\"/><circle cx=\"16\" cy=\"7\" r=\"2\"/><circle cx=\"10\" cy=\"17\" r=\"2\"/></svg><span class=\"ls-ml\">More filters</span><span class=\"ls-ms\">Filters</span><i id=\"ls-mcount\" hidden></i></button>");
            sb.Append("<span class=\"ls-grow\"></span><div class=\"ls-views\" role=\"group\" aria-label=\"View\">");
            foreach ((string k, string n, string icon) in new[] { ("list", "List", "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"4\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"13.5\" y=\"4\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"3.5\" y=\"13\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"13.5\" y=\"13\" width=\"7\" height=\"7\" rx=\"1.5\"/></svg>"), ("split", "Split", "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"4.5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M12 4.5v15\"/></svg>"), ("map", "Map", "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M3 6.5l6-2.5 6 2.5 6-2.5v13.5l-6 2.5-6-2.5-6 2.5z\"/><path d=\"M9 4v13.5M15 6.5V20\"/></svg>") })
            {
                sb.Append("<button type=\"button\" data-view=\"" + k + "\" class=\"ls-vb-" + k + (f.View == k ? " ls-act" : string.Empty) + "\" aria-pressed=\"" + (f.View == k ? "true" : "false") + "\" onclick=\"ListingsJs.view('" + k + "')\">" + icon + "<span>" + n + "</span></button>");
            }
            sb.Append("</div></section>");
            return sb.ToString();
        }

        private async Task<View> Render(SiteData site)
        {
            DateTime today = Store.Today(site);
            Filter f = Parse(site, k => Q(k, 200));
            Result r = f.Saved ? new Result() : Results(site, f, today);
            if (f.Saved)
            {
                r.Head = "<div class=\"ls-rtop\"><div><h1>Saved homes</h1><p>Loading&hellip;</p></div></div>";
                r.Cards = "<p class=\"ls-none\">Loading your saved homes&hellip;</p>";
            }
            StringBuilder sb = new StringBuilder("<div class=\"ls-page ls-v-" + f.View + (f.Saved ? " ls-saved-mode" : string.Empty) + "\" id=\"ls-page\" data-saved=\"" + (f.Saved ? "1" : "0") + "\" data-deal=\"" + f.Deal + "\" data-bx=\"" + (f.Box != null ? string.Join(",", f.Box.Select(b => b.ToString("0.#", Ui.Inv))) : string.Empty) + "\" data-page=\"" + r.Page + "\">");
            sb.Append(f.Saved ? string.Empty : Bar(site, f));
            sb.Append("<div class=\"ls-body\"><div class=\"ls-list\" id=\"ls-list\"><div id=\"ls-rhead\">" + r.Head + "</div><div class=\"ls-grid\" id=\"ls-cards\">" + r.Cards + "</div><div id=\"ls-pager\">" + r.Pager + "</div><p class=\"ls-note\">Prices, sizes and taxes come from the sellers and are not guaranteed. Map positions are approximate.</p></div>");
            sb.Append("<div class=\"ls-mapcol\" id=\"ls-mapcol\">" + MapHtml("ls", "ls-map", "ls-bigmap", site.Areas.Where(a => a.Active), r.Pins, "<button type=\"button\" class=\"ls-here\" id=\"ls-here\" hidden onclick=\"ListingsJs.here()\">Search this area</button><span class=\"ls-mhint\" id=\"ls-mhint\"></span>") + "</div></div>");
            sb.Append("<button type=\"button\" class=\"ls-flip\" id=\"ls-flip\" onclick=\"ListingsJs.flip()\"><span class=\"ls-fl-map\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M3 6.5l6-2.5 6 2.5 6-2.5v13.5l-6 2.5-6-2.5-6 2.5z\"/><path d=\"M9 4v13.5M15 6.5V20\"/></svg> Map</span><span class=\"ls-fl-list\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"4\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"13.5\" y=\"4\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"3.5\" y=\"13\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"13.5\" y=\"13\" width=\"7\" height=\"7\" rx=\"1.5\"/></svg> List</span></button></div>");
            string desc = f.Deal == "rent" ? "Homes for rent in " + site.Settings.Town + ": houses, condos and lofts with photos, prices and a map." : "Homes for sale in " + site.Settings.Town + ": search by price, size and neighborhood, on a list or a map.";
            return new View { Title = f.Saved ? "Saved homes" : f.Deal == "rent" ? "Homes for rent" : "Homes for sale", Desc = desc, Main = sb.ToString(), Nav = f.Saved ? string.Empty : f.Deal == "rent" ? "rent" : "buy" };
        }

        public async Task<ApiResponse> Search()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            DateTime today = Store.Today(site);
            Filter f = Parse(site, k => Ui.Clip(GetDataValue("f" + k), k == "ids" ? 4400 : 200));
            Result r = Results(site, f, today);
            response.SetElementContents("ls-rhead", r.Head);
            response.SetElementContents("ls-cards", r.Cards);
            response.SetElementContents("ls-pager", r.Pager);
            if (!f.Saved)
            {
                response.SetElementContents("ls-min", PriceOpts(f.Deal, f.Min, true));
                response.SetElementContents("ls-max", PriceOpts(f.Deal, f.Max, false));
                response.SetElementContents("ls-l-sold", f.Deal == "rent" ? "Recently rented" : "Recently sold");
            }
            response.ExecuteScript("ListingsJs.results(" + r.Pins + "," + r.Count + "," + r.Page + ");");
            return response;
        }
    }
}
