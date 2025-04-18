using Serilog;
using Autofac;
using Mediator.Net;
using System.Reflection;
using Aliyun.OSS;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using Mediator.Net.Autofac;
using SmartTalk.Core.Settings;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Services.Caching;
using Mediator.Net.Middlewares.Serilog;
using Microsoft.Extensions.Configuration;
using AutoMapper.Contrib.Autofac.DependencyInjection;
using Google.Cloud.Translation.V2;
using SmartTalk.Core.Middlewares.Authorization;
using SmartTalk.Core.Middlewares.FluentMessageValidator;
using SmartTalk.Core.Middlewares.Security;
using SmartTalk.Core.Middlewares.UnifyResponse;
using SmartTalk.Core.Middlewares.UnitOfWork;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Settings.AliYun;
using SmartTalk.Core.Settings.Google;
using SmartTalk.Messages.Enums.Caching;
using StackExchange.Redis;
using Module = Autofac.Module;

namespace SmartTalk.Core;

public class SmartTalkModule : Module
{
     private readonly ILogger _logger;
    private readonly Assembly[] _assemblies;
    private readonly IConfiguration _configuration;

    public SmartTalkModule(ILogger logger,IConfiguration configuration, params Assembly[] assemblies)
    {
        _logger = logger;
        _assemblies = assemblies;
        _configuration = configuration;
    }

    protected override void Load(ContainerBuilder builder)
    {
        RegisterLogger(builder);
        RegisterSettings(builder);
        RegisterMediator(builder);
        RegisterCaching(builder);
        RegisterDatabase(builder);
        RegisterDependency(builder);
        RegisterAutoMapper(builder);
        RegisterAliYunOssClient(builder);
        RegisterTranslationClient(builder);
        RegisterValidators(builder);
    }

    private void RegisterDependency(ContainerBuilder builder)
    {
        foreach (var type in typeof(IDependency).Assembly.GetTypes()
                     .Where(type => type.IsClass && typeof(IDependency).IsAssignableFrom(type)))
        {
            if (typeof(IScopedDependency).IsAssignableFrom(type))
                builder.RegisterType(type).AsSelf().AsImplementedInterfaces().InstancePerLifetimeScope();
            else if (typeof(ISingletonDependency).IsAssignableFrom(type))
                builder.RegisterType(type).AsSelf().AsImplementedInterfaces().SingleInstance();
            else if (typeof(ITransientDependency).IsAssignableFrom(type))
                builder.RegisterType(type).AsSelf().AsImplementedInterfaces().InstancePerDependency();
            else
                builder.RegisterType(type).AsSelf().AsImplementedInterfaces();
        }
    }

    private void RegisterMediator(ContainerBuilder builder)
    {
        var mediatorBuilder = new MediatorBuilder();
        
        mediatorBuilder.RegisterHandlers(_assemblies);
        
        mediatorBuilder.ConfigureGlobalReceivePipe(c =>
        {
            c.UseUnitOfWork();
            c.UseUnifyResponse();
            c.UseSecurity();
            c.UseAuthorization();
            c.UseMessageValidator();
            c.UseSerilog(logger: _logger);
        });

        builder.RegisterMediator(mediatorBuilder);
    }
    
    private void RegisterLogger(ContainerBuilder builder)
    {
        builder.RegisterInstance(_logger).AsSelf().AsImplementedInterfaces().SingleInstance();
    }
    
    private void RegisterSettings(ContainerBuilder builder)
    {
        var settingTypes = typeof(SmartTalkModule).Assembly.GetTypes()
            .Where(t => t.IsClass && typeof(IConfigurationSetting).IsAssignableFrom(t))
            .ToArray();

        builder.RegisterTypes(settingTypes).AsSelf().SingleInstance();
    }
    
    private void RegisterDatabase(ContainerBuilder builder)
    {
        builder.RegisterType<SmartTalkDbContext>()
            .AsSelf()
            .As<DbContext>()
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        builder.RegisterType<EfRepository>().As<IRepository>().InstancePerLifetimeScope();
    }
    
    private void RegisterAutoMapper(ContainerBuilder builder)
    {
        builder.RegisterAutoMapper(typeof(SmartTalkModule).Assembly);
    }
    
    private void RegisterCaching(ContainerBuilder builder)
    {
        builder.Register(cfx =>
        {
            var pool = cfx.Resolve<IRedisConnectionPool>();
            return pool.GetConnection();
        }).Keyed<ConnectionMultiplexer>(RedisServer.System).ExternallyOwned();
        
        builder.Register(cfx =>
        {
            var pool = cfx.Resolve<IRedisConnectionPool>();
            return pool.GetConnection(RedisServer.Vector);
        }).Keyed<ConnectionMultiplexer>(RedisServer.Vector).ExternallyOwned();
    }
    
    private void RegisterAliYunOssClient(ContainerBuilder builder)
    {
        builder.Register(c =>
        {
            var settings = c.Resolve<AliYunSettings>();
            var endpoint = settings.OssEndpoint;
            var accessKeyId = settings.AccessKeyId;
            var accessKeySecret = settings.AccessKeySecret;
            return new OssClient(endpoint, accessKeyId, accessKeySecret);
        }).AsSelf().InstancePerLifetimeScope();
    }
    
    private void RegisterTranslationClient(ContainerBuilder builder)
    {
        builder.Register(c =>
        {
            var googleTranslateApiKey = c.Resolve<GoogleTranslateApiKeySetting>().Value;
            return TranslationClient.CreateFromApiKey(googleTranslateApiKey);
        }).AsSelf().InstancePerLifetimeScope();
    }
    
    private void RegisterValidators(ContainerBuilder builder)
    {
        builder.RegisterTypes(typeof(SmartTalkModule).Assembly.GetTypes()
            .Where(x => x.IsClass && typeof(IFluentMessageValidator).IsAssignableFrom(x)).ToArray()).AsSelf().AsImplementedInterfaces();
    }
}