using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Security.Principal;
using System.Threading;

namespace BE4_LUUTHANHQUAN
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_PostAuthenticateRequest()
        {
            var context = HttpContext.Current;
            if (context == null || context.User == null || !context.Request.IsAuthenticated) return;

            var cookie = context.Request.Cookies[FormsAuthentication.FormsCookieName];
            if (cookie == null || String.IsNullOrEmpty(cookie.Value)) return;

            try
            {
                var ticket = FormsAuthentication.Decrypt(cookie.Value);
                if (ticket == null || ticket.Expired) return;

                var role = (ticket.UserData ?? String.Empty).Trim();
                string[] roles = (role == "A" || role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    ? new[] { "A", "Admin" }
                    : new[] { "C", "Customer" };

                var identity = new FormsIdentity(ticket);
                var principal = new GenericPrincipal(identity, roles);
                context.User = principal;
                Thread.CurrentPrincipal = principal;
            }
            catch
            {
                FormsAuthentication.SignOut();
            }
        }
    }
}
