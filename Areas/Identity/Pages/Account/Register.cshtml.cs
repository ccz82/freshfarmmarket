// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace FreshFarmMarket.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<FreshFarmMarketUser> _signInManager;
        private readonly UserManager<FreshFarmMarketUser> _userManager;
        private readonly IUserStore<FreshFarmMarketUser> _userStore;
        private readonly IUserEmailStore<FreshFarmMarketUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _environment;
        private readonly EncryptionService _encryptionService;

        public RegisterModel(
            UserManager<FreshFarmMarketUser> userManager,
            IUserStore<FreshFarmMarketUser> userStore,
            SignInManager<FreshFarmMarketUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            IWebHostEnvironment environment,
            EncryptionService encryptionService
        )
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _environment = environment;
            _encryptionService = encryptionService;
        }

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
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
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

            [Required]
            [DataType(DataType.MultilineText)]
            [Display(Name = "About Me")]
            [StringLength(2000, ErrorMessage = "About Me section cannot exceed 2000 characters.")]
            public string AboutMe { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(
                100,
                ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.",
                MinimumLength = 12
            )]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare(
                "Password",
                ErrorMessage = "The password and confirmation password do not match."
            )]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (
                await _signInManager.GetExternalAuthenticationSchemesAsync()
            ).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (
                await _signInManager.GetExternalAuthenticationSchemesAsync()
            ).ToList();
            if (ModelState.IsValid)
            {
                var user = CreateUser();

                // Handle photo upload
                var file = Input.Photo;

                // Validate file type
                var allowedContentTypes = new[] { "image/png", "image/jpeg" };
                if (!allowedContentTypes.Contains(file.ContentType.ToLower()))
                {
                    ModelState.AddModelError(
                        "Input.Photo",
                        "Only JPEG and PNG images are allowed."
                    );
                    return Page();
                }

                // Validate file size (5MB)
                if (file.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("Input.Photo", "Maximum file size is 5MB.");
                    return Page();
                }

                // Process the file
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                user.PhotoUrl = "/uploads/" + fileName;

                user.FullName = Input.FullName;
                user.CreditCardNumber = _encryptionService.Encrypt(Input.CreditCardNumber);
                user.Gender = Input.Gender;
                user.MobileNumber = Input.MobileNumber;
                user.DeliveryAddress = Input.DeliveryAddress;
                user.AboutMe = Input.AboutMe;

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new
                        {
                            area = "Identity",
                            userId = userId,
                            code = code,
                            returnUrl = returnUrl,
                        },
                        protocol: Request.Scheme
                    );

                    await _emailSender.SendEmailAsync(
                        Input.Email,
                        "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>."
                    );

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage(
                            "RegisterConfirmation",
                            new { email = Input.Email, returnUrl = returnUrl }
                        );
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private FreshFarmMarketUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<FreshFarmMarketUser>();
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Can't create an instance of '{nameof(FreshFarmMarketUser)}'. "
                        + $"Ensure that '{nameof(FreshFarmMarketUser)}' is not an abstract class and has a parameterless constructor, or alternatively "
                        + $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml"
                );
            }
        }

        private IUserEmailStore<FreshFarmMarketUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException(
                    "The default UI requires a user store with email support."
                );
            }
            return (IUserEmailStore<FreshFarmMarketUser>)_userStore;
        }
    }
}
