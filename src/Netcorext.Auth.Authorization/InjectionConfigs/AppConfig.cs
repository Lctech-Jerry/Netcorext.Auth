using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Netcorext.Auth.Authorization.Services.Authorization;
using Netcorext.Auth.Authorization.Settings;
using Netcorext.Auth.Extensions;
using Netcorext.Auth.Protobufs;
using Netcorext.EntityFramework.UserIdentityPattern.AspNetCore;
using Netcorext.Extensions.AspNetCore.Middlewares;
using Netcorext.Extensions.DependencyInjection;

namespace Netcorext.Auth.Authorization.InjectionConfigs;

[Injection]
public class AppConfig
{
    public AppConfig(WebApplication app)
    {
        var config = app.Services.GetRequiredService<IOptions<ConfigSettings>>().Value;

        app.EnsureCreateDatabase<Domain.Entities.Route>();
        app.UseMiddleware<CustomExceptionMiddleware>();
        app.UseJwtAuthentication();

        app.UseCors(b =>
                    {
                        b.SetIsOriginAllowed(host =>
                                             {
                                                 var allowList = app.Configuration.GetValue("AllowedHosts", string.Empty)
                                                                    .Split(";", StringSplitOptions.RemoveEmptyEntries);

                                                 return allowList.Any(h => h.Equals(host, StringComparison.OrdinalIgnoreCase) || h == "*");
                                             })
                         .AllowAnyHeader()
                         .AllowAnyMethod()
                         .AllowCredentials();
                    });

        if (app.Environment.IsDevelopment() || app.Environment.IsLocalhost())
        {
            var docRoute = config.DocumentUrl.Replace("$id", config.Id).ToLower();

            app.MapSwagger(docRoute + "/{documentName}/swagger.json");

            app.UseSwaggerUI(options =>
                             {
                                 options.SwaggerEndpoint("v1/swagger.json", typeof(ConfigSettings).Assembly.GetName().Name);
                                 options.RoutePrefix = docRoute;
                             });
        }

        app.UseSimpleHealthChecks(provider =>
                                  {
                                      var routePrefixValue = config.AppSettings.RoutePrefix?.Replace("$id", config.Id).ToLower() ?? "";

                                      return "/" + routePrefixValue + config.AppSettings.HealthRoute;
                                  });

        app.MapGrpcService<AuthorizationServiceFacade>();
        app.MapControllers();

        app.RegisterPermissionEndpoints((provider, endpoints) =>
                                        {
                                            var request = new RegisterRouteRequest
                                                          {
                                                              Groups =
                                                              {
                                                                  endpoints.GroupBy(t => t.Protocol)
                                                                           .Select(t => new RegisterRouteRequest.Types.RouteGroup
                                                                                        {
                                                                                            Name = config.Id + " - " + t.Key,
                                                                                            BaseUrl = HttpProtocols.Http2.ToString().Equals(t.Key, StringComparison.OrdinalIgnoreCase)
                                                                                                          ? config.AppSettings.Http2BaseUrl
                                                                                                          : config.AppSettings.HttpBaseUrl,
                                                                                            ForwarderRequestVersion = config.AppSettings.ForwarderRequestVersion,
                                                                                            ForwarderHttpVersionPolicy = config.AppSettings.ForwarderHttpVersionPolicy.HasValue ? (int)config.AppSettings.ForwarderHttpVersionPolicy.Value : null,
                                                                                            ForwarderActivityTimeout = config.AppSettings.ForwarderActivityTimeout.HasValue ? Duration.FromTimeSpan(config.AppSettings.ForwarderActivityTimeout.Value) : null,
                                                                                            ForwarderAllowResponseBuffering = config.AppSettings.ForwarderAllowResponseBuffering,
                                                                                            Routes =
                                                                                            {
                                                                                                t.Select(t2 => new RegisterRouteRequest.Types.Route
                                                                                                               {
                                                                                                                   Protocol = t2.Protocol,
                                                                                                                   HttpMethod = t2.HttpMethod,
                                                                                                                   RelativePath = t2.RelativePath,
                                                                                                                   Template = t2.Template,
                                                                                                                   FunctionId = t2.FunctionId,
                                                                                                                   NativePermission = t2.NativePermission.ToProtobufPermissionType(),
                                                                                                                   AllowAnonymous = t2.AllowAnonymous,
                                                                                                                   Tag = t2.Tag,
                                                                                                                   RouteValues =
                                                                                                                   {
                                                                                                                       t2.RouteValues.Select(t3 => new RegisterRouteRequest.Types.RouteValue
                                                                                                                                                   {
                                                                                                                                                       Key = t3.Key,
                                                                                                                                                       Value = t3.Value
                                                                                                                                                   })
                                                                                                                   }
                                                                                                               })
                                                                                            }
                                                                                        })
                                                              }
                                                          };

                                            var routeService = provider.GetRequiredService<RouteService.RouteServiceClient>();

                                            routeService.RegisterRoute(request);
                                        });

        app.Run();
    }
}