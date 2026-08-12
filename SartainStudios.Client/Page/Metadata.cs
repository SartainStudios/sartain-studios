using MudBlazor;
using SartainStudios.Client.Schema;

namespace SartainStudios.Client.Page;

public static class Metadata
{
    public static class Invoicing
    {
        public const string SectionIcon = Icons.Material.Filled.Schedule;
        private const string BaseRoute = "/invoicing";
        public const string DashboardRoute = BaseRoute + "/dashboard";
        public const string IndexName = "Dashboard";
        public const string IndexIcon = Icons.Material.Filled.Dashboard;
        public const string SessionsRoute = BaseRoute + "/sessions";
        public const string EditSessionRoute = BaseRoute + "/sessions/{Id}/edit";
        public const string HowItWorksRoute = BaseRoute + "/how-it-works";
        public static readonly PageInfo DashboardInfo = new(DashboardRoute, IndexName, IndexIcon);
        public static readonly PageInfo Sessions = new(SessionsRoute, "Sessions", Icons.Material.Filled.ListAlt);

        public static string EditSession(string id)
        {
            return $"{SessionsRoute}?id={Uri.EscapeDataString(id)}&edit=true";
        }
    }

    public static class MainMenu
    {
        public const string IndexRoute = "/";
        private const string IndexName = "Main Menu";
        private const string IndexIcon = Icons.Material.Filled.Dashboard;
        public static readonly PageInfo IndexInfo = new(IndexRoute, IndexName, IndexIcon);
    }

    public static class Organization
    {
        public const string Route = "/organization";
        public const string DetailsRoute = Route + "?tab=details";
        public const string MembersRoute = Route + "?tab=members";
        public const string CreateRoute = Route + "?new=true";
        private const string Name = "Organization";
        private const string Icon = Icons.Material.Filled.Groups;
        public static readonly PageInfo IndexInfo = new(Route, Name, Icon);
    }

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
        public static readonly PageInfo Profile = new(ProfileRoute, "Profile", Icons.Material.Filled.Person);
        public static readonly PageInfo Password = new(PasswordRoute, "Password", Icons.Material.Filled.Password);

        public static readonly PageInfo
            Providers = new(ProvidersRoute, "Sign-in methods", Icons.Material.Filled.VpnKey);

        public static readonly PageInfo SignIn = new(SignInRoute, "Sign In", Icons.Material.Filled.Login);

        public static readonly PageInfo ForgotPassword =
            new(ForgotPasswordRoute, "Forgot Password", Icons.Material.Filled.LockReset);

        public static readonly PageInfo ResetPassword =
            new(ResetPasswordRoute, "Reset Password", Icons.Material.Filled.LockReset);
    }

    public static class Home
    {
        public const string IndexRoute = "/";
        public static readonly PageInfo Info = new(IndexRoute, "Home", Icons.Material.Filled.Home);
    }

    public static class HomeOld
    {
        public const string IndexRoute = "/home-old";
        public static readonly PageInfo Info = new(IndexRoute, "Home (Old)", Icons.Material.Filled.Home);
    }

    public static class MainMenuOld
    {
        public const string IndexRoute = "/main-menu-old";
        public static readonly PageInfo Info = new(IndexRoute, "Main Menu (Old)", Icons.Material.Filled.Dashboard);
    }

    public static class NotFound
    {
        public const string Route = "/not-found";
        public static readonly PageInfo Info = new(Route, "Not Found", Icons.Material.Filled.SearchOff);
    }

    public static class Client
    {
        public const string Route = "/clients";
        public const string CreateRoute = Route + "?new=true";
        public static readonly PageInfo Info = new(Route, "Clients", Icons.Material.Filled.Business);
        public static readonly PageInfo Create = new(CreateRoute, "New Client", Icons.Material.Filled.Add);

        public static string Detail(string id)
        {
            return $"{Route}?id={id}";
        }

        public static string Edit(string id)
        {
            return $"{Route}?id={id}&edit=true";
        }
    }

    public static class Project
    {
        public const string Route = "/projects";
        public const string IndexRoute = Route;
        public const string CreateRoute = Route + "?new=true";
        public static readonly PageInfo Info = new(Route, "Projects", Icons.Material.Filled.Folder);
        public static readonly PageInfo Create = new(CreateRoute, "New Project", Icons.Material.Filled.Add);

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
        public const string CreateRoute = "/invoices/create";
        public const string DetailRoute = "/invoices/{Id}";
        public const string EditRoute = "/invoices/{Id}/edit";
        public static readonly PageInfo Info = new(Route, "Invoices", Icons.Material.Filled.Receipt);
        public static readonly PageInfo Create = new(CreateRoute, "New Invoice", Icons.Material.Filled.Add);

        public static string Detail(string id)
        {
            return $"{Route}?id={id}";
        }

        public static string Edit(string id)
        {
            return $"{Route}?id={id}&edit=true";
        }
    }

    public static class BillingContract
    {
        public const string Route = "/billing-contracts";
        public const string CreateRoute = Route + "?new=true";
        public const string Icon = Icons.Material.Filled.Description;
        public static readonly PageInfo Info = new(Route, "Billing Contracts", Icons.Material.Filled.Description);
        public static readonly PageInfo Create = new(CreateRoute, "New Contract", Icons.Material.Filled.Add);

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

    public static class Legal
    {
        public const string PrivacyRoute = "/privacy";

        public static readonly PageInfo Privacy =
            new(PrivacyRoute, "Privacy Policy", Icons.Material.Filled.PrivacyTip);
    }

    public static class Tool
    {
        public const string TaxCalculatorRoute = "/tax-calculator";
        public const string ProfitCalculatorRoute = "/profit-calculator";
        public const string RequestSoftwareRoute = "/request-software";
        public const string AvailableAppsRoute = "/available-apps";

        public static readonly PageInfo TaxCalculator =
            new(TaxCalculatorRoute, "Tax Calculator", Icons.Material.Filled.Calculate);

        public static readonly PageInfo ProfitCalculator =
            new(ProfitCalculatorRoute, "Profit Calculator", Icons.Material.Filled.TrendingUp);

        public static readonly PageInfo RequestSoftware =
            new(RequestSoftwareRoute, "Request Software", Icons.Material.Filled.Build);
    }
}