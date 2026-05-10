using Microsoft.AspNetCore.Hosting;

namespace Bebochka.Api.Helpers;

public static class AppPaths
{
    /// <summary>
    /// Единый каталог wwwroot для статики и загрузок (совпадает с IWebHostEnvironment, без Directory.GetCurrentDirectory()).
    /// </summary>
    public static string WwwRoot(IWebHostEnvironment env)
    {
        if (!string.IsNullOrEmpty(env.WebRootPath))
            return env.WebRootPath;
        return Path.Combine(env.ContentRootPath, "wwwroot");
    }
}
