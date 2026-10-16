using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class Contact : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ct-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "ct", v.Nav, false))
                .Replace("{plhd_drawer}", Nav(site, "ct", v.Nav, true))
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
                return "<i class=\"ct-shut\"></i>Office opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"ct-shut\"></i>Office closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"ct-shut\"></i>Office closed";
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
                response.SetElementContents("ct-e-nlmail", "Enter an email address like name@example.com.");
                return response;
            }
            bool added = await Store.AddSubscriber(mail);
            response.ExecuteScript("ContactJs.subscribed('" + (added ? "Thanks. You will hear from us when new homes are listed." : "You are already on the list.") + "');");
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
            return "<article class=\"ct-hc\" data-lid=\"" + Ui.Attr(l.Id) + "\"><a class=\"ct-hc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(l.Photos.FirstOrDefault() ?? string.Empty, string.Empty, string.Empty, true) + (text != string.Empty ? "<span class=\"ct-badge ct-b-" + cls + "\">" + text + "</span>" : string.Empty) + "<span class=\"ct-pics\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 8h3l1.6-2.5h6.8L17 8h3v11H4z\"/><circle cx=\"12\" cy=\"13.2\" r=\"3.4\"/></svg>" + l.Photos.Count + "</span></a><button type=\"button\" class=\"ct-heart\" data-heart=\"" + Ui.Attr(l.Id) + "\" aria-pressed=\"false\" aria-label=\"Save this home\" onclick=\"ContactJs.heart(this)\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg></button><div class=\"ct-hc-b\"><span class=\"ct-price\">" + Ui.PriceText(l) + "</span><span class=\"ct-hc-f\">" + Facts(l) + "</span><span class=\"ct-hc-a\"><a href=\"" + href + "\">" + Ui.Enc(l.Street) + "</a></span><span class=\"ct-hc-m\"><span>" + Ui.Enc(Ui.AreaName(site, l.Area)) + "</span><span>" + meta + "</span></span></div></article>";
        }

        private static string Pins(SiteData site, IEnumerable<Property> list)
        {
            var items = list.Select(l => new { i = l.Id, x = Math.Round(l.X, 1), y = Math.Round(l.Y, 1), p = Ui.Short(l.Price) + (l.Deal == "rent" ? "/mo" : string.Empty), r = Ui.PricePlain(l), f = FactsPlain(l), a = l.Street + ", " + Ui.AreaName(site, l.Area), t = Ui.Thumb(l.Photos.FirstOrDefault() ?? string.Empty), h = "Listing?id=" + Uri.EscapeDataString(l.Id), c = "ct-p-" + (l.Status == "active" ? l.Deal : l.Status) });
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
            sb.Append("<div class=\"" + p + "-pins\"></div></div><div class=\"" + p + "-mctl\"><button type=\"button\" aria-label=\"Zoom in\" onclick=\"ContactJs.zoom('" + id + "',1.4)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Zoom out\" onclick=\"ContactJs.zoom('" + id + "',0.714)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Show all\" onclick=\"ContactJs.fit('" + id + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5\"/></svg></button></div>" + extra + "</div>");
            return sb.ToString();
        }

        private async Task<View> Render(SiteData site)
        {
            Settings s = site.Settings;
            string tel = Regex.Replace(s.Phone, "[^0-9+]", string.Empty);
            string pin = JsonSerializer.Serialize(new[] { new { i = "office", x = 548.0, y = 486.0, p = s.Name, c = "ct-office" } });
            StringBuilder sb = new StringBuilder("<section class=\"ct-phead ct-wrap\"><span class=\"ct-kick\">Contact</span><h1>Come and see us</h1><p class=\"ct-lead\">Our office is on Market Square in Old Harbor, five minutes on foot from the ferry. Call, write or drop in.</p></section>");
            sb.Append("<div class=\"ct-wrap ct-cgrid\"><div class=\"ct-cleft\"><div class=\"ct-box ct-office\"><h2>" + Ui.Enc(s.Name) + "</h2><p class=\"ct-oaddr\">" + Ui.Enc(s.Address) + "</p><div class=\"ct-ob\"><a class=\"ct-btn ct-btn-accent ct-btn-sm\" href=\"tel:" + Ui.Attr(tel) + "\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg> " + Ui.Enc(s.Phone) + "</a><a class=\"ct-btn ct-btn-ghost ct-btn-sm\" href=\"mailto:" + Ui.Attr(s.Email) + "\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg> " + Ui.Enc(s.Email) + "</a></div><h3>Office hours</h3><div class=\"ct-hours\">" + HoursRows(site, "ct") + "</div><p class=\"ct-hint\">Viewings also run outside office hours by arrangement. " + Ui.Enc(s.License) + "</p></div>");
            sb.Append(MapHtml("ct", "ct-cmap", "ct-omap", site.Areas.Where(a => a.Active), pin, string.Empty) + "</div>");
            sb.Append("<div class=\"ct-box ct-cform\"><h2>Send us a message</h2><form class=\"ct-form\" id=\"ct-cf\" onsubmit=\"return ContactJs.send()\" novalidate><label class=\"ct-field\"><span>What is it about?</span><select id=\"ct-ctopic\">" + Ui.Opts(new[] { ("Buying", "Buying a home"), ("Renting", "Renting a home"), ("Selling", "Selling or letting my home"), ("Other", "Something else") }, "Buying") + "</select></label><div class=\"ct-row2\"><label class=\"ct-field\"><span>Your name</span><input type=\"text\" id=\"ct-cname\" maxlength=\"80\" autocomplete=\"name\"></label><label class=\"ct-field\"><span>Phone <small>(optional)</small></span><input type=\"tel\" id=\"ct-cphone\" maxlength=\"30\" autocomplete=\"tel\"></label></div><em class=\"ct-err\" id=\"ct-e-cname\" role=\"alert\"></em><label class=\"ct-field\"><span>Email</span><input type=\"email\" id=\"ct-cmail\" maxlength=\"120\" autocomplete=\"email\"></label><em class=\"ct-err\" id=\"ct-e-cmail\" role=\"alert\"></em><label class=\"ct-field\"><span>Message</span><textarea id=\"ct-cmsg\" rows=\"6\" maxlength=\"1500\"></textarea></label><em class=\"ct-err\" id=\"ct-e-cmsg\" role=\"alert\"></em><label class=\"ct-hp\" aria-hidden=\"true\">Website<input type=\"text\" id=\"ct-cweb\" tabindex=\"-1\" autocomplete=\"off\"></label><button type=\"submit\" class=\"ct-btn ct-btn-accent ct-btn-wide\" id=\"ct-csend\">Send message</button><p class=\"ct-hint\">We answer within one working day and only use your details to reply.</p></form><div id=\"ct-cdone\"></div></div></div>");
            return new View { Title = "Contact", Desc = "Contact " + s.Name + ": " + s.Address + ", " + s.Phone + ".", Main = sb.ToString(), Nav = "contact" };
        }

        public async Task<ApiResponse> Send()
        {
            ApiResponse response = new ApiResponse();
            string name = Regex.Replace(Ui.Clip(GetDataValue("cname"), 80), "\\s+", " ").Trim();
            string mail = Ui.Clip(GetDataValue("cmail"), 120).Trim();
            string msg = Ui.Clip(GetDataValue("cmsg"), 1500).Trim();
            string topic = GetDataValue("ctopic") is "Buying" or "Selling" or "Renting" ? GetDataValue("ctopic") : "Other";
            if (name.Length < 2)
            {
                response.ExecuteScript("ContactJs.fieldErr('cname','Please enter your name.');ContactJs.sent(false);");
                return response;
            }
            if (!Ui.Email(mail))
            {
                response.ExecuteScript("ContactJs.fieldErr('cmail','Enter an email address like name@example.com.');ContactJs.sent(false);");
                return response;
            }
            if (msg.Length < 3)
            {
                response.ExecuteScript("ContactJs.fieldErr('cmsg','Please write a short message.');ContactJs.sent(false);");
                return response;
            }
            if (Ui.Clip(GetDataValue("cweb"), 50) == string.Empty)
            {
                using (await Store.Hold())
                {
                    SiteData site = await Store.LoadSite();
                    if (!site.Leads.Any(x => x.Kind == "contact" && x.Email.Equals(mail, StringComparison.OrdinalIgnoreCase) && x.Message == msg))
                    {
                        site.Leads.Add(new Lead { Id = Store.NextId(site.Leads.Select(x => x.Id), "LD-", 1000), Kind = "contact", Name = name, Email = mail, Phone = Ui.CleanPhone(GetDataValue("cphone")), Message = msg, Details = topic, Created = DateTime.UtcNow });
                        await Store.SaveSite(site);
                    }
                }
                await Store.Log("web", "message", name, true);
            }
            response.ExecuteScript("ContactJs.sent(true,'" + Ui.Js("Thanks, " + name.Split(' ')[0] + ". We will reply within one working day.") + "');");
            return response;
        }
    }
}
