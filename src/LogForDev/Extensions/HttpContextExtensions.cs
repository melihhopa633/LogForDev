using LogForDev.Models;

namespace LogForDev.Extensions;

public static class HttpContextExtensions
{
    public static Project? GetProject(this HttpContext context)
    {
        return context.Items.TryGetValue("Project", out var project) ? project as Project : null;
    }
}
