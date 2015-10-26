using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PAYECalcWin
{
    class Tax
    {
        public Tax(string name, OrderedDictionary taxRates)
        {
            this.Name = name;
            this.TaxRates = taxRates;
        }

        public string Name { get; private set; }
        public OrderedDictionary TaxRates { get; private set; }

        //Method to calculate the tax by looping through the thresholds (the key of the dictionary).
        //Calculate the tax and add it to the accumulated amount
        //Stopping when the threshold is above the salary.
        public virtual decimal CalculateTax(decimal amount)
        {
            decimal AccumulatedTax = 0;

            for (int i = this.TaxRates.Count-1; i >=0 ; i--)
            {
                decimal LowerBound = Convert.ToDecimal(this.TaxRates.Cast<DictionaryEntry>().ElementAt(i).Key);
                if (amount > LowerBound)
                {
                    AccumulatedTax += (amount - LowerBound) * Convert.ToDecimal(this.TaxRates[i]);
                    amount = LowerBound;
                }
            }
            return AccumulatedTax;
        }
    }
}
