using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class Listing : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--lt-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "lt", v.Nav, false))
                .Replace("{plhd_drawer}", Nav(site, "lt", v.Nav, true))
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
                return "<i class=\"lt-shut\"></i>Office opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"lt-shut\"></i>Office closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"lt-shut\"></i>Office closed";
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
                response.SetElementContents("lt-e-nlmail", "Enter an email address like name@example.com.");
                return response;
            }
            bool added = await Store.AddSubscriber(mail);
            response.ExecuteScript("ListingJs.subscribed('" + (added ? "Thanks. You will hear from us when new homes are listed." : "You are already on the list.") + "');");
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
            return "<article class=\"lt-hc\" data-lid=\"" + Ui.Attr(l.Id) + "\"><a class=\"lt-hc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(l.Photos.FirstOrDefault() ?? string.Empty, string.Empty, string.Empty, true) + (text != string.Empty ? "<span class=\"lt-badge lt-b-" + cls + "\">" + text + "</span>" : string.Empty) + "<span class=\"lt-pics\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 8h3l1.6-2.5h6.8L17 8h3v11H4z\"/><circle cx=\"12\" cy=\"13.2\" r=\"3.4\"/></svg>" + l.Photos.Count + "</span></a><button type=\"button\" class=\"lt-heart\" data-heart=\"" + Ui.Attr(l.Id) + "\" aria-pressed=\"false\" aria-label=\"Save this home\" onclick=\"ListingJs.heart(this)\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg></button><div class=\"lt-hc-b\"><span class=\"lt-price\">" + Ui.PriceText(l) + "</span><span class=\"lt-hc-f\">" + Facts(l) + "</span><span class=\"lt-hc-a\"><a href=\"" + href + "\">" + Ui.Enc(l.Street) + "</a></span><span class=\"lt-hc-m\"><span>" + Ui.Enc(Ui.AreaName(site, l.Area)) + "</span><span>" + meta + "</span></span></div></article>";
        }

        private static string Pins(SiteData site, IEnumerable<Property> list)
        {
            var items = list.Select(l => new { i = l.Id, x = Math.Round(l.X, 1), y = Math.Round(l.Y, 1), p = Ui.Short(l.Price) + (l.Deal == "rent" ? "/mo" : string.Empty), r = Ui.PricePlain(l), f = FactsPlain(l), a = l.Street + ", " + Ui.AreaName(site, l.Area), t = Ui.Thumb(l.Photos.FirstOrDefault() ?? string.Empty), h = "Listing?id=" + Uri.EscapeDataString(l.Id), c = "lt-p-" + (l.Status == "active" ? l.Deal : l.Status) });
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
            sb.Append("<div class=\"" + p + "-pins\"></div></div><div class=\"" + p + "-mctl\"><button type=\"button\" aria-label=\"Zoom in\" onclick=\"ListingJs.zoom('" + id + "',1.4)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Zoom out\" onclick=\"ListingJs.zoom('" + id + "',0.714)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Show all\" onclick=\"ListingJs.fit('" + id + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5\"/></svg></button></div>" + extra + "</div>");
            return sb.ToString();
        }

        private static readonly Dictionary<string, string> Captions = new Dictionary<string, string>
        {
            { "living-bright", "Living room" }, { "living-fireplace", "Living room with fireplace" }, { "kitchen-white", "Kitchen" }, { "kitchen-wood", "Kitchen" },
            { "dining", "Dining area" }, { "bedroom", "Primary bedroom" }, { "kids-room", "Second bedroom" }, { "bathroom", "Bathroom" }, { "office", "Home office" },
            { "condo-living", "Living room and view" }, { "loft-living", "Open living space" }, { "sunroom", "Sunroom" }, { "condo", "The building" }, { "loft-building", "The building" },
            { "townhouses", "Front of the townhouse" }, { "pool-house", "Pool and terrace" }, { "waterfront", "Waterfront and dock" }
        };

        private static string Caption(string src, int i)
        {
            string key = Path.GetFileNameWithoutExtension(src);
            if (Captions.TryGetValue(key, out string? c))
            {
                return c;
            }
            return i == 0 ? "Front of the house" : "Photo " + (i + 1);
        }

        private static List<(DateTime Day, List<(string Value, string Text)> Times)> ViewDays(SiteData site)
        {
            List<(DateTime, List<(string, string)>)> days = new List<(DateTime, List<(string, string)>)>();
            DateTime now = Store.Now(site);
            int step = Math.Clamp(site.Settings.ViewingMinutes, 15, 120);
            for (int i = 0; i < 21 && days.Count < 14; i++)
            {
                DateTime d = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)d.DayOfWeek);
                TimeSpan? a = o != null && o.Open ? Store.Hm(o.Start) : null;
                TimeSpan? b = o != null && o.Open ? Store.Hm(o.End) : null;
                TimeSpan? va = Store.Hm(site.Settings.ViewingStart);
                TimeSpan? vb = Store.Hm(site.Settings.ViewingEnd);
                if (a == null || b == null)
                {
                    continue;
                }
                TimeSpan from = va != null && va.Value > a.Value ? va.Value : a.Value;
                TimeSpan to = vb != null && vb.Value < b.Value ? vb.Value : b.Value;
                List<(string, string)> times = new List<(string, string)>();
                for (TimeSpan t = from; t.Add(TimeSpan.FromMinutes(step)) <= to; t = t.Add(TimeSpan.FromMinutes(step)))
                {
                    if (d.Add(t) < now.AddHours(2))
                    {
                        continue;
                    }
                    string hm = t.ToString("hh\\:mm", Ui.Inv);
                    times.Add((hm, Ui.HmText(site, hm)));
                }
                if (times.Count > 0)
                {
                    days.Add((d, times));
                }
            }
            return days;
        }

        private static string PayBox(SiteData site, Property l, decimal price, decimal down, decimal rate, int term)
        {
            decimal taxPct = l.Tax > 0 && l.Price > 0 ? Math.Round(l.Tax / l.Price * 100m, 3) : site.Settings.TaxRate;
            Payment p = Calc.Pay(price, down, rate, term, taxPct, site.Settings.Insurance, l.Hoa, site.Settings.Pmi);
            (string Name, decimal V, string C)[] parts = { ("Principal and interest", p.Principal, "#17232F"), ("Property tax", p.Tax, "#B5562F"), ("Home insurance", p.Insurance, "#2F5D50"), ("HOA fees", p.Hoa, "#C9A227"), ("Mortgage insurance", p.Pmi, "#8A8F94") };
            decimal total = Math.Max(1m, p.Total);
            StringBuilder sb = new StringBuilder("<div class=\"lt-ptotal\"><b>" + Ui.Whole(p.Total) + "</b><span>per month</span></div><div class=\"lt-pbar\" role=\"img\" aria-label=\"Monthly cost split\">");
            foreach ((string n, decimal v, string c) in parts.Where(x => x.V > 0))
            {
                sb.Append("<i style=\"width:" + (v / total * 100m).ToString("0.##", Ui.Inv) + "%;background:" + c + "\"></i>");
            }
            sb.Append("</div><dl class=\"lt-plist\">");
            foreach ((string n, decimal v, string c) in parts.Where(x => x.V > 0 || x.Name == "HOA fees" && l.Kind != "house"))
            {
                sb.Append("<dt><i style=\"background:" + c + "\"></i>" + n + "</dt><dd>" + Ui.Whole(v) + "</dd>");
            }
            sb.Append("</dl><p class=\"lt-hint\">Loan " + Ui.Whole(p.Loan) + " &middot; down payment " + Ui.Whole(p.Down) + (p.Pmi > 0 ? " &middot; mortgage insurance applies below 20% down" : string.Empty) + ". " + Ui.Enc(site.Settings.Disclaimer) + "</p>");
            return sb.ToString();
        }

        private static decimal Num(string v, decimal def, decimal min, decimal max)
        {
            return decimal.TryParse((v ?? string.Empty).Replace(",", string.Empty).Replace("$", string.Empty), NumberStyles.Number, Ui.Inv, out decimal d) ? Math.Clamp(d, min, max) : def;
        }

        private static List<Property> Similar(SiteData site, Property l)
        {
            return site.Listings.Where(x => x.Id != l.Id && x.Deal == l.Deal && Ui.Live(x))
                .OrderByDescending(x => (x.Area == l.Area ? 2 : 0) + (x.Kind == l.Kind ? 1 : 0) - (double)Math.Abs(x.Price - l.Price) / (double)Math.Max(1m, l.Price) * 3)
                .Take(3).ToList();
        }

        private static string NotFound()
        {
            return "<div class=\"lt-wrap lt-gone\"><h1>This home is no longer listed</h1><p>It may have been taken off the market, or the link is out of date.</p><div class=\"lt-gacts\"><a class=\"lt-btn lt-btn-accent\" href=\"Listings?deal=sale\">Homes for sale</a><a class=\"lt-btn lt-btn-ghost\" href=\"Listings?deal=rent\">Homes for rent</a></div></div>";
        }

        private async Task<View> Render(SiteData site)
        {
            string id = Q("id", 20);
            Property? l = site.Listings.FirstOrDefault(x => x.Id == id && Ui.Public(x));
            if (l == null)
            {
                return new View { Title = "Home not found", Main = NotFound() };
            }
            using (await Store.Hold())
            {
                SiteData live = await Store.LoadSite();
                Property? lv = live.Listings.FirstOrDefault(x => x.Id == l.Id);
                if (lv != null)
                {
                    lv.Views++;
                    await Store.SaveSite(live);
                }
            }
            DateTime today = Store.Today(site);
            TeamMember? agent = Ui.AgentOf(site, l.Agent);
            Models.Area? area = Ui.AreaOf(site, l.Area);
            string dealPage = "Listings?deal=" + l.Deal;
            string areaName = area?.Name ?? l.Area;
            List<string> photos = l.Photos.Count > 0 ? l.Photos : new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"lt-wrap lt-crumbs\"><nav class=\"lt-crumb\" aria-label=\"Breadcrumb\"><a href=\"" + dealPage + "\">" + (l.Deal == "rent" ? "Homes for rent" : "Homes for sale") + "</a><span>/</span><a href=\"" + dealPage + "&amp;area=" + Uri.EscapeDataString(l.Area) + "\">" + Ui.Enc(areaName) + "</a><span>/</span><span>" + Ui.Enc(l.Street) + "</span></nav></div>");
            string gdata = JsonSerializer.Serialize(photos.Select((p, i) => new { f = p, t = Ui.Thumb(p), c = Caption(p, i) }));
            sb.Append("<section class=\"lt-wrap\"><div class=\"lt-gal lt-gal-" + Math.Min(5, photos.Count) + "\" id=\"lt-gal\" data-photos=\"" + Ui.Attr(gdata) + "\">");
            for (int i = 0; i < Math.Min(5, photos.Count); i++)
            {
                sb.Append("<button type=\"button\" class=\"lt-gi" + (i == 0 ? " lt-gi0" : string.Empty) + "\" onclick=\"ListingJs.show(" + i + ")\" aria-label=\"Open photo " + (i + 1) + " of " + photos.Count + ": " + Ui.Attr(Caption(photos[i], i)) + "\">" + Ui.Pic(photos[i], Caption(photos[i], i), string.Empty, i > 0, i == 0) + "</button>");
            }
            sb.Append("<button type=\"button\" class=\"lt-btn lt-allp\" onclick=\"ListingJs.show(0)\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 8h3l1.6-2.5h6.8L17 8h3v11H4z\"/><circle cx=\"12\" cy=\"13.2\" r=\"3.4\"/></svg> Show all " + photos.Count + " photos</button></div></section>");
            (string bcls, string btext) = Badge(site, l, today);
            int dom = Math.Max(1, (today - l.Listed.Date).Days);
            Payment est = l.Deal == "sale" ? Calc.ForListing(site, l) : new Payment();
            sb.Append("<div class=\"lt-wrap lt-lbody\"><div class=\"lt-lmain\"><div class=\"lt-ltop\"><div class=\"lt-lmeta\">" + Ui.StatusPill("lt", l) + (btext != string.Empty && bcls is "new" or "open" or "cut" ? "<span class=\"lt-tagx lt-t-" + bcls + "\">" + btext + "</span>" : string.Empty) + "<span class=\"lt-lid\">" + Ui.Enc(l.Id) + "</span></div>");
            sb.Append("<div class=\"lt-lpr\"><span class=\"lt-lprice\">" + Ui.PriceText(l) + "</span>" + (l.Deal == "sale" && Ui.Live(l) ? "<a class=\"lt-est\" href=\"#lt-cost\">Est. " + Ui.Whole(est.Total) + "/mo</a>" : string.Empty) + "</div>");
            sb.Append("<h1>" + Ui.Enc(l.Street) + "</h1><p class=\"lt-lsub\">" + Ui.Enc(areaName) + ", " + Ui.Enc(site.Settings.Town) + " &middot; " + Ui.Enc(l.Kind == "house" ? l.Style + " house" : Ui.KindName(l.Kind)) + "</p>");
            sb.Append("<div class=\"lt-bigf\"><div><b>" + (l.Beds == 0 ? "Studio" : l.Beds.ToString(Ui.Inv)) + "</b><span>" + (l.Beds == 0 ? "layout" : l.Beds == 1 ? "bed" : "beds") + "</span></div><div><b>" + Ui.Baths(l.Baths) + "</b><span>" + (l.Baths == 1 ? "bath" : "baths") + "</span></div><div><b>" + Ui.Num(l.Sqft) + "</b><span>sq ft</span></div>" + (l.Lot > 0 ? "<div><b>" + Ui.Num(l.Lot) + "</b><span>sq ft lot</span></div>" : "<div><b>" + (l.Hoa > 0 ? Ui.Whole(l.Hoa) : "&ndash;") + "</b><span>HOA/mo</span></div>") + "</div>");
            sb.Append("<div class=\"lt-lacts\"><button type=\"button\" class=\"lt-btn lt-btn-ghost lt-btn-sm lt-savebtn\" data-heart=\"" + Ui.Attr(l.Id) + "\" aria-pressed=\"false\" onclick=\"ListingJs.heart(this)\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg><span class=\"lt-sv0\">Save</span><span class=\"lt-sv1\">Saved</span></button><button type=\"button\" class=\"lt-btn lt-btn-ghost lt-btn-sm\" onclick=\"ListingJs.share()\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 15V4M7.5 8.5L12 4l4.5 4.5\"/><path d=\"M5 13v6h14v-6\"/></svg> Share</button></div></div>");
            if (l.Status is "sold" or "rented")
            {
                PriceEvent? last = l.History.LastOrDefault(h => h.Event is "Sold" or "Rented");
                sb.Append("<div class=\"lt-info-box lt-closed\">" + (l.Status == "sold" ? "Sold" : "Rented") + (last != null ? " on " + last.When.ToString("MMMM d, yyyy", Ui.Inv) + (l.Status == "sold" ? " for " + Ui.Money(last.Price) : string.Empty) : string.Empty) + ". This page stays up so you can compare prices nearby.</div>");
            }
            else if (l.Status == "pending")
            {
                sb.Append("<div class=\"lt-info-box lt-closed\">An offer has been accepted on this home. Ask " + Ui.Enc(agent?.Name.Split(' ')[0] ?? "us") + " to tell you if it comes back on the market.</div>");
            }
            sb.Append("<section class=\"lt-ls\"><h2>" + Ui.Enc(l.Title) + "</h2><div class=\"lt-desc\">");
            foreach (string para in l.Description.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                sb.Append("<p>" + Ui.Enc(para.Trim()) + "</p>");
            }
            sb.Append("</div>");
            if (l.Features.Count > 0)
            {
                sb.Append("<ul class=\"lt-feats\">");
                foreach (string ft in l.Features)
                {
                    sb.Append("<li>" + Ui.Enc(ft) + "</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section>");
            List<(string K, string V)> facts = new List<(string, string)> { ("Home type", Ui.Enc(Ui.KindName(l.Kind))), ("Style", Ui.Enc(l.Style)), ("Year built", l.Year > 0 ? l.Year.ToString(Ui.Inv) : "&ndash;"), ("Interior", Ui.Num(l.Sqft) + " sq ft") };
            if (l.Lot > 0)
            {
                facts.Add(("Lot", Ui.Num(l.Lot) + " sq ft"));
            }
            facts.Add(("Parking", l.Parking == 0 ? "Street" : l.Parking == 1 ? "1 space" : l.Parking + " spaces"));
            if (l.Deal == "sale")
            {
                facts.Add(("Price per sq ft", l.Sqft > 0 ? Ui.Whole(l.Price / l.Sqft) : "&ndash;"));
                facts.Add(("Property tax", l.Tax > 0 ? Ui.Whole(l.Tax) + " a year" : "&ndash;"));
                facts.Add(("HOA fees", l.Hoa > 0 ? Ui.Whole(l.Hoa) + " a month" : "None"));
                facts.Add(("On the market", Ui.Plural(dom, "day", "days")));
            }
            else
            {
                facts.Add(("Available", l.Available == null || l.Available.Value.Date <= today ? "Now" : l.Available.Value.ToString("MMMM d, yyyy", Ui.Inv)));
                facts.Add(("Building fees", l.Hoa > 0 ? "Included in rent" : "None"));
                facts.Add(("Deposit", Ui.Whole(l.Price)));
            }
            facts.Add(("Listing number", Ui.Enc(l.Id)));
            sb.Append("<section class=\"lt-ls\"><h2>Facts</h2><dl class=\"lt-facts\">");
            foreach ((string k, string v) in facts)
            {
                sb.Append("<div><dt>" + k + "</dt><dd>" + v + "</dd></div>");
            }
            sb.Append("</dl></section>");
            List<OpenHouse> opens = l.Status == "active" ? l.Opens.Where(o => o.Date.Date >= today).OrderBy(o => o.Date).ToList() : new List<OpenHouse>();
            if (opens.Count > 0)
            {
                sb.Append("<section class=\"lt-ls\"><h2>Open houses</h2><div class=\"lt-opens\">");
                foreach (OpenHouse o in opens)
                {
                    sb.Append("<div class=\"lt-open\"><b>" + o.Date.ToString("ddd", Ui.Inv) + "</b><span>" + o.Date.ToString("MMM d", Ui.Inv) + "</span><em>" + Ui.HmText(site, o.Start) + " &ndash; " + Ui.HmText(site, o.End) + "</em></div>");
                }
                sb.Append("</div><p class=\"lt-hint\">No need to book for an open house. Just come by.</p></section>");
            }
            string pin = JsonSerializer.Serialize(new[] { new { i = l.Id, x = Math.Round(l.X, 1), y = Math.Round(l.Y, 1), p = string.Empty, c = "lt-dot" } });
            sb.Append("<section class=\"lt-ls\"><h2>Location</h2><div class=\"lt-locg\">" + MapHtml("lt", "lt-lmap", "lt-smap", site.Areas.Where(a => a.Active), pin, string.Empty) + "<div class=\"lt-area\">" + (area != null ? "<span class=\"lt-kick\">" + Ui.Enc(area.Name) + "</span><p>" + Ui.Enc(area.Text) + "</p><a class=\"lt-more\" href=\"" + dealPage + "&amp;area=" + Uri.EscapeDataString(area.Key) + "\">More homes in " + Ui.Enc(area.Name) + " &rarr;</a>" : string.Empty) + "</div></div></section>");
            if (l.Deal == "sale" && Ui.Live(l))
            {
                decimal rate = site.Settings.Rate;
                sb.Append("<section class=\"lt-ls\" id=\"lt-cost\"><h2>Monthly cost</h2><div class=\"lt-calc\"><form class=\"lt-cin\" onsubmit=\"return false\" oninput=\"ListingJs.pay()\" onchange=\"ListingJs.pay()\"><label class=\"lt-field\"><span>Home price</span><input type=\"text\" inputmode=\"numeric\" id=\"lt-mprice\" value=\"" + l.Price.ToString("#,##0", Ui.Inv) + "\" maxlength=\"12\"></label><label class=\"lt-field\"><span>Down payment</span><select id=\"lt-mdown\">" + Ui.Opts(new[] { 3.5m, 5m, 10m, 15m, 20m, 25m, 30m, 40m }.Select(d => (d.ToString("0.#", Ui.Inv), d.ToString("0.#", Ui.Inv) + "%")), site.Settings.DownPct.ToString("0.#", Ui.Inv)) + "</select></label><label class=\"lt-field\"><span>Interest rate (%)</span><input type=\"text\" inputmode=\"decimal\" id=\"lt-mrate\" value=\"" + rate.ToString("0.###", Ui.Inv) + "\" maxlength=\"6\"></label><label class=\"lt-field\"><span>Loan term</span><select id=\"lt-mterm\">" + Ui.Opts(new[] { ("30", "30 years"), ("20", "20 years"), ("15", "15 years") }, site.Settings.Term.ToString(Ui.Inv)) + "</select></label></form><div class=\"lt-cout\" id=\"lt-pay\" aria-live=\"polite\">" + PayBox(site, l, l.Price, site.Settings.DownPct, rate, site.Settings.Term) + "</div></div><a class=\"lt-more\" href=\"Mortgage?price=" + l.Price.ToString("0", Ui.Inv) + "&amp;hoa=" + l.Hoa.ToString("0", Ui.Inv) + "&amp;tax=" + l.Tax.ToString("0", Ui.Inv) + "\">Open the full calculator &rarr;</a></section>");
            }
            if (l.History.Count > 0)
            {
                sb.Append("<section class=\"lt-ls\"><h2>" + (l.Deal == "rent" ? "Rent history" : "Price history") + "</h2><div class=\"lt-hist\"><div class=\"lt-hr lt-hh\"><span>Date</span><span>Event</span><span>Price</span></div>");
                decimal prev = 0;
                foreach (PriceEvent h in l.History.OrderBy(h => h.When))
                {
                    string change = prev > 0 && h.Price != prev ? "<small class=\"" + (h.Price < prev ? "lt-down" : "lt-up") + "\">" + (h.Price < prev ? "&minus;" : "+") + Math.Abs((h.Price - prev) / prev * 100m).ToString("0.#", Ui.Inv) + "%</small>" : string.Empty;
                    sb.Append("<div class=\"lt-hr\"><span>" + h.When.ToString("MMM d, yyyy", Ui.Inv) + "</span><span>" + Ui.Enc(h.Event) + "</span><span>" + Ui.Money(h.Price) + (l.Deal == "rent" ? "/mo" : string.Empty) + change + "</span></div>");
                    prev = h.Price;
                }
                sb.Append("</div></section>");
            }
            sb.Append("</div><aside class=\"lt-lside\"><div class=\"lt-sbox\">");
            if (agent != null)
            {
                string tel = Regex.Replace(agent.Phone, "[^0-9+]", string.Empty);
                sb.Append("<a class=\"lt-ag\" href=\"Agent?id=" + Uri.EscapeDataString(agent.Id) + "\">" + Ui.Face("lt", agent) + "<span><small>Listed by</small><b>" + Ui.Enc(agent.Name) + "</b><em>" + Ui.Enc(agent.Title) + "</em></span></a><div class=\"lt-agc\"><a class=\"lt-btn lt-btn-ghost lt-btn-sm\" href=\"tel:" + Ui.Attr(tel) + "\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg> " + Ui.Enc(agent.Phone) + "</a><a class=\"lt-btn lt-btn-ghost lt-btn-sm\" href=\"mailto:" + Ui.Attr(agent.Email) + "?subject=" + Uri.EscapeDataString(l.Id + " " + l.Street) + "\" aria-label=\"Email " + Ui.Attr(agent.Name) + "\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg> Email</a></div>");
            }
            List<(DateTime Day, List<(string Value, string Text)> Times)> days = ViewDays(site);
            bool canView = l.Status == "active" && days.Count > 0;
            sb.Append("<div class=\"lt-tabs\" role=\"tablist\">" + (canView ? "<button type=\"button\" role=\"tab\" id=\"lt-tab-viewing\" class=\"lt-act\" aria-selected=\"true\" onclick=\"ListingJs.tab('viewing')\">Book a viewing</button>" : string.Empty) + "<button type=\"button\" role=\"tab\" id=\"lt-tab-question\"" + (canView ? " aria-selected=\"false\"" : " class=\"lt-act\" aria-selected=\"true\"") + " onclick=\"ListingJs.tab('question')\">Ask a question</button></div>");
            sb.Append("<form class=\"lt-form lt-lform\" id=\"lt-lform\" data-kind=\"" + (canView ? "viewing" : "question") + "\" onsubmit=\"return ListingJs.send()\" novalidate>");
            if (canView)
            {
                sb.Append("<div class=\"lt-row2 lt-vonly\"><label class=\"lt-field\"><span>Day</span><select id=\"lt-lday\" onchange=\"ListingJs.days()\">");
                foreach ((DateTime d, List<(string Value, string Text)> times) in days)
                {
                    sb.Append("<option value=\"" + Ui.Ymd(d) + "\" data-t=\"" + Ui.Attr(string.Join(",", times.Select(t => t.Value + "|" + t.Text))) + "\">" + (d == today ? "Today" : d == today.AddDays(1) ? "Tomorrow" : d.ToString("ddd, MMM d", Ui.Inv)) + "</option>");
                }
                sb.Append("</select></label><label class=\"lt-field\"><span>Time</span><select id=\"lt-ltime\"></select></label></div>");
            }
            string first = agent?.Name.Split(' ')[0] ?? "We";
            sb.Append("<label class=\"lt-field\"><span>Your name</span><input type=\"text\" id=\"lt-lname\" maxlength=\"80\" autocomplete=\"name\"></label><em class=\"lt-err\" id=\"lt-e-lname\" role=\"alert\"></em>");
            sb.Append("<label class=\"lt-field\"><span>Email</span><input type=\"email\" id=\"lt-lmail\" maxlength=\"120\" autocomplete=\"email\"></label><em class=\"lt-err\" id=\"lt-e-lmail\" role=\"alert\"></em>");
            sb.Append("<label class=\"lt-field\"><span>Phone <small>(optional)</small></span><input type=\"tel\" id=\"lt-lphone\" maxlength=\"30\" autocomplete=\"tel\"></label>");
            sb.Append("<label class=\"lt-field\"><span>Message <small>(optional)</small></span><textarea id=\"lt-lmsg\" rows=\"3\" maxlength=\"1000\" placeholder=\"" + (l.Status == "active" ? "Anything we should know before the viewing?" : "What would you like to know?") + "\"></textarea></label><em class=\"lt-err\" id=\"lt-e-lmsg\" role=\"alert\"></em>");
            sb.Append("<label class=\"lt-hp\" aria-hidden=\"true\">Website<input type=\"text\" id=\"lt-lweb\" tabindex=\"-1\" autocomplete=\"off\"></label>");
            sb.Append("<button type=\"submit\" class=\"lt-btn lt-btn-accent lt-btn-wide\" id=\"lt-lsend\">" + (canView ? "Request this time" : "Send question") + "</button><p class=\"lt-hint lt-lnote\">" + Ui.Enc(first) + " will " + (canView ? "confirm the time" : "answer") + " by phone or email within one working day. We only use your details to reply.</p></form><div id=\"lt-ldone\"></div></div></aside></div>");
            List<Property> sim = Similar(site, l);
            if (sim.Count > 0)
            {
                sb.Append("<section class=\"lt-sec lt-wrap\"><div class=\"lt-shead\"><div><span class=\"lt-kick\">Keep looking</span><h2 class=\"lt-h2\">Similar homes</h2></div><a class=\"lt-more\" href=\"" + dealPage + "\">See all &rarr;</a></div><div class=\"lt-cards\">");
                foreach (Property x in sim)
                {
                    sb.Append(Card(site, x, today));
                }
                sb.Append("</div></section>");
            }
            sb.Append("<div class=\"lt-lb\" id=\"lt-lb\" role=\"dialog\" aria-modal=\"true\" aria-label=\"Photos\" hidden><div class=\"lt-lbtop\"><span id=\"lt-lbn\"></span><span id=\"lt-lbc\"></span><button type=\"button\" class=\"lt-ibtn lt-lbx\" aria-label=\"Close photos\" onclick=\"ListingJs.hide()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><div class=\"lt-lbstage\"><button type=\"button\" class=\"lt-lbnav lt-lbp\" aria-label=\"Previous photo\" onclick=\"ListingJs.step(-1)\"><svg viewBox=\"0 0 24 24\" width=\"26\" height=\"26\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 5l-7 7 7 7\"/></svg></button><img id=\"lt-lbimg\" alt=\"\"><button type=\"button\" class=\"lt-lbnav lt-lbn\" aria-label=\"Next photo\" onclick=\"ListingJs.step(1)\"><svg viewBox=\"0 0 24 24\" width=\"26\" height=\"26\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 5l7 7-7 7\"/></svg></button></div><div class=\"lt-lbthumbs\" id=\"lt-lbthumbs\"></div></div>");
            string title = l.Street + " · " + Ui.PricePlain(l);
            string desc = Ui.Clip((l.Beds == 0 ? "Studio" : l.Beds + "-bedroom") + " " + Ui.KindName(l.Kind).ToLowerInvariant() + " " + (l.Deal == "rent" ? "for rent" : "for sale") + " in " + areaName + ", " + site.Settings.Town + ". " + l.Description.Split("\n\n")[0], 300);
            return new View { Title = title, Desc = desc, Main = sb.ToString(), Nav = l.Deal == "rent" ? "rent" : "buy" };
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            Property? l = site.Listings.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("lid"), 20) && x.Deal == "sale" && Ui.Public(x));
            if (l == null)
            {
                return response;
            }
            decimal price = Num(GetDataValue("mprice"), l.Price, 0, 100000000m);
            decimal down = Num(GetDataValue("mdown"), site.Settings.DownPct, 0, 100);
            decimal rate = Num(GetDataValue("mrate"), site.Settings.Rate, 0, 30);
            int term = (int)Num(GetDataValue("mterm"), site.Settings.Term, 5, 40);
            response.SetElementContents("lt-pay", PayBox(site, l, price, down, rate, term));
            return response;
        }

        public async Task<ApiResponse> Send()
        {
            ApiResponse response = new ApiResponse();
            string name = Regex.Replace(Ui.Clip(GetDataValue("lname"), 80), "\\s+", " ").Trim();
            string mail = Ui.Clip(GetDataValue("lmail"), 120).Trim();
            string phone = Ui.CleanPhone(GetDataValue("lphone"));
            string msg = Ui.Clip(GetDataValue("lmsg"), 1000).Trim();
            string kind = GetDataValue("lkind") == "viewing" ? "viewing" : "question";
            if (name.Length < 2)
            {
                response.ExecuteScript("ListingJs.fieldErr('lname','Please enter your name.');ListingJs.sent(false);");
                return response;
            }
            if (!Ui.Email(mail))
            {
                response.ExecuteScript("ListingJs.fieldErr('lmail','Enter an email address like name@example.com.');ListingJs.sent(false);");
                return response;
            }
            if (kind == "question" && msg.Length < 3)
            {
                response.ExecuteScript("ListingJs.fieldErr('lmsg','Please write your question.');ListingJs.sent(false);");
                return response;
            }
            if (Ui.Clip(GetDataValue("lweb"), 50) != string.Empty)
            {
                response.ExecuteScript("ListingJs.sent(true,'Thank you. We will be in touch soon.');");
                return response;
            }
            string prefer = string.Empty;
            string stamp = string.Empty;
            string agentFirst = "We";
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Property? l = site.Listings.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("lid"), 20) && Ui.Public(x));
                if (l == null)
                {
                    response.ExecuteScript("ListingJs.sent(false);ListingJs.toast('This home is no longer listed.');");
                    return response;
                }
                if (kind == "viewing")
                {
                    if (l.Status != "active")
                    {
                        kind = "question";
                    }
                    else
                    {
                        DateTime? day = Ui.ParseDay(GetDataValue("lday"));
                        string hm = Ui.Clip(GetDataValue("ltime"), 5);
                        (DateTime Day, List<(string Value, string Text)> Times) slot = ViewDays(site).FirstOrDefault(d => day != null && d.Day == day.Value);
                        if (day == null || slot.Times == null || !slot.Times.Any(t => t.Value == hm))
                        {
                            response.ExecuteScript("ListingJs.sent(false);ListingJs.toast('Please pick a day and time from the list.');");
                            return response;
                        }
                        prefer = slot.Day.ToString("ddd, MMM d", Ui.Inv) + " at " + Ui.HmText(site, hm);
                        stamp = slot.Day.ToString("yyyy-MM-dd", Ui.Inv) + "T" + hm;
                    }
                }
                TeamMember? agent = Ui.AgentOf(site, l.Agent);
                agentFirst = agent?.Name.Split(' ')[0] ?? "We";
                bool dup = site.Leads.Any(x => x.Listing == l.Id && x.Kind == kind && x.Email.Equals(mail, StringComparison.OrdinalIgnoreCase) && x.Created > DateTime.UtcNow.AddMinutes(-10));
                if (!dup)
                {
                    site.Leads.Add(new Lead { Id = Store.NextId(site.Leads.Select(x => x.Id), "LD-", 1000), Kind = kind, Listing = l.Id, Name = name, Email = mail, Phone = phone, Message = msg, Prefer = prefer, Details = stamp, Agent = agent != null && agent.Active ? agent.Id : string.Empty, Created = DateTime.UtcNow });
                    await Store.SaveSite(site);
                }
            }
            await Store.Log("web", kind == "viewing" ? "viewing request" : "question", name, true);
            string first = name.Split(' ')[0];
            string text = kind == "viewing" ? "Thanks, " + first + ". You asked for " + prefer + ". " + (agentFirst == "We" ? "We" : agentFirst) + " will confirm by phone or email within one working day." : "Thanks, " + first + ". " + (agentFirst == "We" ? "We" : agentFirst) + " will answer by email within one working day.";
            response.ExecuteScript("ListingJs.sent(true,'" + Ui.Js(text) + "');");
            return response;
        }
    }
}
