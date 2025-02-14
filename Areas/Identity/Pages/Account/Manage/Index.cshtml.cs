// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FreshFarmMarket.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<FreshFarmMarketUser> _userManager;
        private readonly SignInManager<FreshFarmMarketUser> _signInManager;
        private readonly IWebHostEnvironment _environment;

        public IndexModel(
            UserManager<FreshFarmMarketUser> userManager,
            SignInManager<FreshFarmMarketUser> signInManager,
            IWebHostEnvironment environment
        )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _environment = environment;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Text)]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Required]
            [CreditCard]
            [DataType(DataType.CreditCard)]
            [Display(Name = "Credit Card Number")]
            public string CreditCardNumber { get; set; }

            [Required]
            [DataType(DataType.Text)]
            [Display(Name = "Gender")]
            public string Gender { get; set; }

            [Required]
            [Phone]
            [DataType(DataType.PhoneNumber)]
            [RegularExpression(
                @"^[689][0-9]{7}$",
                ErrorMessage = "Please enter a valid Singapore phone number (8 digits starting with 6, 8, or 9)"
            )]
            [Display(Name = "Mobile Number")]
            public string MobileNumber { get; set; }

            [Required]
            [DataType(DataType.Text)]
            [Display(Name = "Delivery Address")]
            public string DeliveryAddress { get; set; }

            [Required]
            [DataType(DataType.Upload)]
            [Display(Name = "Photo")]
            public IFormFile Photo { get; set; }

            public string PhotoUrl { get; set; }

            [Required]
            [DataType(DataType.MultilineText)]
            [Display(Name = "About Me")]
            [StringLength(2000, ErrorMessage = "About Me section cannot exceed 2000 characters.")]
            public string AboutMe { get; set; }
        }

        private async Task LoadAsync(FreshFarmMarketUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var mobileNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;

            Input = new InputModel
            {
                FullName = user.FullName,
                CreditCardNumber = string.Empty,
                Gender = user.Gender,
                MobileNumber = user.MobileNumber,
                DeliveryAddress = user.DeliveryAddress,
                PhotoUrl = user.PhotoUrl,
                AboutMe = user.AboutMe,
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            // Handle photo upload.
            if (Input.Photo != null)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(Input.Photo.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await Input.Photo.CopyToAsync(fileStream);
                }

                // Delete old photo if exists.
                if (!string.IsNullOrEmpty(user.PhotoUrl))
                {
                    var oldFilePath = Path.Combine(
                        _environment.WebRootPath,
                        user.PhotoUrl.TrimStart('/')
                    );
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                user.PhotoUrl = "/uploads/" + fileName;
            }

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.MobileNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(
                    user,
                    Input.MobileNumber
                );
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Unexpected error when trying to set phone number.";
                    return RedirectToPage();
                }
            }

            if (Input.FullName != user.FullName)
            {
                user.FullName = Input.FullName;
            }

            user.CreditCardNumber = BCrypt.Net.BCrypt.HashPassword(Input.CreditCardNumber);

            if (Input.Gender != user.Gender)
            {
                user.Gender = Input.Gender;
            }
            if (Input.MobileNumber != user.MobileNumber)
            {
                user.MobileNumber = Input.MobileNumber;
            }
            if (Input.DeliveryAddress != user.DeliveryAddress)
            {
                user.DeliveryAddress = Input.DeliveryAddress;
            }
            if (Input.PhotoUrl != user.PhotoUrl)
            {
                user.PhotoUrl = Input.PhotoUrl;
            }
            if (Input.AboutMe != user.AboutMe)
            {
                user.AboutMe = Input.AboutMe;
            }
            await _userManager.UpdateAsync(user);

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your profile has been updated";
            return RedirectToPage();
        }
    }
}
