using System;
using System.Diagnostics.CodeAnalysis;

using CheapLoc;

using Dalamud.Configuration.Internal;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Internal.ReShadeHandling;
using Dalamud.Interface.Internal.Windows.PluginInstaller;
using Dalamud.Interface.Internal.Windows.Settings.Widgets;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Internal;
using Dalamud.Utility;

namespace Dalamud.Interface.Internal.Windows.Settings.Tabs;

[SuppressMessage(
    "StyleCop.CSharp.DocumentationRules",
    "SA1600:Elements should be documented",
    Justification = "Internals")]
public class SettingsTabExperimental : SettingsTab
{
    public override SettingsEntry[] Entries { get; } =
    [
        new SettingsEntry<bool>(
            Loc.Localize("DalamudSettingsPluginTest", "Get plugin testing builds"),
            string.Format(
                Loc.Localize(
                    "DalamudSettingsPluginTestHint",
                    "Receive testing prereleases for selected plugins.\nTo opt-in to testing builds for a plugin, you have to right click it in the \"{0}\" tab of the plugin installer and select \"{1}\"."),
                PluginCategoryManager.Locs.Group_Installed,
                PluginInstallerWindow.Locs.PluginContext_TestingOptIn),
            c => c.DoPluginTest,
            (v, c) => c.DoPluginTest = v),
        new HintSettingsEntry(
            Loc.Localize(
                "DalamudSettingsPluginTestWarning",
                "Testing plugins may contain bugs or crash your game. Please only enable this if you are aware of the risks."),
            ImGuiColors.DalamudRed),

        new GapSettingsEntry(5),

        new ButtonSettingsEntry(
            Loc.Localize("DalamudSettingsClearHidden", "Clear hidden plugins"),
            Loc.Localize(
                "DalamudSettingsClearHiddenHint",
                "Restore plugins you have previously hidden from the plugin installer."),
            () =>
            {
                Service<DalamudConfiguration>.Get().HiddenPluginInternalName.Clear();
                Service<PluginManager>.Get().RefilterPluginMasters();
            }),

        new GapSettingsEntry(5, true),

        new ProxySettingsEntry(),

        new GapSettingsEntry(5),

        new DevPluginsSettingsEntry(),

        new GapSettingsEntry(5, true),

        new ThirdRepoSettingsEntry(),

        new GapSettingsEntry(5, true),

        new EnumSettingsEntry<ReShadeHandlingMode>(
            Loc.Localize("DalamudSettingsReShadeHandlingMode", "ReShade 处理模式"),
            Loc.Localize(
                "DalamudSettingsReShadeHandlingModeHint",
                "当你遇到与 ReShade 相关的问题时，可以选择以下不同选项来尝试解决问题\n注：所有选项需重启游戏后生效"),
            c => c.ReShadeHandlingMode,
            (v, c) => c.ReShadeHandlingMode = v,
            fallbackValue: ReShadeHandlingMode.Default,
            warning: static rshm =>
            {
                var warning = string.Empty;
                warning += rshm is ReShadeHandlingMode.UnwrapReShade or ReShadeHandlingMode.None ||
                           Service<DalamudConfiguration>.Get().SwapChainHookMode == SwapChainHelper.HookMode.ByteCode
                               ? string.Empty
                               : "当前选项将被忽略且不会执行特殊 ReShade 处理，因为已启用 SwapChain vtable Hook 模式。";

                if (ReShadeAddonInterface.ReShadeIsSignedByReShade)
                {
                    warning += warning.Length > 0 ? "\n" : string.Empty;
                    warning += Loc.Localize(
                        "ReShadeNoAddonSupportNotificationContent",
                        "你安装的 ReShade 版本不支持完整 Addon 功能，可能与 Dalamud 或游戏存在兼容性问题\n" +
                        "请下载并安装支持完整 Addon 功能的 ReShade 版本");
                }

                return warning.Length > 0 ? warning : null;
            })
        {
            FriendlyEnumNameGetter = x => x switch
            {
                ReShadeHandlingMode.Default                           => "默认模式",
                ReShadeHandlingMode.UnwrapReShade                     => "解包模式",
                ReShadeHandlingMode.ReShadeAddonPresent               => "ReShade Addon（当前状态）",
                ReShadeHandlingMode.ReShadeAddonReShadeOverlay        => "ReShade Addon（reshade_overlay）",
                ReShadeHandlingMode.HookReShadeDxgiSwapChainOnPresent => "Hook ReShade::DXGISwapChain::OnPresent",
                ReShadeHandlingMode.None                              => "不处理",
                _                                                     => "<无效值>",
            },
        },

        /* // Making this a console command instead, for now
        new GapSettingsEntry(5, true),

        new EnumSettingsEntry<SwapChainHelper.HookMode>(
            Loc.Localize("DalamudSettingsSwapChainHookMode", "Swap chain hooking mode"),
            Loc.Localize(
                "DalamudSettingsSwapChainHookModeHint",
                "Depending on addons aside from Dalamud you use, you may have to use different options for Dalamud and other addons to cooperate.\nRestart is required for changes to take effect."),
            c => c.SwapChainHookMode,
            (v, c) => c.SwapChainHookMode = v,
            fallbackValue: SwapChainHelper.HookMode.ByteCode),
            */

        /* Disabling profiles after they've been enabled doesn't make much sense, at least not if the user has already created profiles.
        new GapSettingsEntry(5, true),

        new SettingsEntry<bool>(
            Loc.Localize("DalamudSettingsEnableProfiles", "Enable plugin collections"),
            Loc.Localize("DalamudSettingsEnableProfilesHint", "Enables plugin collections, which lets you create toggleable lists of plugins."),
            c => c.ProfilesEnabled,
            (v, c) => c.ProfilesEnabled = v),
            */
    ];

    public override string Title => Loc.Localize("DalamudSettingsExperimental", "Experimental");

    public override void Draw()
    {
        base.Draw();

        ImGuiHelpers.SafeTextColoredWrapped(
            ImGuiColors.DalamudGrey,
            "Total memory used by Dalamud & Plugins: " + Util.FormatBytes(GC.GetTotalMemory(false)));
        ImGuiHelpers.ScaledDummy(15);
    }
}
