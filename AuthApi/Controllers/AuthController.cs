namespace AuthApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    UserManager<AppUser> userManager,
    IUserStore<AppUser> userStore,
    IUserEmailStore<AppUser> emailStore,
    IEmailSender<AppUser> emailSender,
    SignInManager<AppUser> signInManager,
    IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
    TimeProvider timeProvider,
    LinkGenerator linkGenerator)
    : IdentityController(userManager, linkGenerator, emailSender) {
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registration, CancellationToken ct) {
        if (!UserManager.SupportsUserEmail)
            throw new NotSupportedException($"{nameof(AuthController)} requires a user store with email support.");
        registration.Sanitize();

        var user = new AppUser();
        await userStore.SetUserNameAsync(user, registration.Username, ct);
        await emailStore.SetEmailAsync(user, registration.Email, ct);
        var result = await UserManager.CreateAsync(user, registration.Password);

        if (!result.Succeeded)
            return ValidationProblem(new ValidationProblemDetails(result.Errors
                .ToDictionary(e => e.Code, e => new[] { e.Description })));

        await SendConfirmationEmailAsync(user, registration.Email);
        return Ok();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest login, [FromQuery] bool? useCookies) {
        login.Sanitize();
        var isPersistent = useCookies == true;
        signInManager.AuthenticationScheme =
            useCookies == true ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;

        var user = await UserManager
            .FindByEmailAsync(login.Email); // we log in with email, in Program.cs it is configured that email is unique

        if (user is null)
            return Problem("Invalid email and/or password.", statusCode: StatusCodes.Status401Unauthorized);

        var result = await signInManager.PasswordSignInAsync(user, login.Password, isPersistent,
            lockoutOnFailure: true);

        if (result.RequiresTwoFactor) {
            if (!string.IsNullOrEmpty(login.TwoFactorCode))
                result = await signInManager.TwoFactorAuthenticatorSignInAsync(login.TwoFactorCode, isPersistent,
                    rememberClient: isPersistent);
            else if (!string.IsNullOrEmpty(login.TwoFactorRecoveryCode))
                result = await signInManager.TwoFactorRecoveryCodeSignInAsync(login.TwoFactorRecoveryCode);
        }

        if (!result.Succeeded)
            return Problem(result.ToString(), statusCode: StatusCodes.Status401Unauthorized);

        return Empty;
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest refreshRequest) {
        refreshRequest.Sanitize();
        var refreshTokenProtector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);

        // Reject the /refresh attempt with a 401 if the token expired or the security stamp validation fails
        if (refreshTicket?.Properties.ExpiresUtc is not { } expiresUtc ||
            timeProvider.GetUtcNow() >= expiresUtc ||
            await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not { } user)
            return Challenge();

        var newPrincipal = await signInManager.CreateUserPrincipalAsync(user);
        return SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    [HttpGet("confirmEmail")]
    public async Task<IActionResult> ConfirmEmail([FromQuery, Required] string userId,
        [FromQuery, Required] string code, [FromQuery] string? changedEmail) {
        userId = userId.Trim();
        code = code.Trim();

        if (await UserManager.FindByIdAsync(userId) is not { } user)
            // We could respond with a 404 instead of a 401 like Identity UI, but that feels like unnecessary information.
            return Unauthorized();

        try {
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException) {
            return Unauthorized();
        }

        IdentityResult result;

        if (string.IsNullOrEmpty(changedEmail))
            result = await UserManager.ConfirmEmailAsync(user, code);
        else
            result = await UserManager.ChangeEmailAsync(user, changedEmail, code);

        if (!result.Succeeded)
            return Unauthorized();

        return Text("Thank you for confirming your email.");
    }

    [HttpPost("resendConfirmationEmail")]
    public async Task<IActionResult> ResendConfirmationEmail([FromQuery, Required, EmailAddress] string email) {
        email = email.Trim();
        if (await UserManager.FindByEmailAsync(email) is not { } user)
            return Ok(); // return ok so that the user can't guess if the email is registered or not

        await SendConfirmationEmailAsync(user, email);
        return Ok();
    }

    [HttpPost("forgotPassword")]
    public async Task<IActionResult> ForgotPassword([FromQuery, Required, EmailAddress] string email) {
        email = email.Trim();
        var user = await UserManager.FindByEmailAsync(email);

        if (user is null || !await UserManager.IsEmailConfirmedAsync(user)) return Ok();


        var code = await UserManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        await EmailSender.SendPasswordResetCodeAsync(user, email, HtmlEncoder.Default.Encode(code));

        return Ok();
    }


    [HttpPost("resetPassword")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest resetRequest) {
        resetRequest.Sanitize();
        var user = await UserManager.FindByEmailAsync(resetRequest.Email);

        if (user is null || !await UserManager.IsEmailConfirmedAsync(user))
            // Don't reveal that the user does not exist or is not confirmed, so don't return a 200 if we would have
            // returned a 400 for an invalid code given a valid user email.
            return ValidationProblem(IdentityResult.Failed(UserManager.ErrorDescriber.InvalidToken()));

        IdentityResult result;
        try {
            var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetRequest.ResetCode));
            result = await UserManager.ResetPasswordAsync(user, code, resetRequest.NewPassword);
        }
        catch (FormatException) {
            result = IdentityResult.Failed(UserManager.ErrorDescriber.InvalidToken());
        }

        if (!result.Succeeded)
            return ValidationProblem(result.Errors);

        return Ok();
    }
}