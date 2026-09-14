using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Application.Validators;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Infrastructure;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Services;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;
using FinanceApp.Mobile.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceApp.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        var configuration = configBuilder.Build();
        builder.Configuration.AddConfiguration(configuration);

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Infrastructure Services
        builder.Services.AddInfrastructure(builder.Configuration);

        // Application Services
        builder.Services.AddScoped<IAccountService, AccountService>();
        builder.Services.AddScoped<ICategoryService, CategoryService>();
        builder.Services.AddScoped<ITransactionService, TransactionService>();
        builder.Services.AddScoped<IBudgetService, BudgetService>();
        builder.Services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
        builder.Services.AddScoped<IFinancialGoalService, FinancialGoalService>();
        builder.Services.AddScoped<IDashboardService, DashboardService>();
        builder.Services.AddScoped<ISyncService, SyncService>();
        builder.Services.AddScoped<IPredictionService, PredictionService>();

        // Validators
        builder.Services.AddScoped<CreateAccountDtoValidator>();
        builder.Services.AddScoped<UpdateAccountDtoValidator>();
        builder.Services.AddScoped<CreateCategoryDtoValidator>();
        builder.Services.AddScoped<UpdateCategoryDtoValidator>();
        builder.Services.AddScoped<CreateTransactionDtoValidator>();
        builder.Services.AddScoped<UpdateTransactionDtoValidator>();
        builder.Services.AddScoped<TransactionFilterDtoValidator>();
        builder.Services.AddScoped<CreateBudgetDtoValidator>();
        builder.Services.AddScoped<UpdateBudgetDtoValidator>();
        builder.Services.AddScoped<CreateRecurringTransactionDtoValidator>();
        builder.Services.AddScoped<UpdateRecurringTransactionDtoValidator>();
        builder.Services.AddScoped<CreateFinancialGoalDtoValidator>();
        builder.Services.AddScoped<UpdateFinancialGoalDtoValidator>();
        builder.Services.AddScoped<GoalProgressDtoValidator>();

        // Mobile Services
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<TransactionsViewModel>();
        builder.Services.AddTransient<AddTransactionViewModel>();
        builder.Services.AddTransient<AccountsViewModel>();
        builder.Services.AddTransient<AddAccountViewModel>();
        builder.Services.AddTransient<CategoriesViewModel>();
        builder.Services.AddTransient<AddCategoryViewModel>();
        builder.Services.AddTransient<BudgetsViewModel>();
        builder.Services.AddTransient<AddBudgetViewModel>();
        builder.Services.AddTransient<GoalsViewModel>();
        builder.Services.AddTransient<AddGoalViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Pages
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<TransactionsPage>();
        builder.Services.AddTransient<AddTransactionPage>();
        builder.Services.AddTransient<AccountsPage>();
        builder.Services.AddTransient<AddAccountPage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<AddCategoryPage>();

        // Routing
        Routing.RegisterRoute("Dashboard", typeof(DashboardPage));
        Routing.RegisterRoute("Transactions", typeof(TransactionsPage));
        Routing.RegisterRoute("AddTransaction", typeof(AddTransactionPage));
        Routing.RegisterRoute("EditTransaction", typeof(AddTransactionPage));
        Routing.RegisterRoute("Accounts", typeof(AccountsPage));
        Routing.RegisterRoute("AddAccount", typeof(AddAccountPage));
        Routing.RegisterRoute("EditAccount", typeof(AddAccountPage));
        Routing.RegisterRoute("Categories", typeof(CategoriesPage));
        Routing.RegisterRoute("AddCategory", typeof(AddCategoryPage));
        Routing.RegisterRoute("EditCategory", typeof(AddCategoryPage));
        Routing.RegisterRoute("Budgets", typeof(BudgetsPage));
        Routing.RegisterRoute("AddBudget", typeof(AddBudgetPage));
        Routing.RegisterRoute("EditBudget", typeof(AddBudgetPage));
        Routing.RegisterRoute("Goals", typeof(GoalsPage));
        Routing.RegisterRoute("AddGoal", typeof(AddGoalPage));
        Routing.RegisterRoute("EditGoal", typeof(AddGoalPage));
        Routing.RegisterRoute("Settings", typeof(SettingsPage));
        Routing.RegisterRoute("Login", typeof(LoginPage));

        // Database Initialization
        builder.Services.AddHostedService<DatabaseInitializer>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

public class DatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IServiceProvider serviceProvider, ILogger<DatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing database...");
            await FinanceApp.Infrastructure.Persistence.DatabaseInitializer.InitializeAsync(_serviceProvider, cancellationToken);
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}