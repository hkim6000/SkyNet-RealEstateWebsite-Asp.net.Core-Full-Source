using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class AdminViewings : WebPage
    {
        private const string AreaKey = "viewings";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Viewings");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--vw-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("vw-menu", Auth.Menu("vw", "AdminViewings", c, site));
            response.SetElementContents("vw-who", Auth.Who("vw", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminViewingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"vw-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"vw-panel\"><p>You don't have access to this area. Ask the office manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminViewingsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminViewingsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "view", AreaKey, false);
                response.SetElementContents("vw-main", NoAccess("Viewings"));
                return response;
            }
            response.SetElementContents("vw-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminViewingsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"vw-tr vw-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 30;

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            string own = Auth.Mine(c);
            StringBuilder sb = new StringBuilder("<div class=\"vw-ptop\"><div><h1>Viewings</h1><p>Booked visits to homes" + (own != string.Empty ? ", your own" : string.Empty) + ". Book one from a lead or straight here.</p></div><div class=\"vw-acts\">" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"vw-btn vw-btn-accent\" onclick=\"AdminViewingsJs.edit('','')\">Book a viewing</button>" : string.Empty) + "</div></div>");
            sb.Append("<div class=\"vw-bar\"><label>Search<input type=\"search\" id=\"vw-q\" list=\"vw-qlist\" placeholder=\"Client or home\" maxlength=\"60\" oninput=\"AdminViewingsJs.search()\"></label>" + Ui.DataList("vw-qlist", site.Viewings.Where(v => own == string.Empty || v.Agent == own).SelectMany(v => new[] { v.Name, Ui.ListingOf(site, v.Listing)?.Street ?? v.Listing })) + "<label>Status<select id=\"vw-wst\" onchange=\"AdminViewingsJs.list(1)\"><option value=\"\">Any status</option>" + Ui.Opts(Ui.ViewingStatuses.Select(s => (s.Key, s.Name)), string.Empty) + "</select></label>");
            if (own == string.Empty)
            {
                sb.Append("<label>" + Ui.Enc(site.Settings.AgentWord) + "<select id=\"vw-wagent\" onchange=\"AdminViewingsJs.list(1)\"><option value=\"\">Everyone</option>" + Ui.Opts(site.Agents.OrderBy(a => a.Order).Select(a => (a.Id, a.Name)), string.Empty) + "</select></label>");
            }
            sb.Append("</div><div class=\"vw-bar\" id=\"vw-perbar\">" + Ui.PeriodBar("vw", "AdminViewingsJs.per", "7", string.Empty, string.Empty, true) + "</div><div class=\"vw-tbl vw-vwtbl\" id=\"vw-list\"></div><div id=\"vw-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminViewingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            string own = Auth.Mine(c);
            string term = Ui.Clip(GetDataValue("wq"), 60);
            string st = GetDataValue("wst");
            string agent = own != string.Empty ? own : GetDataValue("wagent");
            string per = GetDataValue("per") == string.Empty ? "7" : Per(GetDataValue("per"));
            DateTime today = Store.Today(site);
            (DateTime? from, DateTime? to) = Ui.Period(today, per, GetDataValue("pfrom"), GetDataValue("pto"), true);
            IEnumerable<Viewing> q = site.Viewings.Where(v => (agent == string.Empty || v.Agent == agent) && (st == string.Empty || v.Status == st) && Ui.InPeriod(v.Start, from, to));
            if (term != string.Empty)
            {
                q = q.Where(v => (v.Name + " " + v.Phone + " " + v.Email + " " + v.Listing + " " + (Ui.ListingOf(site, v.Listing)?.Street ?? string.Empty)).Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            List<Viewing> all = (per == "all" ? q.OrderByDescending(v => v.Start.Date >= today).ThenBy(v => v.Start.Date >= today ? v.Start.Ticks : -v.Start.Ticks) : q.OrderBy(v => v.Start)).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            bool canE = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder(Head(new[] { ("Time", string.Empty), ("Home", string.Empty), ("Client", string.Empty), (Ui.Enc(site.Settings.AgentWord), string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"vw-tr\"><span class=\"vw-none vw-wide\">No viewings in this period.</span></div>");
            }
            DateTime last = DateTime.MinValue;
            foreach (Viewing v in all.Skip((page - 1) * Size).Take(Size))
            {
                if (v.Start.Date != last)
                {
                    last = v.Start.Date;
                    sb.Append("<div class=\"vw-day\">" + (last == today ? "Today &middot; " : last == today.AddDays(1) ? "Tomorrow &middot; " : string.Empty) + Ui.LongDay(last) + "</div>");
                }
                Property? l = Ui.ListingOf(site, v.Listing);
                string acts = string.Empty;
                if (canE)
                {
                    string who = Ui.Js(v.Name);
                    acts = v.Status == "booked"
                        ? "<button type=\"button\" class=\"vw-btn vw-btn-sm\" onclick=\"AdminViewingsJs.act('" + v.Id + "','done','" + who + "')\">Done</button><button type=\"button\" class=\"vw-btn vw-btn-sm vw-btn-ghost\" onclick=\"AdminViewingsJs.act('" + v.Id + "','noshow','" + who + "')\">No-show</button><button type=\"button\" class=\"vw-btn vw-btn-sm vw-btn-ghost\" onclick=\"AdminViewingsJs.edit('" + v.Id + "','')\">Edit</button><button type=\"button\" class=\"vw-btn vw-btn-sm vw-btn-danger\" onclick=\"AdminViewingsJs.act('" + v.Id + "','cancelled','" + who + "')\">Cancel</button>"
                        : "<button type=\"button\" class=\"vw-btn vw-btn-sm vw-btn-ghost\" onclick=\"AdminViewingsJs.act('" + v.Id + "','booked','" + who + "')\">Reopen</button>";
                }
                sb.Append("<div class=\"vw-tr" + (v.Status is "cancelled" or "noshow" ? " vw-gone" : string.Empty) + "\"><span class=\"vw-vtime\" data-l=\"Time\">" + Ui.Hm(site, v.Start) + "<small>" + v.Minutes + " min</small></span><span class=\"vw-wide\"><span class=\"vw-home\">" + (l != null && l.Photos.Count > 0 ? "<img src=\"" + Ui.Attr(Ui.Thumb(l.Photos[0])) + "\" alt=\"\" loading=\"lazy\">" : string.Empty) + "<span><b>" + Ui.Enc(l?.Street ?? v.Listing) + "</b><small>" + Ui.Enc(v.Listing) + (l != null ? " &middot; " + Ui.PricePlain(l) : string.Empty) + "</small></span></span></span><span data-l=\"Client\"><b>" + Ui.Enc(v.Name) + "</b><small>" + Ui.Enc(v.Phone != string.Empty ? v.Phone : v.Email) + "</small></span><span data-l=\"" + Ui.Attr(site.Settings.AgentWord) + "\">" + Ui.Enc(Ui.AgentName(site, v.Agent)) + "</span><span data-l=\"Status\">" + Ui.Pill("vw", Ui.ViewingStatuses, v.Status) + "</span><span class=\"vw-vacts vw-wide\">" + acts + "</span></div>");
            }
            response.SetElementContents("vw-list", sb.ToString());
            response.SetElementContents("vw-pager", Ui.Pager("vw", page, Size, all.Count, "viewing", "viewings", "AdminViewingsJs.list"));
            response.SetElementContents("vw-perbar", Ui.PeriodBar("vw", "AdminViewingsJs.per", per, GetDataValue("pfrom"), GetDataValue("pto"), true));
            return response;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminViewingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "viewing", GetDataValue("wid"), "You can look but not book viewings.");
            }
            SiteData site = await Store.LoadSite();
            string own = Auth.Mine(c);
            Viewing? found = site.Viewings.FirstOrDefault(v => v.Id == Ui.Clip(GetDataValue("wid"), 20));
            if (found != null && own != string.Empty && found.Agent != own)
            {
                return await Refuse(response, c, "viewing", found.Id, "That viewing belongs to someone else.");
            }
            Lead? lead = site.Leads.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("wlead"), 20));
            DateTime now = Store.Now(site);
            Viewing v = found ?? new Viewing { Start = now.Date.AddDays(1).AddHours(10), Minutes = site.Settings.ViewingMinutes, Agent = own };
            if (found == null && lead != null)
            {
                v.Lead = lead.Id;
                v.Listing = lead.Listing;
                v.Name = lead.Name;
                v.Phone = lead.Phone;
                v.Email = lead.Email;
                v.Agent = own != string.Empty ? own : lead.Agent != string.Empty ? lead.Agent : Ui.ListingOf(site, lead.Listing)?.Agent ?? string.Empty;
                DateTime? want = Ui.ParseStart(lead.Details);
                if (want != null && want.Value > now)
                {
                    v.Start = want.Value;
                }
            }
            List<Property> homes = site.Listings.Where(l => (Ui.Live(l) || l.Id == v.Listing) && (own == string.Empty || l.Agent == own || l.Id == v.Listing)).OrderBy(l => l.Street).ToList();
            StringBuilder sb = new StringBuilder("<h2>" + (found == null ? "Book a viewing" : "Edit viewing") + "</h2>" + (lead != null && found == null ? "<p class=\"vw-info-box\">From " + Ui.Enc(lead.Name) + "'s request" + (lead.Prefer != string.Empty ? ": " + Ui.Enc(lead.Prefer) : string.Empty) + ".</p>" : string.Empty) + "<div class=\"vw-form\" style=\"margin-top:12px\"><input type=\"hidden\" id=\"vw-wlead2\" value=\"" + Ui.Attr(v.Lead) + "\">");
            sb.Append("<div class=\"vw-f2\"><label class=\"vw-field\"><span>Home</span><select id=\"vw-wlisting\"><option value=\"\">Choose a home</option>" + Ui.Opts(homes.Select(l => (l.Id, l.Street + " · " + l.Id)), v.Listing) + "</select></label>");
            if (own != string.Empty)
            {
                sb.Append("<label class=\"vw-field\"><span>" + Ui.Enc(site.Settings.AgentWord) + "</span><select id=\"vw-wagent2\" disabled>" + Ui.Opts(site.Agents.Where(a => a.Id == own).Select(a => (a.Id, a.Name)), own) + "</select></label></div>");
            }
            else
            {
                sb.Append("<label class=\"vw-field\"><span>" + Ui.Enc(site.Settings.AgentWord) + "</span><select id=\"vw-wagent2\"><option value=\"\">Choose</option>" + Ui.Opts(site.Agents.Where(a => a.Active || a.Id == v.Agent).OrderBy(a => a.Order).Select(a => (a.Id, a.Name)), v.Agent) + "</select></label></div>");
            }
            sb.Append("<div class=\"vw-f3\"><label class=\"vw-field\"><span>Day</span><input type=\"date\" id=\"vw-wday\" value=\"" + Ui.Ymd(v.Start) + "\"></label><label class=\"vw-field\"><span>Time</span><input type=\"time\" id=\"vw-wtime\" step=\"900\" value=\"" + v.Start.ToString("HH:mm", Ui.Inv) + "\"></label><label class=\"vw-field\"><span>Length</span><select id=\"vw-wmin\">" + Ui.Opts(new[] { ("15", "15 minutes"), ("30", "30 minutes"), ("45", "45 minutes"), ("60", "1 hour"), ("90", "1 hour 30") }, v.Minutes.ToString(Ui.Inv)) + "</select></label></div>");
            sb.Append("<div class=\"vw-f3\"><label class=\"vw-field\"><span>Client name</span><input type=\"text\" id=\"vw-wname\" maxlength=\"80\" value=\"" + Ui.Attr(v.Name) + "\"></label><label class=\"vw-field\"><span>Phone</span><input type=\"tel\" id=\"vw-wphone\" maxlength=\"30\" value=\"" + Ui.Attr(v.Phone) + "\"></label><label class=\"vw-field\"><span>Email</span><input type=\"email\" id=\"vw-wmail\" maxlength=\"120\" value=\"" + Ui.Attr(v.Email) + "\"></label></div>");
            sb.Append("<label class=\"vw-field\"><span>Note for the " + Ui.Enc(site.Settings.AgentWord.ToLowerInvariant()) + " (optional)</span><input type=\"text\" id=\"vw-wnote\" maxlength=\"200\" value=\"" + Ui.Attr(v.Note) + "\" placeholder=\"Meet at the side gate\"></label><em class=\"vw-err\" id=\"vw-e-view\" role=\"alert\"></em></div><div class=\"vw-mfoot\"><button type=\"button\" class=\"vw-btn vw-btn-ghost\" onclick=\"AdminViewingsJs.closeModal()\">Cancel</button><button type=\"button\" class=\"vw-btn vw-btn-accent\" onclick=\"AdminViewingsJs.save('" + Ui.Attr(found?.Id ?? string.Empty) + "')\">" + (found == null ? "Book viewing" : "Save") + "</button></div>");
            response.SetElementContents("vw-mbody", sb.ToString());
            response.ExecuteScript("AdminViewingsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminViewingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "viewing", GetDataValue("wid"), "You can look but not book viewings.");
            }
            string own = Auth.Mine(c);
            string id = Ui.Clip(GetDataValue("wid"), 20);
            string name = Regex.Replace(Ui.Clip(GetDataValue("wname"), 80), "\\s+", " ").Trim();
            string mail = Ui.Clip(GetDataValue("wmail"), 120).Trim();
            DateTime? day = Ui.ParseDay(GetDataValue("wday"));
            TimeSpan? tm = Store.Hm(GetDataValue("wtime"));
            int minutes = GetDataValue("wmin") is "15" or "30" or "45" or "60" or "90" ? int.Parse(GetDataValue("wmin"), Ui.Inv) : 30;
            string err = string.Empty;
            string text = string.Empty;
            string savedId = id;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Property? l = site.Listings.FirstOrDefault(x => x.Id == GetDataValue("wlisting"));
                string agent = own != string.Empty ? own : site.Agents.Any(a => a.Id == GetDataValue("wagent2")) ? GetDataValue("wagent2") : string.Empty;
                Viewing? v = site.Viewings.FirstOrDefault(x => x.Id == id);
                if (v != null && own != string.Empty && v.Agent != own)
                {
                    return await Refuse(response, c, "viewing", id, "That viewing belongs to someone else.");
                }
                DateTime start = day != null && tm != null ? day.Value.Add(tm.Value) : DateTime.MinValue;
                if (l == null)
                {
                    err = "Choose the home.";
                }
                else if (agent == string.Empty)
                {
                    err = "Choose who shows the home.";
                }
                else if (start == DateTime.MinValue)
                {
                    err = "Choose the day and time.";
                }
                else if (v == null && start < Store.Now(site).AddMinutes(-5))
                {
                    err = "That time has already passed.";
                }
                else if (name.Length < 2)
                {
                    err = "Enter the client's name.";
                }
                else if (mail != string.Empty && !Ui.Email(mail))
                {
                    err = "Check the email address.";
                }
                else
                {
                    Viewing? clash = site.Viewings.FirstOrDefault(x => x.Id != id && x.Agent == agent && x.Status == "booked" && x.Start < start.AddMinutes(minutes) && start < x.Start.AddMinutes(x.Minutes));
                    if (clash != null)
                    {
                        err = Ui.AgentName(site, agent) + " already has a viewing at " + Ui.Hm(site, clash.Start) + " that day.";
                    }
                }
                if (err == string.Empty)
                {
                    bool isNew = v == null;
                    if (v == null)
                    {
                        v = new Viewing { Id = Store.NextId(site.Viewings.Select(x => x.Id), "V-", 2000), Created = DateTime.UtcNow, By = c.U.Id, Status = "booked" };
                        site.Viewings.Add(v);
                    }
                    v.Listing = l!.Id;
                    v.Agent = agent;
                    v.Start = start;
                    v.Minutes = minutes;
                    v.Name = name;
                    v.Phone = Ui.CleanPhone(GetDataValue("wphone"));
                    v.Email = mail;
                    v.Note = Ui.Clip(GetDataValue("wnote"), 200).Trim();
                    Lead? lead = site.Leads.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("wlead2"), 20));
                    if (isNew && lead != null)
                    {
                        v.Lead = lead.Id;
                        lead.Status = "booked";
                        if (lead.Agent == string.Empty)
                        {
                            lead.Agent = agent;
                        }
                        lead.Notes.Add(new Note { When = DateTime.UtcNow, By = c.U.Id, Text = "Viewing booked for " + start.ToString("ddd, MMM d", Ui.Inv) + " at " + Ui.Hm(site, start) + " with " + Ui.AgentName(site, agent) + "." });
                    }
                    savedId = v.Id;
                    text = (isNew ? "Booked " : "Saved ") + start.ToString("ddd, MMM d", Ui.Inv) + " at " + Ui.Hm(site, start) + ".";
                    await Store.SaveSite(site);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("vw-e-view", Ui.Enc(err));
                return response;
            }
            await Store.Log(c.U.Id, "viewing", savedId + " · " + name, true);
            response.ExecuteScript("AdminViewingsJs.saved('" + Ui.Js(text) + "');");
            return response;
        }

        public async Task<ApiResponse> Act()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminViewingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "viewing", GetDataValue("wid"), "You can look but not change viewings.");
            }
            string to = GetDataValue("wact") is "done" or "noshow" or "cancelled" or "booked" ? GetDataValue("wact") : string.Empty;
            string own = Auth.Mine(c);
            string name = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Viewing? v = site.Viewings.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("wid"), 20));
                if (v == null || to == string.Empty || (own != string.Empty && v.Agent != own))
                {
                    return await Refuse(response, c, "viewing", GetDataValue("wid"), "That viewing can't be changed.");
                }
                if (to == "booked" && site.Viewings.Any(x => x.Id != v.Id && x.Agent == v.Agent && x.Status == "booked" && x.Start < v.Start.AddMinutes(v.Minutes) && v.Start < x.Start.AddMinutes(x.Minutes)))
                {
                    response.ExecuteScript("AdminViewingsJs.toast('Another viewing now uses that time. Edit the time first.');");
                    return response;
                }
                v.Status = to;
                string why = Ui.Clip(GetDataValue("wwhy"), 200).Trim();
                if (why != string.Empty)
                {
                    v.Note = (v.Note != string.Empty ? v.Note + " · " : string.Empty) + "Cancelled: " + why;
                }
                Lead? lead = site.Leads.FirstOrDefault(x => x.Id == v.Lead);
                if (lead != null && to is "done" or "cancelled" or "noshow")
                {
                    lead.Notes.Add(new Note { When = DateTime.UtcNow, By = c.U.Id, Text = "Viewing " + Ui.NameOf(Ui.ViewingStatuses, to).ToLowerInvariant() + (why != string.Empty ? ": " + why : string.Empty) + "." });
                }
                name = v.Name;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "viewing " + to, name, true);
            response.ExecuteScript("AdminViewingsJs.acted('" + Ui.Js(Ui.NameOf(Ui.ViewingStatuses, to)) + ".');");
            return response;
        }
    }
}
