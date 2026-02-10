using Microsoft.AspNetCore.Mvc;
using LogForDev.Services;
using LogForDev.Models;

namespace LogForDev.Controllers;

public class HomeController : Controller
{
    private readonly ILogService _logService;
    private readonly IProjectService _projectService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogService logService, IProjectService projectService, ILogger<HomeController> logger)
    {
        _logService = logService;
        _projectService = projectService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var stats = await _logService.GetStatsAsync();
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

    public async Task<IActionResult> Projects()
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
