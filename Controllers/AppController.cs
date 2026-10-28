namespace LibraryManagement.Controllers;

public abstract class AppController : Controller
{
    protected string CurrentUser => User.Identity?.Name ?? "unknown";
}
