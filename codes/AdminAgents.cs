using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class AdminAgents : WebPage
    {
        private const string AreaKey = "agents";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Agents");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ta-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ta-menu", Auth.Menu("ta", "AdminAgents", c, site));
            response.SetElementContents("ta-who", Auth.Who("ta", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminAgentsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ta-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ta-panel\"><p>You don't have access to this area. Ask the office manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminAgentsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAgentsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ta-main", NoAccess("Agents"));
                return response;
            }
            response.SetElementContents("ta-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminAgentsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ta-tr ta-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string ImageExt(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            {
                return "jpg";
            }
            if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            {
                return "png";
            }
            if (b.Length > 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
            {
                return "webp";
            }
            return string.Empty;
        }

        private static byte[]? DataImage(string data, int max)
        {
            Match m = Regex.Match(data ?? string.Empty, "^data:image/(jpeg|png|webp);base64,([A-Za-z0-9+/=]+)$");
            if (!m.Success)
            {
                return null;
            }
            try
            {
                byte[] b = Convert.FromBase64String(m.Groups[2].Value);
                return b.Length <= max && ImageExt(b) != string.Empty ? b : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static async Task<string> SaveUpload(string full, string thumb)
        {
            byte[]? big = DataImage(full, 6 * 1024 * 1024);
            byte[]? small = DataImage(thumb, 2 * 1024 * 1024);
            if (big == null || small == null || ImageExt(big) != ImageExt(small))
            {
                return string.Empty;
            }
            string ext = ImageExt(big);
            string id = Guid.NewGuid().ToString("N").Substring(0, 16);
            string dir = Path.Combine(Store.Root, "images", "uploads");
            Directory.CreateDirectory(Path.Combine(dir, "full"));
            Directory.CreateDirectory(Path.Combine(dir, "thumbs"));
            await File.WriteAllBytesAsync(Path.Combine(dir, "full", id + "." + ext), big);
            await File.WriteAllBytesAsync(Path.Combine(dir, "thumbs", id + "." + ext), small);
            return "images/uploads/full/" + id + "." + ext;
        }

        private static string Arrows(string id, bool first, bool last)
        {
            return "<span class=\"ta-ord\"><button type=\"button\" class=\"ta-ibtn\" aria-label=\"Move up\" onclick=\"AdminAgentsJs.move('" + Ui.Attr(id) + "',-1)\"" + (first ? " disabled" : string.Empty) + "><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button><button type=\"button\" class=\"ta-ibtn\" aria-label=\"Move down\" onclick=\"AdminAgentsJs.move('" + Ui.Attr(id) + "',1)\"" + (last ? " disabled" : string.Empty) + "><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg></button></span>";
        }

        private const int Size = 20;

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            string word = site.Settings.AgentsWord;
            return "<div class=\"ta-ptop\"><div><h1>" + Ui.Enc(word) + "</h1><p>The people shown on the website, in this order. Switch someone off to hide them without losing their history.</p></div><div class=\"ta-acts\">" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"ta-btn ta-btn-accent\" onclick=\"AdminAgentsJs.edit('')\">Add a person</button>" : string.Empty) + "</div></div><div class=\"ta-bar\"><label>Search<input type=\"search\" id=\"ta-q\" list=\"ta-qlist\" placeholder=\"Name, title, phone or email\" maxlength=\"60\" oninput=\"AdminAgentsJs.search()\"></label>" + Ui.DataList("ta-qlist", site.Agents.Select(a => a.Name).Concat(site.Agents.Select(a => a.Title))) + "<label>Neighborhood<select id=\"ta-tfarea\" onchange=\"AdminAgentsJs.list(1)\"><option value=\"\">All</option>" + Ui.Opts(site.Areas.OrderBy(a => a.Order).Select(a => (a.Key, a.Name)), string.Empty) + "</select></label><label>Status<select id=\"ta-tfst\" onchange=\"AdminAgentsJs.list(1)\">" + Ui.Opts(new[] { ("", "Shown and hidden"), ("on", "Shown"), ("off", "Hidden") }, string.Empty) + "</select></label></div><div class=\"ta-tbl ta-tatbl\" id=\"ta-list\"></div><div id=\"ta-pager\"></div>";
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAgentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            List<Staff> staff = await Store.LoadStaff();
            bool canE = Access.Can(c.U, AreaKey, 'E');
            string term = Ui.Clip(GetDataValue("tq"), 60);
            string area = GetDataValue("tfarea");
            string st = GetDataValue("tfst");
            bool filtered = term != string.Empty || area != string.Empty || st != string.Empty;
            List<TeamMember> all = site.Agents.OrderBy(a => a.Order).ToList();
            List<TeamMember> hits = all.Where(a => (term == string.Empty || (a.Name + " " + a.Title + " " + a.Phone + " " + a.Email + " " + string.Join(" ", a.Languages)).Contains(term, StringComparison.OrdinalIgnoreCase)) && (area == string.Empty || a.Areas.Contains(area)) && (st == string.Empty || a.Active == (st == "on"))).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, hits.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { (string.Empty, string.Empty), ("Name", string.Empty), ("Neighborhoods", string.Empty), ("Sign-in", string.Empty), ("Listings", "ta-num"), ("Status", string.Empty), (string.Empty, string.Empty), (string.Empty, string.Empty) }));
            foreach (TeamMember a in hits.Skip((page - 1) * Size).Take(Size))
            {
                int i = all.IndexOf(a);
                int n = site.Listings.Count(l => l.Agent == a.Id && Ui.Live(l));
                Staff? acct = staff.FirstOrDefault(u => u.AgentId == a.Id);
                string sign = acct == null ? "<span class=\"ta-nosign\">No sign-in</span>" : "<b class=\"ta-code\">" + Ui.Enc(acct.Login) + "</b>" + (acct.Active ? string.Empty : "<small>Switched off</small>");
                sb.Append("<div class=\"ta-tr\"><span>" + Ui.Face("ta", a) + "</span><span class=\"ta-wide\"><b>" + Ui.Enc(a.Name) + "</b><small>" + Ui.Enc(a.Title) + " &middot; " + Ui.Enc(a.Phone) + "</small></span><span data-l=\"Neighborhoods\">" + Ui.Enc(string.Join(", ", a.Areas.Select(k => Ui.AreaName(site, k)))) + "</span><span data-l=\"Sign-in\">" + sign + "</span><span data-l=\"Listings\" class=\"ta-num\">" + n + "</span><span data-l=\"Status\"><span class=\"ta-st ta-st-" + (a.Active ? "live\">Shown" : "off\">Hidden") + "</span></span><span>" + (canE && !filtered ? Arrows(a.Id, i == 0, i == all.Count - 1) : string.Empty) + "</span><span><button type=\"button\" class=\"ta-btn ta-btn-sm ta-btn-ghost\" onclick=\"AdminAgentsJs.edit('" + Ui.Attr(a.Id) + "')\">" + (canE ? "Edit" : "View") + "</button></span></div>");
            }
            if (hits.Count == 0)
            {
                sb.Append("<div class=\"ta-tr\"><span class=\"ta-none ta-wide\">" + (filtered ? "Nobody matches." : "Nobody yet.") + "</span></div>");
            }
            response.SetElementContents("ta-list", sb.ToString());
            response.SetElementContents("ta-pager", Ui.Pager("ta", page, Size, hits.Count, "person", "people", "AdminAgentsJs.list") + (filtered && canE ? "<p class=\"ta-hint ta-ordhint\">Clear the search and filters to change the order.</p>" : string.Empty));
            return response;
        }

        private static string Rights(Staff u)
        {
            if (u.Admin)
            {
                return "Office manager: every area";
            }
            List<string> parts = new List<string>();
            foreach (Right r in Access.Rights)
            {
                string lv = Access.Level(u, r.Key);
                if (lv != string.Empty)
                {
                    parts.Add(r.Name + " " + (lv.Contains('E') ? "edit" : "view") + (lv.Contains('X') ? " + " + r.Extra.ToLowerInvariant() : string.Empty));
                }
            }
            return (parts.Count == 0 ? "No areas yet" : string.Join(", ", parts)) + (u.Own ? "; own records only" : string.Empty);
        }

        private static string AccessHtml(Ctx c, TeamMember a, Staff? acct)
        {
            StringBuilder sb = new StringBuilder("<div class=\"ta-acc\"><h3>Sign-in</h3>");
            if (!Access.Can(c.U, "users", 'E'))
            {
                sb.Append("<p class=\"ta-hint\">" + (acct != null ? "Signs in as <b>" + Ui.Enc(acct.Login) + "</b>. " + Ui.Enc(Rights(acct)) + "." : "No sign-in yet. The office manager can add one here or in Users.") + "</p></div>");
                return sb.ToString();
            }
            if (acct != null)
            {
                sb.Append("<div class=\"ta-f3\"><label class=\"ta-field\"><span>User name</span><input type=\"text\" id=\"ta-tlogin\" value=\"" + Ui.Attr(acct.Login) + "\" readonly></label><label class=\"ta-field\"><span>New password (optional)</span><input type=\"password\" id=\"ta-tpass\" maxlength=\"100\" autocomplete=\"new-password\"></label><label class=\"ta-chk\" style=\"align-self:end\"><input type=\"checkbox\" id=\"ta-tsignin\"" + (acct.Active ? " checked" : string.Empty) + "> Can sign in</label></div><p class=\"ta-hint\">Rights: " + Ui.Enc(Rights(acct)) + ". <a href=\"AdminUsers?id=" + Uri.EscapeDataString(acct.Id) + "\">Change rights in Users</a></p></div>");
                return sb.ToString();
            }
            string suggest = Regex.Replace((a.Name.Split(' ').FirstOrDefault() ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]", string.Empty);
            sb.Append("<div class=\"ta-f3\"><label class=\"ta-field\"><span>User name</span><input type=\"text\" id=\"ta-tlogin\" maxlength=\"20\" autocapitalize=\"none\" placeholder=\"" + Ui.Attr(suggest != string.Empty ? suggest : "jane") + "\"></label><label class=\"ta-field\"><span>Password</span><input type=\"password\" id=\"ta-tpass\" maxlength=\"100\" autocomplete=\"new-password\"></label><label class=\"ta-chk\" style=\"align-self:end\"><input type=\"checkbox\" id=\"ta-tsignin\" checked> Can sign in</label></div><p class=\"ta-hint\">Leave the user name empty for no sign-in. A new sign-in starts with Listings, Leads and Viewings (edit) and Neighborhoods (view), own records only. Change any of it later in Users.</p></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAgentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "agent", GetDataValue("tid"), "You don't have access to this area.");
            }
            SiteData site = await Store.LoadSite();
            TeamMember? found = site.Agents.FirstOrDefault(a => a.Id == Ui.Clip(GetDataValue("tid"), 40));
            bool canE = Access.Can(c.U, AreaKey, 'E');
            TeamMember a = found ?? new TeamMember { Years = 1, Languages = new List<string> { "English" } };
            StringBuilder sb = new StringBuilder("<h2>" + (found == null ? "Add a person" : Ui.Enc(a.Name)) + "</h2><fieldset class=\"ta-form\" style=\"border:0;padding:0;margin:0;min-width:0\"" + (canE ? string.Empty : " disabled") + "><div class=\"ta-f3\"><label class=\"ta-field\"><span>Full name</span><input type=\"text\" id=\"ta-tname\" maxlength=\"60\" value=\"" + Ui.Attr(a.Name) + "\"></label><label class=\"ta-field\"><span>Job title</span><input type=\"text\" id=\"ta-ttitle\" maxlength=\"60\" value=\"" + Ui.Attr(a.Title) + "\" placeholder=\"Senior agent\"></label><label class=\"ta-field\"><span>Years in real estate</span><input type=\"number\" id=\"ta-tyears\" min=\"0\" max=\"60\" value=\"" + a.Years + "\"></label></div>");
            sb.Append("<div class=\"ta-f3\"><label class=\"ta-field\"><span>Phone</span><input type=\"tel\" id=\"ta-tphone\" maxlength=\"30\" value=\"" + Ui.Attr(a.Phone) + "\"></label><label class=\"ta-field\"><span>Email</span><input type=\"email\" id=\"ta-tmail\" maxlength=\"120\" value=\"" + Ui.Attr(a.Email) + "\"></label><label class=\"ta-field\"><span>License number</span><input type=\"text\" id=\"ta-tlic\" maxlength=\"60\" value=\"" + Ui.Attr(a.License) + "\"></label></div>");
            sb.Append("<label class=\"ta-field\"><span>Languages (separate with commas)</span><input type=\"text\" id=\"ta-tlang\" maxlength=\"120\" value=\"" + Ui.Attr(string.Join(", ", a.Languages)) + "\"></label><label class=\"ta-field\"><span>Short bio</span><textarea id=\"ta-tbio\" rows=\"4\" maxlength=\"700\">" + Ui.Enc(a.Bio) + "</textarea></label>");
            sb.Append("<div><span class=\"ta-hint\">Neighborhoods they cover (valuation requests go to the first person who covers the area)</span><div class=\"ta-checks\" style=\"margin-top:6px\">");
            foreach (Models.Area ar in site.Areas.OrderBy(x => x.Order))
            {
                sb.Append("<label class=\"ta-chk\"><input type=\"checkbox\" name=\"ta-tarea\" value=\"" + Ui.Attr(ar.Key) + "\"" + (a.Areas.Contains(ar.Key) ? " checked" : string.Empty) + ">" + Ui.Enc(ar.Name) + "</label>");
            }
            sb.Append("</div></div><div><span class=\"ta-hint\">Photo</span><div class=\"ta-ppick\" id=\"ta-ppick\" style=\"margin-top:6px\"><label><input type=\"radio\" name=\"ta-tphoto\" value=\"\"" + (a.Photo == string.Empty ? " checked" : string.Empty) + "><span class=\"ta-pnone\">Initials only</span></label>");
            foreach (string src in Ui.Images("agents").Concat(Ui.Images("uploads/full").Where(s => s.EndsWith(".jpg", StringComparison.Ordinal) && site.Agents.Any(x => x.Photo == s))).Append(a.Photo).Where(s => s != string.Empty).Distinct())
            {
                sb.Append("<label><input type=\"radio\" name=\"ta-tphoto\" value=\"" + Ui.Attr(src) + "\"" + (src == a.Photo ? " checked" : string.Empty) + "><img src=\"" + Ui.Attr(src) + "\" alt=\"\" loading=\"lazy\"></label>");
            }
            sb.Append("</div>" + (canE ? "<div class=\"ta-upl\" style=\"margin-top:8px;display:flex;gap:10px;align-items:center;flex-wrap:wrap\"><input type=\"file\" accept=\"image/jpeg,image/png,image/webp\" onchange=\"AdminAgentsJs.upload(this)\" aria-label=\"Upload a photo\"><span class=\"ta-hint\" id=\"ta-upst\"></span></div><em class=\"ta-err\" id=\"ta-e-upl\" role=\"alert\"></em>" : string.Empty) + "</div><label class=\"ta-chk\"><input type=\"checkbox\" id=\"ta-tactive\"" + (found == null || a.Active ? " checked" : string.Empty) + "> Show on the website</label>" + AccessHtml(c, a, found != null ? (await Store.LoadStaff()).FirstOrDefault(u => u.AgentId == a.Id) : null) + "</fieldset><em class=\"ta-err\" id=\"ta-e-team\" role=\"alert\"></em><div class=\"ta-mfoot\">");
            if (found != null && canE)
            {
                sb.Append("<button type=\"button\" class=\"ta-btn ta-btn-danger ta-left\" onclick=\"AdminAgentsJs.remove('" + Ui.Attr(found.Id) + "','" + Ui.Js(found.Name) + "')\">Remove</button>");
            }
            sb.Append("<button type=\"button\" class=\"ta-btn ta-btn-ghost\" onclick=\"AdminAgentsJs.closeModal()\">" + (canE ? "Cancel" : "Close") + "</button>" + (canE ? "<button type=\"button\" class=\"ta-btn ta-btn-accent\" onclick=\"AdminAgentsJs.save('" + Ui.Attr(found?.Id ?? string.Empty) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("ta-mbody", sb.ToString());
            response.ExecuteScript("AdminAgentsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAgentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "agent", GetDataValue("tid"), "You can look but not change this area.");
            }
            string id = Ui.Clip(GetDataValue("tid"), 40);
            string name = Regex.Replace(Ui.Clip(GetDataValue("tname"), 60), "\\s+", " ").Trim();
            string mail = Ui.Clip(GetDataValue("tmail"), 120).Trim();
            string pic = Ui.Clip(GetDataValue("tpic"), 200);
            bool canU = Access.Can(c.U, "users", 'E');
            string login = canU ? Regex.Replace(Ui.Clip(GetDataValue("tlogin"), 20).Trim().ToLowerInvariant(), "[^a-z0-9._-]", string.Empty) : string.Empty;
            string pass = canU ? GetDataValue("tpass") : string.Empty;
            bool signin = GetDataValue("tsignin") == "1";
            string err = name.Length < 3 ? "Enter the full name." : mail != string.Empty && !Ui.Email(mail) ? "Check the email address." : pic != string.Empty && !Ui.SafeImage(pic) ? "Choose the photo again." : pass != string.Empty && (pass.Length < 8 || pass.Length > 100) ? "Passwords need 8 characters or more." : string.Empty;
            if (err != string.Empty)
            {
                response.SetElementContents("ta-e-team", err);
                return response;
            }
            string closeUser = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                TeamMember? a = site.Agents.FirstOrDefault(x => x.Id == id);
                List<Staff> staff = await Store.LoadStaff();
                Staff? acct = a != null ? staff.FirstOrDefault(u => u.AgentId == a.Id) : null;
                if (canU && acct == null && login != string.Empty)
                {
                    err = login.Length < 3 ? "Choose a user name of 3 characters or more." : staff.Any(u => u.Login == login) ? "That user name is taken." : pass == string.Empty ? "Give the new sign-in a password of 8 characters or more." : string.Empty;
                }
                if (canU && acct != null && acct.Id == c.U.Id && !signin)
                {
                    err = "You can't switch your own sign-in off.";
                }
                if (err != string.Empty)
                {
                    response.SetElementContents("ta-e-team", err);
                    return response;
                }
                if (a == null)
                {
                    string slug = "a-" + Regex.Replace(name.Split(' ').Last().ToLowerInvariant(), "[^a-z0-9]", string.Empty);
                    string key = slug;
                    for (int i = 2; site.Agents.Any(x => x.Id == key); i++)
                    {
                        key = slug + i;
                    }
                    a = new TeamMember { Id = key, Order = site.Agents.Count == 0 ? 0 : site.Agents.Max(x => x.Order) + 1 };
                    site.Agents.Add(a);
                }
                a.Name = name;
                a.Title = Ui.Clip(GetDataValue("ttitle"), 60).Trim();
                a.Years = int.TryParse(GetDataValue("tyears"), out int y) ? Math.Clamp(y, 0, 60) : a.Years;
                a.Phone = Ui.CleanPhone(GetDataValue("tphone"));
                a.Email = mail;
                a.License = Ui.Clip(GetDataValue("tlic"), 60).Trim();
                a.Languages = GetDataValue("tlang").Split(',').Select(x => Ui.Clip(x, 30).Trim()).Where(x => x != string.Empty).Distinct().Take(8).ToList();
                a.Bio = Ui.Clip(GetDataValue("tbio"), 700).Trim();
                a.Areas = GetDataValue("tareas").Split(',').Where(k => site.Areas.Any(x => x.Key == k)).Distinct().ToList();
                a.Photo = pic;
                a.Active = GetDataValue("tactive") == "1";
                id = a.Id;
                await Store.SaveSite(site);
                if (canU && acct != null)
                {
                    acct.Name = name;
                    if (pass != string.Empty)
                    {
                        acct.Hash = Access.HashPassword(pass);
                    }
                    if (pass != string.Empty || (acct.Active && !signin))
                    {
                        closeUser = acct.Id;
                    }
                    acct.Active = signin;
                    await Store.SaveStaff(staff);
                }
                else if (canU && login != string.Empty)
                {
                    staff.Add(new Staff { Id = "u-" + login, Login = login, Name = name, Title = a.Title, Hash = Access.HashPassword(pass), AgentId = a.Id, Own = true, Active = signin, Perms = new Dictionary<string, string>(Access.AgentDefaults) });
                    await Store.SaveStaff(staff);
                    await Store.Log(c.U.Id, "user", login + " \u00B7 " + name, true);
                }
            }
            if (closeUser != string.Empty && closeUser != c.U.Id)
            {
                await Auth.CloseUser(closeUser, string.Empty);
            }
            await Store.Log(c.U.Id, "agent", name, true);
            response.ExecuteScript("AdminAgentsJs.done('Saved.');");
            return response;
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAgentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "agent order", GetDataValue("tid"), "You can look but not change this area.");
            }
            int dir = GetDataValue("tdir") == "-1" ? -1 : 1;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                List<TeamMember> all = site.Agents.OrderBy(a => a.Order).ToList();
                int i = all.FindIndex(a => a.Id == GetDataValue("tid"));
                int j = i + dir;
                if (i < 0 || j < 0 || j >= all.Count)
                {
                    return response;
                }
                (all[i], all[j]) = (all[j], all[i]);
                for (int k = 0; k < all.Count; k++)
                {
                    all[k].Order = k;
                }
                await Store.SaveSite(site);
            }
            response.ExecuteScript("AdminAgentsJs.list(0);");
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAgentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "remove agent", GetDataValue("tid"), "You can look but not change this area.");
            }
            string name = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                TeamMember? a = site.Agents.FirstOrDefault(x => x.Id == GetDataValue("tid"));
                if (a == null)
                {
                    return response;
                }
                int n = site.Listings.Count(l => l.Agent == a.Id);
                if (n > 0)
                {
                    response.ExecuteScript("AdminAgentsJs.toast('" + Ui.Js(a.Name.Split(' ')[0] + " still has " + Ui.Plural(n, "listing", "listings") + ". Give them to someone else first, or switch " + a.Name.Split(' ')[0] + " off instead.") + "');");
                    return response;
                }
                name = a.Name;
                site.Agents.Remove(a);
                foreach (Lead x in site.Leads.Where(x => x.Agent == a.Id))
                {
                    x.Agent = string.Empty;
                }
                await Store.SaveSite(site);
                List<Staff> staff = await Store.LoadStaff();
                foreach (Staff u in staff.Where(u => u.AgentId == a.Id))
                {
                    u.AgentId = string.Empty;
                    u.Own = false;
                }
                await Store.SaveStaff(staff);
            }
            await Store.Log(c.U.Id, "remove agent", name, true);
            response.ExecuteScript("AdminAgentsJs.done('Removed.');");
            return response;
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAgentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "upload", "photo", "You can look but not upload photos.");
            }
            string src = await SaveUpload(GetDataValue("full"), GetDataValue("thumb"));
            if (src == string.Empty)
            {
                response.ExecuteScript("AdminAgentsJs.uploaded('','That file is not a JPG, PNG or WebP photo, or it is too large.');");
                return response;
            }
            await Store.Log(c.U.Id, "upload", src, true);
            response.ExecuteScript("AdminAgentsJs.uploaded('" + Ui.Js(src) + "','');");
            return response;
        }
    }
}
