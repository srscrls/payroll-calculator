using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PAYECalcWin
{
    class PAYETax : Tax
    {
        public PAYETax( string Name, string taxCodeLetter, OrderedDictionary PAYEtaxRates) :base("PAYE",PAYEtaxRates)
        {
            this.TaxCodeLetter = taxCodeLetter;
        }
        
        private string TaxCodeLetter { get; set; }

        public override decimal CalculateTax(decimal amount)
        {
            decimal tax = base.CalculateTax(amount);

            //Uses this calculation if the tax code starts with K and the tax is greater than 50% of the salary
            //otherwise use the base calculation.
            if (this.TaxCodeLetter == "K" && (tax > 0.5m * amount))
            {
                tax = 0.5m * amount;    
            }
            return tax;
        }
    }
}