using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Nuages.Identity.Services.AspNetIdentity;
using Nuages.Identity.Services.Email;

// ReSharper disable TemplateIsNotCompileTimeConstantProblem
// ReSharper disable InconsistentNaming

namespace Nuages.Identity.Services.Login;

public class SMSCodeService : ISMSCodeService
{
    private readonly NuagesUserManager _userManager;
    private readonly NuagesSignInManager _signInManager;
    private readonly IMessageService _sender;
    private readonly IStringLocalizer _localizer;
    private readonly ILogger<SMSCodeService> _logger;
    private readonly NuagesIdentityOptions _options;

    public SMSCodeService(NuagesUserManager userManager, NuagesSignInManager signInManager, IMessageService sender, 
        IOptions<NuagesIdentityOptions> options,
                    IStringLocalizer localizer, ILogger<SMSCodeService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _sender = sender;
        _localizer = localizer;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<SendSMSCodeResultModel> SendCode()
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user == null)
        {
            throw new InvalidOperationException("Unable to load two-factor authentication user.");
        }

        return await SendCode(user.Id);
    }
    
    public async Task<SendSMSCodeResultModel> SendCode(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new SendSMSCodeResultModel
            {
                Success = true //Fake success
            };
        }

        if (string.IsNullOrEmpty(user.PhoneNumber) || !user.PhoneNumberConfirmed)
        {
            return new SendSMSCodeResultModel
            {
                Success = true //Fake success
            };
        }

        var code = await _userManager.GenerateTwoFactorTokenAsync(user, "Phone");

        var message = _localizer["passwordless:message", code, _options.Name];

        _logger.LogInformation($"Message : {message} No: {user.PhoneNumber}");
        
        _sender.SendSms(user.PhoneNumber, message);
        
        return new SendSMSCodeResultModel
        {
            Success = true
        };
    }
}

public interface ISMSCodeService
{
    // ReSharper disable once UnusedMemberInSuper.Global
    Task<SendSMSCodeResultModel> SendCode(string userId);
    Task<SendSMSCodeResultModel> SendCode();
}

public class SendSMSCodeResultModel
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
}
