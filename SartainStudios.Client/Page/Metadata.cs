using MudBlazor;
using SartainStudios.Client.Schema;

namespace SartainStudios.Client.Page;

public static class Metadata
{
    public static class Account
    {
        public const string Route = "/account";
        public const string ProfileRoute = Route + "?tab=profile";
        public const string PasswordRoute = Route + "?tab=password";
        public const string ProvidersRoute = Route + "?tab=providers";
        public const string SignInRoute = "/sign-in";
        public const string ForgotPasswordRoute = "/forgot-password";
        public const string ResetPasswordRoute = "/reset-password";
        public static readonly PageInfo IndexInfo = new(Route, "Account", Icons.Material.Filled.ManageAccounts);
        
        public static readonly PageInfo SignIn = new(SignInRoute, "Sign In", Icons.Material.Filled.Login);
    }

    public static class BillingContract
    {
        public const string Route = "/billing-contracts";
        public const string Icon = Icons.Material.Filled.Description;
        public static readonly PageInfo Info = new(Route, "Billing Contracts", Icons.Material.Filled.Description);

        public static string Detail(string id)
        {
            return $"{Route}?id={id}";
        }

        public static string Edit(string id)
        {
            return $"{Route}?id={id}&edit=true";
        }
    }

    public static class Client
    {
        public const string Route = "/clients";
        public static readonly PageInfo Info = new(Route, "Clients", Icons.Material.Filled.Business);

        public static string Detail(string id)
        {
            return $"{Route}?id={id}";
        }

        public static string Edit(string id)
        {
            return $"{Route}?id={id}&edit=true";
        }
    }

    public static class Invoice
    {
        public const string Route = "/invoices";
        public static readonly PageInfo Info = new(Route, "Invoices", Icons.Material.Filled.Receipt);

        public static string Detail(string id)
        {
            return $"{Route}?id={id}";
        }

        public static string Edit(string id)
        {
            return $"{Route}?id={id}&edit=true";
        }
    }

    public static class Invoicing
    {
        public const string SectionIcon = Icons.Material.Filled.Schedule;
        private const string BaseRoute = "/invoicing";
        public const string DashboardRoute = BaseRoute + "/dashboard";
        private const string IndexName = "Dashboard";
        private const string IndexIcon = Icons.Material.Filled.Dashboard;
        public const string SessionsRoute = BaseRoute + "/sessions";
        public static readonly PageInfo DashboardInfo = new(DashboardRoute, IndexName, IndexIcon);
        public static readonly PageInfo Sessions = new(SessionsRoute, "Sessions", Icons.Material.Filled.ListAlt);

        public static string EditSession(string id)
        {
            return $"{SessionsRoute}?id={Uri.EscapeDataString(id)}&edit=true";
        }
    }

    public static class Legal
    {
        public const string PrivacyRoute = "/privacy";

        public static readonly PageInfo Privacy =
            new(PrivacyRoute, "Privacy Policy", Icons.Material.Filled.PrivacyTip);
    }

    public static class MainMenu
    {
        public const string IndexRoute = "/";

        public const string StayQueryParameter = "menu";

        public const string StayRoute = IndexRoute + "?" + StayQueryParameter + "=true";

        private const string IndexName = "Main Menu";
        private const string IndexIcon = Icons.Material.Filled.Dashboard;
        public static readonly PageInfo IndexInfo = new(IndexRoute, IndexName, IndexIcon);
    }

    public static class NotFound
    {
        public const string Route = "/not-found";
        public static readonly PageInfo Info = new(Route, "Not Found", Icons.Material.Filled.SearchOff);
    }

    public static class Organization
    {
        public const string Route = "/organization";
        private const string Name = "Organization";
        private const string Icon = Icons.Material.Filled.Groups;
        public static readonly PageInfo IndexInfo = new(Route, Name, Icon);
    }

    public static class Project
    {
        public const string Route = "/projects";
        public static readonly PageInfo Info = new(Route, "Projects", Icons.Material.Filled.Folder);

        public static string Detail(string id)
        {
            return $"{Route}?id={id}";
        }

        public static string Edit(string id)
        {
            return $"{Route}?id={id}&edit=true";
        }
    }

    public static class SystemHealth
    {
        public const string Route = "/system-health";
        public static readonly PageInfo Info = new(Route, "System Health", Icons.Material.Filled.HealthAndSafety);
    }

    public static class Tool
    {
        public const string RequestSoftwareRoute = "/request-software";
    }
}