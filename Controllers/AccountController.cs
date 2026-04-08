using Expense_Tracker.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Expense_Tracker.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(UserManager<ApplicationUser> userManager,
                                 SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // REGISTER
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string fullName, string email, string password, IFormFile profileImage)
        {
            string? imagePath = null;

            // 📸 Handle image upload
            if (profileImage != null && profileImage.Length > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/profiles");

                // Create folder if not exists
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(profileImage.FileName);
                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(stream);
                }

                imagePath = fileName;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                ProfileImagePath = imagePath
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim("FullName", user.FullName ?? user.Email),
                    new Claim("ProfileImage", user.ProfileImagePath ?? "")
                };
                await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, claims);
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.Errors = result.Errors;
            return View();
        }

        // LOGIN
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user != null)
            {
                var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);

                if (isPasswordValid)
                {
                    var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim("FullName", user.FullName ?? user.Email),
                new Claim("ProfileImage", user.ProfileImagePath ?? "")
            };

                    await _signInManager.SignOutAsync();
                    await _signInManager.SignInWithClaimsAsync(user, false, claims);

                    return RedirectToAction("Index", "Dashboard");
                }
            }

            ViewBag.Error = "Invalid login attempt";
            return View();
        }

        // LOGOUT
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}