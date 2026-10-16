namespace RealEstate.Models
{
    public class SiteData
    {
        public DateTime Anchor { get; set; }
        public Settings Settings { get; set; } = new Settings();
        public List<Area> Areas { get; set; } = new List<Area>();
        public List<TeamMember> Agents { get; set; } = new List<TeamMember>();
        public List<Property> Listings { get; set; } = new List<Property>();
        public List<Lead> Leads { get; set; } = new List<Lead>();
        public List<Viewing> Viewings { get; set; } = new List<Viewing>();
    }

    public class Settings
    {
        public string Name { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Accent { get; set; } = "#B5562F";
        public string Theme { get; set; } = "light";
        public string License { get; set; } = string.Empty;
        public string AgentWord { get; set; } = "Agent";
        public string AgentsWord { get; set; } = "Agents";
        public string HeroTitle { get; set; } = string.Empty;
        public string HeroText { get; set; } = string.Empty;
        public string AboutTitle { get; set; } = string.Empty;
        public string AboutText { get; set; } = string.Empty;
        public string SellTitle { get; set; } = string.Empty;
        public string SellText { get; set; } = string.Empty;
        public decimal Rate { get; set; } = 6.5m;
        public int Term { get; set; } = 30;
        public decimal DownPct { get; set; } = 20m;
        public decimal TaxRate { get; set; } = 1.1m;
        public decimal Insurance { get; set; } = 1500m;
        public decimal Pmi { get; set; } = 0.5m;
        public string Disclaimer { get; set; } = string.Empty;
        public bool Clock24 { get; set; }
        public string TimeZone { get; set; } = "America/New_York";
        public List<OpenDay> Hours { get; set; } = new List<OpenDay>();
        public int ViewingMinutes { get; set; } = 30;
        public string ViewingStart { get; set; } = "09:00";
        public string ViewingEnd { get; set; } = "18:00";
    }

    public class OpenDay
    {
        public int Day { get; set; }
        public bool Open { get; set; }
        public string Start { get; set; } = "09:00";
        public string End { get; set; } = "18:00";
    }

    public class Area
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Color { get; set; } = "#B5562F";
        public double X { get; set; }
        public double Y { get; set; }
        public string Image { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool Active { get; set; } = true;
    }

    public class TeamMember
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public List<string> Areas { get; set; } = new List<string>();
        public List<string> Languages { get; set; } = new List<string>();
        public int Years { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public int Order { get; set; }
    }

    public class Property
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Kind { get; set; } = "house";
        public string Style { get; set; } = string.Empty;
        public string Deal { get; set; } = "sale";
        public decimal Price { get; set; }
        public int Beds { get; set; }
        public decimal Baths { get; set; }
        public int Sqft { get; set; }
        public int Lot { get; set; }
        public int Year { get; set; }
        public int Parking { get; set; }
        public decimal Hoa { get; set; }
        public decimal Tax { get; set; }
        public string Status { get; set; } = "draft";
        public bool Featured { get; set; }
        public List<string> Photos { get; set; } = new List<string>();
        public string Description { get; set; } = string.Empty;
        public List<string> Features { get; set; } = new List<string>();
        public double X { get; set; }
        public double Y { get; set; }
        public string Agent { get; set; } = string.Empty;
        public DateTime Listed { get; set; }
        public DateTime? Available { get; set; }
        public List<PriceEvent> History { get; set; } = new List<PriceEvent>();
        public List<OpenHouse> Opens { get; set; } = new List<OpenHouse>();
        public int Views { get; set; }
        public int Saves { get; set; }
    }

    public class PriceEvent
    {
        public DateTime When { get; set; }
        public string Event { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class OpenHouse
    {
        public DateTime Date { get; set; }
        public string Start { get; set; } = "13:00";
        public string End { get; set; } = "15:00";
    }

    public class Lead
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "question";
        public string Listing { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Prefer { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Status { get; set; } = "new";
        public string Agent { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public List<Note> Notes { get; set; } = new List<Note>();
    }

    public class Note
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class Viewing
    {
        public string Id { get; set; } = string.Empty;
        public string Lead { get; set; } = string.Empty;
        public string Listing { get; set; } = string.Empty;
        public string Agent { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public int Minutes { get; set; } = 30;
        public string Status { get; set; } = "booked";
        public string Note { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class Staff
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string AgentId { get; set; } = string.Empty;
        public bool Own { get; set; }
        public bool Admin { get; set; }
        public bool Active { get; set; } = true;
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class Activity
    {
        public DateTime When { get; set; }
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; } = true;
    }

    public class Session
    {
        public string Tok { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public DateTime Exp { get; set; }
    }

    public class Subscriber
    {
        public string Email { get; set; } = string.Empty;
        public DateTime When { get; set; }
    }
}
