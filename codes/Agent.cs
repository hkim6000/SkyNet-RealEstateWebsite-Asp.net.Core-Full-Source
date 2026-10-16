using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class Agent : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ap-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav(site, "ap", v.Nav, false))
                .Replace("{plhd_drawer}", Nav(site, "ap", v.Nav, true))
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
                return "<i class=\"ap-shut\"></i>Office opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"ap-shut\"></i>Office closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"ap-shut\"></i>Office closed";
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
                response.SetElementContents("ap-e-nlmail", "Enter an email address like name@example.com.");
                return response;
            }
            bool added = await Store.AddSubscriber(mail);
            response.ExecuteScript("AgentJs.subscribed('" + (added ? "Thanks. You will hear from us when new homes are listed." : "You are already on the list.") + "');");
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
            return "<article class=\"ap-hc\" data-lid=\"" + Ui.Attr(l.Id) + "\"><a class=\"ap-hc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(l.Photos.FirstOrDefault() ?? string.Empty, string.Empty, string.Empty, true) + (text != string.Empty ? "<span class=\"ap-badge ap-b-" + cls + "\">" + text + "</span>" : string.Empty) + "<span class=\"ap-pics\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 8h3l1.6-2.5h6.8L17 8h3v11H4z\"/><circle cx=\"12\" cy=\"13.2\" r=\"3.4\"/></svg>" + l.Photos.Count + "</span></a><button type=\"button\" class=\"ap-heart\" data-heart=\"" + Ui.Attr(l.Id) + "\" aria-pressed=\"false\" aria-label=\"Save this home\" onclick=\"AgentJs.heart(this)\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg></button><div class=\"ap-hc-b\"><span class=\"ap-price\">" + Ui.PriceText(l) + "</span><span class=\"ap-hc-f\">" + Facts(l) + "</span><span class=\"ap-hc-a\"><a href=\"" + href + "\">" + Ui.Enc(l.Street) + "</a></span><span class=\"ap-hc-m\"><span>" + Ui.Enc(Ui.AreaName(site, l.Area)) + "</span><span>" + meta + "</span></span></div></article>";
        }

        private static string Pins(SiteData site, IEnumerable<Property> list)
        {
            var items = list.Select(l => new { i = l.Id, x = Math.Round(l.X, 1), y = Math.Round(l.Y, 1), p = Ui.Short(l.Price) + (l.Deal == "rent" ? "/mo" : string.Empty), r = Ui.PricePlain(l), f = FactsPlain(l), a = l.Street + ", " + Ui.AreaName(site, l.Area), t = Ui.Thumb(l.Photos.FirstOrDefault() ?? string.Empty), h = "Listing?id=" + Uri.EscapeDataString(l.Id), c = "ap-p-" + (l.Status == "active" ? l.Deal : l.Status) });
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
            sb.Append("<div class=\"" + p + "-pins\"></div></div><div class=\"" + p + "-mctl\"><button type=\"button\" aria-label=\"Zoom in\" onclick=\"AgentJs.zoom('" + id + "',1.4)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Zoom out\" onclick=\"AgentJs.zoom('" + id + "',0.714)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Show all\" onclick=\"AgentJs.fit('" + id + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5\"/></svg></button></div>" + extra + "</div>");
            return sb.ToString();
        }

        private async Task<View> Render(SiteData site)
        {
            TeamMember? a = site.Agents.FirstOrDefault(x => x.Id == Q("id", 40) && x.Active);
            if (a == null)
            {
                return new View { Title = "Not found", Main = "<div class=\"ap-wrap ap-gone\"><h1>We could not find that person</h1><p>They may have moved on. The whole team is on the " + Ui.Enc(site.Settings.AgentsWord.ToLowerInvariant()) + " page.</p><a class=\"ap-btn ap-btn-accent\" href=\"Agents\">Meet the team</a></div>", Nav = "agents" };
            }
            DateTime today = Store.Today(site);
            string first = a.Name.Split(' ')[0];
            string tel = Regex.Replace(a.Phone, "[^0-9+]", string.Empty);
            List<Property> live = site.Listings.Where(l => l.Agent == a.Id && Ui.Live(l)).OrderBy(l => l.Status == "active" ? 0 : 1).ThenByDescending(l => l.Listed).ToList();
            List<Property> closed = site.Listings.Where(l => l.Agent == a.Id && l.Status is "sold" or "rented").OrderByDescending(l => l.History.Count > 0 ? l.History[^1].When : l.Listed).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"ap-wrap\"><nav class=\"ap-crumb ap-acrumb\" aria-label=\"Breadcrumb\"><a href=\"Agents\">" + Ui.Enc(site.Settings.AgentsWord) + "</a><span>/</span><span>" + Ui.Enc(a.Name) + "</span></nav><div class=\"ap-prof\"><div class=\"ap-pimg\">" + Ui.Face("ap", a) + "</div><div class=\"ap-pinfo\"><span class=\"ap-kick\">" + Ui.Enc(a.Title) + "</span><h1>" + Ui.Enc(a.Name) + "</h1><p class=\"ap-plead\">" + Ui.Enc(a.Bio) + "</p><dl class=\"ap-pdl\"><div><dt>Neighborhoods</dt><dd>");
            sb.Append(string.Join(", ", a.Areas.Select(k => "<a href=\"Listings?area=" + Uri.EscapeDataString(k) + "\">" + Ui.Enc(Ui.AreaName(site, k)) + "</a>")));
            sb.Append("</dd></div><div><dt>Experience</dt><dd>" + Ui.Plural(a.Years, "year", "years") + "</dd></div><div><dt>Languages</dt><dd>" + Ui.Enc(string.Join(", ", a.Languages)) + "</dd></div><div><dt>License</dt><dd>" + Ui.Enc(a.License) + "</dd></div></dl><div class=\"ap-pc\"><a class=\"ap-btn ap-btn-accent\" href=\"tel:" + Ui.Attr(tel) + "\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg> " + Ui.Enc(a.Phone) + "</a><a class=\"ap-btn ap-btn-ghost\" href=\"mailto:" + Ui.Attr(a.Email) + "\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg> " + Ui.Enc(a.Email) + "</a></div></div>");
            sb.Append("<aside class=\"ap-box ap-pform\"><h2>Message " + Ui.Enc(first) + "</h2><form class=\"ap-form\" id=\"ap-aform\" onsubmit=\"return AgentJs.send()\" novalidate><label class=\"ap-field\"><span>I'm interested in</span><select id=\"ap-atopic\">" + Ui.Opts(new[] { ("Buying", "Buying a home"), ("Selling", "Selling my home"), ("Renting", "Renting"), ("Other", "Something else") }, "Buying") + "</select></label><label class=\"ap-field\"><span>Your name</span><input type=\"text\" id=\"ap-aname\" maxlength=\"80\" autocomplete=\"name\"></label><em class=\"ap-err\" id=\"ap-e-aname\" role=\"alert\"></em><label class=\"ap-field\"><span>Email</span><input type=\"email\" id=\"ap-amail\" maxlength=\"120\" autocomplete=\"email\"></label><em class=\"ap-err\" id=\"ap-e-amail\" role=\"alert\"></em><label class=\"ap-field\"><span>Phone <small>(optional)</small></span><input type=\"tel\" id=\"ap-aphone\" maxlength=\"30\" autocomplete=\"tel\"></label><label class=\"ap-field\"><span>Message</span><textarea id=\"ap-amsg\" rows=\"4\" maxlength=\"1000\"></textarea></label><em class=\"ap-err\" id=\"ap-e-amsg\" role=\"alert\"></em><label class=\"ap-hp\" aria-hidden=\"true\">Website<input type=\"text\" id=\"ap-aweb\" tabindex=\"-1\" autocomplete=\"off\"></label><button type=\"submit\" class=\"ap-btn ap-btn-accent ap-btn-wide\" id=\"ap-asend\">Send message</button><p class=\"ap-hint\">" + Ui.Enc(first) + " answers within one working day. We only use your details to reply.</p></form><div id=\"ap-adone\"></div></aside></div>");
            sb.Append("<section class=\"ap-sec\"><div class=\"ap-shead\"><div><span class=\"ap-kick\">Listed by " + Ui.Enc(first) + "</span><h2 class=\"ap-h2\">" + Ui.Plural(live.Count, "home", "homes") + " on the market</h2></div></div>");
            if (live.Count == 0)
            {
                sb.Append("<p class=\"ap-none\">Nothing listed right now. New homes come up every week.</p>");
            }
            else
            {
                sb.Append("<div class=\"ap-cards\">");
                foreach (Property l in live)
                {
                    sb.Append(Card(site, l, today));
                }
                sb.Append("</div>");
            }
            sb.Append("</section>");
            if (closed.Count > 0)
            {
                sb.Append("<section class=\"ap-sec\"><div class=\"ap-shead\"><div><span class=\"ap-kick\">Track record</span><h2 class=\"ap-h2\">Recently sold and rented</h2></div></div><div class=\"ap-closed\">");
                foreach (Property l in closed)
                {
                    PriceEvent? e = l.History.LastOrDefault(h => h.Event is "Sold" or "Rented");
                    sb.Append("<a class=\"ap-cl\" href=\"Listing?id=" + Uri.EscapeDataString(l.Id) + "\">" + Ui.Pic(l.Photos.FirstOrDefault() ?? string.Empty, string.Empty, string.Empty, true) + "<span><b>" + Ui.Enc(l.Street) + "</b><small>" + Ui.Enc(Ui.AreaName(site, l.Area)) + " &middot; " + FactsPlain(l) + "</small><em>" + (l.Status == "sold" ? "Sold" : "Rented") + (e != null ? " " + e.When.ToString("MMM d", Ui.Inv) + " &middot; " + Ui.Money(e.Price) + (l.Deal == "rent" ? "/mo" : string.Empty) : string.Empty) + "</em></span></a>");
                }
                sb.Append("</div></section>");
            }
            sb.Append("</div>");
            return new View { Title = a.Name, Desc = a.Name + ", " + a.Title + " at " + site.Settings.Name + ". " + a.Bio, Main = sb.ToString(), Nav = "agents" };
        }

        public async Task<ApiResponse> Send()
        {
            ApiResponse response = new ApiResponse();
            string name = Regex.Replace(Ui.Clip(GetDataValue("aname"), 80), "\\s+", " ").Trim();
            string mail = Ui.Clip(GetDataValue("amail"), 120).Trim();
            string msg = Ui.Clip(GetDataValue("amsg"), 1000).Trim();
            string topic = GetDataValue("atopic") is "Buying" or "Selling" or "Renting" ? GetDataValue("atopic") : "Other";
            if (name.Length < 2)
            {
                response.ExecuteScript("AgentJs.fieldErr('aname','Please enter your name.');AgentJs.sent(false);");
                return response;
            }
            if (!Ui.Email(mail))
            {
                response.ExecuteScript("AgentJs.fieldErr('amail','Enter an email address like name@example.com.');AgentJs.sent(false);");
                return response;
            }
            if (msg.Length < 3)
            {
                response.ExecuteScript("AgentJs.fieldErr('amsg','Please write a short message.');AgentJs.sent(false);");
                return response;
            }
            if (Ui.Clip(GetDataValue("aweb"), 50) != string.Empty)
            {
                response.ExecuteScript("AgentJs.sent(true,'Thank you. We will be in touch soon.');");
                return response;
            }
            string first = "We";
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                TeamMember? a = site.Agents.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("aid"), 40) && x.Active);
                if (a == null)
                {
                    response.ExecuteScript("AgentJs.sent(false);AgentJs.toast('Please try again.');");
                    return response;
                }
                first = a.Name.Split(' ')[0];
                if (!site.Leads.Any(x => x.Kind == "contact" && x.Agent == a.Id && x.Email.Equals(mail, StringComparison.OrdinalIgnoreCase) && x.Created > DateTime.UtcNow.AddMinutes(-10)))
                {
                    site.Leads.Add(new Lead { Id = Store.NextId(site.Leads.Select(x => x.Id), "LD-", 1000), Kind = "contact", Name = name, Email = mail, Phone = Ui.CleanPhone(GetDataValue("aphone")), Message = msg, Details = topic, Agent = a.Id, Created = DateTime.UtcNow });
                    await Store.SaveSite(site);
                }
            }
            await Store.Log("web", "message", name, true);
            response.ExecuteScript("AgentJs.sent(true,'" + Ui.Js("Thanks, " + name.Split(' ')[0] + ". " + first + " will reply within one working day.") + "');");
            return response;
        }
    }
}
