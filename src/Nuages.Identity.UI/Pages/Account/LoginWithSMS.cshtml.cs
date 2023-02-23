// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Nuages.Identity.Services.AspNetIdentity;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global

namespace Nuages.Identity.UI.Pages.Account;

// ReSharper disable once InconsistentNaming
public class SMSLoginModel : PageModel
{
    private readonly NuagesSignInManager _signInManager;
    private readonly ILogger<SMSLoginModel> _logger;

    public SMSLoginModel(
        NuagesSignInManager signInManager, ILogger<SMSLoginModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    public string ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(string returnUrl = null)
    {
        try
        {
            // Ensure the user has gone through the username & password screen first
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null) throw new InvalidOperationException("Unable to load two-factor authentication user.");

            ReturnUrl = returnUrl ?? Url.Content("~/");

            return Page();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}", e.Message);

            throw;
        }
    }
}