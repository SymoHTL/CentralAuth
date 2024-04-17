namespace AuthApi.Controllers;

[Authorize]
[ApiController]
[Route("me")]
public class ManageUserController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    LinkGenerator linkGenerator,
    IEmailSender<AppUser> emailSender) : IdentityController(userManager, linkGenerator, emailSender) {
    [HttpPost("manage/2fa")]
    public async Task<IActionResult> CHange2FA([FromBody] TwoFactorRequest request,
        ClaimsPrincipal claimsPrincipal) {
        request.Sanitize();
        if (await UserManager.GetUserAsync(claimsPrincipal) is not { } user)
            return NotFound();

        if (request.Enable == true) {
            if (request.ResetSharedKey)
                return ValidationProblem("TwoFactorRequest.CannotResetSharedKeyAndEnable",
                    "Resetting the 2fa shared key must disable 2fa until a 2fa token based on the new shared key is validated.");

            if (string.IsNullOrEmpty(request.TwoFactorCode))
                return ValidationProblem("TwoFactorRequest.RequiresTwoFactor",
                    "No 2fa token was provided by the request. A valid 2fa token is required to enable 2fa.");

            if (!await UserManager.VerifyTwoFactorTokenAsync(user,
                    UserManager.Options.Tokens.AuthenticatorTokenProvider, request.TwoFactorCode))
                return ValidationProblem("TwoFactorRequest.InvalidTwoFactorCode",
                    "The 2fa token provided by the request was invalid. A valid 2fa token is required to enable 2fa.");

            await UserManager.SetTwoFactorEnabledAsync(user, true);
        }
        else if (request.Enable == false || request.ResetSharedKey) {
            await UserManager.SetTwoFactorEnabledAsync(user, false);
        }

        if (request.ResetSharedKey)
            await UserManager.ResetAuthenticatorKeyAsync(user);


        string[]? recoveryCodes = null;
        if (request.ResetRecoveryCodes ||
            (request.Enable == true && await UserManager.CountRecoveryCodesAsync(user) == 0)) {
            var recoveryCodesEnumerable = await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            recoveryCodes = recoveryCodesEnumerable?.ToArray();
        }

        if (request.ForgetMachine)
            await signInManager.ForgetTwoFactorClientAsync();

        var key = await UserManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key)) {
            await UserManager.ResetAuthenticatorKeyAsync(user);
            key = await UserManager.GetAuthenticatorKeyAsync(user);

            if (string.IsNullOrEmpty(key))
                throw new NotSupportedException("The user manager must produce an authenticator key after reset.");
        }

        return Ok(new TwoFactorResponse {
            SharedKey = key,
            RecoveryCodes = recoveryCodes,
            RecoveryCodesLeft = recoveryCodes?.Length ?? await UserManager.CountRecoveryCodesAsync(user),
            IsTwoFactorEnabled = await UserManager.GetTwoFactorEnabledAsync(user),
            IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user),
        });
    }


    [HttpGet]
    public async Task<ActionResult<UserInfoDto>> GetMe() {
        if (await UserManager.GetUserAsync(HttpContext.User) is not { } user) return NotFound();
        // general claims
        var claims = (await UserManager.GetClaimsAsync(user))
            .Select(claim => new ClaimDto { Type = claim.Type, Value = claim.Value })
            .ToList();
        // roles
        claims.AddRange((await UserManager.GetRolesAsync(user))
            .Select(role => new ClaimDto { Type = ClaimTypes.Role, Value = role }));

        // add username and email claims
        claims.Add(new ClaimDto {
            Type = ClaimTypes.Name,
            Value = user.UserName ?? user.Email ?? "Unknown"
        });
        claims.Add(new ClaimDto {
            Type = ClaimTypes.Email,
            Value = user.Email ?? "Unknown"
        });


        return Ok(new UserInfoDto {
            IsEmailConfirmed = user.EmailConfirmed,
            ClaimDtos = claims
        });
    }


    [HttpPost]
    public async Task<ActionResult<UserInfoDto>> ChangeMe([FromBody] InfoRequest infoRequest) {
        if (await UserManager.GetUserAsync(HttpContext.User) is not { } user)
            return NotFound();

        if (!string.IsNullOrEmpty(infoRequest.NewPassword)) {
            if (string.IsNullOrEmpty(infoRequest.OldPassword))
                return ValidationProblem("ChangeMe.OldPasswordRequired",
                    "The old password is required to set a new password. If the old password is forgotten, use /resetPassword.");

            var changePasswordResult =
                await UserManager.ChangePasswordAsync(user, infoRequest.OldPassword, infoRequest.NewPassword);
            if (!changePasswordResult.Succeeded)
                return ValidationProblem(changePasswordResult.Errors);
        }

        if (!string.IsNullOrEmpty(infoRequest.NewEmail)) {
            var email = await UserManager.GetEmailAsync(user);

            if (email != infoRequest.NewEmail)
                await SendConfirmationEmailAsync(user, infoRequest.NewEmail, isChange: true);
        }

        return Ok(GetMe());
    }
}