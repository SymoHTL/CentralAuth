namespace AuthApi.Controllers;

public abstract class AdvancedController : ControllerBase {
    protected ContentResult Html(string html) => Content(html, "text/html");
    protected ContentResult Text(string text) => Content(text, "text/plain");

    protected ActionResult ValidationProblem(string message, string description) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> {
            { message, [description] }
        }));

    protected ActionResult ValidationProblem(IdentityResult result) =>
        ValidationProblem(
            new ValidationProblemDetails(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));

    protected ActionResult ValidationProblem(IEnumerable<IdentityError> errors) =>
        ValidationProblem(new ValidationProblemDetails(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
}