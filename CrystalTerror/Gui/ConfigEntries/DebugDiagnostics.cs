namespace CrystalTerror.Gui.ConfigEntries;

using CrystalTerror.Helpers;
using NightmareUI.PrimaryUI;
using Dalamud.Bindings.ImGui;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

/// <summary>
/// DEBUG-only config tab surfacing the import-timing samples collected by <see cref="PerfDiagnostics"/>.
/// Registered in <c>ConfigWindow.ConfigTabs</c> only under <c>#if DEBUG</c>.
/// </summary>
public class DebugDiagnostics : ConfigEntry
{
    private static readonly Vector4 SlowColor = new(1f, 0.4f, 0.4f, 1f);
    private const double SlowThresholdMs = 1000;

    public override string Path => "Debug";

    public override NuiBuilder? Builder { get; init; }

    public DebugDiagnostics()
    {
        Builder = new NuiBuilder()
            .Section("Import Timing")
            .Widget(() =>
            {
                ImGui.TextWrapped("Per-import stage timings (DEBUG build only). Each territory change fires a 'Framework' import. All times in milliseconds; Total over 1000ms is highlighted.");
                ImGui.Spacing();

                if (ImGui.Button("Copy"))
                    ImGui.SetClipboardText(PerfDiagnostics.BuildReport());
                ImGui.SameLine();
                if (ImGui.Button("Clear"))
                    PerfDiagnostics.Clear();

                var samples = PerfDiagnostics.Snapshot();
                ImGui.SameLine();
                ImGui.TextDisabled($"{samples.Count} sample(s)");

                if (samples.Count == 0)
                {
                    ImGui.Spacing();
                    ImGui.TextWrapped("No samples yet - change territory (or force an import) to capture timings.");
                    return;
                }

                if (ImGui.BeginTable("ct_perf_table", 8,
                        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Time");
                    ImGui.TableSetupColumn("Source");
                    ImGui.TableSetupColumn("Import");
                    ImGui.TableSetupColumn("Merge");
                    ImGui.TableSetupColumn("Save");
                    ImGui.TableSetupColumn("Total");
                    ImGui.TableSetupColumn("Ret");
                    ImGui.TableSetupColumn("RetIPC");
                    ImGui.TableHeadersRow();

                    for (int i = samples.Count - 1; i >= 0; i--) // newest first
                    {
                        var s = samples[i];
                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(s.TimeUtc.ToLocalTime().ToString("HH:mm:ss"));
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(s.Source);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(s.ImportMs.ToString("F1"));
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(s.MergeMs.ToString("F1"));
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(s.SaveMs.ToString("F1"));
                        ImGui.TableNextColumn();
                        if (s.TotalMs >= SlowThresholdMs)
                            ImGui.TextColored(SlowColor, s.TotalMs.ToString("F1"));
                        else
                            ImGui.TextUnformatted(s.TotalMs.ToString("F1"));
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(s.RetainerCount.ToString());
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(s.RetainerIpcMs.ToString("F1"));
                    }

                    ImGui.EndTable();
                }
            });
    }
}
