using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class AdminLeads : WebPage
    {
        private const string AreaKey = "leads";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Leads");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--le-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("le-menu", Auth.Menu("le", "AdminLeads", c, site));
            response.SetElementContents("le-who", Auth.Who("le", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminLeadsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"le-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"le-panel\"><p>You don't have access to this area. Ask the office manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminLeadsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminLeadsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "view", AreaKey, false);
                response.SetElementContents("le-main", NoAccess("Leads"));
                return response;
            }
            response.SetElementContents("le-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminLeadsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"le-tr le-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 20;

        private static bool Sees(Ctx c, Lead x)
        {
            string own = Auth.Mine(c);
            return own == string.Empty || x.Agent == own || x.Agent == string.Empty;
        }

        private static bool Mine(Ctx c, Lead x)
        {
            string own = Auth.Mine(c);
            return own == string.Empty || x.Agent == own;
        }

        private static string About(SiteData site, Lead x)
        {
            Property? l = Ui.ListingOf(site, x.Listing);
            if (l != null)
            {
                return Ui.Enc(l.Street) + "<small>" + Ui.Enc(l.Id) + " &middot; " + Ui.PricePlain(l) + "</small>";
            }
            if (x.Kind == "valuation")
            {
                return Ui.Enc(x.Address) + "<small>Valuation</small>";
            }
            return Ui.Enc(x.Details != string.Empty ? x.Details : "General") + "<small>" + (x.Agent != string.Empty && x.Kind == "contact" ? "To " + Ui.Enc(Ui.AgentName(site, x.Agent)) : "Website message") + "</small>";
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            string own = Auth.Mine(c);
            StringBuilder sb = new StringBuilder("<div class=\"le-ptop\"><div><h1>Leads</h1><p>Viewing requests, questions, valuations and messages from the website." + (own != string.Empty ? " You see your own leads and unassigned ones." : string.Empty) + "</p></div></div>");
            sb.Append("<div class=\"le-bar\"><label>Search<input type=\"search\" id=\"le-q\" list=\"le-qlist\" placeholder=\"Name, email, phone or home\" maxlength=\"60\" oninput=\"AdminLeadsJs.search()\"></label>" + Ui.DataList("le-qlist", site.Leads.Where(x => own == string.Empty || x.Agent == own || x.Agent == string.Empty).SelectMany(x => new[] { x.Name, x.Email, Ui.ListingOf(site, x.Listing)?.Street ?? x.Address })) + "<label>Type<select id=\"le-ekind\" onchange=\"AdminLeadsJs.list(1)\"><option value=\"\">All types</option>" + Ui.Opts(Ui.LeadKinds.Select(k => (k.Key, k.Name)), string.Empty) + "</select></label><label>Status<select id=\"le-est\" onchange=\"AdminLeadsJs.list(1)\"><option value=\"\">Open (not closed)</option>" + Ui.Opts(Ui.LeadStatuses.Select(k => (k.Key, k.Name)), string.Empty) + "<option value=\"all\">Everything</option></select></label>");
            if (own == string.Empty)
            {
                sb.Append("<label>" + Ui.Enc(site.Settings.AgentWord) + "<select id=\"le-eagent\" onchange=\"AdminLeadsJs.list(1)\"><option value=\"\">Everyone</option><option value=\"none\">Unassigned</option>" + Ui.Opts(site.Agents.OrderBy(a => a.Order).Select(a => (a.Id, a.Name)), string.Empty) + "</select></label>");
            }
            sb.Append("</div><div class=\"le-bar\" id=\"le-perbar\">" + Ui.PeriodBar("le", "AdminLeadsJs.per", "all", string.Empty, string.Empty) + "</div><div class=\"le-tbl le-letbl\" id=\"le-list\"></div><div id=\"le-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminLeadsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            string term = Ui.Clip(GetDataValue("eq"), 60);
            string kind = GetDataValue("ekind");
            string st = GetDataValue("est");
            string agent = GetDataValue("eagent");
            string per = Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(Store.Today(site), per, GetDataValue("pfrom"), GetDataValue("pto"));
            IEnumerable<Lead> q = site.Leads.Where(x => Sees(c, x) && (kind == string.Empty || x.Kind == kind) && (st == "all" || (st == string.Empty ? x.Status != "closed" : x.Status == st)) && (agent == string.Empty || (agent == "none" ? x.Agent == string.Empty : x.Agent == agent)) && Ui.InPeriod(Store.Local(site, x.Created), from, to));
            if (term != string.Empty)
            {
                q = q.Where(x => (x.Name + " " + x.Email + " " + x.Phone + " " + x.Address + " " + x.Listing + " " + (Ui.ListingOf(site, x.Listing)?.Street ?? string.Empty)).Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            List<Lead> all = q.OrderBy(x => x.Status == "new" ? 0 : 1).ThenByDescending(x => x.Created).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("Person", string.Empty), ("Type", string.Empty), ("About", string.Empty), ("Asked for", string.Empty), (Ui.Enc(site.Settings.AgentWord), string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"le-tr\"><span class=\"le-none le-wide\">No leads match. New ones arrive here from the website.</span></div>");
            }
            foreach (Lead x in all.Skip((page - 1) * Size).Take(Size))
            {
                string asked = x.Kind == "viewing" ? Ui.Enc(x.Prefer) : x.Kind == "valuation" ? Ui.Enc(x.Details.Split(" · ").Last()) : Ui.Enc(Ui.Clip(x.Message, 60));
                sb.Append("<div class=\"le-tr" + (x.Status == "new" ? " le-fresh" : string.Empty) + "\"><span class=\"le-wide\"><b>" + Ui.Enc(x.Name) + "</b><small>" + Ui.Enc(x.Email) + (x.Phone != string.Empty ? " &middot; " + Ui.Enc(x.Phone) : string.Empty) + "</small></span><span data-l=\"Type\">" + Ui.Enc(Ui.NameOf(Ui.LeadKinds, x.Kind)) + "<small>" + Ui.Time(x.Created) + "</small></span><span data-l=\"About\">" + About(site, x) + "</span><span data-l=\"Asked for\">" + (asked != string.Empty ? asked : "&ndash;") + "</span><span data-l=\"" + Ui.Attr(site.Settings.AgentWord) + "\">" + (x.Agent != string.Empty ? Ui.Enc(Ui.AgentName(site, x.Agent)) : "<i class=\"le-hint\">Unassigned</i>") + "</span><span data-l=\"Status\">" + Ui.Pill("le", Ui.LeadStatuses, x.Status) + "</span><span><button type=\"button\" class=\"le-btn le-btn-sm le-btn-ghost\" onclick=\"AdminLeadsJs.open('" + Ui.Attr(x.Id) + "')\">Open</button></span></div>");
            }
            response.SetElementContents("le-list", sb.ToString());
            response.SetElementContents("le-pager", Ui.Pager("le", page, Size, all.Count, "lead", "leads", "AdminLeadsJs.list"));
            response.SetElementContents("le-perbar", Ui.PeriodBar("le", "AdminLeadsJs.per", per, GetDataValue("pfrom"), GetDataValue("pto")));
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminLeadsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "lead", GetDataValue("eid"), "You don't have access to leads.");
            }
            SiteData site = await Store.LoadSite();
            Lead? x = site.Leads.FirstOrDefault(v => v.Id == Ui.Clip(GetDataValue("eid"), 20));
            if (x == null || !Sees(c, x))
            {
                response.ExecuteScript("AdminLeadsJs.closeModal();AdminLeadsJs.toast('That lead is gone or belongs to someone else.');");
                return response;
            }
            bool canE = Access.Can(c.U, AreaKey, 'E') && Mine(c, x);
            bool canX = Access.Can(c.U, AreaKey, 'X');
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff());
            Property? l = Ui.ListingOf(site, x.Listing);
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(x.Name) + "</h2><dl class=\"le-dl\"><dt>Type</dt><dd>" + Ui.Enc(Ui.NameOf(Ui.LeadKinds, x.Kind)) + " &middot; " + Ui.Pill("le", Ui.LeadStatuses, x.Status) + "</dd><dt>Received</dt><dd>" + Ui.Time(x.Created, "full") + "</dd><dt>Email</dt><dd><a href=\"mailto:" + Ui.Attr(x.Email) + "\">" + Ui.Enc(x.Email) + "</a></dd>" + (x.Phone != string.Empty ? "<dt>Phone</dt><dd><a href=\"tel:" + Ui.Attr(Regex.Replace(x.Phone, "[^0-9+]", string.Empty)) + "\">" + Ui.Enc(x.Phone) + "</a></dd>" : string.Empty));
            if (l != null)
            {
                sb.Append("<dt>Home</dt><dd><a href=\"Listing?id=" + Uri.EscapeDataString(l.Id) + "\" target=\"_blank\" rel=\"noopener\">" + Ui.Enc(l.Street) + "</a> &middot; " + Ui.PricePlain(l) + " &middot; " + Ui.StatusText(l) + "</dd>");
            }
            if (x.Kind == "viewing" && x.Prefer != string.Empty)
            {
                sb.Append("<dt>Asked for</dt><dd><b>" + Ui.Enc(x.Prefer) + "</b></dd>");
            }
            if (x.Address != string.Empty)
            {
                sb.Append("<dt>Address</dt><dd>" + Ui.Enc(x.Address) + "</dd>");
            }
            if (x.Kind != "viewing" && x.Details != string.Empty)
            {
                sb.Append("<dt>" + (x.Kind == "valuation" ? "Home" : "Topic") + "</dt><dd>" + Ui.Enc(x.Details) + "</dd>");
            }
            sb.Append("<dt>" + Ui.Enc(site.Settings.AgentWord) + "</dt><dd>" + (x.Agent != string.Empty ? Ui.Enc(Ui.AgentName(site, x.Agent)) : "Unassigned") + "</dd></dl>");
            if (x.Message != string.Empty)
            {
                sb.Append("<h3>Message</h3><div class=\"le-lmsg\">" + Ui.Enc(x.Message) + "</div>");
            }
            if (canE)
            {
                sb.Append("<h3>Status</h3><div class=\"le-stbtns\">");
                foreach ((string k, string n) in Ui.LeadStatuses)
                {
                    sb.Append("<button type=\"button\" class=\"le-chip" + (k == x.Status ? " le-act" : string.Empty) + "\" onclick=\"AdminLeadsJs.status('" + k + "')\"" + (k == x.Status ? " aria-pressed=\"true\"" : string.Empty) + ">" + n + "</button>");
                }
                sb.Append("</div>");
            }
            string own = Auth.Mine(c);
            if (canX)
            {
                sb.Append("<h3>Assign</h3><div class=\"le-assign\"><select id=\"le-eto\" aria-label=\"Assign to\"><option value=\"\">Unassigned</option>" + Ui.Opts(site.Agents.Where(a => a.Active || a.Id == x.Agent).OrderBy(a => a.Order).Select(a => (a.Id, a.Name)), x.Agent) + "</select><button type=\"button\" class=\"le-btn le-btn-sm\" onclick=\"AdminLeadsJs.assign()\">Assign</button></div>");
            }
            else if (Access.Can(c.U, AreaKey, 'E') && x.Agent == string.Empty && c.U.AgentId != string.Empty)
            {
                sb.Append("<h3>Assign</h3><button type=\"button\" class=\"le-btn le-btn-sm\" onclick=\"AdminLeadsJs.take()\">Take this lead</button>");
            }
            sb.Append("<h3>Notes</h3><div class=\"le-notes\">");
            if (x.Notes.Count == 0)
            {
                sb.Append("<p class=\"le-hint\">No notes yet.</p>");
            }
            foreach (Note n in x.Notes.OrderByDescending(n => n.When))
            {
                sb.Append("<div class=\"le-note\">" + Ui.Enc(n.Text) + "<small>" + Ui.Enc(Who(staff, n.By)) + " &middot; " + Ui.Time(n.When) + "</small></div>");
            }
            sb.Append("</div>");
            if (canE)
            {
                sb.Append("<div class=\"le-form\" style=\"margin-top:10px\"><label class=\"le-field\"><span>Add a note</span><textarea id=\"le-enote\" rows=\"2\" maxlength=\"500\" placeholder=\"Called back, wants a 3-bed near the river\"></textarea></label><p class=\"le-hint\">Keep notes to the home search. No financial or personal details beyond what is needed.</p><div><button type=\"button\" class=\"le-btn le-btn-sm\" onclick=\"AdminLeadsJs.note()\">Save note</button></div></div>");
            }
            sb.Append("<div class=\"le-mfoot\">");
            if (canX)
            {
                sb.Append("<button type=\"button\" class=\"le-btn le-btn-danger le-left\" onclick=\"AdminLeadsJs.remove('" + Ui.Js(x.Name) + "')\">Delete lead</button>");
            }
            if (canE && l != null && Access.Can(c.U, "viewings", 'E') && Ui.Live(l))
            {
                sb.Append("<a class=\"le-btn le-btn-accent\" href=\"AdminViewings?lead=" + Uri.EscapeDataString(x.Id) + "\">Book a viewing</a>");
            }
            sb.Append("<button type=\"button\" class=\"le-btn le-btn-ghost\" onclick=\"AdminLeadsJs.closeModal()\">Close</button></div>");
            response.SetElementContents("le-mbody", sb.ToString());
            response.ExecuteScript("AdminLeadsJs.openModal();");
            return response;
        }

        private async Task<(Lead? Lead, SiteData Site)> Find(Ctx c)
        {
            SiteData site = await Store.LoadSite();
            Lead? x = site.Leads.FirstOrDefault(v => v.Id == Ui.Clip(GetDataValue("eid"), 20));
            return (x != null && Sees(c, x) ? x : null, site);
        }

        public async Task<ApiResponse> SetStatus()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminLeadsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "lead status", GetDataValue("eid"), "You can look but not change leads.");
            }
            string to = Ui.LeadStatuses.Any(s => s.Key == GetDataValue("est2")) ? GetDataValue("est2") : string.Empty;
            string name = string.Empty;
            using (await Store.Hold())
            {
                (Lead? x, SiteData site) = await Find(c);
                if (x == null || to == string.Empty || !Mine(c, x))
                {
                    return await Refuse(response, c, "lead status", GetDataValue("eid"), "Take the lead first, or ask the office manager.");
                }
                x.Status = to;
                name = x.Name;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "lead " + to, name, true);
            response.ExecuteScript("AdminLeadsJs.changed('Marked " + Ui.Js(Ui.NameOf(Ui.LeadStatuses, to).ToLowerInvariant()) + ".');");
            return response;
        }

        public async Task<ApiResponse> Assign()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminLeadsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "assign lead", GetDataValue("eid"), "You can look but not change leads.");
            }
            string to = GetDataValue("eto");
            bool canX = Access.Can(c.U, AreaKey, 'X');
            string name = string.Empty;
            string who = string.Empty;
            using (await Store.Hold())
            {
                (Lead? x, SiteData site) = await Find(c);
                if (x == null)
                {
                    return response;
                }
                if (to == "me")
                {
                    if (x.Agent != string.Empty || c.U.AgentId == string.Empty)
                    {
                        return await Refuse(response, c, "assign lead", x.Id, "Someone already has this lead.");
                    }
                    to = c.U.AgentId;
                }
                else if (!canX)
                {
                    return await Refuse(response, c, "assign lead", x.Id, "Assigning leads needs the Assign right.");
                }
                if (to != string.Empty && !site.Agents.Any(a => a.Id == to))
                {
                    return response;
                }
                x.Agent = to;
                who = to == string.Empty ? "nobody" : Ui.AgentName(site, to);
                x.Notes.Add(new Note { When = DateTime.UtcNow, By = c.U.Id, Text = "Assigned to " + who + "." });
                name = x.Name;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "assign lead", name + " → " + who, true);
            response.ExecuteScript("AdminLeadsJs.changed('Assigned to " + Ui.Js(who) + ".');");
            return response;
        }

        public async Task<ApiResponse> AddNote()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminLeadsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "lead note", GetDataValue("eid"), "You can look but not change leads.");
            }
            string text = Regex.Replace(Ui.Clip(GetDataValue("enote"), 500), "\\s+", " ").Trim();
            if (text.Length < 2)
            {
                response.ExecuteScript("AdminLeadsJs.toast('Write the note first.');");
                return response;
            }
            using (await Store.Hold())
            {
                (Lead? x, SiteData site) = await Find(c);
                if (x == null || !Mine(c, x))
                {
                    return await Refuse(response, c, "lead note", GetDataValue("eid"), "Take the lead first, or ask the office manager.");
                }
                x.Notes.Add(new Note { When = DateTime.UtcNow, By = c.U.Id, Text = text });
                if (x.Status == "new")
                {
                    x.Status = "contacted";
                }
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "lead note", GetDataValue("eid"), true);
            response.ExecuteScript("AdminLeadsJs.changed('Note saved.');");
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminLeadsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "delete lead", GetDataValue("eid"), "Deleting leads needs the Assign right.");
            }
            string name = string.Empty;
            using (await Store.Hold())
            {
                (Lead? x, SiteData site) = await Find(c);
                if (x == null)
                {
                    return response;
                }
                name = x.Name;
                site.Leads.Remove(x);
                foreach (Viewing v in site.Viewings.Where(v => v.Lead == x.Id))
                {
                    v.Lead = string.Empty;
                }
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "delete lead", name, true);
            response.ExecuteScript("AdminLeadsJs.deleted();");
            return response;
        }
    }
}
