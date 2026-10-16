using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class Mortgage : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--mg-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "mg", v.Nav, false))
                .Replace("{plhd_drawer}", Nav(site, "mg", v.Nav, true))
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
                return "<i class=\"mg-shut\"></i>Office opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"mg-shut\"></i>Office closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"mg-shut\"></i>Office closed";
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
                response.SetElementContents("mg-e-nlmail", "Enter an email address like name@example.com.");
                return response;
            }
            bool added = await Store.AddSubscriber(mail);
            response.ExecuteScript("MortgageJs.subscribed('" + (added ? "Thanks. You will hear from us when new homes are listed." : "You are already on the list.") + "');");
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
            return "<article class=\"mg-hc\" data-lid=\"" + Ui.Attr(l.Id) + "\"><a class=\"mg-hc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(l.Photos.FirstOrDefault() ?? string.Empty, string.Empty, string.Empty, true) + (text != string.Empty ? "<span class=\"mg-badge mg-b-" + cls + "\">" + text + "</span>" : string.Empty) + "<span class=\"mg-pics\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 8h3l1.6-2.5h6.8L17 8h3v11H4z\"/><circle cx=\"12\" cy=\"13.2\" r=\"3.4\"/></svg>" + l.Photos.Count + "</span></a><button type=\"button\" class=\"mg-heart\" data-heart=\"" + Ui.Attr(l.Id) + "\" aria-pressed=\"false\" aria-label=\"Save this home\" onclick=\"MortgageJs.heart(this)\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg></button><div class=\"mg-hc-b\"><span class=\"mg-price\">" + Ui.PriceText(l) + "</span><span class=\"mg-hc-f\">" + Facts(l) + "</span><span class=\"mg-hc-a\"><a href=\"" + href + "\">" + Ui.Enc(l.Street) + "</a></span><span class=\"mg-hc-m\"><span>" + Ui.Enc(Ui.AreaName(site, l.Area)) + "</span><span>" + meta + "</span></span></div></article>";
        }

        private static string Pins(SiteData site, IEnumerable<Property> list)
        {
            var items = list.Select(l => new { i = l.Id, x = Math.Round(l.X, 1), y = Math.Round(l.Y, 1), p = Ui.Short(l.Price) + (l.Deal == "rent" ? "/mo" : string.Empty), r = Ui.PricePlain(l), f = FactsPlain(l), a = l.Street + ", " + Ui.AreaName(site, l.Area), t = Ui.Thumb(l.Photos.FirstOrDefault() ?? string.Empty), h = "Listing?id=" + Uri.EscapeDataString(l.Id), c = "mg-p-" + (l.Status == "active" ? l.Deal : l.Status) });
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
            sb.Append("<div class=\"" + p + "-pins\"></div></div><div class=\"" + p + "-mctl\"><button type=\"button\" aria-label=\"Zoom in\" onclick=\"MortgageJs.zoom('" + id + "',1.4)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Zoom out\" onclick=\"MortgageJs.zoom('" + id + "',0.714)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Show all\" onclick=\"MortgageJs.fit('" + id + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5\"/></svg></button></div>" + extra + "</div>");
            return sb.ToString();
        }

        private static decimal Dec(string v, decimal def, decimal min, decimal max)
        {
            string s = (v ?? string.Empty).Replace(",", string.Empty).Replace("$", string.Empty).Replace("%", string.Empty).Trim();
            return s != string.Empty && decimal.TryParse(s, NumberStyles.Number, Ui.Inv, out decimal d) ? Math.Clamp(d, min, max) : def;
        }

        private static string Field(string id, string label, string value, string hint, string mode = "decimal")
        {
            return "<label class=\"mg-field\"><span>" + label + "</span><input type=\"text\" inputmode=\"" + mode + "\" id=\"mg-" + id + "\" value=\"" + Ui.Attr(value) + "\" maxlength=\"14\" autocomplete=\"off\">" + (hint != string.Empty ? "<small class=\"mg-fh\" id=\"mg-h-" + id + "\">" + hint + "</small>" : string.Empty) + "</label>";
        }

        private static string Results(SiteData site, decimal price, decimal down, decimal rate, int term, decimal tax, decimal ins, decimal hoa, decimal pmi)
        {
            Payment p = Calc.Pay(price, down, rate, term, tax, ins, hoa, pmi);
            (string Name, decimal V, string C)[] parts = { ("Principal and interest", p.Principal, "#17232F"), ("Property tax", p.Tax, "#B5562F"), ("Home insurance", p.Insurance, "#2F5D50"), ("HOA fees", p.Hoa, "#C9A227"), ("Mortgage insurance", p.Pmi, "#8A8F94") };
            decimal total = p.Total;
            StringBuilder sb = new StringBuilder("<div class=\"mg-res\"><div class=\"mg-donut\"><svg viewBox=\"0 0 160 160\" width=\"190\" height=\"190\" role=\"img\" aria-label=\"Monthly payment split\"><circle cx=\"80\" cy=\"80\" r=\"62\" fill=\"none\" stroke=\"var(--mg-soft)\" stroke-width=\"22\"/>");
            double c = 2 * Math.PI * 62;
            double off = 0;
            if (total > 0)
            {
                foreach ((string n, decimal v, string col) in parts.Where(x => x.V > 0))
                {
                    double len = (double)(v / total) * c;
                    sb.Append("<circle cx=\"80\" cy=\"80\" r=\"62\" fill=\"none\" stroke=\"" + col + "\" stroke-width=\"22\" stroke-dasharray=\"" + len.ToString("0.##", Ui.Inv) + " " + (c - len).ToString("0.##", Ui.Inv) + "\" stroke-dashoffset=\"" + (-off).ToString("0.##", Ui.Inv) + "\" transform=\"rotate(-90 80 80)\"/>");
                    off += len;
                }
            }
            sb.Append("</svg><div class=\"mg-dc\"><b>" + Ui.Whole(total) + "</b><span>per month</span></div></div><dl class=\"mg-plist\">");
            foreach ((string n, decimal v, string col) in parts)
            {
                if (v <= 0 && n is "Mortgage insurance" or "HOA fees")
                {
                    continue;
                }
                sb.Append("<dt><i style=\"background:" + col + "\"></i>" + n + "</dt><dd>" + Ui.Whole(v) + "</dd>");
            }
            sb.Append("<dt class=\"mg-tot\">Total each month</dt><dd class=\"mg-tot\">" + Ui.Whole(total) + "</dd></dl></div>");
            DateTime payoff = DateTime.Today.AddMonths(term * 12 + 1);
            sb.Append("<div class=\"mg-sum\"><div><span>Loan amount</span><b>" + Ui.Whole(p.Loan) + "</b></div><div><span>Down payment</span><b>" + Ui.Whole(p.Down) + "</b></div><div><span>Total interest</span><b>" + Ui.Whole(p.Interest) + "</b></div><div><span>Paid off</span><b>" + payoff.ToString("MMM yyyy", Ui.Inv) + "</b></div></div>");
            if (p.Pmi > 0)
            {
                sb.Append("<p class=\"mg-info-box mg-pmi\">With less than 20% down, lenders usually add mortgage insurance until you own 20% of the home.</p>");
            }
            List<YearRow> rows = Calc.Schedule(p.Loan, rate, term);
            if (rows.Count > 0)
            {
                decimal max = rows.Max(r => r.Principal + r.Interest);
                double w = 600.0 / rows.Count;
                StringBuilder bars = new StringBuilder();
                for (int i = 0; i < rows.Count; i++)
                {
                    YearRow r = rows[i];
                    double hi = max > 0 ? (double)(r.Interest / max) * 170 : 0;
                    double hp = max > 0 ? (double)(r.Principal / max) * 170 : 0;
                    string x = (i * w + w * 0.12).ToString("0.##", Ui.Inv);
                    string bw = (w * 0.76).ToString("0.##", Ui.Inv);
                    bars.Append("<rect x=\"" + x + "\" y=\"" + (180 - hp - hi).ToString("0.##", Ui.Inv) + "\" width=\"" + bw + "\" height=\"" + hi.ToString("0.##", Ui.Inv) + "\" fill=\"#B5562F\"/><rect x=\"" + x + "\" y=\"" + (180 - hp).ToString("0.##", Ui.Inv) + "\" width=\"" + bw + "\" height=\"" + hp.ToString("0.##", Ui.Inv) + "\" fill=\"#17232F\"/>");
                }
                sb.Append("<div class=\"mg-chart\"><h3>Where each year's payments go</h3><svg viewBox=\"0 0 600 200\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"Principal and interest paid each year\">" + bars + "<line x1=\"0\" y1=\"180.5\" x2=\"600\" y2=\"180.5\" stroke=\"var(--mg-line)\"/></svg><div class=\"mg-axis\"><span>Year 1</span><span>Year " + rows.Count + "</span></div><div class=\"mg-legend\"><span><i style=\"background:#17232F\"></i>Principal</span><span><i style=\"background:#B5562F\"></i>Interest</span></div></div>");
                sb.Append("<details class=\"mg-amort\"><summary>Yearly schedule</summary><div class=\"mg-tb\"><div class=\"mg-tr mg-th\"><span>Year</span><span>Principal</span><span>Interest</span><span>Balance</span></div>");
                foreach (YearRow r in rows)
                {
                    sb.Append("<div class=\"mg-tr\"><span>" + r.Year + "</span><span>" + Ui.Whole(r.Principal) + "</span><span>" + Ui.Whole(r.Interest) + "</span><span>" + Ui.Whole(r.Balance) + "</span></div>");
                }
                sb.Append("</div></details>");
            }
            sb.Append("<p class=\"mg-hint mg-disc\">" + Ui.Enc(site.Settings.Disclaimer) + "</p>");
            return sb.ToString();
        }

        private static string AffordText(SiteData site, decimal income, decimal debts, decimal down, decimal rate, int term)
        {
            decimal max = Calc.Afford(income, debts, down, rate, term, site.Settings.TaxRate, site.Settings.Insurance, 0);
            if (income <= 0)
            {
                return "<p class=\"mg-none\">Enter your yearly income before tax.</p>";
            }
            if (max <= down)
            {
                return "<div class=\"mg-abig\"><span>With these numbers</span><b>Your debts use most of your budget</b></div><p class=\"mg-hint\">Lenders usually keep all monthly debts, including the new home, under 36% of income. Paying down other loans first helps.</p>";
            }
            Payment p = Calc.Pay(max, max > 0 ? Math.Min(100m, down / max * 100m) : 0, rate, term, site.Settings.TaxRate, site.Settings.Insurance, 0, site.Settings.Pmi);
            int homes = site.Listings.Count(l => l.Deal == "sale" && l.Status == "active" && l.Price <= max);
            return "<div class=\"mg-abig\"><span>You could afford a home up to about</span><b>" + Ui.Whole(max) + "</b><em>about " + Ui.Whole(p.Total) + " a month with taxes and insurance</em></div><a class=\"mg-btn mg-btn-accent\" href=\"Listings?deal=sale&amp;max=" + max.ToString("0", Ui.Inv) + "\">See " + Ui.Plural(homes, "home", "homes") + " up to " + Ui.Short(max) + "</a><p class=\"mg-hint\">Based on the common 28/36 rule: housing costs up to 28% of your monthly income, and all debts together up to 36%. " + Ui.Enc(site.Settings.Disclaimer) + "</p>";
        }

        private async Task<View> Render(SiteData site)
        {
            Settings s = site.Settings;
            decimal price = Dec(Q("price"), 650000m, 0, 100000000m);
            decimal down = Dec(Q("down"), s.DownPct, 0, 100);
            decimal hoa = Dec(Q("hoa"), 0, 0, 100000);
            decimal taxYear = Dec(Q("tax"), 0, 0, 10000000);
            decimal taxPct = taxYear > 0 && price > 0 ? Math.Round(taxYear / price * 100m, 2) : s.TaxRate;
            StringBuilder sb = new StringBuilder("<section class=\"mg-phead mg-wrap\"><span class=\"mg-kick\">Mortgage calculator</span><h1>What will it cost each month?</h1><p class=\"mg-lead\">Change any number and the payment updates. Taxes, insurance and fees are included, so there are no surprises.</p></section>");
            sb.Append("<div class=\"mg-wrap\"><div class=\"mg-mtabs\" role=\"tablist\"><button type=\"button\" role=\"tab\" id=\"mg-t-pay\" class=\"mg-act\" aria-selected=\"true\" onclick=\"MortgageJs.tab('pay')\">Monthly payment</button><button type=\"button\" role=\"tab\" id=\"mg-t-aff\" aria-selected=\"false\" onclick=\"MortgageJs.tab('aff')\">How much can I afford?</button></div>");
            sb.Append("<div class=\"mg-mgrid\" id=\"mg-p-pay\"><form class=\"mg-box mg-mform\" onsubmit=\"return false\" oninput=\"MortgageJs.calc()\" onchange=\"MortgageJs.calc()\"><h2>Your numbers</h2>");
            sb.Append(Field("kprice", "Home price ($)", price.ToString("#,##0", Ui.Inv), string.Empty, "numeric"));
            sb.Append(Field("kdown", "Down payment (%)", down.ToString("0.##", Ui.Inv), Ui.Whole(Math.Round(price * down / 100m))));
            sb.Append("<div class=\"mg-row2\">" + Field("krate", "Interest rate (%)", s.Rate.ToString("0.###", Ui.Inv), string.Empty) + "<label class=\"mg-field\"><span>Loan term</span><select id=\"mg-kterm\">" + Ui.Opts(new[] { ("30", "30 years"), ("20", "20 years"), ("15", "15 years"), ("10", "10 years") }, s.Term.ToString(Ui.Inv)) + "</select></label></div>");
            sb.Append("<details class=\"mg-extra\"" + (taxYear > 0 || hoa > 0 ? " open" : string.Empty) + "><summary>Taxes, insurance and fees</summary><div class=\"mg-row2\">" + Field("ktax", "Property tax (% a year)", taxPct.ToString("0.###", Ui.Inv), string.Empty) + Field("kins", "Home insurance ($ a year)", s.Insurance.ToString("0", Ui.Inv), string.Empty, "numeric") + Field("khoa", "HOA fees ($ a month)", hoa.ToString("0", Ui.Inv), string.Empty, "numeric") + Field("kpmi", "Mortgage insurance (% a year)", s.Pmi.ToString("0.##", Ui.Inv), "Only below 20% down") + "</div></details></form>");
            sb.Append("<div class=\"mg-box mg-out\" id=\"mg-out\" aria-live=\"polite\">" + Results(site, price, down, s.Rate, s.Term, taxPct, s.Insurance, hoa, s.Pmi) + "</div></div>");
            sb.Append("<div class=\"mg-mgrid\" id=\"mg-p-aff\" hidden><form class=\"mg-box mg-mform\" onsubmit=\"return false\" oninput=\"MortgageJs.afford()\" onchange=\"MortgageJs.afford()\"><h2>About you</h2>" + Field("aincome", "Household income ($ a year, before tax)", "120,000", string.Empty, "numeric") + Field("adebts", "Other debts ($ a month)", "400", "Car, student loans, card minimums", "numeric") + Field("adown", "Savings for a down payment ($)", "100,000", string.Empty, "numeric") + "<div class=\"mg-row2\">" + Field("arate", "Interest rate (%)", s.Rate.ToString("0.###", Ui.Inv), string.Empty) + "<label class=\"mg-field\"><span>Loan term</span><select id=\"mg-aterm\">" + Ui.Opts(new[] { ("30", "30 years"), ("20", "20 years"), ("15", "15 years") }, s.Term.ToString(Ui.Inv)) + "</select></label></div></form><div class=\"mg-box mg-out mg-aout\" id=\"mg-aout\" aria-live=\"polite\">" + AffordText(site, 120000, 400, 100000, s.Rate, s.Term) + "</div></div>");
            sb.Append("<div class=\"mg-cta2\"><div><h2>Ready to look?</h2><p>Every listing shows its own monthly estimate with the real tax and HOA figures.</p></div><a class=\"mg-btn mg-btn-accent\" href=\"Listings?deal=sale\">Browse homes for sale</a></div></div>");
            return new View { Title = "Mortgage calculator", Desc = "Estimate your monthly mortgage payment in " + s.Town + ", with taxes, insurance and HOA fees, and see how much home you can afford.", Main = sb.ToString(), Nav = "mortgage" };
        }

        public async Task<ApiResponse> Figure()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            Settings s = site.Settings;
            decimal price = Dec(GetDataValue("kprice"), 0, 0, 100000000m);
            decimal down = Dec(GetDataValue("kdown"), s.DownPct, 0, 100);
            decimal rate = Dec(GetDataValue("krate"), s.Rate, 0, 30);
            int term = (int)Dec(GetDataValue("kterm"), s.Term, 5, 40);
            decimal tax = Dec(GetDataValue("ktax"), s.TaxRate, 0, 10);
            decimal ins = Dec(GetDataValue("kins"), s.Insurance, 0, 100000);
            decimal hoa = Dec(GetDataValue("khoa"), 0, 0, 100000);
            decimal pmi = Dec(GetDataValue("kpmi"), s.Pmi, 0, 5);
            response.SetElementContents("mg-out", Results(site, price, down, rate, term, tax, ins, hoa, pmi));
            response.SetElementContents("mg-h-kdown", Ui.Whole(Math.Round(price * down / 100m)));
            return response;
        }

        public async Task<ApiResponse> Afford()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            decimal income = Dec(GetDataValue("aincome"), 0, 0, 100000000m);
            decimal debts = Dec(GetDataValue("adebts"), 0, 0, 10000000m);
            decimal down = Dec(GetDataValue("adown"), 0, 0, 100000000m);
            decimal rate = Dec(GetDataValue("arate"), site.Settings.Rate, 0, 30);
            int term = (int)Dec(GetDataValue("aterm"), site.Settings.Term, 5, 40);
            response.SetElementContents("mg-aout", AffordText(site, income, debts, down, rate, term));
            return response;
        }
    }
}
