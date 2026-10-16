using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstate.Core;
using RealEstate.Models;
using SkyNet;

namespace RealEstate.codes
{
    public class AdminListings : WebPage
    {
        private const string AreaKey = "listings";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Listings");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--li-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("li-menu", Auth.Menu("li", "AdminListings", c, site));
            response.SetElementContents("li-who", Auth.Who("li", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminListingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"li-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"li-panel\"><p>You don't have access to this area. Ask the office manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminListingsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminListingsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite();
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "view", AreaKey, false);
                response.SetElementContents("li-main", NoAccess("Listings"));
                return response;
            }
            response.SetElementContents("li-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminListingsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"li-tr li-th\">");
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
            sb.Append("<div class=\"" + p + "-pins\"></div></div><div class=\"" + p + "-mctl\"><button type=\"button\" aria-label=\"Zoom in\" onclick=\"AdminListingsJs.zoom('" + id + "',1.4)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Zoom out\" onclick=\"AdminListingsJs.zoom('" + id + "',0.714)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M5 12h14\"/></svg></button><button type=\"button\" aria-label=\"Show all\" onclick=\"AdminListingsJs.fit('" + id + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5\"/></svg></button></div>" + extra + "</div>");
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

        private const int Size = 20;

        private static readonly (string Key, string Name)[] SortOpts = { ("new", "Newest first"), ("old", "Oldest first"), ("phigh", "Price, high to low"), ("plow", "Price, low to high"), ("views", "Most viewed") };

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            string own = Auth.Mine(c);
            string st = Ui.ListingStatuses.Any(s => s.Key == arg) ? arg : string.Empty;
            StringBuilder sb = new StringBuilder("<div class=\"li-ptop\"><div><h1>Listings</h1><p>" + (own != string.Empty ? "Your own listings. " : string.Empty) + "Homes for sale and rent. Drafts stay hidden until someone with the Publish right puts them live.</p></div><div class=\"li-acts\">" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"li-btn li-btn-accent\" onclick=\"AdminListingsJs.edit('')\">Add a listing</button>" : string.Empty) + "</div></div>");
            sb.Append("<div class=\"li-bar\"><label>Search<input type=\"search\" id=\"li-q\" list=\"li-qlist\" placeholder=\"Street, title or ID\" maxlength=\"60\" oninput=\"AdminListingsJs.search()\"></label>" + Ui.DataList("li-qlist", site.Listings.Where(l => own == string.Empty || l.Agent == own).Select(l => l.Street)));
            sb.Append("<label>Deal<select id=\"li-fdeal\" onchange=\"AdminListingsJs.list(1)\">" + Ui.Opts(new[] { ("", "Sale and rent"), ("sale", "For sale"), ("rent", "For rent") }, string.Empty) + "</select></label>");
            sb.Append("<label>Status<select id=\"li-fstat\" onchange=\"AdminListingsJs.list(1)\"><option value=\"\">Any status</option>" + Ui.Opts(Ui.ListingStatuses.Select(s => (s.Key, s.Name)), st) + "</select></label>");
            sb.Append("<label>Neighborhood<select id=\"li-farea\" onchange=\"AdminListingsJs.list(1)\"><option value=\"\">All</option>" + Ui.Opts(site.Areas.OrderBy(a => a.Order).Select(a => (a.Key, a.Name)), string.Empty) + "</select></label>");
            if (own == string.Empty)
            {
                sb.Append("<label>" + Ui.Enc(site.Settings.AgentWord) + "<select id=\"li-fagent\" onchange=\"AdminListingsJs.list(1)\"><option value=\"\">Everyone</option>" + Ui.Opts(site.Agents.OrderBy(a => a.Order).Select(a => (a.Id, a.Name)), string.Empty) + "</select></label>");
            }
            sb.Append("<label>Sort<select id=\"li-fsort\" onchange=\"AdminListingsJs.list(1)\">" + Ui.Opts(SortOpts.Select(s => (s.Key, s.Name)), "new") + "</select></label></div><div class=\"li-tbl li-litbl\" id=\"li-list\"></div><div id=\"li-pager\"></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminListingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            string own = Auth.Mine(c);
            string term = Ui.Clip(GetDataValue("lq"), 60);
            string deal = GetDataValue("ldeal");
            string st = GetDataValue("lstat");
            string area = GetDataValue("larea");
            string agent = own != string.Empty ? own : GetDataValue("lagent");
            IEnumerable<Property> q = site.Listings.Where(l => (agent == string.Empty || l.Agent == agent) && (deal == string.Empty || l.Deal == deal) && (st == string.Empty || l.Status == st) && (area == string.Empty || l.Area == area));
            if (term != string.Empty)
            {
                q = q.Where(l => (l.Street + " " + l.Title + " " + l.Id).Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            q = GetDataValue("lsort") switch
            {
                "old" => q.OrderBy(l => l.Listed),
                "phigh" => q.OrderByDescending(l => l.Price),
                "plow" => q.OrderBy(l => l.Price),
                "views" => q.OrderByDescending(l => l.Views),
                _ => q.OrderByDescending(l => l.Status == "draft").ThenByDescending(l => l.Listed)
            };
            List<Property> all = q.ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { (string.Empty, string.Empty), ("Home", string.Empty), ("Price", "li-num"), ("Status", string.Empty), (Ui.Enc(site.Settings.AgentWord), string.Empty), ("Views", "li-num"), ("Listed", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"li-tr\"><span class=\"li-none li-wide\">No listings match.</span></div>");
            }
            foreach (Property l in all.Skip((page - 1) * Size).Take(Size))
            {
                string img = l.Photos.Count > 0 ? "<img class=\"li-thumb\" src=\"" + Ui.Attr(Ui.Thumb(l.Photos[0])) + "\" alt=\"\" loading=\"lazy\">" : "<span class=\"li-hint\">No photo</span>";
                sb.Append("<div class=\"li-tr\"><span>" + img + "</span><span class=\"li-wide\"><b>" + Ui.Enc(l.Street) + "</b><small>" + Ui.Enc(l.Id) + " &middot; " + Ui.Enc(Ui.AreaName(site, l.Area)) + " &middot; " + Ui.Enc(Ui.KindName(l.Kind)) + " &middot; " + (l.Beds == 0 ? "Studio" : l.Beds + " bd") + "</small></span><span data-l=\"Price\" class=\"li-num\"><b>" + Ui.PricePlain(l) + "</b></span><span data-l=\"Status\">" + Ui.StatusPill("li", l) + (l.Featured ? "<small>Featured</small>" : string.Empty) + "</span><span data-l=\"" + Ui.Attr(site.Settings.AgentWord) + "\">" + Ui.Enc(Ui.AgentName(site, l.Agent)) + "</span><span data-l=\"Views\" class=\"li-num\">" + Ui.Num(l.Views) + "<small>" + Ui.Num(l.Saves) + " saves</small></span><span data-l=\"Listed\">" + (l.Status == "draft" ? "&ndash;" : l.Listed.ToString("MMM d", Ui.Inv)) + "</span><span><button type=\"button\" class=\"li-btn li-btn-sm li-btn-ghost\" onclick=\"AdminListingsJs.edit('" + Ui.Attr(l.Id) + "')\">" + (Access.Can(c.U, AreaKey, 'E') ? "Edit" : "View") + "</button></span></div>");
            }
            response.SetElementContents("li-list", sb.ToString());
            response.SetElementContents("li-pager", Ui.Pager("li", page, Size, all.Count, "listing", "listings", "AdminListingsJs.list"));
            return response;
        }

        private static string F(string id, string label, string value, int max, string type = "text", string extra = "")
        {
            return "<label class=\"li-field\"><span>" + label + "</span><input type=\"" + type + "\" id=\"li-" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Attr(value) + "\"" + extra + "></label>";
        }

        private static string OpenRow(string date, string start, string end)
        {
            return "<div class=\"li-ohrow\"><input type=\"date\" value=\"" + Ui.Attr(date) + "\" aria-label=\"Date\"><input type=\"time\" step=\"900\" value=\"" + Ui.Attr(start) + "\" aria-label=\"Starts\"><input type=\"time\" step=\"900\" value=\"" + Ui.Attr(end) + "\" aria-label=\"Ends\"><button type=\"button\" class=\"li-ibtn\" aria-label=\"Remove\" onclick=\"AdminListingsJs.dropRow(this)\">&times;</button></div>";
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminListingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "listing", GetDataValue("lid"), "You don't have access to listings.");
            }
            SiteData site = await Store.LoadSite();
            string own = Auth.Mine(c);
            Property? found = site.Listings.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("lid"), 20));
            if (found != null && own != string.Empty && found.Agent != own)
            {
                return await Refuse(response, c, "listing", found.Id, "That listing belongs to someone else.");
            }
            if (found == null && !Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "listing", "new", "You can look but not add listings.");
            }
            bool canE = Access.Can(c.U, AreaKey, 'E');
            bool canX = Access.Can(c.U, AreaKey, 'X');
            Models.Area first = site.Areas.OrderBy(a => a.Order).FirstOrDefault() ?? new Models.Area { X = 500, Y = 350 };
            Property l = found ?? new Property { Area = first.Key, X = first.X, Y = first.Y, Agent = own, Status = "draft", Kind = "house", Style = "Craftsman", Beds = 3, Baths = 2 };
            string ro = canE ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<h2>" + (found == null ? "Add a listing" : Ui.Enc(l.Street)) + "</h2><div id=\"li-ldata\" data-id=\"" + Ui.Attr(found?.Id ?? string.Empty) + "\" data-photos=\"" + Ui.Attr(JsonSerializer.Serialize(l.Photos)) + "\" hidden></div>");
            sb.Append("<div class=\"li-tabs\" role=\"tablist\"><button type=\"button\" class=\"li-tab li-act\" data-p=\"det\" onclick=\"AdminListingsJs.pane('det')\">Details</button><button type=\"button\" class=\"li-tab\" data-p=\"txt\" onclick=\"AdminListingsJs.pane('txt')\">Description</button><button type=\"button\" class=\"li-tab\" data-p=\"pho\" onclick=\"AdminListingsJs.pane('pho')\">Photos<em id=\"li-pcount\">" + l.Photos.Count + "</em></button><button type=\"button\" class=\"li-tab\" data-p=\"map\" onclick=\"AdminListingsJs.pane('map')\">Map pin</button><button type=\"button\" class=\"li-tab\" data-p=\"opn\" onclick=\"AdminListingsJs.pane('opn')\">Open houses</button>" + (l.History.Count > 0 ? "<button type=\"button\" class=\"li-tab\" data-p=\"his\" onclick=\"AdminListingsJs.pane('his')\">History</button>" : string.Empty) + "</div>");
            sb.Append("<fieldset class=\"li-form li-lfs\" id=\"li-lform\" style=\"border:0;padding:0;margin:0;min-width:0\"" + ro + "><div class=\"li-panes\">");
            sb.Append("<div data-p=\"det\" class=\"li-act\"><div class=\"li-f2\">" + F("ltitle", "Headline", l.Title, 80) + F("lstreet", "Street address", l.Street, 80) + "</div><div class=\"li-f4\"><label class=\"li-field\"><span>Neighborhood</span><select id=\"li-larea\">" + Ui.Opts(site.Areas.OrderBy(a => a.Order).Select(a => (a.Key, a.Name)), l.Area) + "</select></label><label class=\"li-field\"><span>Deal</span><select id=\"li-ldeal\" onchange=\"AdminListingsJs.dealView()\">" + Ui.Opts(new[] { ("sale", "For sale"), ("rent", "For rent") }, l.Deal) + "</select></label><label class=\"li-field\"><span>Home type</span><select id=\"li-lkind\">" + Ui.Opts(Ui.Kinds.Select(k => (k.Key, k.Name)), l.Kind) + "</select></label>" + F("lstyle", "Style", l.Style, 30, "text", " list=\"li-styles\"") + Ui.DataList("li-styles", site.Listings.Select(x => x.Style)) + "</div>");
            sb.Append("<div class=\"li-f4\">" + F("lprice", "Price ($, monthly rent for rentals)", l.Price > 0 ? l.Price.ToString("0", Ui.Inv) : string.Empty, 10, "text", " inputmode=\"numeric\"") + F("lbeds", "Bedrooms (0 = studio)", l.Beds.ToString(Ui.Inv), 2, "number", " min=\"0\" max=\"20\"") + F("lbaths", "Bathrooms", Ui.Baths(l.Baths), 4, "number", " min=\"0\" max=\"20\" step=\"0.5\"") + F("lsqft", "Interior (sq ft)", l.Sqft > 0 ? l.Sqft.ToString(Ui.Inv) : string.Empty, 6, "text", " inputmode=\"numeric\"") + "</div>");
            sb.Append("<div class=\"li-f4\">" + F("llot", "Lot (sq ft)", l.Lot > 0 ? l.Lot.ToString(Ui.Inv) : string.Empty, 7, "text", " inputmode=\"numeric\"") + F("lyear", "Year built", l.Year > 0 ? l.Year.ToString(Ui.Inv) : string.Empty, 4, "text", " inputmode=\"numeric\"") + F("lpark", "Parking spaces", l.Parking.ToString(Ui.Inv), 2, "number", " min=\"0\" max=\"20\"") + F("lhoa", "HOA ($ a month)", l.Hoa > 0 ? l.Hoa.ToString("0", Ui.Inv) : string.Empty, 6, "text", " inputmode=\"numeric\"") + "</div>");
            sb.Append("<div class=\"li-f4\"><span class=\"li-saleonly\">" + F("ltax", "Property tax ($ a year)", l.Tax > 0 ? l.Tax.ToString("0", Ui.Inv) : string.Empty, 7, "text", " inputmode=\"numeric\"") + "</span><span class=\"li-rentonly\">" + F("lavail", "Available from", l.Available != null ? Ui.Ymd(l.Available.Value) : string.Empty, 10, "date") + "</span>");
            if (own != string.Empty)
            {
                sb.Append("<label class=\"li-field\"><span>" + Ui.Enc(site.Settings.AgentWord) + "</span><select id=\"li-lagent\" disabled>" + Ui.Opts(site.Agents.Where(a => a.Id == own).Select(a => (a.Id, a.Name)), own) + "</select></label>");
            }
            else
            {
                sb.Append("<label class=\"li-field\"><span>" + Ui.Enc(site.Settings.AgentWord) + "</span><select id=\"li-lagent\"><option value=\"\">Unassigned</option>" + Ui.Opts(site.Agents.Where(a => a.Active || a.Id == l.Agent).OrderBy(a => a.Order).Select(a => (a.Id, a.Name)), l.Agent) + "</select></label>");
            }
            sb.Append("<label class=\"li-field\"><span>Status</span><select id=\"li-lstatus\"" + (canX ? string.Empty : " disabled") + ">" + Ui.Opts(Ui.ListingStatuses.Select(s => (s.Key, s.Key == "active" ? "Live" : s.Name)), l.Status) + "</select></label>" + F("lclosed", "Sold or rented price", string.Empty, 10, "text", " inputmode=\"numeric\" placeholder=\"Only when closing\"") + "</div>");
            sb.Append("<label class=\"li-chk\"><input type=\"checkbox\" id=\"li-lfeatured\"" + (l.Featured ? " checked" : string.Empty) + (canX ? string.Empty : " disabled") + "> Featured on the home page</label>" + (canX ? string.Empty : "<p class=\"li-lock\">Status and Featured need the Publish right. New listings start as drafts.</p>") + "</div>");
            sb.Append("<div data-p=\"txt\"><label class=\"li-field\"><span>Description (leave an empty line between paragraphs)</span><textarea id=\"li-ldesc\" rows=\"12\" maxlength=\"4000\">" + Ui.Enc(l.Description) + "</textarea></label><label class=\"li-field\"><span>Features (one per line, shown as ticks)</span><textarea id=\"li-lfeat\" rows=\"6\" maxlength=\"800\">" + Ui.Enc(string.Join("\n", l.Features)) + "</textarea></label><p class=\"li-hint\">Must-have filters on the website look for Garage, Garden, Pool, Waterfront, Fireplace, Balcony, In-unit laundry and Home office.</p></div>");
            sb.Append("<div data-p=\"pho\"><p class=\"li-hint\">The first photo is the cover on cards and the map. Use the arrows to change the order.</p><div class=\"li-photos\" id=\"li-photos\"></div>" + (canE ? "<h3>Upload a photo</h3><div class=\"li-upl\"><input type=\"file\" id=\"li-upbtn\" accept=\"image/jpeg,image/png,image/webp\" onchange=\"AdminListingsJs.upload(this)\"><span class=\"li-hint\" id=\"li-upst\"></span></div><em class=\"li-err\" id=\"li-e-upl\" role=\"alert\"></em><p class=\"li-hint\">Large photos are resized to 1600 pixels in your browser before upload.</p><h3>Or pick from the library</h3><div class=\"li-lib\" id=\"li-lib\">" : string.Empty));
            if (canE)
            {
                foreach (string src in Ui.Images("uploads/full").Concat(Ui.Images("homes/full")).Where(s => !s.Contains("hero-street", StringComparison.Ordinal)))
                {
                    sb.Append("<button type=\"button\" onclick=\"AdminListingsJs.pick('" + Ui.Js(src) + "')\" aria-label=\"Add this photo\"><img src=\"" + Ui.Attr(Ui.Thumb(src)) + "\" alt=\"\" loading=\"lazy\"></button>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div>");
            sb.Append("<div data-p=\"map\"><p class=\"li-hint\">Click the map where the home is. Drag to move, scroll to zoom. Position: <b id=\"li-lxy\">" + l.X.ToString("0.#", Ui.Inv) + ", " + l.Y.ToString("0.#", Ui.Inv) + "</b></p><input type=\"hidden\" id=\"li-lx\" value=\"" + l.X.ToString("0.#", Ui.Inv) + "\"><input type=\"hidden\" id=\"li-ly\" value=\"" + l.Y.ToString("0.#", Ui.Inv) + "\">" + MapHtml("li", "li-pm", "li-pmap", site.Areas.Where(a => a.Active), "[]", string.Empty) + "</div>");
            sb.Append("<div data-p=\"opn\"><p class=\"li-hint\">Open houses show on the listing and as a badge on cards. Past dates drop off by themselves.</p><div id=\"li-opens\" style=\"display:grid;gap:8px\">");
            DateTime today = Store.Today(site);
            foreach (OpenHouse o in l.Opens.Where(o => o.Date.Date >= today).OrderBy(o => o.Date))
            {
                sb.Append(OpenRow(Ui.Ymd(o.Date), o.Start, o.End));
            }
            sb.Append("</div><template id=\"li-ohtpl\">" + OpenRow(string.Empty, "13:00", "15:00") + "</template><div><button type=\"button\" class=\"li-btn li-btn-sm li-btn-ghost\" onclick=\"AdminListingsJs.addOpen()\">Add an open house</button></div></div>");
            if (l.History.Count > 0)
            {
                sb.Append("<div data-p=\"his\"><div class=\"li-hist\">");
                foreach (PriceEvent h in l.History.OrderByDescending(h => h.When))
                {
                    sb.Append("<div><span>" + h.When.ToString("MMM d, yyyy", Ui.Inv) + " &middot; " + Ui.Enc(h.Event) + "</span><b>" + Ui.Money(h.Price) + "</b></div>");
                }
                sb.Append("</div><p class=\"li-hint\">Listed " + l.Listed.ToString("MMM d, yyyy", Ui.Inv) + " &middot; " + Ui.Num(l.Views) + " views &middot; " + Ui.Num(l.Saves) + " saves. Price and status changes are added here when you save.</p></div>");
            }
            sb.Append("</div></fieldset><em class=\"li-err\" id=\"li-e-save\" role=\"alert\"></em><div class=\"li-mfoot\">");
            if (found != null && canX)
            {
                sb.Append("<button type=\"button\" class=\"li-btn li-btn-danger li-left\" onclick=\"AdminListingsJs.remove('" + Ui.Attr(found.Id) + "','" + Ui.Js(found.Street) + "')\">Delete</button>");
            }
            if (found != null && Ui.Public(found))
            {
                sb.Append("<a class=\"li-btn li-btn-ghost\" href=\"Listing?id=" + Uri.EscapeDataString(found.Id) + "\" target=\"_blank\" rel=\"noopener\">Open on the website</a>");
            }
            sb.Append("<button type=\"button\" class=\"li-btn li-btn-ghost\" onclick=\"AdminListingsJs.closeModal()\">" + (canE ? "Cancel" : "Close") + "</button>" + (canE ? "<button type=\"button\" class=\"li-btn li-btn-accent\" onclick=\"AdminListingsJs.save()\">" + (found == null ? "Add listing" : "Save") + "</button>" : string.Empty) + "</div>");
            response.SetElementContents("li-mbody", sb.ToString());
            response.ExecuteScript("AdminListingsJs.opened();");
            return response;
        }

        private static decimal Money(string v)
        {
            string s = Regex.Replace(v ?? string.Empty, "[$,\\s]", string.Empty);
            return decimal.TryParse(s, NumberStyles.Number, Ui.Inv, out decimal d) ? Math.Round(Math.Clamp(d, 0, 100000000m), 0) : 0;
        }

        private static int Whole(string v, int max)
        {
            return int.TryParse(Regex.Replace(v ?? string.Empty, "[,\\s]", string.Empty), NumberStyles.Integer, Ui.Inv, out int n) ? Math.Clamp(n, 0, max) : 0;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminListingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "listing", GetDataValue("lid"), "You can look but not change listings.");
            }
            string id = Ui.Clip(GetDataValue("lid"), 20);
            string own = Auth.Mine(c);
            bool canX = Access.Can(c.U, AreaKey, 'X');
            string title = Regex.Replace(Ui.Clip(GetDataValue("ltitle"), 80), "\\s+", " ").Trim();
            string street = Regex.Replace(Ui.Clip(GetDataValue("lstreet"), 80), "\\s+", " ").Trim();
            string deal = GetDataValue("ldeal") == "rent" ? "rent" : "sale";
            decimal price = Money(GetDataValue("lprice"));
            decimal baths = decimal.TryParse(GetDataValue("lbaths"), NumberStyles.Number, Ui.Inv, out decimal bv) ? Math.Clamp(Math.Round(bv * 2) / 2, 0, 20) : 0;
            int sqft = Whole(GetDataValue("lsqft"), 100000);
            int year = Whole(GetDataValue("lyear"), 2100);
            string err = string.Empty;
            string tab = "det";
            if (street.Length < 4)
            {
                err = "Enter the street address.";
            }
            else if (title.Length < 4)
            {
                err = "Write a short headline, for example “Bright condo near the river path”.";
            }
            else if (price <= 0)
            {
                err = deal == "rent" ? "Enter the monthly rent." : "Enter the asking price.";
            }
            else if (sqft <= 0)
            {
                err = "Enter the interior size in square feet.";
            }
            else if (year != 0 && (year < 1700 || year > DateTime.UtcNow.Year + 3))
            {
                err = "Check the year built.";
            }
            List<string> photos = GetDataValue("lphotos").Split('|').Where(p => p != string.Empty && Ui.SafeImage(p)).Distinct().Take(24).ToList();
            double x = double.TryParse(GetDataValue("lx"), NumberStyles.Float, Ui.Inv, out double xv) ? Math.Clamp(xv, 0, 1000) : 500;
            double y = double.TryParse(GetDataValue("ly"), NumberStyles.Float, Ui.Inv, out double yv) ? Math.Clamp(yv, 0, 700) : 350;
            List<OpenHouse> opens = new List<OpenHouse>();
            foreach (string row in GetDataValue("lopens").Split('~'))
            {
                string[] p = row.Split('|');
                DateTime? d = p.Length == 3 ? Ui.ParseDay(p[0]) : null;
                TimeSpan? a = p.Length == 3 ? Store.Hm(p[1]) : null;
                TimeSpan? b = p.Length == 3 ? Store.Hm(p[2]) : null;
                if (d == null)
                {
                    continue;
                }
                if (a == null || b == null || b <= a)
                {
                    err = err == string.Empty ? "Each open house needs a start and an end time after it." : err;
                    tab = err.StartsWith("Each open", StringComparison.Ordinal) ? "opn" : tab;
                    continue;
                }
                opens.Add(new OpenHouse { Date = d.Value, Start = p[1], End = p[2] });
            }
            if (err != string.Empty)
            {
                response.ExecuteScript("AdminListingsJs.fail('" + Ui.Js(err) + "','" + tab + "');");
                return response;
            }
            string savedId = id;
            bool isNew = false;
            string note = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Property? l = site.Listings.FirstOrDefault(v => v.Id == id);
                if (l != null && own != string.Empty && l.Agent != own)
                {
                    return await Refuse(response, c, "listing", id, "That listing belongs to someone else.");
                }
                DateTime now = Store.Now(site);
                if (l == null)
                {
                    isNew = true;
                    l = new Property { Id = Store.NextId(site.Listings.Select(v => v.Id), "L-", 1040), Status = "draft", Listed = now };
                    site.Listings.Add(l);
                    savedId = l.Id;
                }
                string oldStatus = l.Status;
                decimal oldPrice = l.Price;
                l.Title = title;
                l.Street = street;
                l.Area = site.Areas.Any(a => a.Key == GetDataValue("larea")) ? GetDataValue("larea") : l.Area;
                l.Deal = deal;
                l.Kind = Ui.Kinds.Any(k => k.Key == GetDataValue("lkind")) ? GetDataValue("lkind") : l.Kind;
                l.Style = Ui.Clip(GetDataValue("lstyle"), 30).Trim();
                l.Price = price;
                l.Beds = Whole(GetDataValue("lbeds"), 20);
                l.Baths = baths;
                l.Sqft = sqft;
                l.Lot = Whole(GetDataValue("llot"), 10000000);
                l.Year = year;
                l.Parking = Whole(GetDataValue("lpark"), 20);
                l.Hoa = Money(GetDataValue("lhoa"));
                l.Tax = deal == "sale" ? Money(GetDataValue("ltax")) : 0;
                l.Available = deal == "rent" ? Ui.ParseDay(GetDataValue("lavail")) : null;
                l.Agent = own != string.Empty ? own : site.Agents.Any(a => a.Id == GetDataValue("lagent")) ? GetDataValue("lagent") : string.Empty;
                l.Description = Regex.Replace(Ui.Clip(GetDataValue("ldesc"), 4000).Replace("\r", string.Empty), "\n{3,}", "\n\n").Trim();
                l.Features = Ui.SplitLines(GetDataValue("lfeat"), 20, 40).Distinct().ToList();
                l.Photos = photos;
                l.X = Math.Round(x, 1);
                l.Y = Math.Round(y, 1);
                l.Opens = opens.OrderBy(o => o.Date).ToList();
                string want = Ui.ListingStatuses.Any(s => s.Key == GetDataValue("lstatus")) ? GetDataValue("lstatus") : oldStatus;
                if (want != oldStatus && !canX)
                {
                    note = " Status needs the Publish right, so it stays " + Ui.NameOf(Ui.ListingStatuses, oldStatus).ToLowerInvariant() + ".";
                    want = oldStatus;
                }
                if (want is "sold" && deal == "rent")
                {
                    want = "rented";
                }
                if (want is "rented" && deal == "sale")
                {
                    want = "sold";
                }
                if (canX)
                {
                    l.Featured = GetDataValue("lfeatured") == "1";
                }
                if (want == "active" && oldStatus == "draft")
                {
                    l.Listed = now;
                    l.History.Add(new PriceEvent { When = now, Event = "Listed", Price = price });
                }
                else if (want != oldStatus)
                {
                    decimal closed = Money(GetDataValue("lclosed"));
                    string ev = want switch { "pending" => "Under offer", "sold" => "Sold", "rented" => "Rented", "active" => "Back on market", "off" => "Taken off market", _ => string.Empty };
                    if (ev != string.Empty && want != "draft")
                    {
                        l.History.Add(new PriceEvent { When = now, Event = ev, Price = want is "sold" or "rented" && closed > 0 ? closed : price });
                    }
                }
                if (!isNew && oldStatus != "draft" && want == oldStatus && price != oldPrice && Ui.Live(l))
                {
                    l.History.Add(new PriceEvent { When = now, Event = price < oldPrice ? "Price cut" : "Price change", Price = price });
                }
                l.Status = want;
                if (l.Status is not ("active" or "pending"))
                {
                    l.Featured = l.Featured && l.Status == "active";
                }
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, isNew ? "listing added" : "listing", savedId + " · " + street, true);
            response.ExecuteScript("AdminListingsJs.saved('" + Ui.Js(savedId) + "'," + (isNew ? "true" : "false") + ");" + (note != string.Empty ? "AdminListingsJs.toast('" + Ui.Js(note.Trim()) + "');" : string.Empty));
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminListingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "delete listing", GetDataValue("lid"), "Deleting needs the Publish right.");
            }
            string id = Ui.Clip(GetDataValue("lid"), 20);
            string own = Auth.Mine(c);
            string street = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Property? l = site.Listings.FirstOrDefault(v => v.Id == id);
                if (l == null || (own != string.Empty && l.Agent != own))
                {
                    return await Refuse(response, c, "delete listing", id, "That listing can't be deleted.");
                }
                street = l.Street;
                site.Listings.Remove(l);
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "delete listing", id + " · " + street, true);
            response.ExecuteScript("AdminListingsJs.deleted();");
            return response;
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminListingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "upload", "photo", "You can look but not upload photos.");
            }
            string src = await SaveUpload(GetDataValue("full"), GetDataValue("thumb"));
            if (src == string.Empty)
            {
                response.ExecuteScript("AdminListingsJs.uploaded('','That file is not a JPG, PNG or WebP photo, or it is too large.');");
                return response;
            }
            await Store.Log(c.U.Id, "upload", src, true);
            response.ExecuteScript("AdminListingsJs.uploaded('" + Ui.Js(src) + "','');");
            return response;
        }
    }
}
