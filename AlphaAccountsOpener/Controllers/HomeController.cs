using AlphaAccountsOpener.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TinyCsvParser;

namespace AlphaAccountsOpener.Controllers
{
    public class HomeController : Controller
    {

        public HomeController()
        {
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult IndexWithFile()
        {
            var requestFile = Request.Form.Files.FirstOrDefault();
            var formFilter = Request.Form["orderByAmount"];
            bool orderByAmount = false;
            var formFilter2 = Request.Form["groupByReason"];
            bool groupByReason = false;
            if (formFilter.Count > 0)
                orderByAmount = formFilter[0] == "on";
            if (formFilter2.Count > 0)
                groupByReason = formFilter2[0] == "on";

            if (requestFile is null)
                return RedirectToAction("Index");

            Stream fileStream = requestFile.OpenReadStream();
            List<string> originalLines;
            using (StreamReader reader = new StreamReader(fileStream))
            {
                originalLines = reader.ReadAllLines().ToList();
            }

            string validLines = "";
            foreach (var line in originalLines)
            {
                if (line.Count(c => c == ';') > 5)
                {
                    validLines += line + "\n";
                }
            }
            validLines = validLines.Remove(validLines.Length - 1);
            var validStream = new MemoryStream(Encoding.UTF8.GetBytes(validLines));

            CsvParserOptions csvParserOptions = new CsvParserOptions(true, ';');
            CsvUserDetailsMapping csvMapper = new CsvUserDetailsMapping();
            CsvParser<TransactionDetails> csvParser = new CsvParser<TransactionDetails>(csvParserOptions, csvMapper);
            List<TinyCsvParser.Mapping.CsvMappingResult<TransactionDetails>> result = csvParser.ReadFromStream(validStream, Encoding.UTF8).ToList();
            var items = result.Select(r => r?.Result).Where(r => r != null).ToList();
            foreach (var item in items)
            {
                item.AmountDecimal = decimal.Parse(item.Amount.Trim().Replace(",", "."));
                item.isDebit = item.Debit == "Χ";
                item.Reason = item.Reason.Replace("=\"", "").Replace("\"", "");
                item.TransactionNumber = item.TransactionNumber.Replace("=\"", "").Replace("\"", "");
            }
            if (groupByReason)
            {
                var counter = 0;
                items = items.GroupBy(i => new { i.Reason, i.isDebit }).Select(i => new TransactionDetails
                {
                    ID = (counter++).ToString(),
                    Date = null,
                    CreditDate = null,
                    Store = null,
                    TransactionNumber = null,
                    isDebit = i.Key.isDebit,
                    Reason = i.Key.Reason,
                    AmountDecimal = i.Sum(i => i.AmountDecimal),
                    Amount = i.Sum(i => i.AmountDecimal).ToString(),
                    Debit = i.Key.isDebit ? "Χ" : "P"
                }).ToList();
            }
            if (orderByAmount)
            {
                items = items.OrderByDescending(o => o.AmountDecimal).ToList();
            }
            var containsInvalids = result.FirstOrDefault(r => r.IsValid == false) != null;
            return View(new ViewModel
            {
                ContainsInvalids = containsInvalids,
                Transactions = items
            });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class ViewModel
    {
        public bool ContainsInvalids { get; set; }
        public List<TransactionDetails> Transactions { get; set; }
    }
    public class CsvUserDetailsMapping : TinyCsvParser.Mapping.CsvMapping<TransactionDetails>
    {
        public CsvUserDetailsMapping()
            : base()
        {
            MapProperty(0, x => x.ID);
            MapProperty(1, x => x.Date);
            MapProperty(2, x => x.Reason);
            MapProperty(3, x => x.Store);

            MapProperty(4, x => x.CreditDate);

            MapProperty(5, x => x.TransactionNumber);
            MapProperty(6, x => x.Amount);
            MapProperty(7, x => x.Debit);
        }
    }
    public class TransactionDetails
    {
        public string Store { get; set; }
        public string ID { get; set; }
        public string Date { get; set; }
        public string Reason { get; set; }
        public string CreditDate { get; set; }
        public string TransactionNumber { get; set; }
        public string Amount { get; set; }
        public decimal AmountDecimal { get; set; }
        public string Debit { get; set; }
        public bool isDebit { get; set; }
    }
}
public static class Extensions
{
    public static IEnumerable<string> ReadAllLines(this StreamReader reader)
    {
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            yield return line;
        }
    }
}
