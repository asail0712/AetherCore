using AetherCore.Utility.Attributes;
using AetherCore.Utility.Caches;
using AetherCore.Utility.Databases;
using AetherCore.Utility.Exceptions;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Entities;
using System.Reflection;

namespace AetherCore.Utility
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 加入全域例外過濾器
        /// </summary>
        public static IServiceCollection AddExceptionHandling<T>(this IServiceCollection services) where T : GlobalExceptionFilter
        {
            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add<T>();
            });

            return services;
        }

        /// <summary>
        /// 加入快取設定及記憶體快取
        /// </summary>
        public static IServiceCollection AddCacheSettings(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<CacheSettings>(configuration.GetSection("CacheSettings"));
            services.AddMemoryCache();
            return services;
        }

        /// <summary>
        /// 初始化 MongoDB 連線與註冊相關服務
        /// </summary>
        public static IServiceCollection InitialMongoDB(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MongoDBSettings>(configuration.GetSection("MongoDBSettings"));

            services.AddSingleton<IMongoClient>((sp) =>
            {
                var settings    = sp.GetRequiredService<IOptions<MongoDBSettings>>().Value;
                var client      = new MongoClient(settings.ConnectionString);
                return client;
            });

            services.AddSingleton<IMongoDbContext, MongoDBContext>();

            return services;
        }

        /// <summary>
        /// 初始化 MongoDB.Entities 的 DB 連線
        /// </summary>
        public async static Task InitialMongoDBEntity(this IServiceCollection services, IConfiguration configuration)
        {
            var section                 = configuration.GetSection("MongoDBSettings");
            MongoDBSettings? dbSetting  = section.Get<MongoDBSettings>();

            if (dbSetting is null)
            {
                throw new InvalidOperationException("Missing or invalid MongoDBSetting section in configuration.");
            }

            await DB.InitAsync(dbSetting.DatabaseName, MongoClientSettings.FromConnectionString(dbSetting.ConnectionString));
        }

        /// <summary>
        /// 註冊 AutoMapper 映射設定並加入 DI
        /// </summary>
        public static IServiceCollection AddAutoMapperProfiles(this IServiceCollection services, ILoggerFactory loggerFactory)
        {
            var configExpression    = new MapperConfigurationExpression();
            configExpression.AddMaps(AppDomain.CurrentDomain.GetAssemblies());

            var mapperConfig        = new MapperConfiguration(configExpression);//, loggerFactory);
            var mapper              = mapperConfig.CreateMapper();

            services.AddSingleton<IMapper>(mapper);
            return services;
        }

        /// <summary>
        /// 針對AppSettingsAttribute 自動註冊app settings
        /// </summary>
        public static IServiceCollection AutoRegisterAppSettings(
                                    this IServiceCollection services,
                                    IConfiguration config,
                                    string[]? assemblyName = null)
        {
            List<Assembly> assemblies = new List<Assembly>();

            // 1) 依照前綴設定 取得要掃描的 assemblies
            Utils.FindAssemblyByName(assemblyName, ref assemblies);

            // 2) 將當前assembly加入掃描清單，確保至少掃描到目前專案的類型
            assemblies.Add(typeof(ServiceCollectionExtensions).Assembly);

            // 3) 掃描所有類型，找出有 [AppSettingsAttribute] 標記的類別
            var types = Utils.FindAllTypeWithAttribute<AppSettingsAttribute>(assemblies);

            // 4) 取得 Configure<T>(IServiceCollection, IConfiguration) 的 MethodInfo
            var configureMethod = typeof(OptionsConfigurationServiceCollectionExtensions)
                            .GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .First(m =>
                                m.Name == nameof(OptionsConfigurationServiceCollectionExtensions.Configure) &&
                                m.IsGenericMethodDefinition &&
                                m.GetParameters().Length == 2 &&
                                m.GetParameters()[1].ParameterType == typeof(IConfiguration));

            // 5) 對每個找到的類型，呼叫 Configure<T>(services, section)
            foreach (var t in types)
            {
                var attr    = t.Attribute;
                var section = config.GetSection(attr.ResolveSectionName(t.Implementation));
                var generic = configureMethod.MakeGenericMethod(t.Implementation);

                generic.Invoke(null, new object[] { services, section });
            }

            return services;
        }


        /// <summary>
        /// AutoInjectAttribute 自動註冊
        /// </summary>
        public static IServiceCollection AutoInject(this IServiceCollection services, string[]? assemblyNames = null)
        {
            List<Assembly> assemblies = new List<Assembly>();

            // 1) 依照前綴設定 取得要掃描的 assemblies
            Utils.FindAssemblyByName(assemblyNames, ref assemblies);

            // 2) 將當前assembly加入掃描清單
            assemblies.Add(typeof(ServiceCollectionExtensions).Assembly);

            // 3) 掃描所有類型
            var types = Utils.FindAllTypeWithAttribute<AutoInjectAttribute>(assemblies);

            foreach (var type in types)
            {
                var impl        = type.Implementation;
                var lifetime    = type.Attribute!.Lifetime;

                // 防呆：不能註冊抽象/介面
                if (impl.IsAbstract || impl.IsInterface) continue;

                // 判斷是否為 HostedService (包含繼承自 BackgroundService 的類別)
                var isHostedService = typeof(IHostedService).IsAssignableFrom(impl);

                if (isHostedService)
                {
                    // === C) 背景服務處理 ===
                    // 背景服務通常需要註冊為 Singleton 且透過 AddHostedService 啟動
                    // 這裡使用反射調用 Microsoft.Extensions.DependencyInjection.ServiceCollectionHostedServiceExtensions.AddHostedService
                    var method = typeof(ServiceCollectionHostedServiceExtensions)
                        .GetMethods()
                        .FirstOrDefault(m => m.Name == "AddHostedService" && m.IsGenericMethod);

                    method?.MakeGenericMethod(impl).Invoke(null, new object[] { services });

                    // 注意：若背景服務同時也需要被注入到其他地方作為介面使用，
                    // 則下方 A) B) 區塊仍需執行，但建議小心生命週期衝突。
                }

                var interfaces = impl.GetInterfaces();

                // === A) 有介面：以介面註冊 ===
                if (interfaces.Length > 0)
                {
                    foreach (var @interface in interfaces)
                    {
                        // 過濾掉 IHostedService 介面，避免重複註冊導致邏輯混亂
                        if (@interface == typeof(IHostedService) || @interface == typeof(IDisposable)) continue;

                        services.TryAdd(new ServiceDescriptor(@interface, impl, lifetime));
                    }
                }
                // === B) 沒介面：以自己型別註冊 ===
                else if (!isHostedService) // 如果已經是 HostedService 且沒介面，通常 AddHostedService 已處理
                {
                    services.TryAdd(new ServiceDescriptor(impl, impl, lifetime));
                }
            }

            return services;
        }
    }
}
