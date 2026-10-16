using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class Home : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--hm-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "hm", v.Nav, false))
                .Replace("{plhd_drawer}", Nav(site, "hm", v.Nav, true))
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
                return "<i class=\"hm-shut\"></i>Office opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"hm-shut\"></i>Office closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"hm-shut\"></i>Office closed";
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
                response.SetElementContents("hm-e-nlmail", "Enter an email address like name@example.com.");
                return response;
            }
            bool added = await Store.AddSubscriber(mail);
            response.ExecuteScript("HomeJs.subscribed('" + (added ? "Thanks. You will hear from us when new homes are listed." : "You are already on the list.") + "');");
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
            return "<article class=\"hm-hc\" data-lid=\"" + Ui.Attr(l.Id) + "\"><a class=\"hm-hc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(l.Photos.FirstOrDefault() ?? string.Empty, string.Empty, string.Empty, true) + (text != string.Empty ? "<span class=\"hm-badge hm-b-" + cls + "\">" + text + "</span>" : string.Empty) + "<span class=\"hm-pics\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 8h3l1.6-2.5h6.8L17 8h3v11H4z\"/><circle cx=\"12\" cy=\"13.2\" r=\"3.4\"/></svg>" + l.Photos.Count + "</span></a><button type=\"button\" class=\"hm-heart\" data-heart=\"" + Ui.Attr(l.Id) + "\" aria-pressed=\"false\" aria-label=\"Save this home\" onclick=\"HomeJs.heart(this)\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg></button><div class=\"hm-hc-b\"><span class=\"hm-price\">" + Ui.PriceText(l) + "</span><span class=\"hm-hc-f\">" + Facts(l) + "</span><span class=\"hm-hc-a\"><a href=\"" + href + "\">" + Ui.Enc(l.Street) + "</a></span><span class=\"hm-hc-m\"><span>" + Ui.Enc(Ui.AreaName(site, l.Area)) + "</span><span>" + meta + "</span></span></div></article>";
        }

        private static string Pins(SiteData site, IEnumerable<Property> list)
        {
            var items = list.Select(l => new { i = l.Id, x = Math.Round(l.X, 1), y = Math.Round(l.Y, 1), p = Ui.Short(l.Price) + (l.Deal == "rent" ? "/mo" : string.Empty), r = Ui.PricePlain(l), f = FactsPlain(l), a = l.Street + ", " + Ui.AreaName(site, l.Area), t = Ui.Thumb(l.Photos.FirstOrDefault() ?? string.Empty), h = "Listing?id=" + Uri.EscapeDataString(l.Id), c = "hm-p-" + (l.Status == "active" ? l.Deal : l.Status) });
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
            sb.Append("<div class=\"" + p + "-pins\"></div></div><div class=\"" + p + "-mctl\"><button type=\"button\" aria-label=\"Zoom in\" onclick=\"HomeJs.zoom('" + id + "',1.4)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Zoom out\" onclick=\"HomeJs.zoom('" + id + "',0.714)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Show all\" onclick=\"HomeJs.fit('" + id + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5\"/></svg></button></div>" + extra + "</div>");
            return sb.ToString();
        }

        private static decimal Median(List<decimal> v)
        {
            if (v.Count == 0)
            {
                return 0;
            }
            List<decimal> s = v.OrderBy(x => x).ToList();
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2;
        }

        private async Task<View> Render(SiteData site)
        {
            DateTime today = Store.Today(site);
            List<Property> live = site.Listings.Where(Ui.Live).ToList();
            List<Models.Area> areas = site.Areas.Where(a => a.Active).OrderBy(a => a.Order).ToList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<section class=\"hm-hero\"><img class=\"hm-hero-img\" src=\"images/homes/full/hero-street.webp\" alt=\"\" width=\"1600\" height=\"1067\"><div class=\"hm-wrap hm-hero-in\"><span class=\"hm-kick hm-hk\">" + Ui.Enc(site.Settings.Name) + " &middot; " + Ui.Enc(site.Settings.Town) + "</span><h1>" + Ui.Enc(site.Settings.HeroTitle) + "</h1><p>" + Ui.Enc(site.Settings.HeroText) + "</p>");
            sb.Append("<form class=\"hm-hs\" action=\"Listings\" method=\"get\" role=\"search\" onsubmit=\"HomeJs.tidy(this)\"><div class=\"hm-hseg\" role=\"group\" aria-label=\"Buy or rent\"><button type=\"button\" class=\"hm-act\" id=\"hm-hd-sale\" aria-pressed=\"true\" onclick=\"HomeJs.deal('sale')\">Buy</button><button type=\"button\" id=\"hm-hd-rent\" aria-pressed=\"false\" onclick=\"HomeJs.deal('rent')\">Rent</button></div><input type=\"hidden\" name=\"deal\" id=\"hm-hdeal\" value=\"sale\">");
            sb.Append("<div class=\"hm-hsrow\"><label class=\"hm-hf hm-hq\"><span>Where</span><input type=\"search\" name=\"q\" maxlength=\"60\" placeholder=\"Street or neighborhood\" list=\"hm-places\" autocomplete=\"off\"></label>" + Ui.DataList("hm-places", areas.Select(a => a.Name)));
            sb.Append("<label class=\"hm-hf\"><span>Max price</span><select name=\"max\" id=\"hm-hmax\"></select></label><label class=\"hm-hf\"><span>Beds</span><select name=\"beds\">" + Ui.Opts(new[] { ("", "Any"), ("1", "1+"), ("2", "2+"), ("3", "3+"), ("4", "4+") }, string.Empty) + "</select></label><button type=\"submit\" class=\"hm-btn hm-btn-accent hm-hgo\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"11\" cy=\"11\" r=\"6.5\"/><path d=\"M16 16l4.5 4.5\"/></svg><span>Search</span></button></div></form>");
            int sale = live.Count(l => l.Deal == "sale" && l.Status == "active");
            int rent = live.Count(l => l.Deal == "rent" && l.Status == "active");
            List<int> doms = site.Listings.Where(l => l.Status == "active" && l.Deal == "sale").Select(l => Math.Max(1, (today - l.Listed.Date).Days)).ToList();
            sb.Append("<div class=\"hm-hstats\"><a href=\"Listings?deal=sale\"><b>" + sale + "</b><span>for sale</span></a><a href=\"Listings?deal=rent\"><b>" + rent + "</b><span>for rent</span></a><a href=\"#hm-areas\"><b>" + areas.Count + "</b><span>neighborhoods</span></a><a href=\"Agents\"><b>" + site.Agents.Count(a => a.Active) + "</b><span>local " + Ui.Enc(site.Settings.AgentsWord.ToLowerInvariant()) + "</span></a></div></div></section>");
            List<Property> feat = live.Where(l => l.Featured && l.Status == "active").OrderByDescending(l => l.Listed).Take(6).ToList();
            if (feat.Count < 3)
            {
                feat = live.Where(l => l.Deal == "sale" && l.Status == "active").OrderByDescending(l => l.Listed).Take(6).ToList();
            }
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-shead\"><div><span class=\"hm-kick\">Featured</span><h2 class=\"hm-h2\">Homes we love this week</h2></div><a class=\"hm-more\" href=\"Listings?deal=sale\">All homes for sale &rarr;</a></div><div class=\"hm-cards\">");
            foreach (Property l in feat)
            {
                sb.Append(Card(site, l, today));
            }
            sb.Append("</div></div></section>");
            sb.Append("<section class=\"hm-sec\" id=\"hm-areas\"><div class=\"hm-wrap\"><div class=\"hm-shead\"><div><span class=\"hm-kick\">Neighborhoods</span><h2 class=\"hm-h2\">Six corners of " + Ui.Enc(site.Settings.Town) + "</h2></div></div><div class=\"hm-areas\">");
            foreach (Models.Area a in areas)
            {
                List<Property> inArea = live.Where(l => l.Area == a.Key).ToList();
                decimal med = Median(inArea.Where(l => l.Deal == "sale").Select(l => l.Price).ToList());
                sb.Append("<a class=\"hm-at\" href=\"Listings?area=" + Uri.EscapeDataString(a.Key) + "\" style=\"--c:" + (Ui.SafeColor(a.Color) ? a.Color : "#B5562F") + "\">" + Ui.Pic(a.Image, string.Empty, string.Empty, true) + "<span class=\"hm-at-b\"><b>" + Ui.Enc(a.Name) + "</b><small>" + Ui.Enc(a.Blurb) + "</small><em>" + Ui.Plural(inArea.Count, "home", "homes") + (med > 0 ? " &middot; median " + Ui.Short(med) : string.Empty) + "</em></span></a>");
            }
            sb.Append("</div></div></section>");
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap hm-mapband\"><div class=\"hm-mtext\"><span class=\"hm-kick\">Map search</span><h2 class=\"hm-h2\">See every home on the map</h2><p class=\"hm-lead\">Drag, zoom and search just the streets you want. Prices sit right on the pins, and sold homes nearby help you compare.</p><a class=\"hm-btn hm-btn-accent\" href=\"Listings?view=map\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M3 6.5l6-2.5 6 2.5 6-2.5v13.5l-6 2.5-6-2.5-6 2.5z\"/><path d=\"M9 4v13.5M15 6.5V20\"/></svg> Open the map</a></div>" + MapHtml("hm", "hm-hmap", "hm-tmap", areas, Pins(site, live), string.Empty) + "</div></section>");
            decimal sample = Median(live.Where(l => l.Deal == "sale").Select(l => l.Price).ToList());
            sample = sample > 0 ? Math.Round(sample / 5000m) * 5000m : 600000m;
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-calcband\"><div><span class=\"hm-kick\">Mortgage</span><h2 class=\"hm-h2\">What would you pay each month?</h2><p class=\"hm-lead\">A " + site.Settings.Term + "-year loan at " + site.Settings.Rate.ToString("0.###", Ui.Inv) + "% with taxes and insurance included. Change the numbers to see.</p></div><form class=\"hm-qc\" onsubmit=\"return false\" oninput=\"HomeJs.quick()\" onchange=\"HomeJs.quick()\"><label class=\"hm-field\"><span>Home price</span><input type=\"text\" inputmode=\"numeric\" id=\"hm-kprice\" value=\"" + sample.ToString("#,##0", Ui.Inv) + "\" maxlength=\"12\"></label><label class=\"hm-field\"><span>Down payment</span><select id=\"hm-kdown\">" + Ui.Opts(new[] { "3.5", "5", "10", "15", "20", "25", "30" }.Select(d => (d, d + "%")), site.Settings.DownPct.ToString("0.#", Ui.Inv)) + "</select></label><div class=\"hm-qres\" id=\"hm-qres\" aria-live=\"polite\">" + QuickText(site, sample, site.Settings.DownPct) + "</div></form></div></div></section>");
            List<TeamMember> team = site.Agents.Where(a => a.Active).OrderBy(a => a.Order).ToList();
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap hm-about\"><div><span class=\"hm-kick\">About us</span><h2 class=\"hm-h2\">" + Ui.Enc(site.Settings.AboutTitle) + "</h2><p class=\"hm-lead\">" + Ui.Enc(site.Settings.AboutText) + "</p><a class=\"hm-more\" href=\"Agents\">Meet the " + Ui.Enc(site.Settings.AgentsWord.ToLowerInvariant()) + " &rarr;</a></div><div class=\"hm-faces\">");
            foreach (TeamMember a in team.Take(6))
            {
                sb.Append("<a href=\"Agent?id=" + Uri.EscapeDataString(a.Id) + "\" class=\"hm-fc\">" + Ui.Face("hm", a) + "<b>" + Ui.Enc(a.Name) + "</b><small>" + Ui.Enc(a.Title) + "</small></a>");
            }
            sb.Append("</div></div></section>");
            List<Property> rents = live.Where(l => l.Deal == "rent" && l.Status == "active").OrderByDescending(l => l.Listed).Take(3).ToList();
            if (rents.Count > 0)
            {
                sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-shead\"><div><span class=\"hm-kick\">To rent</span><h2 class=\"hm-h2\">Newest rentals</h2></div><a class=\"hm-more\" href=\"Listings?deal=rent\">All rentals &rarr;</a></div><div class=\"hm-cards\">");
                foreach (Property l in rents)
                {
                    sb.Append(Card(site, l, today));
                }
                sb.Append("</div></div></section>");
            }
            sb.Append("<section class=\"hm-sec\"><div class=\"hm-wrap\"><div class=\"hm-sellband\"><img src=\"images/homes/thumbs/colonial.webp\" alt=\"\" loading=\"lazy\" width=\"640\" height=\"427\"><div><span class=\"hm-kick\">Selling?</span><h2 class=\"hm-h2\">" + Ui.Enc(site.Settings.SellTitle) + "</h2><p class=\"hm-lead\">" + Ui.Enc(site.Settings.SellText) + "</p><a class=\"hm-btn hm-btn-accent\" href=\"Sell\">Get a free valuation</a></div></div></div></section>");
            return new View { Desc = site.Settings.Tagline, Main = sb.ToString(), Nav = "home" };
        }

        private static string QuickText(SiteData site, decimal price, decimal down)
        {
            Payment p = Calc.Pay(price, down, site.Settings.Rate, site.Settings.Term, site.Settings.TaxRate, site.Settings.Insurance, 0, site.Settings.Pmi);
            return "<b>" + Ui.Whole(p.Total) + "<small>/mo</small></b><span>" + Ui.Whole(p.Principal) + " loan payment, " + Ui.Whole(p.Tax + p.Insurance + p.Pmi) + " taxes and insurance</span><a class=\"hm-more\" href=\"Mortgage?price=" + price.ToString("0", Ui.Inv) + "&amp;down=" + down.ToString("0.#", Ui.Inv) + "\">Full calculator &rarr;</a>";
        }

        public async Task<ApiResponse> Quick()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            decimal price = decimal.TryParse(Ui.Clip(GetDataValue("kprice"), 14).Replace(",", string.Empty).Replace("$", string.Empty), NumberStyles.Number, Ui.Inv, out decimal pv) ? Math.Clamp(pv, 0, 100000000m) : 0;
            decimal down = decimal.TryParse(GetDataValue("kdown"), NumberStyles.Number, Ui.Inv, out decimal dv) ? Math.Clamp(dv, 0, 100) : site.Settings.DownPct;
            response.SetElementContents("hm-qres", QuickText(site, price, down));
            return response;
        }
    }
}
