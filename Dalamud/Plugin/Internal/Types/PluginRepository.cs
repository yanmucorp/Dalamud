using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Configuration.Internal;
using Dalamud.Logging.Internal;
using Dalamud.Networking.Http;
using Dalamud.Plugin.Internal.Types.Manifest;
using Dalamud.Utility;
using Newtonsoft.Json;

namespace Dalamud.Plugin.Internal.Types;

/// <summary>
///     表示单个插件仓库
/// </summary>
internal class PluginRepository
{
    /// <summary>
    ///     官方主仓库地址
    /// </summary>
    
    public const string MainRepoUrlDailyRoutines = "https://raw.githubusercontent.com/yanmucorp/PluginDistD17/refs/heads/main/pluginmaster.json";
    public const string MainRepoUrlGoatCorp = "https://kamori.goats.dev/Plugin/PluginMaster";

    public static string MainRepoUrl => Service<DalamudConfiguration>.Get().MainRepoUrl;

    public const string MainRepoDRUrl = "https://raw.githubusercontent.com/yanmucorp/PluginDistD17/refs/heads/main/pluginmaster.json";

    private const int HttpRequestTimeoutSeconds = 20;

    private static readonly ModuleLog Log = new("PLUGINR");

    private readonly HttpClient httpClient;

    /// <summary>
    ///     创建插件仓库实例
    /// </summary>
    /// <param name="happyHttpClient">HTTP客户端实例</param>
    /// <param name="pluginMasterUrl">插件主文件地址</param>
    /// <param name="isEnabled">仓库是否启用</param>
    public PluginRepository(HappyHttpClient happyHttpClient, string pluginMasterUrl, bool isEnabled)
    {
        this.httpClient = new(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback        = happyHttpClient.SharedHappyEyeballsCallback.ConnectCallback
        })
        {
            Timeout = TimeSpan.FromSeconds(20),
            DefaultRequestHeaders =
            {
                Accept =
                {
                    new MediaTypeWithQualityHeaderValue("application/json")
                },
                CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true
                },
                UserAgent =
                {
                    new ProductInfoHeaderValue("Dalamud", Util.AssemblyVersion)
                }
            }
        };

        this.httpClient.DefaultRequestHeaders.Add("X-Machine-Token", DeviceUtils.GetDeviceId());
        PluginMasterUrl = pluginMasterUrl;
        IsEnabled       = isEnabled;
    }

    /// <summary>
    ///     插件主文件地址
    /// </summary>
    public string PluginMasterUrl { get; }

    /// <summary>
    ///     是否为第三方仓库
    /// </summary>
    public bool IsThirdParty => true;

    /// <summary>
    ///     仓库是否启用
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    ///     可用插件列表
    /// </summary>
    public ReadOnlyCollection<RemotePluginManifest>? PluginMaster { get; private set; }

    /// <summary>
    ///     仓库初始化状态
    /// </summary>
    public PluginRepositoryState State { get; private set; }

    /// <summary>
    ///     创建主仓库实例
    /// </summary>
    /// <param name="happyHttpClient">HTTP客户端实例</param>
    /// <returns>主仓库实例</returns>
    public static PluginRepository CreateMainRepo(HappyHttpClient happyHttpClient)
    {
        // 摊手.jpg
        var dalamudConfig = Service<DalamudConfiguration>.Get();
        if (dalamudConfig.MainRepoUrl.Contains("https://aonyx.ffxiv.wang/Plugin/PluginMaster", StringComparison.OrdinalIgnoreCase))
        {
            dalamudConfig.MainRepoUrl = MainRepoUrlDailyRoutines;
            dalamudConfig.QueueSave();
        }

        return new(happyHttpClient, MainRepoUrl, true);
    }

    /// <summary>
    ///     异步重新加载插件列表
    /// </summary>
    /// <returns>更新后的状态</returns>
    public async Task ReloadPluginMasterAsync()
    {
        this.State        = PluginRepositoryState.InProgress;
        this.PluginMaster = new List<RemotePluginManifest>().AsReadOnly();

        try
        {
            Log.Information($"开始获取仓库数据：{this.PluginMasterUrl}");

            using var response = await this.GetPluginMaster(this.PluginMasterUrl);

            response.EnsureSuccessStatusCode();

            var data         = await response.Content.ReadAsStringAsync();
            var pluginMaster = JsonConvert.DeserializeObject<List<RemotePluginManifest>>(data);

            if (pluginMaster == null) { throw new Exception("插件列表反序列化失败，结果为空"); }

            pluginMaster.Sort((pm1, pm2) => string.Compare(pm1.Name, pm2.Name, StringComparison.Ordinal));

            // Set the source for each remote manifest. Allows for checking if is 3rd party.
            foreach (var manifest in pluginMaster) { manifest.SourceRepo = this; }

            this.PluginMaster = pluginMaster.Where(this.IsValidManifest).ToList().AsReadOnly();

            // API9 HACK: Force IsHide to false, we should remove that
            if (!this.IsThirdParty)
            {
                foreach (var manifest in this.PluginMaster) { manifest.IsHide = false; }
            }

            Log.Information($"仓库数据获取成功：{this.PluginMasterUrl}");
            this.State = PluginRepositoryState.Success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"仓库数据获取失败：{this.PluginMasterUrl}");
            this.State = PluginRepositoryState.Fail;
        }
    }

    private bool IsValidManifest(RemotePluginManifest manifest)
    {
        if (manifest.InternalName.IsNullOrWhitespace())
        {
            Log.Error("仓库 {RepoLink} 中存在插件缺少有效的内部名称", PluginMasterUrl);
            return false;
        }

        if (manifest.Name.IsNullOrWhitespace())
        {
            Log.Error("仓库 {RepoLink} 中的插件 {PluginName} 缺少有效的名称", manifest.InternalName, PluginMasterUrl);
            return false;
        }

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (manifest.AssemblyVersion == null)
        {
            Log.Error("仓库 {RepoLink} 中的插件 {PluginName} 缺少有效的程序集版本", manifest.InternalName,
                      PluginMasterUrl);
            return false;
        }

        if (manifest.TestingAssemblyVersion != null                    &&
            manifest.TestingAssemblyVersion > manifest.AssemblyVersion &&
            manifest.TestingDalamudApiLevel == null)
            Log.Warning(
                "仓库 {RepoLink} 中的插件 {PluginName} 有测试版本可用，但未指定测试API版本，需要提供 'TestingDalamudApiLevel' 属性",
                manifest.InternalName, PluginMasterUrl);

        return true;
    }

    private async Task<HttpResponseMessage> GetPluginMaster(string url, int timeout = HttpRequestTimeoutSeconds)
    {
        var client = Service<HappyHttpClient>.Get().SharedHttpClient;

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

        using var requestCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

        return await client.SendAsync(request, requestCts.Token);
    }
}
