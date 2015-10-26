using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.XPath;

namespace PAYECalcWin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Year.ItemsSource = ReadXML("//@Year");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //Initialize Tax table rates using ordered dictionaries
            OrderedDictionary payetable = new OrderedDictionary();
            OrderedDictionary nitable = new OrderedDictionary();
            OrderedDictionary sltable = new OrderedDictionary();
            OrderedDictionary empnitable = new OrderedDictionary();
            
            string YearSelected = Year.SelectedItem.ToString();
            
            //Checks to see if NI Category is not null otherwise select the default
            if (NICategory.SelectedItem == null){
                NICategory.SelectedItem = ReadXML("//YearEnd[@Year='" + YearSelected + "']/NIContributions/DefaultBand").First();
            }
            
            string BandLetter = NICategory.SelectedItem.ToString();
            decimal personalAllowance = 0m;
            string taxCodeLetter = "L";

            //Reads the tax rates from the XML file
            nitable = ReadRatesXML("//YearEnd[@Year='"+YearSelected+"']/NIContributions/Band[@letter='"+BandLetter+"']");
            sltable = ReadRatesXML("//YearEnd[@Year='" + YearSelected + "']/StudentLoan");
            empnitable = ReadRatesXML("//YearEnd[@Year='" + YearSelected + "']/EmployersNI/Band[@letter='" + BandLetter + "']");
            payetable = CreatePayeTable(YearSelected, out personalAllowance, out taxCodeLetter);

            //Initializes Tax objects
            Tax PAYE = new PAYETax("PAYE", taxCodeLetter, payetable);
            Tax NI = new Tax("National Insurance",nitable);
            Tax empNI = new Tax("Employer's National Insurance",empnitable);
            Tax SL = new Tax("Student Loan", sltable);

            List<Tax> taxes = new List<Tax> { PAYE, NI };
            List<Tax> taxesPayable = new List<Tax> { PAYE, NI ,empNI};

            //Checks if Student Loans is selected
            if (SLDeductions.IsChecked == true)
            {
                taxes.Add(SL);
                taxesPayable.Add(SL);
                StudentLoanRow.Height = new GridLength(0,GridUnitType.Auto);
            }
            else
            {
                StudentLoanRow.Height = new GridLength(0);
            }

            decimal Amount = 0m;
            decimal.TryParse(Salary.Text, out Amount);

            //Calculates which period we are using
            switch (Period.SelectionBoxItem.ToString())
            {
                case "Month":
                    Amount = Amount * 12;
                    break;
                case "Week":
                    Amount = Amount * 52;
                    break;
                default:
                    Period.Text = "Year";
                    break;
            }

            decimal GrossAmount = 0;

            //Checks to see whether we are calculating gross or net
            if (Net.IsChecked == true)
            {
                GrossAmount = CalculateGrossSalary(Amount, taxes);
            }
            else
            {
                GrossAmount = Amount;
            }

            //Displays the various calculations
            GrossYearlyAmount.Content = GrossAmount;
            GrossMonthlyAmount.Content = (GrossAmount / 12);
            GrossWeeklyAmount.Content = (GrossAmount / 52);

            decimal PAYEAmount = PAYE.CalculateTax(GrossAmount);
            PAYEYearlyAmount.Content = PAYEAmount;
            PAYEMonthlyAmount.Content = (PAYEAmount / 12);
            PAYEWeeklyAmount.Content = (PAYEAmount / 52);
            
            decimal NIAmount = NI.CalculateTax(GrossAmount);
            NIYearlyAmount.Content = NIAmount;
            NIMonthlyAmount.Content = (NIAmount / 12);
            NIWeeklyAmount.Content = (NIAmount / 52);

            decimal SLAmount = SL.CalculateTax(GrossAmount);
            SLYearlyAmount.Content = SLAmount;
            SLMonthlyAmount.Content = (SLAmount / 12);
            SLWeeklyAmount.Content = (SLAmount / 52);
            
            decimal NetAmount = CalculateNetSalary(GrossAmount,taxes);
            NetYearlyAmount.Content = NetAmount;
            NetMonthlyAmount.Content = (NetAmount / 12);
            NetWeeklyAmount.Content = (NetAmount / 52);

            decimal empNIAmount = empNI.CalculateTax(GrossAmount);
            empNIYearlyAmount.Content = empNIAmount;
            empNIMonthlyAmount.Content = (empNIAmount / 12);
            empNIWeeklyAmount.Content = (empNIAmount / 52);

            decimal TotalTaxAmount = CalculateTotalTax(GrossAmount, taxesPayable);
            TotalTaxYearlyAmount.Content = TotalTaxAmount;
            TotalTaxMonthlyAmount.Content = (TotalTaxAmount / 12);
            TotalTaxWeeklyAmount.Content = (TotalTaxAmount / 52);
        }

        private OrderedDictionary CreatePayeTable(string YearSelected, out decimal personalAllowance, out string taxCodeLetter)
        {
            OrderedDictionary payetable = new OrderedDictionary();
            personalAllowance = 0m;
            taxCodeLetter = "";
            decimal numericAmount = 0m;
            
            List<string> TaxCodes = ReadXML("//YearEnd[@Year='"+YearSelected+"']/PAYE/BandCodes//@letter");
            List<string> TaxCodesEndLetters = ReadXML("//YearEnd[@Year='" + YearSelected + "']/PAYE/BandEndLetters/Letter");
            List<string> TaxCodesStartLetters = ReadXML("//YearEnd[@Year='" + YearSelected + "']/PAYE/BandStartLetters/Letter");
            
            //If no Tax code was enter, use default and call method again.
            if (TaxCode.Text == "") {
                TaxCode.Text = ReadXML("//YearEnd[@Year='" + YearSelected + "']/PAYE/Default").First();
                payetable = CreatePayeTable(YearSelected, out personalAllowance, out taxCodeLetter);
            }   
            else if (TaxCodes.Contains(TaxCode.Text)) //Checks for single rate NT, BR, D0 etc. 
            {
                payetable = ReadRatesXML("//YearEnd[@Year='" + YearSelected + "']/PAYE/BandCodes/code[@letter='" + TaxCode.Text + "']");
            }
            //The case for the L (and related) codes is complex. We need to take into consideration that the personal Allowance
            //reduces by 2 after the salary goes past £100,000 (the adjusted level). 
            //To do this, the rate will be increased by 1.5 post £100,000 until the personal allowance reaches 0.
            //This threshold will be 100,000 + 2 * personal allowance.
            else if (TaxCodesEndLetters.Contains(TaxCode.Text.Substring(TaxCode.Text.Length - 1, 1)))
            {
                if (decimal.TryParse(TaxCode.Text.Substring(0, TaxCode.Text.Length - 1), out numericAmount))
                {
                    personalAllowance = numericAmount * 10 + 9;
                    OrderedDictionary payetableRate = new OrderedDictionary();
                    payetableRate = ReadRatesXML("//YearEnd[@Year='" + YearSelected + "']/PAYE/Bands", personalAllowance);
                    decimal Adjusted = 0;
                    if (ReadXML("//YearEnd[@Year='" + YearSelected + "']/PAYE/Adjusted").Count == 1)
                    {
                        Adjusted = Convert.ToDecimal(ReadXML("//YearEnd[@Year='" + YearSelected + "']/PAYE/Adjusted").First());
                    }
                        
                    if (Adjusted != 0)
                    {
                        decimal AdjustedUpper = Adjusted + 2 * (personalAllowance-9);
                        int i = 0;
                        decimal Bound = 0;
                        decimal Rate = 0;
                        decimal RateAdjusted = 0;
                        int NumberOfEntries = payetableRate.Count;
                        payetable.Add(Bound, Rate);

                        //3 stages here
                        //i. Add thresholds below the Adjusted level
                        //ii. Add thresholds between Adjusted level and Adjusted level + 2 * personal allowance
                        //iii. Add thresholds post Adjusted level + 2 * personal allowance

                        //Stage i
                        while (true) //infinite loop but breaking when either of two conditions are met
                        {
                            Bound = Convert.ToDecimal(payetableRate.Cast<DictionaryEntry>().ElementAt(i).Key);
                            Rate = Convert.ToDecimal(payetableRate[i]);
                            if (Bound < Adjusted)
                            { payetable.Add(Bound, Rate); }
                            else //break when Adjusted level is reached
                            {
                                RateAdjusted = Convert.ToDecimal(payetableRate[i - 1]) * 1.5m;
                                payetable.Add(Adjusted, RateAdjusted);
                                break;
                            }
                            if (i < NumberOfEntries - 1)
                            { i++; }
                            else  //break also when end of table is reached
                            {
                                RateAdjusted = Convert.ToDecimal(payetableRate[i]) * 1.5m;
                                payetable.Add(Adjusted, RateAdjusted);
                                break;
                            }
                        }

                        //Stage ii
                        decimal BoundAdjusted = 0;
                        while (AdjustedUpper > Bound - personalAllowance)
                        {
                            Bound = Convert.ToDecimal(payetableRate.Cast<DictionaryEntry>().ElementAt(i).Key);
                            if (Bound < Adjusted)
                            {
                                Rate = Convert.ToDecimal(payetableRate[i]);
                                payetable.Add(AdjustedUpper, Rate);
                                break;
                            }
                            if (Bound - personalAllowance < AdjustedUpper)
                            {
                                BoundAdjusted = AdjustedUpper / 3 + 2 * (Bound - personalAllowance) / 3;
                                RateAdjusted = Convert.ToDecimal(payetableRate[i]) * 1.5m;
                                payetable.Add(BoundAdjusted, RateAdjusted);
                            }
                            else
                            {
                                Rate = Convert.ToDecimal(payetableRate[i - 1]);
                                payetable.Add(AdjustedUpper, Rate);
                                break;
                            }
                            if (i < NumberOfEntries - 1)
                            { i++; }
                            else
                            {
                                Rate = Convert.ToDecimal(payetableRate[i]);
                                payetable.Add(AdjustedUpper, Rate);
                                break;
                            }
                        }

                        //Stage iii
                        while (true)//infinite loop but breaking when either of two conditions are met
                        {
                            Bound = Convert.ToDecimal(payetableRate.Cast<DictionaryEntry>().ElementAt(i).Key);
                            Rate = Convert.ToDecimal(payetableRate[i]);
                            if (Bound - personalAllowance > AdjustedUpper)
                            {
                                payetable.Add(Bound - personalAllowance, Rate);
                            }
                            else { break; } //breaks if this threshold is last in table
                            if (i < NumberOfEntries - 1)
                            { i++; } //breaks when end of table is reached
                            else { break; }
                        }
                    }
                    else //Case when there is no adjusted level
                    {
                        int i = 0;
                        decimal Bound = 0;
                        decimal Rate = 0;
                        int NumberOfEntries = payetableRate.Count;
                        while (i < NumberOfEntries)
                        {
                            payetable.Add(Bound, Rate);
                            Bound = Convert.ToDecimal(payetableRate.Cast<DictionaryEntry>().ElementAt(i).Key);
                            Rate = Convert.ToDecimal(payetableRate[i]);
                            i++;
                        }
                    }
                    taxCodeLetter = TaxCode.Text.Substring(TaxCode.Text.Length - 1, 1);
                }
            }
            else if (TaxCodesStartLetters.Contains(TaxCode.Text.Substring(0, 1))) //Case for K codes
            {
                if (decimal.TryParse(TaxCode.Text.Substring(1, TaxCode.Text.Length - 1), out numericAmount)){
                    personalAllowance = -numericAmount * 10;
                    payetable = ReadRatesXML("//YearEnd[@Year='" + YearSelected + "']/PAYE/Bands", personalAllowance);

                    taxCodeLetter = TaxCode.Text.Substring(0, 1);
                }
            }
            else  //Case for invalid tax code enter. Then use default and call method again
            {
                TaxCode.Text = ReadXML("//YearEnd[@Year='" + YearSelected + "']/PAYE/Default").First();
                payetable = CreatePayeTable(YearSelected, out personalAllowance, out taxCodeLetter);
            }           

            return payetable;
            
        }

        private decimal CalculateNetSalary(decimal grossSalary, List<Tax> taxes)
        {
            decimal NetSalary = grossSalary;
            foreach (Tax tax in taxes)
            {
                NetSalary -= tax.CalculateTax(grossSalary);
            }
            return NetSalary;
        }

        private decimal CalculateTotalTax(decimal grossSalary, List<Tax> taxes)
        {
            decimal Totaltax = 0;
            foreach (Tax tax in taxes)
            {
                Totaltax += tax.CalculateTax(grossSalary);
            }
            return Totaltax;
        }

        //Algorithm to calculate gross salary from the net salary.
        //It works by combining all the thresholds from all the taxes into a dictionary.
        //It calculates the net salary at each threshold. From this we can deduce which threshold the 
        //gross salary is at in each tax. We can thus use a little bit of algebra to work out the 
        //actual gross salary.
        private decimal CalculateGrossSalary(decimal netSalary, List<Tax> taxes)
        {

            decimal GrossSalary = netSalary;
            decimal x = 1;
            decimal LowerBound = 0;
            decimal NetLowerBound = 0;
            decimal rate = 0;

            foreach (Tax tax in taxes)
            {
                LowerBound = 0;
                NetLowerBound = 0;
                for (int i = tax.TaxRates.Count - 1; i >= 0; i--)
                {
                    LowerBound = Convert.ToDecimal(tax.TaxRates.Cast<DictionaryEntry>().ElementAt(i).Key);
                    
                    NetLowerBound = CalculateNetSalary(LowerBound, taxes);
                    if (NetLowerBound < netSalary)
                    {

                        rate = Convert.ToDecimal(tax.TaxRates[i]);
                        GrossSalary += tax.CalculateTax(LowerBound) - LowerBound * rate;
                        x -= rate;
                        break;
                    }
                }
            }

            GrossSalary = GrossSalary / x;

            return GrossSalary;
        }

        //Resets the program.
        private void Clear_Selection(object sender, RoutedEventArgs e)
        {

            Year.Text = "";
            Salary.Text = "";
            Period.Text = "";
            TaxCode.Text = "";
            Gross.IsChecked = true;
            NICategory.Text = "";
            SLDeductions.IsChecked = false;
            Calculate.IsEnabled = false;

            GrossYearlyAmount.Content = "";
            GrossMonthlyAmount.Content = "";
            GrossWeeklyAmount.Content = "";

            PAYEYearlyAmount.Content = "";
            PAYEMonthlyAmount.Content = "";
            PAYEWeeklyAmount.Content = "";

            NIYearlyAmount.Content = "";
            NIMonthlyAmount.Content = "";
            NIWeeklyAmount.Content = "";

            SLYearlyAmount.Content = "";
            SLMonthlyAmount.Content = "";
            SLWeeklyAmount.Content = "";

            NetYearlyAmount.Content = "";
            NetMonthlyAmount.Content = "";
            NetWeeklyAmount.Content = "";

            empNIYearlyAmount.Content = "";
            empNIMonthlyAmount.Content = "";
            empNIWeeklyAmount.Content = "";

            TotalTaxYearlyAmount.Content = "";
            TotalTaxMonthlyAmount.Content = "";
            TotalTaxWeeklyAmount.Content = "";
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        //Returns a List query by supplying a string query. Algorithm copied from Stack Overflow.
        public static List<String> ReadXML(string expression){
            List<string> items = new List<string>();

            XPathDocument xmlDoc = new XPathDocument("Rates.xml");
            XPathNavigator nav = xmlDoc.CreateNavigator();      //initialise XML document
            XPathExpression expr = nav.Compile(expression);     //Returns a query
            XPathNodeIterator iterator = nav.Select(expr);      
            while (iterator.MoveNext())                         //Moves along the iterator, copies current value
            {                                                   //and adds it to the list
                XPathNavigator nav2 = iterator.Current.Clone();
                items.Add(nav2.Value);
            }

            return items;
        }

        //Similar algorithm to ReadXML but designed to input the tax thresholds and rates
        public static OrderedDictionary ReadRatesXML(string expression)
        {
            OrderedDictionary TaxTables = new OrderedDictionary();

            XPathDocument xmlDoc = new XPathDocument("Rates.xml");
            XPathNavigator nav = xmlDoc.CreateNavigator();
            XPathExpression expr = nav.Compile(expression+"/Amount");
            XPathExpression expr1 = nav.Compile(expression+"/Rate");
            XPathNodeIterator iterator = nav.Select(expr);
            XPathNodeIterator iterator1 = nav.Select(expr1);
            while (iterator.MoveNext())
            {
                iterator1.MoveNext();
                XPathNavigator nav1 = iterator.Current.Clone();
                XPathNavigator nav2 = iterator1.Current.Clone();
                decimal amount = decimal.Parse(nav1.Value);
                decimal rate = decimal.Parse(nav2.Value);
                TaxTables.Add(amount, rate);
            }

            return TaxTables;
        }

        //Again, similar algorithm to ReadXML but this is designed to read PAYE tax thresholds and rates
        public static OrderedDictionary ReadRatesXML(string expression, decimal personalAllowance)
        {
            OrderedDictionary TaxTables = new OrderedDictionary();

            XPathDocument xmlDoc = new XPathDocument("Rates.xml");
            XPathNavigator nav = xmlDoc.CreateNavigator();
            XPathExpression expr = nav.Compile(expression + "/Amount");
            XPathExpression expr1 = nav.Compile(expression + "/Rate");
            XPathNodeIterator iterator = nav.Select(expr);
            XPathNodeIterator iterator1 = nav.Select(expr1);
            while (iterator.MoveNext())
            {
                iterator1.MoveNext();
                XPathNavigator nav1 = iterator.Current.Clone();
                XPathNavigator nav2 = iterator1.Current.Clone();
                decimal amount = decimal.Parse(nav1.Value) + personalAllowance;
                decimal rate = decimal.Parse(nav2.Value);
                TaxTables.Add(amount, rate);
            }

            return TaxTables;
        }

        //Once the year has been selected, enable the calculate button.
        private void Year_Selected(object sender, RoutedEventArgs e)
        {
            Calculate.IsEnabled = true;
            NICategory.ItemsSource = ReadXML("//YearEnd[@Year='"+Year.SelectedValue+"']/NIContributions//@letter");
        }

    }

   
}
