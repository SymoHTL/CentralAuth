namespace AuthApi.Controllers;

public abstract class IdentityController(
    UserManager<AppUser> userManager,
    LinkGenerator linkGenerator,
    IEmailSender<AppUser> emailSender)
    : AdvancedController {
    protected readonly UserManager<AppUser> UserManager = userManager;
    protected readonly IEmailSender<AppUser> EmailSender = emailSender;

    protected async Task SendConfirmationEmailAsync(AppUser user, string email,
        string confirmEmailEndpoint = "ConfirmEmail", bool isChange = false) {
        var code = isChange
            ? await UserManager.GenerateChangeEmailTokenAsync(user, email)
            : await UserManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var userId = await UserManager.GetUserIdAsync(user);
        var routeValues = new RouteValueDictionary {
            ["userId"] = userId,
            ["code"] = code,
        };

        if (isChange) routeValues.Add("changedEmail", email);

        var confirmEmailUrl = linkGenerator.GetUriByName(HttpContext, confirmEmailEndpoint, routeValues);

        if (confirmEmailUrl is null)
            throw new InvalidOperationException("The link generator failed to generate a confirmation email URL.");

        await EmailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(confirmEmailUrl));
    }
}