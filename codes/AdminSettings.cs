using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class AdminSettings : WebPage
    {
        private const string AreaKey = "settings";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Settings");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--st-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("st-menu", Auth.Menu("st", "AdminSettings", c, site));
            response.SetElementContents("st-who", Auth.Who("st", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"st-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"st-panel\"><p>You don't have access to this area. Ask the office manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminSettingsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "view", AreaKey, false);
                response.SetElementContents("st-main", NoAccess("Settings"));
                return response;
            }
            response.SetElementContents("st-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminSettingsJs.loaded();");
            return response;
        }

        private static string Per(string v)
        {
            return v is "today" or "7" or "30" or "range" ? v : "all";
        }

        private static string Who(Dictionary<string, Staff> staff, string id)
        {
            return id == "web" ? "Website" : staff.TryGetValue(id, out Staff? s) ? s.Name : id;
        }

        private static string Head((string Name, string Cls)[] cols)
        {
            StringBuilder sb = new StringBuilder("<div class=\"st-tr st-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static readonly string[] Zones = { "America/New_York", "America/Chicago", "America/Denver", "America/Phoenix", "America/Los_Angeles", "America/Anchorage", "Pacific/Honolulu", "America/Toronto", "America/Vancouver", "Europe/London", "Europe/Paris", "Europe/Berlin", "Asia/Tokyo", "Asia/Seoul", "Australia/Sydney", "Pacific/Auckland", "UTC" };

        private static string Field(string id, string label, string value, int max, string type = "text", string extra = "")
        {
            return "<label class=\"st-field\"><span>" + label + "</span><input type=\"" + type + "\" id=\"st-s" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Attr(value) + "\"" + extra + "></label>";
        }

        private static string TextArea(string id, string label, string value, int max, int rows)
        {
            return "<label class=\"st-field\"><span>" + label + "</span><textarea id=\"st-s" + id + "\" rows=\"" + rows + "\" maxlength=\"" + max + "\">" + Ui.Enc(value) + "</textarea></label>";
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            Settings s = site.Settings;
            StringBuilder sb = new StringBuilder("<div class=\"st-ptop\"><div><h1>Settings</h1><p>Agency details, website text, mortgage defaults and office hours.</p></div></div><nav class=\"st-setnav\"><a href=\"#s-agency\">Agency</a><a href=\"#s-look\">Look and words</a><a href=\"#s-text\">Website text</a><a href=\"#s-mort\">Mortgage</a><a href=\"#s-hours\">Hours and viewings</a></nav><form class=\"st-form\" onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<section class=\"st-panel\" id=\"s-agency\"><h2>Agency</h2><div class=\"st-form\"><div class=\"st-f3\">" + Field("name", "Name", s.Name, 40) + Field("town", "Town", s.Town, 40) + Field("tag", "Tagline", s.Tagline, 140) + "</div><div class=\"st-f3\">" + Field("addr", "Office address", s.Address, 100) + Field("phone", "Phone", s.Phone, 30) + Field("email", "Email", s.Email, 80, "email") + "</div>" + Field("lic", "License line (shown in the footer)", s.License, 120) + "</div></section>");
            sb.Append("<section class=\"st-panel\" id=\"s-look\"><h2>Look and words</h2><div class=\"st-f4\"><label class=\"st-field\"><span>Accent colour</span><input type=\"color\" id=\"st-saccent\" value=\"" + Ui.Attr(Ui.SafeColor(s.Accent) ? s.Accent : "#B5562F") + "\"></label><label class=\"st-field\"><span>Website theme</span><select id=\"st-stheme\">" + Ui.Opts(new[] { ("light", "Light"), ("dark", "Dark") }, s.Theme) + "</select></label>" + Field("aw", "One agent", s.AgentWord, 24) + Field("aws", "Many agents", s.AgentsWord, 24) + "</div><p class=\"st-hint\" style=\"margin-top:8px\">Call your team Agents, Realtors, Brokers or Advisors. The menu, website and filters follow.</p></section>");
            sb.Append("<section class=\"st-panel\" id=\"s-text\"><h2>Website text</h2><div class=\"st-form\"><div class=\"st-f2\">" + Field("htitle", "Home headline", s.HeroTitle, 80) + TextArea("htext", "Home intro", s.HeroText, 300, 2) + "</div><div class=\"st-f2\">" + Field("atitle", "About headline", s.AboutTitle, 80) + TextArea("atext", "About text", s.AboutText, 600, 3) + "</div><div class=\"st-f2\">" + Field("stitle", "Selling headline", s.SellTitle, 80) + TextArea("stext", "Selling text", s.SellText, 400, 3) + "</div></div></section>");
            sb.Append("<section class=\"st-panel\" id=\"s-mort\"><h2>Mortgage calculator defaults</h2><div class=\"st-f3\">" + Field("rate", "Interest rate (%)", s.Rate.ToString("0.###", Ui.Inv), 6, "text", " inputmode=\"decimal\"") + "<label class=\"st-field\"><span>Loan term</span><select id=\"st-sterm\">" + Ui.Opts(new[] { ("30", "30 years"), ("20", "20 years"), ("15", "15 years") }, s.Term.ToString(Ui.Inv)) + "</select></label>" + Field("down", "Down payment (%)", s.DownPct.ToString("0.##", Ui.Inv), 5, "text", " inputmode=\"decimal\"") + "</div><div class=\"st-f3\">" + Field("tax", "Property tax when a listing has none (% a year)", s.TaxRate.ToString("0.###", Ui.Inv), 6, "text", " inputmode=\"decimal\"") + Field("ins", "Home insurance ($ a year)", s.Insurance.ToString("0", Ui.Inv), 7, "text", " inputmode=\"numeric\"") + Field("pmi", "Mortgage insurance below 20% down (% a year)", s.Pmi.ToString("0.##", Ui.Inv), 5, "text", " inputmode=\"decimal\"") + "</div><div class=\"st-form\" style=\"margin-top:12px\">" + Field("disc", "Note under every estimate", s.Disclaimer, 200) + "</div></section>");
            sb.Append("<section class=\"st-panel\" id=\"s-hours\"><h2>Office hours and viewings</h2><div id=\"st-hours\">");
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay o = s.Hours.FirstOrDefault(h => h.Day == d) ?? new OpenDay { Day = d };
                sb.Append("<div class=\"st-orow" + (o.Open ? string.Empty : " st-shut") + "\" data-day=\"" + d + "\"><b>" + Ui.DayNames[d] + "</b><label><input type=\"checkbox\"" + (o.Open ? " checked" : string.Empty) + " onchange=\"AdminSettingsJs.dayOn(this)\" aria-label=\"Open on " + Ui.DayNames[d] + "\"></label><input type=\"time\" step=\"900\" value=\"" + Ui.Attr(o.Start) + "\" aria-label=\"Opens\"><input type=\"time\" step=\"900\" value=\"" + Ui.Attr(o.End) + "\" aria-label=\"Closes\"></div>");
            }
            List<(string, string)> zones = Zones.Select(z => (z, z.Replace('_', ' '))).ToList();
            if (!Zones.Contains(s.TimeZone))
            {
                zones.Insert(0, (s.TimeZone, s.TimeZone));
            }
            sb.Append("</div><p class=\"st-hint\" style=\"margin:8px 0 12px\">Viewing requests on the website offer times on open days, inside both these hours and the viewing window below.</p><div class=\"st-f4\"><label class=\"st-field\"><span>Viewing length</span><select id=\"st-svmin\">" + Ui.Opts(new[] { ("15", "15 minutes"), ("30", "30 minutes"), ("45", "45 minutes"), ("60", "1 hour") }, s.ViewingMinutes.ToString(Ui.Inv)) + "</select></label>" + Field("vstart", "First viewing", s.ViewingStart, 5, "time", " step=\"900\"") + Field("vend", "Viewings end by", s.ViewingEnd, 5, "time", " step=\"900\"") + "<label class=\"st-field\"><span>Time zone (now " + Ui.Hm(site, Store.Now(site)) + ")</span><select id=\"st-stz\">" + Ui.Opts(zones, s.TimeZone) + "</select></label></div><label class=\"st-chk\" style=\"margin-top:12px\"><input type=\"checkbox\" id=\"st-sh24\"" + (s.Clock24 ? " checked" : string.Empty) + "> 24-hour clock</label></section>");
            sb.Append("<div class=\"st-savebar\"><em class=\"st-err\" id=\"st-e-set\" role=\"alert\"></em><button type=\"submit\" class=\"st-btn st-btn-accent\">Save settings</button></div></form>");
            return sb.ToString();
        }

        private static string Word(string v, string fallback)
        {
            string w = Regex.Replace(Ui.Clip(v, 24), "[<>\"]", string.Empty).Trim();
            return w.Length >= 2 ? w : fallback;
        }

        private static decimal Dec(string v, decimal min, decimal max, decimal cur)
        {
            return decimal.TryParse((v ?? string.Empty).Replace(",", string.Empty).Replace("$", string.Empty).Replace("%", string.Empty).Trim(), NumberStyles.Number, Ui.Inv, out decimal d) ? Math.Clamp(d, min, max) : cur;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "settings", "settings", "Only the office manager changes settings.");
            }
            string name = Regex.Replace(Ui.Clip(GetDataValue("sname"), 40), "\\s+", " ").Trim();
            string email = Ui.Clip(GetDataValue("semail"), 80).Trim();
            string tz = Ui.Clip(GetDataValue("stz"), 60);
            string vstart = Store.Hm(GetDataValue("svstart")) != null ? GetDataValue("svstart") : string.Empty;
            string vend = Store.Hm(GetDataValue("svend")) != null ? GetDataValue("svend") : string.Empty;
            string err = name.Length < 2 ? "Enter the agency name." : email != string.Empty && !Ui.Email(email) ? "Check the email address." : !Store.KnownZone(tz) ? "Choose a time zone." : vstart == string.Empty || vend == string.Empty || string.CompareOrdinal(vstart, vend) >= 0 ? "Viewings must end after the first viewing time." : string.Empty;
            List<OpenDay> hours = new List<OpenDay>();
            foreach (string row in GetDataValue("shours").Split('~'))
            {
                string[] x = row.Split('|');
                if (x.Length != 4 || !int.TryParse(x[0], out int d) || d < 0 || d > 6 || Store.Hm(x[2]) == null || Store.Hm(x[3]) == null)
                {
                    continue;
                }
                OpenDay o = new OpenDay { Day = d, Open = x[1] == "1", Start = x[2], End = x[3] };
                if (o.Open && string.CompareOrdinal(o.Start, o.End) >= 0 && err == string.Empty)
                {
                    err = Ui.DayNames[d] + ": closing time must be after opening time.";
                }
                hours.Add(o);
            }
            if (err != string.Empty)
            {
                response.SetElementContents("st-e-set", err);
                return response;
            }
            string accent = Ui.SafeColor(GetDataValue("saccent")) ? GetDataValue("saccent") : "#B5562F";
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Settings s = site.Settings;
                s.Name = name;
                s.Town = Ui.Clip(GetDataValue("stown"), 40).Trim();
                s.Tagline = Ui.Clip(GetDataValue("stag"), 140).Trim();
                s.Address = Ui.Clip(GetDataValue("saddr"), 100).Trim();
                s.Phone = Ui.CleanPhone(GetDataValue("sphone"));
                s.Email = email;
                s.License = Ui.Clip(GetDataValue("slic"), 120).Trim();
                s.Accent = accent;
                s.Theme = GetDataValue("stheme") == "dark" ? "dark" : "light";
                s.AgentWord = Word(GetDataValue("saw"), s.AgentWord);
                s.AgentsWord = Word(GetDataValue("saws"), s.AgentsWord);
                s.HeroTitle = Ui.Clip(GetDataValue("shtitle"), 80).Trim();
                s.HeroText = Ui.Clip(GetDataValue("shtext"), 300).Trim();
                s.AboutTitle = Ui.Clip(GetDataValue("satitle"), 80).Trim();
                s.AboutText = Ui.Clip(GetDataValue("satext"), 600).Trim();
                s.SellTitle = Ui.Clip(GetDataValue("sstitle"), 80).Trim();
                s.SellText = Ui.Clip(GetDataValue("sstext"), 400).Trim();
                s.Rate = Dec(GetDataValue("srate"), 0, 30, s.Rate);
                s.Term = GetDataValue("sterm") is "15" or "20" or "30" ? int.Parse(GetDataValue("sterm"), Ui.Inv) : s.Term;
                s.DownPct = Dec(GetDataValue("sdown"), 0, 100, s.DownPct);
                s.TaxRate = Dec(GetDataValue("stax"), 0, 10, s.TaxRate);
                s.Insurance = Math.Round(Dec(GetDataValue("sins"), 0, 100000, s.Insurance));
                s.Pmi = Dec(GetDataValue("spmi"), 0, 5, s.Pmi);
                s.Disclaimer = Ui.Clip(GetDataValue("sdisc"), 200).Trim();
                s.ViewingMinutes = GetDataValue("svmin") is "15" or "30" or "45" or "60" ? int.Parse(GetDataValue("svmin"), Ui.Inv) : s.ViewingMinutes;
                s.ViewingStart = vstart;
                s.ViewingEnd = vend;
                s.TimeZone = tz;
                s.Clock24 = GetDataValue("sh24") == "1";
                if (hours.Count == 7)
                {
                    s.Hours = hours.OrderBy(h => h.Day).ToList();
                }
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "settings", name, true);
            response.ExecuteScript("AdminSettingsJs.toast('Settings saved');document.getElementById('st-site').style.setProperty('--st-accent','" + accent + "');");
            return response;
        }
    }
}
