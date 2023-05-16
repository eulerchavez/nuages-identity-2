using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Nuages.AspNetIdentity.Stores.Mongo;
using Nuages.Fido2.Storage.Mongo;
using Nuages.Identity.AWS;
using Nuages.Identity.Services;
using Nuages.Identity.Services.AspNetIdentity;
using Nuages.Identity.Services.Email.Sender;
using Nuages.Identity.Services.Fido2.AspNetIdentity;
using Nuages.Identity.Services.Fido2.Storage;
using Nuages.Identity.UI.OpenIdDict;
using Octokit;
using OpenIddict.Abstractions;

namespace Nuages.Identity.UI.Setup;

public static class IdentityExtensions
{
    public static void AddNuagesIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        var identityBuilder =
            services.AddNuagesAspNetIdentity<NuagesApplicationUser<string>, NuagesApplicationRole<string>, string>(
                identity =>
                {
                    identity.User = new UserOptions
                    {
                        RequireUniqueEmail = true /* Not the default*/
                    };

                    identity.ClaimsIdentity = new ClaimsIdentityOptions
                    {
                        RoleClaimType = OpenIddictConstants.Claims.Role,
                        UserNameClaimType = OpenIddictConstants.Claims.Name,
                        UserIdClaimType = OpenIddictConstants.Claims.Subject
                    };

                    identity.SignIn = new SignInOptions
                    {
                        RequireConfirmedEmail = true,
                        RequireConfirmedPhoneNumber = false, //MUST be false
                        RequireConfirmedAccount = false //MUST be false
                    };

                    configuration.GetSection("Nuages:Identity:Password").Bind(identity.Password);
                });

        var storage = Enum.Parse<StorageType>(configuration["Nuages:Data:Storage"]!);
        var connectionString = configuration["Nuages:Data:ConnectionString"]!;

        switch (storage)
        {
            case StorageType.SqlServer:
            {
                services.AddDbContext<NuagesIdentityDbContext>(options =>
                {
                    options
                        .UseSqlServer(connectionString);

                    options.UseOpenIddict();
                });

                identityBuilder.AddEntityFrameworkStores<NuagesIdentityDbContext>();

                break;
            }
            case StorageType.MySql:
            {
                services.AddDbContext<NuagesIdentityDbContext>(options =>
                {
                    options
                        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

                    options.UseOpenIddict();
                });
                identityBuilder.AddEntityFrameworkStores<NuagesIdentityDbContext>();


                break;
            }
            case StorageType.InMemory:
            {
                services
                    .AddDbContext<NuagesIdentityDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("Identity");

                        options.UseOpenIddict();
                    });

                identityBuilder.AddEntityFrameworkStores<NuagesIdentityDbContext>();

                break;
            }
            case StorageType.MongoDb:
            {
                identityBuilder.AddMongoStores<NuagesApplicationUser<string>, NuagesApplicationRole<string>, string>(
                    options =>
                    {
                        options.ConnectionString = connectionString;

                        if (!BsonClassMap.IsClassMapRegistered(typeof(NuagesApplicationUser<string>)))
                            BsonClassMap.RegisterClassMap<NuagesApplicationUser<string>>(cm =>
                            {
                                cm.AutoMap();
                                cm.SetIgnoreExtraElements(true);
                                cm.MapMember(c => c.LastFailedLoginReason)
                                    .SetSerializer(new EnumSerializer<FailedLoginReason>(BsonType.String));
                            });
                    });
                break;
            }
            default:
                throw new Exception("Invalid storage");
        }

        identityBuilder.AddNuagesIdentityServices(configuration, _ => { });

        var uri = new Uri(configuration["Nuages:Identity:Authority"]!);

        var fidoBuilder2 = identityBuilder.AddNuagesFido2(options =>
        {
            options.ServerDomain = uri.Host;
            options.ServerName = configuration["Nuages:Identity:Name"];
            options.Origins = new HashSet<string> { configuration["Nuages:Identity:Authority"]! };
            options.TimestampDriftTolerance = 300000;
        });

        identityBuilder.AddMessageService(configure =>
        {
            configure.SendFromEmail = configuration["Nuages:MessageService:SendFromEmail"];
            configure.DefaultCulture = configuration["Nuages:MessageService:DefaultCulture"];
        });


        services.AddScoped<IEmailMessageSender, MessageSender>();
        services.AddScoped<ISmsMessageSender, MessageSender>();

        switch (storage)
        {
            case StorageType.InMemory:
            {
                services.AddScoped<IFido2Storage, Fido2StorageEntityFramework<NuagesIdentityDbContext>>();
                break;
            }
            case StorageType.MongoDb:
            {
                fidoBuilder2.AddFido2MongoStorage(config =>
                {
                    config.ConnectionString = configuration["Nuages:Data:ConnectionString"]!;
                });
                break;
            }
            case StorageType.MySql:
            {
                services.AddScoped<IFido2Storage, Fido2StorageEntityFramework<NuagesIdentityDbContext>>();
                break;
            }
            case StorageType.SqlServer:
            {
                services.AddScoped<IFido2Storage, Fido2StorageEntityFramework<NuagesIdentityDbContext>>();
                break;
            }
            default:
                throw new Exception("Invalid storage");
        }

        var auth = services.AddNuagesAuthentication(configuration);

        AddGoogle(configuration, auth);
        AddGitHub(configuration, auth);
        AddTwitter(configuration, auth);
        AddFacebook(configuration, auth);
        AddMicrosoft(configuration, auth);

        
        services.AddUI(configuration);
        services.AddNuagesOpenIdDict(configuration);

        //Add at the end sot it overrides dependencies
        services.AddAWS(configuration, "templates.json");
    }

    private static void AddGoogle(IConfiguration configuration, AuthenticationBuilder auth)
    {
        if (!string.IsNullOrEmpty(configuration["Nuages:OpenIdProviders:Google:ClientId"]))
        {
            Console.WriteLine("Adding Google Auth Provider");

            auth.AddGoogle(googleOptions =>
            {
                googleOptions.ClientId = configuration["Nuages:OpenIdProviders:Google:ClientId"]!;
                googleOptions.ClientSecret = configuration["Nuages:OpenIdProviders:Google:ClientSecret"]!;
            });
        }
    }

    private static void AddGitHub(IConfiguration configuration, AuthenticationBuilder auth)
    {
        if (!string.IsNullOrEmpty(configuration["Nuages:OpenIdProviders:GitHub:ClientId"]))
        {
            Console.WriteLine("Adding GitHub Auth Provider");

            auth.AddGitHub(o =>
            {
                o.ClientId = configuration["Nuages:OpenIdProviders:GitHub:ClientId"]!;
                o.ClientSecret = configuration["Nuages:OpenIdProviders:GitHub:ClientSecret"]!;
                o.CallbackPath = "/signin-github";

                // Grants access to read a user's profile data.
                // https://docs.github.com/en/developers/apps/building-oauth-apps/scopes-for-oauth-apps
                o.Scope.Add("user:email"); //read:user 

                // Optional
                // if you need an access token to call GitHub Apis
                SetupGithubOnCreatingEvent(o);
            });
        }
    }

    [ExcludeFromCodeCoverage]
    private static void SetupGithubOnCreatingEvent(OAuthOptions o)
    {
        o.Events.OnCreatingTicket += async context =>
        {
            if (context.AccessToken is not null)
            {
                var github = new GitHubClient(new ProductHeaderValue("NuagesIdentity"))
                {
                    Credentials = new Credentials(context.AccessToken)
                };

                var emails = await github.User.Email.GetAll();


                context.Identity?.AddClaim(new Claim("email", emails.Single(e => e.Primary).Email));
            }
        };
    }

    private static void AddTwitter(IConfiguration configuration, AuthenticationBuilder auth)
    {
        if (!string.IsNullOrEmpty(configuration["Nuages:OpenIdProviders:Twitter:ConsumerKey"]))
        {
            Console.WriteLine("Adding Twitter Auth Provider");

            auth.AddTwitter(twitterOptions =>
            {
                twitterOptions.ConsumerKey = configuration["Nuages:OpenIdProviders:Twitter:ConsumerKey"];
                twitterOptions.ConsumerSecret = configuration["Nuages:OpenIdProviders:Twitter:ConsumerSecret"];

                twitterOptions.RetrieveUserDetails = true;
            });
        }
    }

    private static void AddFacebook(IConfiguration configuration, AuthenticationBuilder auth)
    {
        if (!string.IsNullOrEmpty(configuration["Nuages:OpenIdProviders:Facebook:AppId"]))
        {
            Console.WriteLine("Adding Facebook Auth Provider");

            auth.AddFacebook(facebookOptions =>
            {
                facebookOptions.AppId = configuration["Nuages:OpenIdProviders:Facebook:AppId"]!;
                facebookOptions.AppSecret = configuration["Nuages:OpenIdProviders:Facebook:AppSecret"]!;
            });
        }
    }

    private static void AddMicrosoft(IConfiguration configuration, AuthenticationBuilder auth)
    {
        if (!string.IsNullOrEmpty(configuration["Nuages:OpenIdProviders:Microsoft:ClientId"]))
        {
            Console.WriteLine("Adding Microsoft Auth Provider");

            auth.AddMicrosoftAccount(microsoftOptions =>
            {
                microsoftOptions.ClientId = configuration["Nuages:OpenIdProviders:Microsoft:ClientId"]!;
                microsoftOptions.ClientSecret = configuration["Nuages:OpenIdProviders:Microsoft:ClientSecret"]!;
            });
        }
    }
}