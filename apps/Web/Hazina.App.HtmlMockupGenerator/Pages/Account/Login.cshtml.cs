using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HtmlMockupGenerator.Pages.Account;

public class LoginModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Page("/Index")
        };
        
        return Challenge(properties, "Google");
    }
} 