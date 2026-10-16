using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class AdminAccount : WebPage
    {
        private const string AreaKey = "";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | My account");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ac-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ac-menu", Auth.Menu("ac", "AdminAccount", c, site));
            response.SetElementContents("ac-who", Auth.Who("ac", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminAccountJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ac-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ac-panel\"><p>You don't have access to this area. Ask the office manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminAccountJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ac-main", NoAccess("My account"));
                return response;
            }
            response.SetElementContents("ac-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminAccountJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ac-tr ac-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            Staff u = c.U;
            StringBuilder sb = new StringBuilder("<div class=\"ac-ptop\"><div><h1>My account</h1><p>" + Ui.Enc(u.Title) + " &middot; signed in as <b>" + Ui.Enc(u.Login) + "</b></p></div></div><div class=\"ac-grid2\" style=\"margin-top:0\"><section class=\"ac-panel\"><h2>Profile &amp; password</h2><form class=\"ac-form\" onsubmit=\"return AdminAccountJs.save()\" novalidate><label class=\"ac-field\"><span>Name</span><input type=\"text\" id=\"ac-aname\" maxlength=\"60\" value=\"" + Ui.Attr(u.Name) + "\"></label><label class=\"ac-field\"><span>Current password</span><input type=\"password\" id=\"ac-acur\" maxlength=\"100\" autocomplete=\"current-password\"></label><label class=\"ac-field\"><span>New password (optional)</span><input type=\"password\" id=\"ac-anew\" maxlength=\"100\" autocomplete=\"new-password\"></label><em class=\"ac-err\" id=\"ac-e-acc\"></em><div><button type=\"submit\" class=\"ac-btn ac-btn-accent\">Save</button></div></form></section><section class=\"ac-panel\"><h2>What you can do</h2>");
            if (u.Admin)
            {
                sb.Append("<p>You are a office manager: every area, plus users and settings.</p>");
            }
            else
            {
                foreach (Right a in Access.Rights)
                {
                    string lv = Access.Level(u, a.Key);
                    sb.Append("<div class=\"ac-row\"><div><b>" + Ui.Enc(a.Name) + "</b></div><span class=\"ac-st ac-st-" + (lv.Contains('E') ? "live\">Edit" + (lv.Contains('X') ? " + " + Ui.Enc(a.Extra.ToLowerInvariant()) : string.Empty) : lv.Contains('V') ? "view\">View" : "off\">None") + "</span></div>");
                }
            }
            sb.Append("</section></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
                return response;
            }
            string name = Regex.Replace(Ui.Clip(GetDataValue("aname"), 60), "\\s+", " ");
            string cur = GetDataValue("acur");
            string nw = GetDataValue("anew");
            if (name.Length < 2)
            {
                response.SetElementContents("ac-e-acc", "Enter your name.");
                return response;
            }
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == c.U.Id);
                if (u == null)
                {
                    return response;
                }
                if (nw != string.Empty)
                {
                    if (!Access.Verify(cur, u.Hash))
                    {
                        response.SetElementContents("ac-e-acc", "Your current password isn't right.");
                        return response;
                    }
                    if (nw.Length < 8 || nw.Length > 100)
                    {
                        response.SetElementContents("ac-e-acc", "Use 8 characters or more.");
                        return response;
                    }
                    u.Hash = Access.HashPassword(nw);
                }
                u.Name = name;
                await Store.SaveStaff(staff);
            }
            if (nw != string.Empty)
            {
                await Auth.CloseUser(c.U.Id, Store.CleanId(GetDataValue("tok")));
            }
            await Store.Log(c.U.Id, "account", nw != string.Empty ? "password changed" : "name", true);
            response.ExecuteScript("document.getElementById('ac-acur').value='';document.getElementById('ac-anew').value='';AdminAccountJs.toast('Saved');");
            return response;
        }
    }
}
