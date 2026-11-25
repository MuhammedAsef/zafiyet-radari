using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZafiyetRadari.Application.Dashboard;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Web.Models;

namespace ZafiyetRadari.Web.Controllers;

public sealed class HomeController(IDashboardService dashboard, IAssetGroupService groups) : AppControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(int? groupId, CancellationToken cancellationToken)
    {
        var result = await dashboard.GetAsync(groupId, cancellationToken);
        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? Forbid();
        }

        var myGroups = await groups.ListMyGroupsAsync(cancellationToken);

        return View(new DashboardViewModel
        {
            Data = result.Value,
            Groups = myGroups.IsSuccess ? myGroups.Value : []
        });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));

    [AllowAnonymous]
    [Route("Home/Status/{code:int}")]
    public IActionResult Status(int code) => View("StatusCode", code);
}
