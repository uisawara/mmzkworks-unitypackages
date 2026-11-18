using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muDashboard.Editor
{
    internal static class DashboardCommands
    {
        internal const string ModeOwnWindow = "Own Window";
        internal const string ModeThisWindow = "This Window";

        public static IEnumerable<ShortcutCommand> Create()
        {
            yield return new ShortcutCommand
            {
                Id = "dashboard.open",
                Category = ShortcutCommandCategory.Dashboard,
                Name = "Open Dashboard",
                Description = "Opens another dashboard panel (or the selected *.dashboard.json). " +
                              "Own Window gives it its own Dashboard window; This Window replaces the panel shown here, " +
                              "which ends a running action.",
                Params = new[]
                {
                    ShortcutCommandParam.Asset<TextAsset>("dashboard", "Dashboard", "Empty = the selected *.dashboard.json")
                        .WithAssetFilter(DashboardFiles.IsPanelFile, "Pick a *.dashboard.json file"),
                    ShortcutCommandParam.Choice("mode", "Open In", new[] { ModeOwnWindow, ModeThisWindow }, null,
                        "Own Window = a Dashboard window per panel (an open one is focused); This Window = switch the window that ran the command")
                },
                Run = OpenDashboard
            };
        }

        private static bool OpenDashboard(ShortcutCommandContext ctx)
        {
            if (!ctx.TryGetAsset<TextAsset>("dashboard", out var asset)) return false;

            string path;
            if (asset != null)
            {
                path = AssetDatabase.GetAssetPath(asset);
                if (!DashboardFiles.IsPanelFile(path)) return ctx.Fail($"Not a dashboard file: {path}");
            }
            else
            {
                path = ShortcutCommandContext.SelectedAssets()
                    .Select(AssetDatabase.GetAssetPath)
                    .FirstOrDefault(DashboardFiles.IsPanelFile);
                if (path == null) return ctx.Fail("Set a dashboard or select a *.dashboard.json");
            }

            var replace = ctx.GetString("mode") == ModeThisWindow;
            DashboardWindow.OpenPanel(path, replace ? ctx.Window : null);
            return true;
        }
    }
}
