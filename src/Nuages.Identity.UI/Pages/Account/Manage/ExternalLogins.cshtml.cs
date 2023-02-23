// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Nuages.Identity.Services.AspNetIdentity;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Nuages.Identity.UI.Pages.Account.Manage;

[Authorize]
public class ExternalLoginsModel : PageModel
{
    private readonly NuagesSignInManager _signInManager;
    private readonly NuagesUserManager _userManager;
    private readonly IUserStore<NuagesApplicationUser<string>> _userStore;
    private readonly ILogger<ExternalLoginsModel> _logger;

    public ExternalLoginsModel(
        NuagesUserManager userManager,
        NuagesSignInManager signInManager,
        IUserStore<NuagesApplicationUser<string>> userStore, ILogger<ExternalLoginsModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userStore = userStore;
        _logger = logger;
    }

    public IList<UserLoginInfo> CurrentLogins { get; set; }

    public IList<AuthenticationScheme> OtherLogins { get; set; }

    public bool ShowRemoveButton { get; set; }

    [TempData] public string StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            CurrentLogins = await _userManager.GetLoginsAsync(user);
            OtherLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync())
                .Where(auth => CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
                .Where(l => l.Name != "JwtOrCookie")
                .ToList();

            string passwordHash = null;
            if (_userStore is IUserPasswordStore<NuagesApplicationUser<string>> userPasswordStore)
                passwordHash = await userPasswordStore.GetPasswordHashAsync(user, HttpContext.RequestAborted);

            ShowRemoveButton = passwordHash != null || CurrentLogins.Count > 1;
            return Page();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}",e.Message);

            throw;
        }
    }

    public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            var result = await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
            if (!result.Succeeded)
            {
                StatusMessage = "The external login was not removed.";
                return RedirectToPage();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "The external login was removed.";
            return RedirectToPage();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}", e.Message);

            throw;
        }
    }

    public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
    {
        try
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            // Request a redirect to the external login provider to link a login for the current user
            var redirectUrl = Url.Page("./ExternalLogins", "LinkLoginCallback")!.Replace("http:", "https");
            var properties =
                _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl,
                    _userManager.GetUserId(User));
            return new ChallengeResult(provider, properties);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}", e.Message);

            throw;
        }
    }

    public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            var userId = await _userManager.GetUserIdAsync(user);
            var info = await _signInManager.GetExternalLoginInfoAsync(userId);
            if (info == null) throw new InvalidOperationException("Unexpected error occurred loading external login info.");

            var result = await _userManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                StatusMessage =
                    "The external login was not added. External logins can only be associated with one account.";
                return RedirectToPage();
            }

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            StatusMessage = "The external login was added.";
            return RedirectToPage();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}",e.Message);

            throw;
        }
    }
}