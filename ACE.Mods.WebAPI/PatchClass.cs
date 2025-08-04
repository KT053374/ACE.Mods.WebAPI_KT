namespace ACE.Mods.WebAPI;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    //private static readonly JsonSerializerOptions jsonSerializerOptions = new()
    //{
    //    WriteIndented = true,
    //    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    //};

    private IServerHost? serverHost;
    private Task? serverTask;

    public override void Init()
    {
        base.Init();
    }

    public override async Task OnStartSuccess()
    {
        Settings = SettingsContainer?.Settings ?? new();
       await StartServicesAsync();
    }

    //public override Task OnWorldOpen()
    //{
    //    Settings = SettingsContainer?.Settings ?? new();
    //    await StartServicesAsync();

    //    return Task.CompletedTask;
    //}

    protected override async Task SettingsChanged(object? sender, EventArgs e)
    {
        try
        {
            await StopServicesAsync();
        }
        catch (Exception ex)
        {
            Mod.Log($"ERROR stopping services - {ex.Message}", ModManager.LogLevel.Error);
            throw;
        }

        await base.SettingsChanged(sender, e);
        Settings = SettingsContainer?.Settings ?? new();

        try
        {
            await StartServicesAsync();
        }
        catch (Exception ex)
        {
            Mod.Log($"ERROR starting services - {ex.Message}", ModManager.LogLevel.Error);
            throw;
        }
    }

    public override async Task Stop()
    {
        await StopServicesAsync();
        await base.Stop();
    }

    public async Task StartServicesAsync()
    {
        try
        {
            // ensure an existing server isn't already running
            if (serverTask != null && !serverTask.IsCompleted)
            {
                Mod.Log("API Server is already running");
                return;
            }

            if (serverHost != null || serverTask != null)
            {
                await StopServicesAsync();
            }

            //var content = Content.From(Resource.FromString("Hello World!"));

            //var server = Host.Create()
            //                 .Handler(content)
            //                 .Defaults()
            //                 .StartAsync(); // or .RunAsync() to block until the application is shut down
            //                 //.RunAsync();

            serverHost = Host.Create();

            //var app = Layout.Create();
                            //.AddService<BookService>("books")
                            //.AddController<IotController>("device")
                            //.AddOpenApi()
                            //.AddSwaggerUI()
                            //.AddRedoc()
                            //.AddScalar();

            var api = Layout.Create();

            api.AddController<StatusController>("status");
            api.AddService<DerethPulseService>("derethpulse");
            //api.AddController<EventManagerController>("events2");
            api.AddService<EventManagerService>("events");

            var description = ApiDescription.Create()
                                .Title(Mod.Instance.Container.Meta.Name)
                                .Version(Mod.Instance.Container.Meta.Version)
                                .PostProcessor((r, doc) =>
                                {
                                    doc.Servers.Clear();
                                    doc.Servers.Add(new NSwag.OpenApiServer() { Url = Settings.APIBaseUrl + "/" + Settings.APIBasePath });
                                });
                                //.PostProcessor((r, doc) => doc.Info.TermsOfService = "https://mycompany.com/tos");

            if (Settings.EnableOpenAPI)
                api.AddOpenApi().Add(description);

            if (Settings.EnableSwaggerUI)
                api.AddSwaggerUI();

            if (Settings.EnableRedoc)
                api.AddRedoc();

            if (Settings.EnableScalar)
                api.AddScalar();

            var auth = ApiKeyAuthentication.Create()
                                           //.WithQueryParameter("apiKey")
                                           .WithHeader("X-API-Key")
                                           .Authenticator(AuthenticateRequestAsync);

            //api.Add(auth);

            var app = Layout.Create().Add(Settings.APIBasePath, api);

            serverHost?.Handler(app);
                       //.Defaults()
                       //.Development()
                       //.Console()
                       //.StartAsync();

            serverHost?.Defaults();

            if (!System.Net.IPAddress.TryParse(Settings.Host, out var host))
            {
                Mod.Log($"Invalid host address '{Settings.Host}'", ModManager.LogLevel.Error);
                return;
            }

            var port = Settings.Port;

            serverHost?.Bind(host, port);

            if (Settings.OutputToConsole)
                serverHost?.Console();

            // Start the server without awaiting so the method returns immediately
            serverTask = serverHost!.StartAsync();
            
            serverTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Mod.Log($"API Server terminated: {t.Exception?.GetBaseException().Message}", ModManager.LogLevel.Error);
                }
                else
                {
                    Mod.Log("API Server task completed");
                }
            }, TaskScheduler.Default);
            
            Mod.Log($"API Server Online and listening to requests at http://{host}:{port}");

        }
        catch (Exception ex)
        {
            Mod.Log($"ERROR during initialization - {ex.Message}", ModManager.LogLevel.Error);
            throw;
        }
    }

    public async Task StopServicesAsync()
    {
        try
        {
            if (serverHost != null)
            {
                await serverHost.StopAsync();
            }

            if (serverTask != null)
            {
                try
                {
                    await serverTask;
                }
                catch (Exception ex)
                {
                    Mod.Log($"ERROR during server execution - {ex.Message}", ModManager.LogLevel.Error);
                }
                finally
                {
                    serverTask = null;
                }
            }
        }
        catch (Exception ex)
        {
            Mod.Log($"ERROR during shutdown - {ex.Message}", ModManager.LogLevel.Error);
        }
        finally
        {
            serverHost?.Dispose();
            serverHost = null;
            Mod.Log("API Server Offline");
        }
    }

    static ValueTask<IUser?> AuthenticateRequestAsync(IRequest request, string apiKey)
    {
        //if (apiKey == "abc")
        //{
        //    return new(new ApiKeyUser(apiKey, "ADMIN", "USER"));
        //}

        //if (apiKey == "bcd")
        //{
        //    return new(new ApiKeyUser(apiKey, "USER"));
        //}

        return default;
    }
}

