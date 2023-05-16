using System.Text.Json.Serialization;
using Microsoft.Extensions.Localization;
using Nuages.Identity.Services.AspNetIdentity;
using Nuages.Identity.Services.Email.Sender;
using Nuages.Web;
using Nuages.Web.Exceptions;

// ReSharper disable TemplateIsNotCompileTimeConstantProblem
// ReSharper disable InconsistentNaming

namespace Nuages.Identity.Services.Manage;

public class SendSMSVerificationCodeService : ISendSMSVerificationCodeService
{
    private readonly IStringLocalizer _localizer;
    private readonly ILogger<SendSMSVerificationCodeService> _logger;
    private readonly IRuntimeConfiguration _runtimeConfiguration;
    private readonly IIdentityEventBus _identityEventBus;
    private readonly IMessageService _sender;
    private readonly NuagesUserManager _userManager;

    public SendSMSVerificationCodeService(NuagesUserManager userManager, IMessageService sender,
        IStringLocalizer localizer,
        ILogger<SendSMSVerificationCodeService> logger, IRuntimeConfiguration runtimeConfiguration, IIdentityEventBus identityEventBus)
    {
        _userManager = userManager;
        _sender = sender;
        _localizer = localizer;
        _logger = logger;
        _runtimeConfiguration = runtimeConfiguration;
        _identityEventBus = identityEventBus;
    }

    public async Task<SendSMSVerificationCodeResultModel> SendCode(string userId, string phoneNumber)
    {
        ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(userId);
        ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(phoneNumber);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) throw new NotFoundException("UswerNotFound");

        phoneNumber = phoneNumber.Replace("+", "").Replace("+", " ").Replace("+", "-");

        var code = await _userManager.GenerateChangePhoneNumberTokenAsync(user, phoneNumber);

        var message = _localizer["changePhoneNumber:smsMessage", code];

        _logger.LogInformation("Message : {Message} No: {PhoneNumber}",message,phoneNumber);

        _sender.SendSms(phoneNumber, message);

        await _identityEventBus.PutEvent(IdentityEvents.SMSVerificationCodeSent, user);
        
        return new SendSMSVerificationCodeResultModel
        {
            Code = _runtimeConfiguration.IsTest ? code : null,
            Success = true
        };
    }
}

public interface ISendSMSVerificationCodeService
{
    // ReSharper disable once UnusedMemberInSuper.Global
    Task<SendSMSVerificationCodeResultModel> SendCode(string userId, string phoneNumber);
}

public class SendSMSVerificationCodeResultModel
{
    public bool Success { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }

    // ReSharper disable once CollectionNeverQueried.Global
    public List<string> Errors { get; set; } = new();
}

public class SendSMSVerificationCodeModel
{
    public string PhoneNumber { get; set; } = string.Empty;
}