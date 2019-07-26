using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VirtoCommerce.CatalogModule.Core;
using VirtoCommerce.CatalogModule.Core.Events;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.OutlinePart;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CatalogModule.Data.ExportImport;
using VirtoCommerce.CatalogModule.Data.ExportImport.Converters;
using VirtoCommerce.CatalogModule.Data.Handlers;
using VirtoCommerce.CatalogModule.Data.Repositories;
using VirtoCommerce.CatalogModule.Data.Search;
using VirtoCommerce.CatalogModule.Data.Search.BrowseFilters;
using VirtoCommerce.CatalogModule.Data.Search.Indexing;
using VirtoCommerce.CatalogModule.Data.Services;
using VirtoCommerce.CatalogModule.Data.Validation;
using VirtoCommerce.CatalogModule.Web.Authorization;
using VirtoCommerce.CatalogModule.Web.JsonConverters;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.ExportModule.Core.Model;
using VirtoCommerce.ExportModule.Core.Services;
using VirtoCommerce.Platform.Core.Bus;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.Extensions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.CatalogModule.Web
{
    public class Module : IModule, IExportSupport, IImportSupport
    {
        private IApplicationBuilder _appBuilder;

        public ManifestModuleInfo ModuleInfo { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            var configuration = serviceCollection.BuildServiceProvider().GetRequiredService<IConfiguration>();
            serviceCollection.AddTransient<ICatalogRepository, CatalogRepositoryImpl>();
            var connectionString = configuration.GetConnectionString("VirtoCommerce.Catalog") ?? configuration.GetConnectionString("VirtoCommerce");
            serviceCollection.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(connectionString));
            serviceCollection.AddSingleton<Func<ICatalogRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<ICatalogRepository>());

            serviceCollection.AddSingleton<IProductSearchService, ProductSearchService>();
            serviceCollection.AddSingleton<ICategorySearchService, CategorySearchService>();

            serviceCollection.AddSingleton<ICatalogService, CatalogService>();
            serviceCollection.AddSingleton<Func<ICatalogService>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<ICatalogService>());
            serviceCollection.AddSingleton<ICatalogSearchService, CatalogSearchService>();
            serviceCollection.AddSingleton<Func<ICatalogSearchService>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<ICatalogSearchService>());

            serviceCollection.AddSingleton<IListEntrySearchService, ListEntrySearchService>();

            serviceCollection.AddSingleton<ICategoryService, CategoryService>();
            serviceCollection.AddSingleton<ICategoryIndexedSearchService, CategoryIndexedSearchService>();

            serviceCollection.AddSingleton<IItemService, ItemService>();
            serviceCollection.AddSingleton<IProductIndexedSearchService, ProductIndexedSearchService>();
            serviceCollection.AddSingleton<IAssociationService, AssociationService>();

            serviceCollection.AddSingleton<IAggregationConverter, AggregationConverter>();
            serviceCollection.AddSingleton<IBrowseFilterService, BrowseFilterService>();
            serviceCollection.AddSingleton<ITermFilterBuilder, TermFilterBuilder>();

            serviceCollection.AddSingleton<ISearchRequestBuilder, ProductSearchRequestBuilder>();
            serviceCollection.AddSingleton<ISearchRequestBuilder, CategorySearchRequestBuilder>();

            serviceCollection.AddSingleton<IPropertyService, PropertyService>();
            serviceCollection.AddSingleton<IPropertySearchService, PropertySearchService>();
            serviceCollection.AddSingleton<IProperyDictionaryItemService, PropertyDictionaryItemService>();
            serviceCollection.AddSingleton<IProperyDictionaryItemSearchService, ProperyDictionaryItemSearchService>();
            serviceCollection.AddSingleton<IProductAssociationSearchService, ProductAssociationSearchService>();
            serviceCollection.AddSingleton<IOutlineService, OutlineService>();
            serviceCollection.AddSingleton<ISkuGenerator, DefaultSkuGenerator>();

            serviceCollection.AddSingleton<LogChangesChangedEventHandler>();

            serviceCollection.AddSingleton<ISeoBySlugResolver, SeoBySlugResolver>();

            PropertyValueValidator PropertyValueValidatorFactory(PropertyValidationRule rule) => new PropertyValueValidator(rule);
            serviceCollection.AddSingleton((Func<PropertyValidationRule, PropertyValueValidator>)PropertyValueValidatorFactory);
            serviceCollection.AddSingleton<AbstractValidator<IHasProperties>, HasPropertiesValidator>();

            serviceCollection.AddSingleton<CatalogExportImport>();

            serviceCollection.AddSingleton<IOutlinePartResolver>(provider =>
            {
                var settingsManager = provider.GetService<ISettingsManager>();
                if (settingsManager.GetValue(ModuleConstants.Settings.General.CodesInOutline.Name, false))
                {
                    return new CodeOutlinePartResolver();
                }
                else
                {
                    return new IdOutlinePartResolver();
                }
            });

            serviceCollection.AddSingleton<ProductDocumentChangesProvider>();
            serviceCollection.AddSingleton<ProductDocumentBuilder>();
            serviceCollection.AddSingleton<CategoryDocumentChangesProvider>();
            serviceCollection.AddSingleton<CategoryDocumentBuilder>();

            // Product indexing configuration
            serviceCollection.AddSingleton(provider => new IndexDocumentConfiguration
            {
                DocumentType = KnownDocumentTypes.Product,
                DocumentSource = new IndexDocumentSource
                {
                    ChangesProvider = provider.GetService<ProductDocumentChangesProvider>(),
                    DocumentBuilder = provider.GetService<ProductDocumentBuilder>(),
                },
            });

            // Category indexing configuration
            serviceCollection.AddSingleton(provider => new IndexDocumentConfiguration
            {
                DocumentType = KnownDocumentTypes.Category,
                DocumentSource = new IndexDocumentSource
                {
                    ChangesProvider = provider.GetService<CategoryDocumentChangesProvider>(),
                    DocumentBuilder = provider.GetService<CategoryDocumentBuilder>(),
                },
            });

            serviceCollection.AddSingleton<IAuthorizationHandler, CatalogAuthorizationHandler>();

            serviceCollection.AddScoped<CatalogExportPagedDataSource>(); // Adding as scoped, because of used services (UserManager, PrincipalFactory) scoped too
            serviceCollection.AddSingleton<Func<ExportDataQuery, CatalogExportPagedDataSource>>(provider => (exportDataQuery) => CreateExportPagedDataSource<CatalogExportPagedDataSource>(provider, exportDataQuery));

            serviceCollection.AddScoped<ProductExportPagedDataSource>();
            serviceCollection.AddSingleton<Func<ExportDataQuery, ProductExportPagedDataSource>>(provider => (exportDataQuery) => CreateExportPagedDataSource<ProductExportPagedDataSource>(provider, exportDataQuery));
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            _appBuilder = appBuilder;

            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            //Register module permissions
            var permissionsProvider = appBuilder.ApplicationServices.GetRequiredService<IPermissionsRegistrar>();
            permissionsProvider.RegisterPermissions(ModuleConstants.Security.Permissions.AllPermissions.Select(x => new Permission() { GroupName = "Catalog", Name = x }).ToArray());


            //Register Permission scopes
            AbstractTypeFactory<PermissionScope>.RegisterType<SelectedCatalogScope>();
            permissionsProvider.WithAvailabeScopesForPermissions(new[] {
                                                                        ModuleConstants.Security.Permissions.Read,
                                                                        ModuleConstants.Security.Permissions.Update,
                                                                        ModuleConstants.Security.Permissions.Delete,
                                                                         }, new SelectedCatalogScope());

            var mvcJsonOptions = appBuilder.ApplicationServices.GetService<IOptions<MvcJsonOptions>>();
            mvcJsonOptions.Value.SerializerSettings.Converters.Add(new SearchCriteriaJsonConverter());

            var inProcessBus = appBuilder.ApplicationServices.GetService<IHandlerRegistrar>();
            inProcessBus.RegisterHandler<ProductChangedEvent>(async (message, token) => await appBuilder.ApplicationServices.GetService<LogChangesChangedEventHandler>().Handle(message));
            inProcessBus.RegisterHandler<CategoryChangedEvent>(async (message, token) => await appBuilder.ApplicationServices.GetService<LogChangesChangedEventHandler>().Handle(message));

            //Register types allowed to export
            var registrar = appBuilder.ApplicationServices.GetService<IKnownExportTypesRegistrar>();
            var catalogExportPagedDataSourceFactory = appBuilder.ApplicationServices.GetService<Func<ExportDataQuery, CatalogExportPagedDataSource>>();
            var productExportPagedDataSourceFactory = appBuilder.ApplicationServices.GetService<Func<ExportDataQuery, ProductExportPagedDataSource>>();


            registrar.RegisterType(typeof(Catalog).Name, "Catalog", typeof(CatalogExportDataQuery).Name)
                .WithDataSourceFactory(dataQuery => catalogExportPagedDataSourceFactory(dataQuery))
                .WithMetadata(ExportedTypeMetadata.GetFromType<Catalog>(true))
                .WithTabularDataConverter(new TabularCatalogDataConverter())
                .WithTabularMetadata(ExportedTypeMetadata.GetFromType<TabularCatalog>(false));

            AbstractTypeFactory<ExportDataQuery>.RegisterType<CatalogExportDataQuery>();

            registrar.RegisterType(typeof(CatalogProduct).Name, "Catalog", typeof(ProductExportDataQuery).Name)
                .WithDataSourceFactory(dataQuery => productExportPagedDataSourceFactory(dataQuery))
                .WithMetadata(ExportedTypeMetadata.GetFromType<CatalogProduct>(true))
                .WithTabularDataConverter(new TabularProductDataConverter())
                .WithTabularMetadata(ExportedTypeMetadata.GetFromType<TabularProduct>(false));

            AbstractTypeFactory<ExportDataQuery>.RegisterType<ProductExportDataQuery>();

            //Force migrations
            using (var serviceScope = appBuilder.ApplicationServices.CreateScope())
            {
                var catalogDbContext = serviceScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                catalogDbContext.Database.MigrateIfNotApplied(MigrationName.GetUpdateV2MigrationName(ModuleInfo.Id));
                catalogDbContext.Database.EnsureCreated();
                catalogDbContext.Database.Migrate();
            }
        }

        public void Uninstall()
        {
        }

        public async Task ExportAsync(Stream outStream, ExportImportOptions options, Action<ExportImportProgressInfo> progressCallback,
            ICancellationToken cancellationToken)
        {
            await _appBuilder.ApplicationServices.GetRequiredService<CatalogExportImport>().DoExportAsync(outStream, options,
                progressCallback, cancellationToken);
        }

        public async Task ImportAsync(Stream inputStream, ExportImportOptions options, Action<ExportImportProgressInfo> progressCallback,
            ICancellationToken cancellationToken)
        {
            await _appBuilder.ApplicationServices.GetRequiredService<CatalogExportImport>().DoImportAsync(inputStream, options,
                progressCallback, cancellationToken);
        }

        /// <summary>
        /// Helps to create ExportDataSource factory method
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="provider"></param>
        /// <param name="exportDataQuery"></param>
        /// <returns></returns>
        private static T CreateExportPagedDataSource<T>(IServiceProvider provider, ExportDataQuery exportDataQuery) where T : BaseExportPagedDataSource
        {
            var result = provider.CreateScope().ServiceProvider.GetRequiredService<T>();
            result.DataQuery = exportDataQuery;
            return result;
        }
    }
}
