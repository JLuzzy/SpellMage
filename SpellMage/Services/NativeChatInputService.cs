using System;
using SamplePlugin.Models;
using Dalamud.Game.Gui;
using System.Linq;
using System.Text;
// Avoid compile-time dependency on specific FFXIVClientStructs member names.
// We'll probe the dev-provided assembly at runtime using reflection and Marshal reads.

// NOTE: Only `NativeChatInputService` contains unsafe/native access. All operations are defensive and read-only.

namespace SamplePlugin.Services;

/// <summary>
/// Attempts to detect and read the native FFXIV chat input.
/// This implementation is conservative: it does not use any unsafe pointer access
/// and returns IsAvailable=false explaining that native reading is not implemented.
/// Future improvements may attempt FFXIVClientStructs / sigscan-based reads behind an explicit experimental setting.
/// </summary>
public sealed class NativeChatInputService
{
    public NativeChatInputService()
    {
    }

    /// <summary>
    /// Try to get the current native chat input. This method never throws.
    /// Default conservative behavior: attempts a reflective probe and returns info in StatusMessage.
    /// </summary>
    public ChatInputSnapshot TryGetCurrentChatInput()
    {
        try
        {
            return new ChatInputSnapshot
            {
                IsAvailable = false,
                IsFocused = false,
                Text = string.Empty,
                CursorPosition = null,
                StatusMessage = "Native read requires explicit Inspect action (use Inspect Native Chat Input)"
            };
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "NativeChatInputService.TryGetCurrentChatInput failed");
            return new ChatInputSnapshot
            {
                IsAvailable = false,
                IsFocused = false,
                Text = string.Empty,
                CursorPosition = null,
                StatusMessage = "Error while attempting to read native chat input"
            };
        }
    }

    private DateTime lastInspect = DateTime.MinValue;

    /// <summary>
    /// Performs guarded inspection using Dalamud IGameGui.GetAddonByName (read-only).
    /// Throttled to avoid spam. Returns a detailed report string.
    /// </summary>
    public string InspectNativeAddons()
    {
        try
        {
            if (Plugin.GameGui == null) return "IGameGui not available via IoC.";

            // throttle
            var now = DateTime.UtcNow;
            if ((now - lastInspect).TotalSeconds < 2)
                return "Inspection throttled; try again shortly.";
            lastInspect = now;

            var candidateNames = new[] { "ChatLog", "ChatEdit", "ChatInput", "ChatFrame", "Chat" };
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Native addon inspection using IGameGui.GetAddonByName:");

            foreach (var name in candidateNames)
            {
                try
                {
                    var ptr = Plugin.GameGui.GetAddonByName(name);
                    sb.AppendLine($"Addon '{name}': {ptr.ToString()}");
                    // If the configuration allows native reads, attempt a typed FFXIVClientStructs inspection (read-only, heavily guarded)
                    if (Plugin.PluginInterface.GetPluginConfig() is SamplePlugin.Configuration cfg && cfg.EnableNativeChatRead)
                    {
                        sb.AppendLine(InspectNativeAddonTyped(ptr, name));
                    }
                    else
                    {
                        sb.AppendLine("  Note: deeper AtkUnitBase field reads are available but disabled by configuration.");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Addon '{name}': inspect failed: {ex.Message}");
                }
            }

            sb.AppendLine("Inspection complete.");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "InspectNativeAddons failed");
            return "InspectNativeAddons error: " + ex.Message;
        }
    }

    /// <summary>
    /// Inspect loaded assemblies for a type exposing GetAddonByName and attempt to query a list of common chat addon names.
    /// Returns a textual report summarizing findings. This is read-only and uses reflection only.
    /// </summary>
    public string InspectAddons()
    {
        try
        {
            var candidateNames = new[] { "ChatLog", "ChatEdit", "ChatInput", "ChatFrame", "Chat" };
            var report = new System.Text.StringBuilder();
            report.AppendLine("Inspecting loaded assemblies for GetAddonByName-like APIs...");

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    System.Reflection.MethodInfo? getAddon = null;
                    try { getAddon = t.GetMethod("GetAddonByName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static); } catch { }
                    if (getAddon == null) continue;

                    report.AppendLine($"Found candidate type: {t.FullName} in {asm.GetName().Name}");

                    // Try to obtain an instance: look for static Instance/Singleton properties or fields
                    object? instance = null;
                    try
                    {
                        var prop = t.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (prop != null) instance = prop.GetValue(null);
                        if (instance == null)
                        {
                            var field = t.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            if (field != null) instance = field.GetValue(null);
                        }
                    }
                    catch { }

                    // If no instance, and method is static, we can invoke statically
                    foreach (var name in candidateNames)
                    {
                        try
                        {
                            object? addonObj = null;
                            if (getAddon.IsStatic)
                                addonObj = getAddon.Invoke(null, new object[] { name });
                            else if (instance != null)
                                addonObj = getAddon.Invoke(instance, new object[] { name });
                            else
                                continue;

                            if (addonObj == null)
                            {
                                report.AppendLine($"  {name}: not found (null)");
                                continue;
                            }

                            report.AppendLine($"  {name}: found object type {addonObj.GetType().FullName}");

                            // Inspect object for string-like fields/properties that might contain input text
                            var foundText = false;
                            var ot = addonObj.GetType();
                            foreach (var p in ot.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                            {
                                try
                                {
                                    if (p.PropertyType == typeof(string))
                                    {
                                        var val = p.GetValue(addonObj) as string;
                                        if (!string.IsNullOrEmpty(val))
                                        {
                                            report.AppendLine($"    Property {p.Name}: '{(val.Length>200?val.Substring(0,200)+"...":val)}'");
                                            foundText = true;
                                        }
                                    }
                                }
                                catch { }
                            }

                            foreach (var f in ot.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                            {
                                try
                                {
                                    if (f.FieldType == typeof(string))
                                    {
                                        var val = f.GetValue(addonObj) as string;
                                        if (!string.IsNullOrEmpty(val))
                                        {
                                            report.AppendLine($"    Field {f.Name}: '{(val.Length>200?val.Substring(0,200)+"...":val)}'");
                                            foundText = true;
                                        }
                                    }
                                    else if (f.FieldType == typeof(System.IntPtr))
                                    {
                                        var ip = (System.IntPtr)f.GetValue(addonObj)!;
                                        report.AppendLine($"    Field {f.Name}: IntPtr 0x{ip.ToString("x")}");
                                    }
                                }
                                catch { }
                            }

                            if (!foundText)
                                report.AppendLine("    No obvious managed string fields/properties found; native text may be in unmanaged nodes.");
                        }
                        catch (System.Reflection.TargetInvocationException tie)
                        {
                            report.AppendLine($"  {name}: invocation threw {tie.InnerException?.GetType().FullName}: {tie.InnerException?.Message}");
                        }
                        catch (Exception ex)
                        {
                            report.AppendLine($"  {name}: error {ex.GetType().FullName}: {ex.Message}");
                        }
                    }
                }
            }

            report.AppendLine("Inspection complete.");
            return report.ToString();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "InspectAddons failed");
            return "InspectAddons failed: " + ex.Message;
        }
    }

    // Runtime-detection inspector: checks whether FFXIVClientStructs types are available
    // and reports guidance for enabling typed reads. This avoids compile-time dependency
    // so the plugin builds by default.
    private string InspectNativeAddonTyped(object addonHandle, string name)
    {
        var sb = new StringBuilder();
        try
        {
            if (addonHandle == null)
            {
                sb.AppendLine($"  {name}: addon handle is null.");
                return sb.ToString();
            }

            // Try to extract an IntPtr from common wrapper shapes
            IntPtr handle = IntPtr.Zero;
            try
            {
                if (addonHandle is IntPtr ip) handle = ip;
                else if (addonHandle is long l) handle = new IntPtr(l);
                else
                {
                    var t = addonHandle.GetType();
                    var props = new[] { "Address", "RawAddress", "Raw", "Ptr", "Pointer", "Handle" };
                    foreach (var pn in props)
                    {
                        try
                        {
                            var p = t.GetProperty(pn, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            if (p == null) continue;
                            var val = p.GetValue(addonHandle);
                            if (val is IntPtr ip2) { handle = ip2; break; }
                            if (val is long l2) { handle = new IntPtr(l2); break; }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to extract IntPtr from addon handle");
            }

            if (handle == IntPtr.Zero)
            {
                sb.AppendLine($"  {name}: could not obtain native handle from addon object (type: {addonHandle.GetType().FullName}).");
                sb.AppendLine("  Tip: Dalamud dev files normally provide a handle type (AtkUnitBasePtr) with an Address/Raw property; ensure dev files are present.");
                return sb.ToString();
            }

            sb.AppendLine($"  {name}: native handle 0x{handle.ToInt64():x}");

            // Probe for FFXIVClientStructs assembly and Atk types at runtime
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name?.StartsWith("FFXIVClientStructs") == true || a.GetName().Name?.Contains("FFXIVClientStructs") == true);
            if (asm == null)
            {
                sb.AppendLine("  FFXIVClientStructs assembly not loaded in AppDomain.");
                sb.AppendLine("  Tip: ensure XIVLauncher/Dalamud dev hooks are available to the plugin at runtime.");
                return sb.ToString();
            }

            Type? tUnit = asm.GetTypes().FirstOrDefault(t => t.Name.IndexOf("AtkUnitBase", StringComparison.OrdinalIgnoreCase) >= 0);
            if (tUnit == null)
            {
                sb.AppendLine("  Could not find a type named AtkUnitBase in FFXIVClientStructs.");
                return sb.ToString();
            }

            sb.AppendLine($"  Found type: {tUnit.FullName}");

            // Try to locate a field that points to a root/node pointer
            string[] rootCandidates = { "RootNode", "rootNode", "AtkResNode", "Root" };
            System.Reflection.FieldInfo? rootField = null;
            foreach (var c in rootCandidates)
            {
                rootField = tUnit.GetField(c, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (rootField != null) break;
            }

            if (rootField == null)
            {
                // fallback: pick any IntPtr/UInt64 field
                rootField = tUnit.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .FirstOrDefault(f => f.FieldType == typeof(IntPtr) || f.FieldType == typeof(ulong) || f.FieldType == typeof(long));
            }

            if (rootField == null)
            {
                sb.AppendLine("  Could not find a candidate root node field on AtkUnitBase. Listing fields:");
                foreach (var f in tUnit.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    sb.AppendLine($"    {f.FieldType.Name} {f.Name}");
                return sb.ToString();
            }

            sb.AppendLine($"  Using root field: {rootField.Name} (type {rootField.FieldType.Name})");

            // Obtain offset and read the pointer value
            try
            {
                var offset = (int)System.Runtime.InteropServices.Marshal.OffsetOf(tUnit, rootField.Name);
                sb.AppendLine($"    Field offset: {offset}");
                var rootPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(handle, offset);
                sb.AppendLine($"    Root pointer: 0x{rootPtr.ToInt64():x}");

                if (rootPtr == IntPtr.Zero)
                {
                    sb.AppendLine("    Root pointer is null; UI may not be initialized or names differ.");
                    return sb.ToString();
                }

                // Find node type and its child/sibling offsets
                var tNode = asm.GetTypes().FirstOrDefault(t => t.Name.IndexOf("AtkResNode", StringComparison.OrdinalIgnoreCase) >= 0 || t.Name.IndexOf("Node", StringComparison.OrdinalIgnoreCase) >= 0);
                if (tNode == null)
                {
                    sb.AppendLine("    Could not find AtkResNode-like type. Listing assembly types for debugging:");
                    foreach (var tn in asm.GetTypes().Where(tt => tt.Name.IndexOf("Atk", StringComparison.OrdinalIgnoreCase) >= 0).Take(40))
                        sb.AppendLine($"      {tn.FullName}");
                    return sb.ToString();
                }

                sb.AppendLine($"    Node type: {tNode.FullName}");

                // Candidate child/sibling field names
                string[] childNames = { "FirstChild", "Child", "ChildNode", "FirstChildNode" };
                string[] nextNames = { "NextSibling", "Next", "Sibling" };

                System.Reflection.FieldInfo? childField = childNames.Select(n => tNode.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)).FirstOrDefault(f => f != null);
                System.Reflection.FieldInfo? nextField = nextNames.Select(n => tNode.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)).FirstOrDefault(f => f != null);

                if (childField == null || nextField == null)
                {
                    sb.AppendLine("    Could not locate child/next fields by common names. Listing node fields:");
                    foreach (var f in tNode.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                        sb.AppendLine($"      {f.FieldType.Name} {f.Name}");
                    // continue anyway with best-effort
                }

                // Traverse a small number of nodes using Marshal.ReadIntPtr at computed offsets
                int maxDepth = 4;
                int maxNodes = 80;
                int visited = 0;

                void Traverse(IntPtr nodePtr, int depth)
                {
                    if (nodePtr == IntPtr.Zero) return;
                    if (depth > maxDepth) return;
                    if (visited >= maxNodes) return;
                    visited++;
                    sb.AppendLine($"      Node ptr=0x{nodePtr.ToInt64():x} depth={depth}");

                    // try to read an id/type field if available
                    var idField = tNode.GetField("NodeID", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                               ?? tNode.GetField("Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (idField != null)
                    {
                        try
                        {
                            var idOffset = (int)System.Runtime.InteropServices.Marshal.OffsetOf(tNode, idField.Name);
                            var idVal = System.Runtime.InteropServices.Marshal.ReadInt32(nodePtr, idOffset);
                            sb.AppendLine($"        {idField.Name} = {idVal}");
                        }
                        catch { }
                    }

                    // recurse child
                    if (childField != null)
                    {
                        try
                        {
                            var off = (int)System.Runtime.InteropServices.Marshal.OffsetOf(tNode, childField.Name);
                            var childPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(nodePtr, off);
                            if (childPtr != IntPtr.Zero) Traverse(childPtr, depth + 1);
                        }
                        catch { }
                    }

                    // next sibling
                    if (nextField != null)
                    {
                        try
                        {
                            var off2 = (int)System.Runtime.InteropServices.Marshal.OffsetOf(tNode, nextField.Name);
                            var nextPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(nodePtr, off2);
                            if (nextPtr != IntPtr.Zero) Traverse(nextPtr, depth);
                        }
                        catch { }
                    }
                }

                try { Traverse(rootPtr, 0); } catch { }
                sb.AppendLine($"    Traversal visited {visited} nodes (limit {maxNodes}).");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error while probing AtkUnitBase via reflection/Marshal");
                sb.AppendLine("    Error while probing native structures: " + ex.Message);
            }

        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "InspectNativeAddonTyped error");
            sb.AppendLine($"  error: {ex.Message}");
        }

        return sb.ToString();
    }
}
