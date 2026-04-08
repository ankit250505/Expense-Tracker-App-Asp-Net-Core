using Microsoft.AspNetCore.Identity;

namespace Expense_Tracker.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }

        public string? ProfileImagePath { get; set; }
    }
}