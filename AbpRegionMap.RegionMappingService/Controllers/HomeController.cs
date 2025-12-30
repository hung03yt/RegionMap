using Volo.Abp;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace AbpRegionMap.RegionMappingService.Controllers;

[RemoteService(false)]
public class HomeController : AbpController
{
    public ActionResult Index()
    {
        return Redirect("~/swagger");
    }
}