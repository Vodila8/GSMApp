using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator,Technician")]
public class DeviceDiagnosticsModel : PageModel
{
    public void OnGet() { }
}
