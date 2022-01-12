

using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using MongoDB.Bson;
using Nuages.Identity.Services.AspNetIdentity;
using Nuages.Sender.API.Sdk;

namespace Nuages.Identity.Services;

public class RegisterService : IRegisterService
{
    private readonly NuagesUserManager _userManager;
    private readonly NuagesSignInManager _signInManager;
    private readonly IStringLocalizer _localizer;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _env;
    private readonly IEmailSender _emailSender;

    public RegisterService(NuagesUserManager userManager, NuagesSignInManager signInManager, IStringLocalizer localizer, IHttpContextAccessor httpContextAccessor, IWebHostEnvironment env, IEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _localizer = localizer;
        _httpContextAccessor = httpContextAccessor;
        _env = env;
        _emailSender = emailSender;
    }
    
    public async Task<RegisterResultModel> Register(RegisterModel model)
    {
        if (model.Password != model.PasswordConfirm)
        {
            return new RegisterResultModel
            {
                Success = false,
                Message = _localizer["register.passwordDoesNotMatch"]
            };
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null)
        {
            return new RegisterResultModel
            {
                Success = false,
                Message = _localizer["register.userEmailAlreadyExists"]
            };
        }

        user = new NuagesApplicationUser
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Email = model.Email,
            UserName = model.Email
        };
        
        var res = await _userManager.CreateAsync(user, model.Password);

        if (res.Succeeded)
        {
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var scheme = _httpContextAccessor.HttpContext!.Request.Scheme;
            var host = _httpContextAccessor.HttpContext.Request.Host.Host;
            if (_env.IsDevelopment())
                host += ":" + _httpContextAccessor.HttpContext!.Request.Host.Port;
        
            var url =
                $"{scheme}://{host}/Account/ConfirmEmail?code={code}&userId={user.Id}";

            await _emailSender.SendEmailUsingTemplateAsync(model.Email, "Confirm_Email", new Dictionary<string, string>
            {
                { "Link", url }
            });
        
            if (_userManager.Options.SignIn.RequireConfirmedEmail)
            {
                return new RegisterResultModel
                {
                    ShowConfirmationMessage = true,
                    Success = true
                };
            }
            
            await _signInManager.SignInAsync(user, isPersistent: false);

            return new RegisterResultModel
            {
                Success = true
            };
        }

        return new RegisterResultModel
        {
            Success = false,
            Message = _localizer[$"identity.{res.Errors.First().Code}"]
        };
    }
}

public interface IRegisterService
{
    Task<RegisterResultModel> Register(RegisterModel model);
}

public class RegisterModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PasswordConfirm { get; set; } = string.Empty;
    public string? RecaptchaToken { get; set; }
}
public class RegisterResultModel
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool ShowConfirmationMessage { get; set; }
}