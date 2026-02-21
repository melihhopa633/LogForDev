using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LogForDev.Data;
using LogForDev.Services;
using LogForDev.Models;
using LogForDev.Authentication;

namespace LogForDev.Controllers;

[Authorize(AuthenticationSchemes = CookieAuthenticationOptions.Scheme)]
public class HomeController : Controller
{
    private readonly ILogRepository _logRepository;
    private readonly IProjectService _projectService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogRepository logRepository, IProjectService projectService, ILogger<HomeController> logger)
    {
        _logRepository = logRepository;
        _projectService = projectService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var stats = await _logRepository.GetStatsAsync(cancellationToken);
            ViewBag.Stats = stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard");
            ViewBag.Stats = new LogStats();
        }

        try
        {
            ViewBag.Projects = await _projectService.GetAllProjectsAsync();
        }
        catch
        {
            ViewBag.Projects = new List<Project>();
        }

        return View();
    }

    public async Task<IActionResult> Projects(CancellationToken cancellationToken)
    {
        try
        {
            ViewBag.Projects = await _projectService.GetAllProjectsAsync();
        }
        catch
        {
            ViewBag.Projects = new List<Project>();
        }

        return View();
    }

    public IActionResult Docs()
    {
        return View();
    }

    public IActionResult AppLogs()
    {
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}
