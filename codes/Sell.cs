using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class Sell : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--sl-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "sl", v.Nav, false))
                .Replace("{plhd_drawer}", Nav(site, "sl", v.Nav, true))
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
                return "<i class=\"sl-shut\"></i>Office opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"sl-shut\"></i>Office closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"sl-shut\"></i>Office closed";
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
                response.SetElementContents("sl-e-nlmail", "Enter an email address like name@example.com.");
                return response;
            }
            bool added = await Store.AddSubscriber(mail);
            response.ExecuteScript("SellJs.subscribed('" + (added ? "Thanks. You will hear from us when new homes are listed." : "You are already on the list.") + "');");
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
            return "<article class=\"sl-hc\" data-lid=\"" + Ui.Attr(l.Id) + "\"><a class=\"sl-hc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(l.Photos.FirstOrDefault() ?? string.Empty, string.Empty, string.Empty, true) + (text != string.Empty ? "<span class=\"sl-badge sl-b-" + cls + "\">" + text + "</span>" : string.Empty) + "<span class=\"sl-pics\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 8h3l1.6-2.5h6.8L17 8h3v11H4z\"/><circle cx=\"12\" cy=\"13.2\" r=\"3.4\"/></svg>" + l.Photos.Count + "</span></a><button type=\"button\" class=\"sl-heart\" data-heart=\"" + Ui.Attr(l.Id) + "\" aria-pressed=\"false\" aria-label=\"Save this home\" onclick=\"SellJs.heart(this)\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg></button><div class=\"sl-hc-b\"><span class=\"sl-price\">" + Ui.PriceText(l) + "</span><span class=\"sl-hc-f\">" + Facts(l) + "</span><span class=\"sl-hc-a\"><a href=\"" + href + "\">" + Ui.Enc(l.Street) + "</a></span><span class=\"sl-hc-m\"><span>" + Ui.Enc(Ui.AreaName(site, l.Area)) + "</span><span>" + meta + "</span></span></div></article>";
        }

        private static string Pins(SiteData site, IEnumerable<Property> list)
        {
            var items = list.Select(l => new { i = l.Id, x = Math.Round(l.X, 1), y = Math.Round(l.Y, 1), p = Ui.Short(l.Price) + (l.Deal == "rent" ? "/mo" : string.Empty), r = Ui.PricePlain(l), f = FactsPlain(l), a = l.Street + ", " + Ui.AreaName(site, l.Area), t = Ui.Thumb(l.Photos.FirstOrDefault() ?? string.Empty), h = "Listing?id=" + Uri.EscapeDataString(l.Id), c = "sl-p-" + (l.Status == "active" ? l.Deal : l.Status) });
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
            sb.Append("<div class=\"" + p + "-pins\"></div></div><div class=\"" + p + "-mctl\"><button type=\"button\" aria-label=\"Zoom in\" onclick=\"SellJs.zoom('" + id + "',1.4)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Zoom out\" onclick=\"SellJs.zoom('" + id + "',0.714)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Show all\" onclick=\"SellJs.fit('" + id + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5\"/></svg></button></div>" + extra + "</div>");
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
            List<Models.Area> areas = site.Areas.Where(a => a.Active).OrderBy(a => a.Order).ToList();
            StringBuilder sb = new StringBuilder("<section class=\"sl-shero\"><img src=\"images/homes/full/craftsman.webp\" alt=\"\" width=\"1600\" height=\"1067\"><div class=\"sl-wrap sl-shero-in\"><div class=\"sl-stext\"><span class=\"sl-kick\">Sell with " + Ui.Enc(site.Settings.Name) + "</span><h1>" + Ui.Enc(site.Settings.SellTitle) + "</h1><p>" + Ui.Enc(site.Settings.SellText) + "</p><ul class=\"sl-ticks\"><li>Free, with no obligation</li><li>Recent sales on your street</li><li>A plan for getting the best price</li></ul></div>");
            sb.Append("<form class=\"sl-box sl-vform\" id=\"sl-vform\" onsubmit=\"return SellJs.send()\" novalidate><h2>Request a valuation</h2><label class=\"sl-field\"><span>Street address</span><input type=\"text\" id=\"sl-vaddr\" maxlength=\"120\" autocomplete=\"street-address\" placeholder=\"e.g. 12 Fern Lane\"></label><em class=\"sl-err\" id=\"sl-e-vaddr\" role=\"alert\"></em>");
            sb.Append("<div class=\"sl-row2\"><label class=\"sl-field\"><span>Neighborhood</span><select id=\"sl-varea\"><option value=\"\">Not sure</option>" + Ui.Opts(areas.Select(a => (a.Key, a.Name)), string.Empty) + "</select></label><label class=\"sl-field\"><span>Home type</span><select id=\"sl-vkind\">" + Ui.Opts(Ui.Kinds.Select(k => (k.Key, k.Name)), "house") + "</select></label></div>");
            sb.Append("<div class=\"sl-row3\"><label class=\"sl-field\"><span>Beds</span><select id=\"sl-vbeds\">" + Ui.Opts(new[] { ("0", "Studio"), ("1", "1"), ("2", "2"), ("3", "3"), ("4", "4"), ("5", "5+") }, "3") + "</select></label><label class=\"sl-field\"><span>Baths</span><select id=\"sl-vbaths\">" + Ui.Opts(new[] { ("1", "1"), ("1.5", "1.5"), ("2", "2"), ("2.5", "2.5"), ("3", "3+") }, "2") + "</select></label><label class=\"sl-field\"><span>Size (sq ft)</span><input type=\"text\" inputmode=\"numeric\" id=\"sl-vsqft\" maxlength=\"6\" placeholder=\"1,800\"></label></div>");
            sb.Append("<label class=\"sl-field\"><span>When would you like to sell?</span><select id=\"sl-vwhen\">" + Ui.Opts(new[] { ("soon", "As soon as possible"), ("3m", "Within 3 months"), ("6m", "Within 6 months"), ("curious", "Just curious") }, "3m") + "</select></label>");
            sb.Append("<div class=\"sl-row2\"><label class=\"sl-field\"><span>Your name</span><input type=\"text\" id=\"sl-vname\" maxlength=\"80\" autocomplete=\"name\"></label><label class=\"sl-field\"><span>Phone <small>(optional)</small></span><input type=\"tel\" id=\"sl-vphone\" maxlength=\"30\" autocomplete=\"tel\"></label></div><em class=\"sl-err\" id=\"sl-e-vname\" role=\"alert\"></em>");
            sb.Append("<label class=\"sl-field\"><span>Email</span><input type=\"email\" id=\"sl-vmail\" maxlength=\"120\" autocomplete=\"email\"></label><em class=\"sl-err\" id=\"sl-e-vmail\" role=\"alert\"></em><label class=\"sl-field\"><span>Anything else? <small>(optional)</small></span><textarea id=\"sl-vmsg\" rows=\"2\" maxlength=\"1000\" placeholder=\"Recent work, a good time to visit\"></textarea></label><label class=\"sl-hp\" aria-hidden=\"true\">Website<input type=\"text\" id=\"sl-vweb\" tabindex=\"-1\" autocomplete=\"off\"></label><button type=\"submit\" class=\"sl-btn sl-btn-accent sl-btn-wide\" id=\"sl-vsend\">Get my free valuation</button><p class=\"sl-hint\">We only use your details for the valuation.</p></form><div class=\"sl-box sl-vdone\" id=\"sl-vdone\" hidden></div></div></section>");
            sb.Append("<section class=\"sl-sec\"><div class=\"sl-wrap\"><div class=\"sl-shead\"><div><span class=\"sl-kick\">How it works</span><h2 class=\"sl-h2\">From valuation to keys in four steps</h2></div></div><ol class=\"sl-steps\"><li><b>Valuation</b><span>An agent visits, walks the home with you and sends a written price range with the sales it is based on.</span></li><li><b>Get ready</b><span>We suggest small fixes worth doing, book the photographer and draw the floor plan.</span></li><li><b>On the market</b><span>Your home goes live here with photos, a map pin and open houses. We run every viewing.</span></li><li><b>Offer to closing</b><span>We negotiate, handle the paperwork with your lawyer and keep you posted until closing day.</span></li></ol></div></section>");
            sb.Append("<section class=\"sl-sec\"><div class=\"sl-wrap\"><div class=\"sl-shead\"><div><span class=\"sl-kick\">Market today</span><h2 class=\"sl-h2\">Asking prices by neighborhood</h2></div><a class=\"sl-more\" href=\"Listings?deal=sale\">Homes for sale &rarr;</a></div><div class=\"sl-mkt\"><div class=\"sl-mr sl-mh\"><span>Neighborhood</span><span>For sale</span><span>Median price</span><span>Per sq ft</span></div>");
            foreach (Models.Area a in areas)
            {
                List<Property> act = site.Listings.Where(l => l.Area == a.Key && l.Deal == "sale" && l.Status == "active" && l.Sqft > 0).ToList();
                decimal med = Median(act.Select(l => l.Price).ToList());
                decimal ppsf = Median(act.Select(l => l.Price / l.Sqft).ToList());
                sb.Append("<a class=\"sl-mr\" href=\"Listings?deal=sale&amp;area=" + Uri.EscapeDataString(a.Key) + "\"><span><i style=\"background:" + (Ui.SafeColor(a.Color) ? a.Color : "#B5562F") + "\"></i>" + Ui.Enc(a.Name) + "</span><span data-l=\"For sale\">" + act.Count + "</span><span data-l=\"Median\">" + (med > 0 ? Ui.Whole(med) : "&ndash;") + "</span><span data-l=\"Per sq ft\">" + (ppsf > 0 ? Ui.Whole(ppsf) : "&ndash;") + "</span></a>");
            }
            sb.Append("</div><p class=\"sl-hint sl-mnote\">Based on homes listed with us today. Your valuation uses recent sales close to your home.</p></div></section>");
            return new View { Title = "Sell your home", Desc = "Get a free valuation of your home in " + site.Settings.Town + " from a local agent, with recent sales nearby.", Main = sb.ToString(), Nav = "sell" };
        }

        public async Task<ApiResponse> Send()
        {
            ApiResponse response = new ApiResponse();
            string addr = Regex.Replace(Ui.Clip(GetDataValue("vaddr"), 120), "\\s+", " ").Trim();
            string name = Regex.Replace(Ui.Clip(GetDataValue("vname"), 80), "\\s+", " ").Trim();
            string mail = Ui.Clip(GetDataValue("vmail"), 120).Trim();
            if (addr.Length < 4)
            {
                response.ExecuteScript("SellJs.fieldErr('vaddr','Please enter the street address.');SellJs.sent(false);");
                return response;
            }
            if (name.Length < 2)
            {
                response.ExecuteScript("SellJs.fieldErr('vname','Please enter your name.');SellJs.sent(false);");
                return response;
            }
            if (!Ui.Email(mail))
            {
                response.ExecuteScript("SellJs.fieldErr('vmail','Enter an email address like name@example.com.');SellJs.sent(false);");
                return response;
            }
            if (Ui.Clip(GetDataValue("vweb"), 50) != string.Empty)
            {
                response.ExecuteScript("SellJs.sent(true,'Thank you.');");
                return response;
            }
            string kind = Ui.Kinds.Any(k => k.Key == GetDataValue("vkind")) ? GetDataValue("vkind") : "house";
            string beds = GetDataValue("vbeds") is "0" or "1" or "2" or "3" or "4" or "5" ? GetDataValue("vbeds") : "3";
            string baths = GetDataValue("vbaths") is "1" or "1.5" or "2" or "2.5" or "3" ? GetDataValue("vbaths") : "2";
            int sqft = int.TryParse(Ui.Digits(Ui.Clip(GetDataValue("vsqft"), 8)), out int sq) ? Math.Clamp(sq, 0, 50000) : 0;
            string when = GetDataValue("vwhen") switch { "soon" => "as soon as possible", "6m" => "within 6 months", "curious" => "just curious", _ => "within 3 months" };
            string first = "An agent";
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Models.Area? area = site.Areas.FirstOrDefault(a => a.Key == GetDataValue("varea") && a.Active);
                TeamMember? agent = area != null ? site.Agents.Where(a => a.Active && a.Areas.Contains(area.Key)).OrderBy(a => a.Order).FirstOrDefault() : null;
                if (agent != null)
                {
                    first = agent.Name.Split(' ')[0];
                }
                string details = Ui.KindName(kind) + " · " + (beds == "0" ? "studio" : beds + (beds == "5" ? "+" : string.Empty) + " beds") + " · " + baths + (baths == "3" ? "+" : string.Empty) + " baths" + (sqft > 0 ? " · " + Ui.Num(sqft) + " sq ft" : string.Empty) + (area != null ? " · " + area.Name : string.Empty) + " · Selling " + when;
                if (!site.Leads.Any(x => x.Kind == "valuation" && x.Email.Equals(mail, StringComparison.OrdinalIgnoreCase) && x.Address == addr && x.Created > DateTime.UtcNow.AddMinutes(-10)))
                {
                    site.Leads.Add(new Lead { Id = Store.NextId(site.Leads.Select(x => x.Id), "LD-", 1000), Kind = "valuation", Name = name, Email = mail, Phone = Ui.CleanPhone(GetDataValue("vphone")), Address = addr, Details = details, Message = Ui.Clip(GetDataValue("vmsg"), 1000).Trim(), Agent = agent?.Id ?? string.Empty, Created = DateTime.UtcNow });
                    await Store.SaveSite(site);
                }
            }
            await Store.Log("web", "valuation request", name, true);
            response.ExecuteScript("SellJs.sent(true,'" + Ui.Js("Thanks, " + name.Split(' ')[0] + ". " + first + " will call or email within two working days to arrange a visit to " + addr + ".") + "');");
            return response;
        }
    }
}
