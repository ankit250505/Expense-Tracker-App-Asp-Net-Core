using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Expense_Tracker.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Expense_Tracker.Controllers
{
    [Authorize]
    public class TransactionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransactionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Transaction
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var applicationDbContext = _context.Transactions
                .Where(t => t.UserId == userId)
                .Include(t => t.Category);

            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Transaction/AddOrEdit
        public IActionResult AddOrEdit(int id = 0)
        {
            PopulateCategories();

            if (id == 0)
                return View(new Transaction());
            else
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var transaction = _context.Transactions
                    .FirstOrDefault(t => t.TransactionId == id && t.UserId == userId);

                if (transaction == null)
                    return NotFound();

                return View(transaction);
            }
        }

        // POST: Transaction/AddOrEdit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit([Bind("TransactionId,CategoryId,Amount,Note,Date")] Transaction transaction)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (ModelState.IsValid)
            {
                if (transaction.TransactionId == 0)
                {
                    // CREATE
                    transaction.UserId = userId;
                    _context.Add(transaction);
                }
                else
                {
                    // UPDATE (SAFE)
                    var existingTransaction = await _context.Transactions
                        .FirstOrDefaultAsync(t => t.TransactionId == transaction.TransactionId && t.UserId == userId);

                    if (existingTransaction == null)
                        return NotFound();

                    existingTransaction.CategoryId = transaction.CategoryId;
                    existingTransaction.Amount = transaction.Amount;
                    existingTransaction.Note = transaction.Note;
                    existingTransaction.Date = transaction.Date;

                    _context.Update(existingTransaction);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            PopulateCategories();
            return View(transaction);
        }

        // POST: Transaction/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

            if (transaction == null)
                return NotFound();

            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [NonAction]
        public void PopulateCategories()
        {
            var CategoryCollection = _context.Categories.ToList();

            Category DefaultCategory = new Category()
            {
                CategoryId = 0,
                Title = "Choose a Category"
            };

            CategoryCollection.Insert(0, DefaultCategory);

            ViewBag.Categories = CategoryCollection;
        }

        // GET: Transaction/ExportPdf
        public async Task<IActionResult> ExportPdf(DateTime? startDate, DateTime? endDate)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var start = startDate ?? DateTime.Now.AddMonths(-1);
            var end = endDate ?? DateTime.Now;

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId && t.Date >= start && t.Date <= end)
                .Include(t => t.Category)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            var totalIncome = transactions.Where(t => t.Category?.Type == "Income").Sum(t => t.Amount);
            var totalExpense = transactions.Where(t => t.Category?.Type == "Expense").Sum(t => t.Amount);
            var balance = totalIncome - totalExpense;

            using var ms = new MemoryStream();

            var writer = new iText.Kernel.Pdf.PdfWriter(ms);
            var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
            var document = new iText.Layout.Document(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(40, 40, 40, 40);

            // ── Colours ──
            var green = new iText.Kernel.Colors.DeviceRgb(74, 222, 128);
            var red = new iText.Kernel.Colors.DeviceRgb(248, 113, 113);
            var blue = new iText.Kernel.Colors.DeviceRgb(96, 165, 250);
            var darkBg = new iText.Kernel.Colors.DeviceRgb(13, 20, 25);
            var cardBg = new iText.Kernel.Colors.DeviceRgb(17, 25, 32);
            var mutedText = new iText.Kernel.Colors.DeviceRgb(122, 145, 128);
            var lightText = new iText.Kernel.Colors.DeviceRgb(232, 240, 233);

            // ── Fonts ──
            var bold = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
            var regular = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);

            
            // ── Header ──
            var header = new iText.Layout.Element.Paragraph("SpendSense")
                .SetFont(bold).SetFontSize(22).SetFontColor(green)
                .SetMarginBottom(2);
            document.Add(header);

            var sub = new iText.Layout.Element.Paragraph("Financial Report  •  " +
                      start.ToString("dd MMM yyyy") + " – " + end.ToString("dd MMM yyyy"))
                .SetFont(regular).SetFontSize(9).SetFontColor(mutedText)
                .SetMarginBottom(20);
            document.Add(sub);

            // ── Summary Cards (3-column table) ──
            var summaryTable = new iText.Layout.Element.Table(
                new float[] { 1, 1, 1 }, true);
            summaryTable.SetWidth(iText.Layout.Properties.UnitValue.CreatePercentValue(100))
                        .SetMarginBottom(24);

            void AddSummaryCell(string label, string value, iText.Kernel.Colors.Color color)
            {
                var cell = new iText.Layout.Element.Cell()
                    .SetBackgroundColor(cardBg)
                    .SetBorder(new iText.Layout.Borders.SolidBorder(color, 1))
                    .SetBorderRadius(new iText.Layout.Properties.BorderRadius(8))
                    .SetPadding(14);

                cell.Add(new iText.Layout.Element.Paragraph(label)
                    .SetFont(regular).SetFontSize(8).SetFontColor(mutedText).SetMarginBottom(4));
                cell.Add(new iText.Layout.Element.Paragraph(value)
                    .SetFont(bold).SetFontSize(16).SetFontColor(color).SetMarginBottom(0));

                summaryTable.AddCell(cell);
            }

            AddSummaryCell("TOTAL INCOME", "₹ " + totalIncome.ToString("N0"), green);
            AddSummaryCell("TOTAL EXPENSE", "₹ " + totalExpense.ToString("N0"), red);
            AddSummaryCell("BALANCE", "₹ " + balance.ToString("N0"), blue);
            document.Add(summaryTable);

            // ── Section title ──
            document.Add(new iText.Layout.Element.Paragraph("Transaction Details")
                .SetFont(bold).SetFontSize(11).SetFontColor(lightText).SetMarginBottom(10));

            // ── Transactions Table ──
            var table = new iText.Layout.Element.Table(
                new float[] { 2, 1.5f, 2.5f, 1.2f }, true);
            table.SetWidth(iText.Layout.Properties.UnitValue.CreatePercentValue(100));

            var headerBg = new iText.Kernel.Colors.DeviceRgb(8, 13, 16);

            foreach (var h in new[] { "CATEGORY", "DATE", "NOTE", "AMOUNT" })
            {
                table.AddHeaderCell(
                    new iText.Layout.Element.Cell()
                        .SetBackgroundColor(headerBg)
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                        .SetBorderBottom(new iText.Layout.Borders.SolidBorder(mutedText, 1))
                        .SetPaddingTop(10).SetPaddingBottom(10)
                        .SetPaddingLeft(8).SetPaddingRight(8)
                        .Add(new iText.Layout.Element.Paragraph(h)
                            .SetFont(bold).SetFontSize(7.5f).SetFontColor(mutedText)));
            }

            bool alt = false;
            foreach (var t in transactions)
            {
                var rowBg = alt ? cardBg : darkBg;
                var isIncome = t.Category?.Type == "Income";
                var amtColor = isIncome ? green : red;
                var amtText = (isIncome ? "+ " : "- ") + "₹ " + t.Amount.ToString("N0");

                iText.Layout.Element.Cell MakeCell(string text, iText.Kernel.Colors.Color? color = null)
                {
                    return new iText.Layout.Element.Cell()
                        .SetBackgroundColor(rowBg)
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                        .SetBorderBottom(new iText.Layout.Borders.SolidBorder(
                            new iText.Kernel.Colors.DeviceRgb(255, 255, 255), 0.03f))
                        .SetPaddingTop(9).SetPaddingBottom(9)
                        .SetPaddingLeft(8).SetPaddingRight(8)
                        .Add(new iText.Layout.Element.Paragraph(text)
                            .SetFont(regular).SetFontSize(9)
                            .SetFontColor(color ?? lightText));
                }

                table.AddCell(MakeCell(t.Category?.Title ?? "—"));
                table.AddCell(MakeCell(t.Date.ToString("dd MMM yy")));
                table.AddCell(MakeCell(t.Note ?? "—"));
                table.AddCell(MakeCell(amtText, amtColor));

                alt = !alt;
            }

            document.Add(table);

            // ── Footer ──
            document.Add(new iText.Layout.Element.Paragraph(
                    "\nGenerated by SpendSense  •  " + DateTime.Now.ToString("dd MMM yyyy, hh:mm tt"))
                .SetFont(regular).SetFontSize(8).SetFontColor(mutedText)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                .SetMarginTop(16));

            document.Close();

            var fileName = $"SpendSense_Report_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf";
            return File(ms.ToArray(), "application/pdf", fileName);
        }
    }
}