using Serilog;
using SmartTalk.Messages;
using Correlate.AspNetCore;
using SmartTalk.Api.Filters;
using SmartTalk.Api.Extensions;
using Correlate.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace SmartTalk.Api;

public class Startup
{
    private IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCorrelate(options => options.RequestHeaders = SmartTalkConstants.CorrelationIdHeaders);
        services.AddControllers().AddNewtonsoftJson();
        services.AddHttpClientInternal();
        services.AddMemoryCache();
        services.AddResponseCaching();
        services.AddHealthChecks();
        services.AddEndpointsApiExplorer();
        services.AddHttpContextAccessor();
        services.AddCustomSwagger();
        services.AddCustomAuthentication(Configuration);
        services.AddCorsPolicy(Configuration);

        services.AddMvc(options =>
        {
            options.Filters.Add<GlobalExceptionFilter>();
        });

        services.AddHangfireInternal(Configuration);
    }
    
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartTalk.Api.xml");
                c.DocExpansion(DocExpansion.None);
            });
        }
        app.UseSerilogRequestLogging();
        app.UseCorrelate();
        app.UseRouting();
        app.UseCors();
        app.UseResponseCaching();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHangfireInternal(Configuration);
        app.UseWebSockets();
        
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHealthChecks("health");
        });
    }
}