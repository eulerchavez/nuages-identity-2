﻿using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

// ReSharper disable ClassNeverInstantiated.Global

namespace Nuages.Identity.Services.AspNetIdentity;

public class NuagesUserManager : UserManager<NuagesApplicationUser> 
{
    public NuagesUserManager(IUserStore<NuagesApplicationUser> store, 
        IOptions<IdentityOptions> optionsAccessor, 
        IPasswordHasher<NuagesApplicationUser> passwordHasher, 
        IEnumerable<IUserValidator<NuagesApplicationUser>> userValidators, 
        IEnumerable<IPasswordValidator<NuagesApplicationUser>> passwordValidators, 
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors, IServiceProvider services, 
        ILogger<UserManager<NuagesApplicationUser>> logger) : 
        base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
    {
    }
    
    public override async Task<IdentityResult> CreateAsync(NuagesApplicationUser user)
    {
        var res = await base.CreateAsync(user);
       
        if (res.Succeeded)
        {
            user.CreatedOn = DateTime.UtcNow;
            user.LastPasswordChangedDate = user.CreatedOn;
            
            await UpdateAsync(user);
        }

        return res;
    }

    public async Task<NuagesApplicationUser?> FindAsync(string userNameOrEmail)
    {
        var user = await FindByNameAsync(userNameOrEmail);
        
        // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
        if (user == null)
            user = await FindByEmailAsync(userNameOrEmail);

        return user;
    }

    protected override async Task<IdentityResult> UpdatePasswordHash(NuagesApplicationUser user, string newPassword, bool validatePassword)
    {
        var res = await base.UpdatePasswordHash(user, newPassword, validatePassword);

        if (res.Succeeded)
        {
            user.LastPasswordChangedDate = DateTime.UtcNow;
            await UpdateAsync(user);
        }

        return res;
    }

    public async Task<List<string>> GetRecoveryCodes(NuagesApplicationUser user)
    {
        var recoveryCode = await GetAuthenticationTokenAsync(user, "[AspNetUserStore]", "RecoveryCodes");
        return recoveryCode?.Split(";").ToList() ?? new List<string>();
    }
}

