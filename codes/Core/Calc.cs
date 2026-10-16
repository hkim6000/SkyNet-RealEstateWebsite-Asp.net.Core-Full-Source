using RealEstate.Models;

namespace RealEstate.Core
{
    public sealed class Payment
    {
        public decimal Price { get; set; }
        public decimal Down { get; set; }
        public decimal Loan { get; set; }
        public decimal Principal { get; set; }
        public decimal Tax { get; set; }
        public decimal Insurance { get; set; }
        public decimal Hoa { get; set; }
        public decimal Pmi { get; set; }
        public decimal Total => Principal + Tax + Insurance + Hoa + Pmi;
        public decimal Interest { get; set; }
        public int Months { get; set; }
    }

    public sealed class YearRow
    {
        public int Year { get; set; }
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public decimal Balance { get; set; }
    }

    public static class Calc
    {
        public static decimal Monthly(decimal loan, decimal ratePct, int years)
        {
            int n = years * 12;
            if (loan <= 0 || n <= 0)
            {
                return 0;
            }
            double r = (double)ratePct / 100.0 / 12.0;
            if (r <= 0)
            {
                return Math.Round(loan / n, 2);
            }
            double m = (double)loan * r / (1 - Math.Pow(1 + r, -n));
            return Math.Round((decimal)m, 2);
        }

        public static Payment Pay(decimal price, decimal downPct, decimal ratePct, int years, decimal taxPct, decimal insYear, decimal hoa, decimal pmiPct)
        {
            Payment p = new Payment { Price = Math.Max(0, price), Months = years * 12 };
            downPct = Math.Clamp(downPct, 0, 100);
            p.Down = Math.Round(p.Price * downPct / 100m, 0);
            p.Loan = p.Price - p.Down;
            p.Principal = Monthly(p.Loan, ratePct, years);
            p.Tax = Math.Round(p.Price * taxPct / 100m / 12m, 2);
            p.Insurance = Math.Round(insYear / 12m, 2);
            p.Hoa = Math.Max(0, hoa);
            p.Pmi = downPct < 20m ? Math.Round(p.Loan * pmiPct / 100m / 12m, 2) : 0;
            p.Interest = Math.Max(0, p.Principal * p.Months - p.Loan);
            return p;
        }

        public static Payment ForListing(SiteData s, Property l)
        {
            decimal tax = l.Tax > 0 && l.Price > 0 ? l.Tax / l.Price * 100m : s.Settings.TaxRate;
            return Pay(l.Price, s.Settings.DownPct, s.Settings.Rate, s.Settings.Term, tax, s.Settings.Insurance, l.Hoa, s.Settings.Pmi);
        }

        public static List<YearRow> Schedule(decimal loan, decimal ratePct, int years)
        {
            List<YearRow> rows = new List<YearRow>();
            decimal pay = Monthly(loan, ratePct, years);
            decimal bal = loan;
            decimal r = ratePct / 100m / 12m;
            for (int y = 1; y <= years && bal > 0; y++)
            {
                decimal pr = 0;
                decimal it = 0;
                for (int m = 0; m < 12 && bal > 0; m++)
                {
                    decimal i = Math.Round(bal * r, 2);
                    decimal princ = Math.Min(bal, pay - i);
                    bal -= princ;
                    pr += princ;
                    it += i;
                }
                rows.Add(new YearRow { Year = y, Principal = pr, Interest = it, Balance = Math.Max(0, bal) });
            }
            return rows;
        }

        public static decimal Afford(decimal income, decimal debts, decimal down, decimal ratePct, int years, decimal taxPct, decimal insYear, decimal hoa)
        {
            decimal month = income / 12m;
            decimal budget = Math.Min(month * 0.28m, month * 0.36m - debts) - insYear / 12m - hoa;
            if (budget <= 0)
            {
                return 0;
            }
            double r = (double)ratePct / 100.0 / 12.0;
            int n = years * 12;
            double factor = r > 0 ? r / (1 - Math.Pow(1 + r, -n)) : 1.0 / n;
            double t = (double)taxPct / 100.0 / 12.0;
            double price = ((double)budget + (double)down * factor) / (factor + t);
            return Math.Max(0, Math.Floor((decimal)price / 1000m) * 1000m);
        }
    }
}
