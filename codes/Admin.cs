using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class Admin : WebPage
    {
        private const string AreaKey = "";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Dashboard");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--db-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("db-menu", Auth.Menu("db", "Admin", c, site));
            response.SetElementContents("db-who", Auth.Who("db", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"db-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"db-panel\"><p>You don't have access to this area. Ask the office manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "view", AreaKey, false);
                response.SetElementContents("db-main", NoAccess("Dashboard"));
                return response;
            }
            response.SetElementContents("db-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"db-tr db-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Kpi(string label, string value, string sub, string href, bool hot = false)
        {
            string inner = "<span>" + label + "</span><b>" + value + "</b><small>" + sub + "</small>";
            return href != string.Empty ? "<a class=\"db-kpi" + (hot ? " db-hot" : string.Empty) + "\" href=\"" + href + "\">" + inner + "</a>" : "<div class=\"db-kpi\">" + inner + "</div>";
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            string own = Auth.Mine(c);
            DateTime now = Store.Now(site);
            DateTime today = now.Date;
            bool canL = Access.Can(c.U, "listings", 'V');
            bool canLead = Access.Can(c.U, "leads", 'V');
            bool canV = Access.Can(c.U, "viewings", 'V');
            List<Property> mine = site.Listings.Where(l => own == string.Empty || l.Agent == own).ToList();
            List<Lead> leads = site.Leads.Where(x => own == string.Empty || x.Agent == own || x.Agent == string.Empty).ToList();
            List<Viewing> views = site.Viewings.Where(v => own == string.Empty || v.Agent == own).ToList();
            string hello = now.Hour < 12 ? "Good morning" : now.Hour < 18 ? "Good afternoon" : "Good evening";
            StringBuilder sb = new StringBuilder("<div class=\"db-ptop\"><div><h1>" + hello + ", " + Ui.Enc(c.U.Name.Split(' ')[0]) + "</h1><p>" + Ui.LongDay(today) + (own != string.Empty ? " &middot; showing your own listings and clients" : string.Empty) + "</p></div><div class=\"db-acts\">" + (Access.Can(c.U, "listings", 'E') ? "<a class=\"db-btn db-btn-accent\" href=\"AdminListings?id=new\">Add a listing</a>" : string.Empty) + "<a class=\"db-btn db-btn-ghost\" href=\"Home\" target=\"_blank\" rel=\"noopener\">View site</a></div></div><div class=\"db-kpis\">");
            if (canL)
            {
                sb.Append(Kpi("For sale", mine.Count(l => l.Deal == "sale" && l.Status == "active").ToString(Ui.Inv), Ui.Plural(mine.Count(l => l.Status == "pending"), "under offer", "under offer"), "AdminListings"));
                sb.Append(Kpi("For rent", mine.Count(l => l.Deal == "rent" && l.Status == "active").ToString(Ui.Inv), Ui.Plural(mine.Count(l => l.Status == "draft"), "draft", "drafts"), "AdminListings"));
            }
            if (canLead)
            {
                int fresh = leads.Count(x => x.Status == "new");
                sb.Append(Kpi("New leads", fresh.ToString(Ui.Inv), Ui.Plural(leads.Count(x => x.Created >= DateTime.UtcNow.AddDays(-7)), "this week", "this week"), "AdminLeads", fresh > 0));
            }
            if (canV)
            {
                int vt = views.Count(v => v.Status == "booked" && v.Start.Date == today);
                sb.Append(Kpi("Viewings today", vt.ToString(Ui.Inv), Ui.Plural(views.Count(v => v.Status == "booked" && v.Start.Date > today && v.Start.Date <= today.AddDays(7)), "next 7 days", "next 7 days"), "AdminViewings", vt > 0));
            }
            if (canL)
            {
                List<Property> live = mine.Where(Ui.Live).ToList();
                sb.Append(Kpi("Page views", Ui.Num(live.Sum(l => l.Views)), "on live listings", string.Empty));
                sb.Append(Kpi("Saves", Ui.Num(live.Sum(l => l.Saves)), "hearts on live listings", string.Empty));
            }
            sb.Append("</div><div class=\"db-grid2\" style=\"margin-top:0\">");
            if (canV)
            {
                List<Viewing> td = views.Where(v => v.Start.Date == today && v.Status != "cancelled").OrderBy(v => v.Start).ToList();
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Today's viewings</h2><a href=\"AdminViewings\">All viewings</a></div>");
                if (td.Count == 0)
                {
                    sb.Append("<p class=\"db-none\">No viewings today.</p>");
                }
                foreach (Viewing v in td)
                {
                    Property? l = Ui.ListingOf(site, v.Listing);
                    sb.Append("<div class=\"db-row\"><span class=\"db-vt\">" + Ui.Hm(site, v.Start) + "</span><div><b>" + Ui.Enc(l?.Street ?? v.Listing) + "</b><small>" + Ui.Enc(v.Name) + " &middot; " + Ui.Enc(Ui.AgentName(site, v.Agent)) + "</small></div>" + Ui.Pill("db", Ui.ViewingStatuses, v.Status) + "</div>");
                }
                sb.Append("</section>");
            }
            if (canLead)
            {
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Newest leads</h2><a href=\"AdminLeads\">All leads</a></div>");
                List<Lead> nl = leads.OrderByDescending(x => x.Created).Take(6).ToList();
                if (nl.Count == 0)
                {
                    sb.Append("<p class=\"db-none\">No leads yet. Viewing requests, questions, valuations and messages from the website arrive here.</p>");
                }
                foreach (Lead x in nl)
                {
                    Property? l = Ui.ListingOf(site, x.Listing);
                    sb.Append("<a class=\"db-row\" href=\"AdminLeads?id=" + Uri.EscapeDataString(x.Id) + "\"><div><b>" + Ui.Enc(x.Name) + "</b><small>" + Ui.Enc(Ui.NameOf(Ui.LeadKinds, x.Kind)) + (l != null ? " &middot; " + Ui.Enc(l.Street) : x.Address != string.Empty ? " &middot; " + Ui.Enc(x.Address) : string.Empty) + " &middot; " + Ui.Time(x.Created) + "</small></div>" + Ui.Pill("db", Ui.LeadStatuses, x.Status) + "</a>");
                }
                sb.Append("</section>");
            }
            if (canL)
            {
                List<(string Text, int N, string Href)> todo = new List<(string, int, string)>
                {
                    ("Drafts waiting to be published", mine.Count(l => l.Status == "draft"), "AdminListings?status=draft"),
                    ("Listings with fewer than 3 photos", mine.Count(l => Ui.Live(l) && l.Photos.Count < 3), "AdminListings"),
                    ("Under offer for more than 30 days", mine.Count(l => l.Status == "pending" && l.History.Count > 0 && l.History[^1].When < today.AddDays(-30)), "AdminListings?status=pending"),
                    ("Rentals with a past available date", mine.Count(l => l.Deal == "rent" && l.Status == "active" && l.Available != null && l.Available.Value.Date < today.AddDays(-30)), "AdminListings?status=active"),
                    ("Open houses in the next 7 days", mine.Count(l => l.Status == "active" && l.Opens.Any(o => o.Date.Date >= today && o.Date.Date <= today.AddDays(7))), "AdminListings")
                };
                sb.Append("<section class=\"db-panel\"><h2>Needs attention</h2><div class=\"db-todo\">");
                int shown = 0;
                foreach ((string t, int n, string h) in todo.Where(x => x.N > 0))
                {
                    sb.Append("<a href=\"" + h + "\"><span>" + t + "</span><b>" + n + "</b></a>");
                    shown++;
                }
                if (shown == 0)
                {
                    sb.Append("<p class=\"db-none\">Nothing waiting. Nice work.</p>");
                }
                sb.Append("</div></section>");
                sb.Append("<section class=\"db-panel\"><div class=\"db-ph\"><h2>Most viewed</h2><a href=\"AdminListings\">All listings</a></div>");
                foreach (Property l in mine.Where(Ui.Live).OrderByDescending(l => l.Views).Take(5))
                {
                    sb.Append("<a class=\"db-row\" href=\"AdminListings?id=" + Uri.EscapeDataString(l.Id) + "\"><img class=\"db-th64\" src=\"" + Ui.Attr(Ui.Thumb(l.Photos.FirstOrDefault() ?? string.Empty)) + "\" alt=\"\" loading=\"lazy\"><div><b>" + Ui.Enc(l.Street) + "</b><small>" + Ui.PricePlain(l) + " &middot; " + Ui.Enc(Ui.AreaName(site, l.Area)) + "</small></div><span class=\"db-rank\"><b>" + Ui.Num(l.Views) + "</b> views<br><b>" + Ui.Num(l.Saves) + "</b> saves</span></a>");
                }
                sb.Append("</section>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }
    }
}
