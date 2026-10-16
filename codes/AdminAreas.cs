using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class AdminAreas : WebPage
    {
        private const string AreaKey = "areas";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Neighborhoods");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ar-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ar-menu", Auth.Menu("ar", "AdminAreas", c, site));
            response.SetElementContents("ar-who", Auth.Who("ar", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminAreasJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ar-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ar-panel\"><p>You don't have access to this area. Ask the office manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminAreasJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAreasJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ar-main", NoAccess("Neighborhoods"));
                return response;
            }
            response.SetElementContents("ar-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminAreasJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ar-tr ar-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string MapHtml(string p, string id, string cls, IEnumerable<Models.Area> labels, string pins, string extra)
        {
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-map " + cls + "\" id=\"" + id + "\" data-pins=\"" + Ui.Attr(pins) + "\" tabindex=\"-1\"><div class=\"" + p + "-mlayer\"><img src=\"images/map/ashby-harbor.svg\" alt=\"Map of Ashby Harbor\" width=\"1600\" height=\"1200\" draggable=\"false\">");
            foreach (Models.Area a in labels)
            {
                sb.Append("<span class=\"" + p + "-mlab\" style=\"left:" + (a.X / 10).ToString("0.##", Ui.Inv) + "%;top:" + (a.Y / 7).ToString("0.##", Ui.Inv) + "%\"><i style=\"background:" + (Ui.SafeColor(a.Color) ? a.Color : "#B5562F") + "\"></i>" + Ui.Enc(a.Name) + "</span>");
            }
            sb.Append("<div class=\"" + p + "-pins\"></div></div><div class=\"" + p + "-mctl\"><button type=\"button\" aria-label=\"Zoom in\" onclick=\"AdminAreasJs.zoom('" + id + "',1.4)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Zoom out\" onclick=\"AdminAreasJs.zoom('" + id + "',0.714)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Show all\" onclick=\"AdminAreasJs.fit('" + id + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5\"/></svg></button></div>" + extra + "</div>");
            return sb.ToString();
        }

        private static string Arrows(string id, bool first, bool last)
        {
            return "<span class=\"ar-ord\"><button type=\"button\" class=\"ar-ibtn\" aria-label=\"Move up\" onclick=\"AdminAreasJs.move('" + Ui.Attr(id) + "',-1)\"" + (first ? " disabled" : string.Empty) + "><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button><button type=\"button\" class=\"ar-ibtn\" aria-label=\"Move down\" onclick=\"AdminAreasJs.move('" + Ui.Attr(id) + "',1)\"" + (last ? " disabled" : string.Empty) + "><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg></button></span>";
        }

        private const int Size = 20;

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            return "<div class=\"ar-ptop\"><div><h1>Neighborhoods</h1><p>Areas used in search filters, on the map and on the home page, in this order.</p></div><div class=\"ar-acts\">" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"ar-btn ar-btn-accent\" onclick=\"AdminAreasJs.edit('')\">Add a neighborhood</button>" : string.Empty) + "</div></div><div class=\"ar-bar\"><label>Search<input type=\"search\" id=\"ar-q\" list=\"ar-qlist\" placeholder=\"Name or text\" maxlength=\"60\" oninput=\"AdminAreasJs.search()\"></label>" + Ui.DataList("ar-qlist", site.Areas.Select(a => a.Name)) + "<label>Status<select id=\"ar-afst\" onchange=\"AdminAreasJs.list(1)\">" + Ui.Opts(new[] { ("", "Shown and hidden"), ("on", "Shown"), ("off", "Hidden") }, string.Empty) + "</select></label></div><div class=\"ar-tbl ar-artbl\" id=\"ar-list\"></div><div id=\"ar-pager\"></div>";
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAreasJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            bool canE = Access.Can(c.U, AreaKey, 'E');
            string term = Ui.Clip(GetDataValue("aq"), 60);
            string st = GetDataValue("afst");
            bool filtered = term != string.Empty || st != string.Empty;
            List<Models.Area> all = site.Areas.OrderBy(a => a.Order).ToList();
            List<Models.Area> hits = all.Where(a => (term == string.Empty || (a.Name + " " + a.Key + " " + a.Blurb + " " + a.Text).Contains(term, StringComparison.OrdinalIgnoreCase)) && (st == string.Empty || a.Active == (st == "on"))).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, hits.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { (string.Empty, string.Empty), ("Name", string.Empty), ("Short text", string.Empty), ("Homes", "ar-num"), ("Status", string.Empty), (string.Empty, string.Empty), (string.Empty, string.Empty) }));
            if (hits.Count == 0)
            {
                sb.Append("<div class=\"ar-tr\"><span class=\"ar-none ar-wide\">" + (filtered ? "No neighborhoods match." : "No neighborhoods yet.") + "</span></div>");
            }
            foreach (Models.Area a in hits.Skip((page - 1) * Size).Take(Size))
            {
                int i = all.IndexOf(a);
                int n = site.Listings.Count(l => l.Area == a.Key && Ui.Live(l));
                string sw = "<i class=\"ar-sw\" style=\"background:" + (Ui.SafeColor(a.Color) ? a.Color : "#B5562F") + "\"></i>";
                sb.Append("<div class=\"ar-tr\"><span class=\"ar-swc\">" + sw + "</span><span><b>" + "<span class=\"ar-swm\">" + sw + "</span>" + Ui.Enc(a.Name) + "</b><small>" + Ui.Enc(a.Key) + "</small></span><span data-l=\"Short text\" class=\"ar-wide\">" + Ui.Enc(a.Blurb) + "</span><span data-l=\"Homes\" class=\"ar-num\">" + n + "</span><span data-l=\"Status\"><span class=\"ar-st ar-st-" + (a.Active ? "live\">Shown" : "off\">Hidden") + "</span></span><span>" + (canE && !filtered ? Arrows(a.Key, i == 0, i == all.Count - 1) : string.Empty) + "</span><span><button type=\"button\" class=\"ar-btn ar-btn-sm ar-btn-ghost\" onclick=\"AdminAreasJs.edit('" + Ui.Attr(a.Key) + "')\">" + (canE ? "Edit" : "View") + "</button></span></div>");
            }
            response.SetElementContents("ar-list", sb.ToString());
            response.SetElementContents("ar-pager", Ui.Pager("ar", page, Size, hits.Count, "neighborhood", "neighborhoods", "AdminAreasJs.list") + (filtered && canE ? "<p class=\"ar-hint ar-ordhint\">Clear the search and filter to change the order.</p>" : string.Empty));
            return response;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAreasJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "area", GetDataValue("akey"), "You don't have access to this area.");
            }
            SiteData site = await Store.LoadSite();
            Models.Area? found = site.Areas.FirstOrDefault(a => a.Key == Ui.Clip(GetDataValue("akey"), 40));
            bool canE = Access.Can(c.U, AreaKey, 'E');
            Models.Area a = found ?? new Models.Area { X = 500, Y = 350, Color = "#2F5D50" };
            StringBuilder sb = new StringBuilder("<h2>" + (found == null ? "Add a neighborhood" : Ui.Enc(a.Name)) + "</h2><fieldset class=\"ar-form\" style=\"border:0;padding:0;margin:0;min-width:0\"" + (canE ? string.Empty : " disabled") + "><div class=\"ar-f3\"><label class=\"ar-field\"><span>Name</span><input type=\"text\" id=\"ar-aname\" maxlength=\"40\" value=\"" + Ui.Attr(a.Name) + "\"></label><label class=\"ar-field\"><span>Colour on the map</span><input type=\"color\" id=\"ar-acolor\" value=\"" + Ui.Attr(Ui.SafeColor(a.Color) ? a.Color : "#2F5D50") + "\"></label><label class=\"ar-chk\" style=\"align-self:end\"><input type=\"checkbox\" id=\"ar-aactive\"" + (found == null || a.Active ? " checked" : string.Empty) + "> Show on the website</label></div>");
            sb.Append("<label class=\"ar-field\"><span>Short text (cards and filters)</span><input type=\"text\" id=\"ar-ablurb\" maxlength=\"140\" value=\"" + Ui.Attr(a.Blurb) + "\"></label><label class=\"ar-field\"><span>About the neighborhood (listing pages)</span><textarea id=\"ar-atext\" rows=\"3\" maxlength=\"600\">" + Ui.Enc(a.Text) + "</textarea></label>");
            sb.Append("<div><span class=\"ar-hint\">Label position: click the map where the name should sit.</span><input type=\"hidden\" id=\"ar-ax\" value=\"" + a.X.ToString("0.#", Ui.Inv) + "\"><input type=\"hidden\" id=\"ar-ay\" value=\"" + a.Y.ToString("0.#", Ui.Inv) + "\"><div style=\"margin-top:6px\">" + MapHtml("ar", "ar-am", "ar-amap", site.Areas.Where(x => x.Active && x.Key != a.Key), "[]", string.Empty) + "</div></div>");
            sb.Append("<div><span class=\"ar-hint\">Picture on the home page</span><div class=\"ar-ipick\" style=\"margin-top:6px\">");
            foreach (string src in Ui.Images("homes/full").Concat(Ui.Images("uploads/full")).Where(s => !s.Contains("hero-street", StringComparison.Ordinal)))
            {
                sb.Append("<label><input type=\"radio\" name=\"ar-aimg\" value=\"" + Ui.Attr(src) + "\"" + (src == a.Image ? " checked" : string.Empty) + "><img src=\"" + Ui.Attr(Ui.Thumb(src)) + "\" alt=\"\" loading=\"lazy\"></label>");
            }
            sb.Append("</div></div></fieldset><em class=\"ar-err\" id=\"ar-e-area\" role=\"alert\"></em><div class=\"ar-mfoot\">");
            if (found != null && canE)
            {
                sb.Append("<button type=\"button\" class=\"ar-btn ar-btn-danger ar-left\" onclick=\"AdminAreasJs.remove('" + Ui.Attr(found.Key) + "','" + Ui.Js(found.Name) + "')\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"ar-btn ar-btn-ghost\" onclick=\"AdminAreasJs.closeModal()\">" + (canE ? "Cancel" : "Close") + "</button>" + (canE ? "<button type=\"button\" class=\"ar-btn ar-btn-accent\" onclick=\"AdminAreasJs.save('" + Ui.Attr(found?.Key ?? string.Empty) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("ar-mbody", sb.ToString());
            response.ExecuteScript("AdminAreasJs.opened();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAreasJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "area", GetDataValue("akey"), "You can look but not change neighborhoods.");
            }
            string key = Ui.Clip(GetDataValue("akey"), 40);
            string name = Regex.Replace(Ui.Clip(GetDataValue("aname"), 40), "\\s+", " ").Trim();
            string img = Ui.Clip(GetDataValue("aimg"), 200);
            if (name.Length < 2)
            {
                response.SetElementContents("ar-e-area", "Enter the name.");
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Models.Area? a = site.Areas.FirstOrDefault(x => x.Key == key);
                if (site.Areas.Any(x => x.Key != key && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    response.SetElementContents("ar-e-area", "There is already a neighborhood with that name.");
                    return response;
                }
                if (a == null)
                {
                    string slug = Regex.Replace(Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-"), "^-+|-+$", string.Empty);
                    slug = slug == string.Empty ? "area" : slug;
                    string k = slug;
                    for (int i = 2; site.Areas.Any(x => x.Key == k); i++)
                    {
                        k = slug + "-" + i;
                    }
                    a = new Models.Area { Key = k, Order = site.Areas.Count == 0 ? 0 : site.Areas.Max(x => x.Order) + 1 };
                    site.Areas.Add(a);
                }
                a.Name = name;
                a.Blurb = Ui.Clip(GetDataValue("ablurb"), 140).Trim();
                a.Text = Ui.Clip(GetDataValue("atext"), 600).Trim();
                a.Color = Ui.SafeColor(GetDataValue("acolor")) ? GetDataValue("acolor") : a.Color;
                a.X = double.TryParse(GetDataValue("ax"), NumberStyles.Float, Ui.Inv, out double x) ? Math.Round(Math.Clamp(x, 0, 1000), 1) : a.X;
                a.Y = double.TryParse(GetDataValue("ay"), NumberStyles.Float, Ui.Inv, out double y) ? Math.Round(Math.Clamp(y, 0, 700), 1) : a.Y;
                a.Image = img != string.Empty && Ui.SafeImage(img) ? img : a.Image;
                a.Active = GetDataValue("aactive") == "1";
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "neighborhood", name, true);
            response.ExecuteScript("AdminAreasJs.done('Saved.');");
            return response;
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAreasJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "area order", GetDataValue("akey"), "You can look but not change neighborhoods.");
            }
            int dir = GetDataValue("adir") == "-1" ? -1 : 1;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                List<Models.Area> all = site.Areas.OrderBy(a => a.Order).ToList();
                int i = all.FindIndex(a => a.Key == GetDataValue("akey"));
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
            response.ExecuteScript("AdminAreasJs.list(0);");
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAreasJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete area", GetDataValue("akey"), "You can look but not change neighborhoods.");
            }
            string name = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Models.Area? a = site.Areas.FirstOrDefault(x => x.Key == GetDataValue("akey"));
                if (a == null)
                {
                    return response;
                }
                int n = site.Listings.Count(l => l.Area == a.Key);
                if (n > 0)
                {
                    response.ExecuteScript("AdminAreasJs.toast('" + Ui.Js(Ui.Plural(n, "listing is", "listings are") + " in " + a.Name + ". Move them first, or hide the neighborhood instead.") + "');");
                    return response;
                }
                name = a.Name;
                site.Areas.Remove(a);
                foreach (TeamMember t in site.Agents)
                {
                    t.Areas.Remove(a.Key);
                }
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "delete neighborhood", name, true);
            response.ExecuteScript("AdminAreasJs.done('Deleted.');");
            return response;
        }
    }
}
