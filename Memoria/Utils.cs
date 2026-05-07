using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using Memoria.API.Models.Responses.Player;
using Memoria.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Memoria
{
    public static class Utils
    {
        public static void DrawHelp(bool AtTheEnd, string helpMessage)
        {
            if (AtTheEnd)
            {
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");

                SetHoverTooltip(helpMessage);
            }
            else
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "(?)");
                SetHoverTooltip(helpMessage);
                ImGui.SameLine();
            }
        }
        public static string GetWorldName(uint worldId)
        {
            var world = Plugin.DataManager.GetExcelSheet<World>().GetRowOrDefault(worldId);
            if (world != null)
            {
                return world.Value.Name.ToString();
            }
            return "Unknown";
        }

        public static World? GetWorld(uint worldId)
        {
            var worldSheet = Plugin.DataManager.GetExcelSheet<World>();
            if (worldSheet.TryGetRow(worldId, out var world))
            {
                return world;
            }

            return null;
        }

        public static string GetRegionCode(World? world)
        {
            if (world == null)
            {
                return string.Empty;
            }

            return world.Value.DataCenter.ValueNullable?.Region.RowId switch
            {
                1u => "JP",
                2u => "NA",
                3u => "EU",
                4u => "OC",
                _ => string.Empty,
            };
        }
        public static string GetRegionLongName(World? world)
        {
            if (world == null)
            {
                return string.Empty;
            }

            return world.Value.DataCenter.ValueNullable?.Region.RowId switch
            {
                1u => Loc.UtilsJP,
                2u => Loc.UtilsNA,
                3u => Loc.UtilsEU,
                4u => Loc.UtilsOCE,
                _ => string.Empty,
            };
        }

        public static bool IsWorldValid(uint worldId)
        {
            var world = GetWorld(worldId);
            return IsWorldValid(world);
        }

        public static bool IsWorldValid(World? world)
        {
            if (world == null || world.Value.Name.IsEmpty)
            {
                return false;
            }

            var regionCode = GetRegionCode(world);
            if (string.IsNullOrEmpty(regionCode))
            {
                return false;
            }

            return char.IsUpper(world.Value.Name.ToString()[0]);
        }

        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }
            return string.Join(" ", words);
        }

        public static string GetJobName(byte? jobId)
        {
            if (!jobId.HasValue || jobId.Value == 0)
                return "Unknown";

            // Validate job ID is within reasonable bounds (1-100 to allow for future jobs)
            if (jobId.Value > 100)
            {
                Plugin.Log.Warning($"Invalid job ID {jobId.Value} detected - outside valid range (1-100)");
                return "Unknown";
            }

            var jobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
            if (jobSheet.TryGetRow(jobId.Value, out var job))
            {
                var jobName = job.Name.ToString();
                
                // Additional validation - ensure we got a valid name
                if (string.IsNullOrWhiteSpace(jobName))
                {
                    Plugin.Log.Warning($"Job ID {jobId.Value} returned empty name from game data");
                    return "Unknown";
                }
                
                return ToTitleCase(jobName);
            }

            Plugin.Log.Warning($"Job ID {jobId.Value} not found in game data sheet");
            return "Unknown";
        }

        public static string GetJobAbbreviation(byte? jobId)
        {
            if (!jobId.HasValue || jobId.Value == 0)
                return "???";

            // Validate job ID is within reasonable bounds (1-100 to allow for future jobs)
            if (jobId.Value > 100)
            {
                Plugin.Log.Warning($"Invalid job ID {jobId.Value} detected for abbreviation - outside valid range (1-100)");
                return "???";
            }

            var jobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
            if (jobSheet.TryGetRow(jobId.Value, out var job))
            {
                var abbreviation = job.Abbreviation.ToString();
                
                // Additional validation - ensure we got a valid abbreviation
                if (string.IsNullOrWhiteSpace(abbreviation))
                {
                    Plugin.Log.Warning($"Job ID {jobId.Value} returned empty abbreviation from game data");
                    return "???";
                }
                
                return abbreviation;
            }

            Plugin.Log.Warning($"Job ID {jobId.Value} not found in game data sheet for abbreviation");
            return "???";
        }

        public static string FormatJobAndLevel(byte? jobId, short? jobLevel)
        {
            if (!jobId.HasValue || jobId.Value == 0)
                return "Unknown";

            var jobName = GetJobName(jobId);
            if (jobLevel.HasValue && jobLevel.Value > 0)
            {
                return $"{jobName} Lv.{jobLevel.Value}";
            }

            return jobName;
        }

        public static void TextCopy(Vector4 col, string text)
        {
            ImGui.TextColored(col, text);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
#pragma warning disable
                ImGui.SetClipboardText(text);
#pragma warning restore
            }
        }

        public static bool ButtonCopy(string buttonText, string copyText)
        {
            ImGui.Button(buttonText);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
#pragma warning disable
                ImGui.SetClipboardText(copyText);
                return true;
#pragma warning restore
            }
            return false;
        }

        public static void CompletionProgressBar(int progress, int total, int height = 20, bool parseColors = true)
        {
            ImGui.BeginGroup();

            var cursor = ImGui.GetCursorPos();
            var sizeVec = new Vector2(ImGui.GetContentRegionAvail().X, height);

            //Calculate percentage earlier in code
            decimal percentage2 = (decimal)progress / total;

            var percentage = (float)progress / (float)total;
            var label = string.Format("{0:P} Complete ({1}/{2})", percentage2, progress, total);
            var labelSize = ImGui.CalcTextSize(label);

            if (parseColors) ImGui.PushStyleColor(ImGuiCol.PlotHistogram, GetBarseColor(percentage));
            ImGui.ProgressBar(percentage, sizeVec, "");
            if (parseColors) ImGui.PopStyleColor();

            ImGui.SetCursorPos(new Vector2(cursor.X + sizeVec.X - labelSize.X - 4, cursor.Y));
            ImGui.TextUnformatted(label);

            ImGui.EndGroup();
        }
        public static void CenteredWrappedText(string text)
        {
            var availableWidth = ImGui.GetContentRegionAvail().X;
            var textWidth = ImGui.CalcTextSize(text).X;

            // calculate the indentation that centers the text on one line, relative
            // to window left, regardless of the `ImGuiStyleVar_WindowPadding` value
            var textIndentation = (availableWidth - textWidth) * 0.5f;

            // if text is too long to be drawn on one line, `text_indentation` can
            // become too small or even negative, so we check a minimum indentation
            var minIndentation = 20.0f;
            if (textIndentation <= minIndentation)
            {
                textIndentation = minIndentation;
            }

            ImGui.Dummy(new Vector2(0));
            ImGui.SameLine(textIndentation);
            ImGui.PushTextWrapPos(availableWidth - textIndentation);
            ImGui.TextWrapped(text);
            ImGui.PopTextWrapPos();
        }

        public static void TextWrapped(string s)
        {
            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(s);
            ImGui.PopTextWrapPos();
        }

        public static void TextWrapped(Vector4? col, string s)
        {
            ImGui.PushTextWrapPos(0);
            Text(col, s);
            ImGui.PopTextWrapPos();
        }

        public static void OpenFolder(string path)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        public static void ColoredErrorTextWrapped(string s)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                ImGui.PushTextWrapPos(0);
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (s.StartsWith(Loc.ApiError))
                    textColor = ImGuiColors.DalamudRed;

                Text(textColor, s);
                ImGui.PopTextWrapPos();
            }
        }

        public static void ColoredTextWrapped(Vector4? textColor, string s)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                ImGui.PushTextWrapPos(0);
                Text(textColor, s);
                ImGui.PopTextWrapPos();
            }
        }

        public static void ColoredTextWrapped(string s, string ping)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                ImGui.PushTextWrapPos(0);
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (s.StartsWith(Loc.ApiError))
                    textColor = ImGuiColors.DalamudRed;
                if (!string.IsNullOrWhiteSpace(ping))
                    Text(textColor, $"{s} ({ping})");
                else
                    Text(textColor, s);
                ImGui.PopTextWrapPos();
            }
        }

        public static void Text(Vector4? col, string s)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, (System.Numerics.Vector4)col!.Value);
            ImGui.TextUnformatted(s);
            ImGui.PopStyleColor();
        }

        public static Vector4 GetBarseColor(double value)
        {
            return value switch
            {
                1 => ImGuiColors.ParsedGold,
                >= 0.95 => ImGuiColors.ParsedOrange,
                >= 0.75 => ImGuiColors.ParsedPurple,
                >= 0.50 => ImGuiColors.ParsedBlue,
                >= 0.25 => ImGuiColors.ParsedGreen,
                _ => ImGuiColors.ParsedGrey * 1.75f
            };
        }
        public static void ShowColoredMessage(string Message)
        {
            if (!string.IsNullOrWhiteSpace(Message))
            {
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (Message.StartsWith(Loc.ApiError))
                    textColor = ImGuiColors.DalamudRed;
                ImGui.TextColored(textColor, $"{Message}");
            }
        }
        public static void ShowColoredMessage(string Message, string Ping)
        {
            if (!string.IsNullOrWhiteSpace(Message))
            {
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (Message.StartsWith(Loc.ApiError))
                    textColor = ImGuiColors.DalamudRed;
                ImGui.TextColored(textColor, $"{Message} ({Ping})");
            }
        }

        public static void SetHoverTooltip(string tooltip)
        {
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tooltip);
                ImGui.EndTooltip();
            }
        }

        public static void IconText(Vector4 textColor, FontAwesomeIcon icon)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            {
                ImGui.TextUnformatted(icon.ToIconString());
            }
        }

        public static void HeaderWarningText(Vector4 textColor, FontAwesomeIcon icon, string text)
        {
            ImGuiHelpers.ScaledDummy(5.0f);

            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            {
                ImGui.TextUnformatted($"{icon.ToIconString()}");
                using (ImRaii.PushFont(UiBuilder.DefaultFont))
                {
                    ImGui.SameLine();
                    ImGui.TextUnformatted($"{text}");
                }
            }
        }

        public static void HeaderProfileVisitInfoText(PlayerDetailed.PlayerProfileVisitInfoDto visitInfo)
        {
            ImGuiHelpers.ScaledDummy(5.0f);
            TextWrapped(string.Format(Loc.DtCharacterVisitInfo, Tools.ToTimeSinceString((int)visitInfo.LastProfileVisitDate!.Value), visitInfo.ProfileTotalVisitCount, visitInfo.UniqueVisitorCount));
            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
        }

        public static string GenerateRandomKey(int length = 20)
        {
            char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            byte[] array = new byte[length * 4];
            using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
            {
                randomNumberGenerator.GetBytes(array);
            }

            StringBuilder stringBuilder = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                long num = BitConverter.ToUInt32(array, i * 4) % chars.Length;
                stringBuilder.Append(chars[num]);
            }

            return stringBuilder.ToString();
        }

        public static string clientVer => Assembly.GetExecutingAssembly().GetName().Version!.ToString();

        public static string Vector3ToString(Vector3 v)
        {
            return string.Format("{0:0.00}.{1:0.00}.{2:0.00}", v.X, v.Y, v.Z);
        }

        public static Vector3 Vector3FromString(String s)
        {
            string[] parts = s.Split(new string[] { "." }, StringSplitOptions.None);
            return new Vector3(
                float.Parse(parts[0]),
                float.Parse(parts[1]),
                float.Parse(parts[2]));
        }

        public static void AddNotification(string content, NotificationType type, bool minimized = true)
        {
            Plugin.Notification.AddNotification(new Notification { Content = content, Type = type, Minimized = minimized });
        }

        public static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0 " + suf[0];

            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 2);
            return (Math.Sign(byteCount) * num).ToString("N2") + " " + suf[place];
        }

        public static bool CtrlShiftButton(FontAwesomeIcon icon, string label, string tooltip = "")
        {
            var ctrlShiftHeld = ImGui.GetIO() is { KeyCtrl: true, KeyShift: true };

            bool ret;
            using (ImRaii.Disabled(!ctrlShiftHeld))
                ret = ImGuiComponents.IconButtonWithText(icon, label) && ctrlShiftHeld;

            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(tooltip);

            return ret;
        }

        public static void TryOpenURI(Uri uri)
        {
            try
            {
                Dalamud.Utility.Util.OpenLink(uri.ToString());
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Plugin.Log.Warning($"Failed to open link - no default browser configured: {ex.Message}");
                AddNotification("No default browser configured. Please set a default browser.", NotificationType.Error);
            }
            catch (System.IO.FileNotFoundException ex)
            {
                Plugin.Log.Warning($"Failed to open link - browser executable not found: {ex.Message}");
                AddNotification("Default browser not found. Please check your browser installation.", NotificationType.Error);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Unexpected error while opening link {uri}: {ex.Message}");
                AddNotification("Failed to open the link in the browser, please report this issue", NotificationType.Error);
            }
        }

        public static void SetupTableColumns(string[] columns)
        {
            foreach (var column in columns)
            {
                ImGui.TableSetupColumn(column, ImGuiTableColumnFlags.WidthFixed);
            }
            ImGui.TableHeadersRow();
        }

        /// <summary>
        /// Displays the world information in the ImGui table.
        /// </summary>
        /// <param name="worldId">The ID of the world to resolve.</param>
        /// <param name="helpText">Whether to display the detailed help text.</param>
        public static void DisplayWorldInfo(uint? worldId, bool helpText = true)
        {
            if (!worldId.HasValue)
            {
                ImGui.Text("---");
                return;
            }

            var world = Utils.GetWorld((uint)worldId);
            if (world != null)
            {
                if (helpText)
                {
                    Utils.DrawHelp(false, $"{Loc.UtilRegion}: {Utils.GetRegionCode(world.Value)}\n{Loc.UtilDataCenter}: {world.Value.DataCenter.Value.Name}\n{Loc.MnWorld}: {world.Value.Name}");
                }

                ImGui.Text(world.Value.Name.ExtractText());
            }
            else
            {
                ImGui.Text("---");
            }
        }

        /// <summary>
        /// Displays a text with a copy button when "Ctrl" key is pressed.
        /// </summary>
        /// <param name="clipboardText">The text to copy to the clipboard.</param>
        /// <param name="buttonId">The unique ID for the copy button.</param>
        public static void CopyButton(string clipboardText, string buttonId)
        {
            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, $"##{buttonId}", new System.Numerics.Vector2(23, 22)))
                {
                    ImGui.SetClipboardText(clipboardText);
                }
                SetHoverTooltip(Loc.UtilsCopyText);
                ImGui.SameLine();
            }
        }

        public static void WarningIconWithTooltip(string tooltipText)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudWhite))
            {
                ImGui.TextUnformatted($"{FontAwesomeIcon.Exclamation.ToIconString()}");
                using (ImRaii.PushFont(UiBuilder.DefaultFont))
                {
                    SetHoverTooltip(tooltipText);
                }
            }
            ImGui.SameLine();
        }

        public static void IconWithTooltip(FontAwesomeIcon icon, string tooltipText)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudWhite))
            {
                ImGui.TextUnformatted($"{icon.ToIconString()}");
                using (ImRaii.PushFont(UiBuilder.DefaultFont))
                {
                    SetHoverTooltip(tooltipText);
                }
            }
            ImGui.SameLine();
        }

        public static long[] ExternalDbTimestamps = new long[]
            {
                0,
                1716465600,
                1716465601,
                1716465602,
                1716465603,
                1716465604,
                1716465605,
                1716465606,
                1723032000,
                1723982400,
                1724068800,
                1724068810,
                1724328000,
                1724414400,
                1725451200,
                1725624000,
                1725710400,
                1726228800,
                1726488000,
                1726920000,
                1727006400,
                1727179215,
                1727265600,
                1727265602,
                1727265603,
                1727265604,
                1727265605, //-
                1727265606,
                1727265607,
                1727265608,
                1727265609,
                1727265610,
                1727265611,
                1727265612,
                1727265613,
            };

        /// <summary>
        /// Returns a ISharedImmediateTexture for the appropriate icon.
        /// </summary>
        /// <param name="iconID">ID of the icon.</param>
        public static ISharedImmediateTexture GetIcon(uint iconID)
            => Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconID, false, true));

        /// <summary>
        /// Job role categories for organizing jobs in UI
        /// </summary>
        public enum JobRole
        {
            Tank,
            Healer,
            MeleeDPS,
            PhysicalRangedDPS,
            MagicalRangedDPS,
            Crafter,
            Gatherer,
            Unknown
        }

        /// <summary>
        /// Gets the icon ID for a specific job using a static mapping.
        /// This is needed because the ClassJob.Icon property is not available in the current Lumina version.
        /// Icon IDs are based on FFXIV's internal icon numbering system.
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <returns>The icon ID for the job</returns>
        private static uint GetJobIconFromMapping(byte jobId)
        {
            return jobId switch
            {
                // Tank Jobs
                1 => 62001, // GLA
                19 => 62019, // PLD
                3 => 62003, // MRD
                21 => 62021, // WAR
                32 => 62032, // DRK
                37 => 62037, // GNB
                
                // Healer Jobs
                6 => 62006, // CNJ
                24 => 62024, // WHM
                26 => 62026, // ACN
                28 => 62028, // SCH
                33 => 62033, // AST
                40 => 62040, // SGE
                
                // Melee DPS Jobs
                2 => 62002, // PGL
                20 => 62020, // MNK
                4 => 62004, // LNC
                22 => 62022, // DRG
                29 => 62029, // ROG
                30 => 62030, // NIN
                34 => 62034, // SAM
                39 => 62039, // RPR
                41 => 62041, // VPR
                
                // Physical Ranged DPS Jobs
                5 => 62005, // ARC
                23 => 62023, // BRD
                31 => 62031, // MCH
                38 => 62038, // DNC
                
                // Magical Ranged DPS Jobs
                7 => 62007, // THM
                25 => 62025, // BLM
                27 => 62027, // SMN
                35 => 62035, // RDM
                36 => 62136, // BLU
                42 => 62042, // PCT
                
                // Crafting Jobs
                8 => 62008, // CRP
                9 => 62009, // BSM
                10 => 62010, // ARM
                11 => 62011, // GSM
                12 => 62012, // LTW
                13 => 62013, // WVR
                14 => 62014, // ALC
                15 => 62015, // CUL
                
                // Gathering Jobs
                16 => 62016, // MIN
                17 => 62017, // BTN
                18 => 62018, // FSH
                
                // Default fallback for unknown jobs
                _ => 62001 // Default to GLA icon
            };
        }

        /// <summary>
        /// Gets the icon ID for a specific job
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <returns>The icon ID for the job, or null if not found</returns>
        public static uint? GetJobIconId(byte? jobId)
        {
            if (!jobId.HasValue || jobId.Value == 0)
                return null;

            // Validate job ID is within reasonable bounds
            if (jobId.Value > 100)
            {
                Plugin.Log.Warning($"Invalid job ID {jobId.Value} detected for icon - outside valid range (1-100)");
                return null;
            }

            var jobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
            if (jobSheet.TryGetRow(jobId.Value, out var job))
            {
                // Use static mapping since ClassJob.Icon property is not available in this Lumina version
                return GetJobIconFromMapping(jobId.Value);
            }

            Plugin.Log.Warning($"Job ID {jobId.Value} not found in game data sheet for icon");
            return null;
        }

        /// <summary>
        /// Gets the texture for a specific job icon
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <returns>The job icon texture, or null if not found</returns>
        public static ISharedImmediateTexture? GetJobIcon(byte? jobId)
        {
            var iconId = GetJobIconId(jobId);
            if (!iconId.HasValue)
                return null;

            return Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId.Value, false, true));
        }

        /// <summary>
        /// Gets the role category for a specific job
        /// </summary>
        /// <param name="jobId">The job ID</param>
        /// <returns>The job role category</returns>
        public static JobRole GetJobRole(byte jobId)
        {
            return jobId switch
            {
                // Tank Jobs
                19 or 21 or 32 or 37 => JobRole.Tank, // PLD, WAR, DRK, GNB
                
                // Healer Jobs
                24 or 28 or 33 or 40 => JobRole.Healer, // WHM, SCH, AST, SGE
                
                // Melee DPS Jobs
                20 or 22 or 30 or 34 or 39 or 41 => JobRole.MeleeDPS, // MNK, DRG, NIN, SAM, RPR, VPR
                
                // Physical Ranged DPS Jobs
                23 or 31 or 38 => JobRole.PhysicalRangedDPS, // BRD, MCH, DNC
                
                // Magical Ranged DPS Jobs
                25 or 27 or 35 or 36 or 42 => JobRole.MagicalRangedDPS, // BLM, SMN, RDM, BLU, PCT
                
                // Crafting Jobs (Disciples of the Hand)
                8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 => JobRole.Crafter, // CRP, BSM, ARM, GSM, LTW, WVR, ALC, CUL
                
                // Gathering Jobs (Disciples of the Land)
                16 or 17 or 18 => JobRole.Gatherer, // MIN, BTN, FSH
                
                // Default for unknown or base classes
                _ => JobRole.Unknown
            };
        }

        /// <summary>
        /// Gets the display name for a job role
        /// </summary>
        /// <param name="role">The job role</param>
        /// <returns>The display name for the role</returns>
        public static string GetJobRoleDisplayName(JobRole role)
        {
            return role switch
            {
                JobRole.Tank => "Tank",
                JobRole.Healer => "Healer", 
                JobRole.MeleeDPS => "Melee DPS",
                JobRole.PhysicalRangedDPS => "Physical Ranged DPS",
                JobRole.MagicalRangedDPS => "Magical Ranged DPS",
                JobRole.Crafter => "Disciples of the Hand",
                JobRole.Gatherer => "Disciples of the Land",
                _ => "Other"
            };
        }

        /// <summary>
        /// Gets the color for a job role
        /// </summary>
        /// <param name="role">The job role</param>
        /// <returns>The color vector for the role</returns>
        public static Vector4 GetJobRoleColor(JobRole role)
        {
            return role switch
            {
                JobRole.Tank => new Vector4(0.2f, 0.6f, 1.0f, 1.0f), // Blue
                JobRole.Healer => new Vector4(0.0f, 0.8f, 0.0f, 1.0f), // Green
                JobRole.MeleeDPS => new Vector4(1.0f, 0.4f, 0.4f, 1.0f), // Red
                JobRole.PhysicalRangedDPS => new Vector4(1.0f, 0.6f, 0.2f, 1.0f), // Orange
                JobRole.MagicalRangedDPS => new Vector4(0.8f, 0.4f, 1.0f, 1.0f), // Purple
                JobRole.Crafter => new Vector4(0.8f, 0.8f, 0.2f, 1.0f), // Yellow
                JobRole.Gatherer => new Vector4(0.6f, 0.8f, 0.4f, 1.0f), // Light Green
                _ => new Vector4(0.7f, 0.7f, 0.7f, 1.0f) // Gray
            };
        }

        /// <summary>
        /// Gets FFLogs-style parse color based on job level (treating level as percentile)
        /// </summary>
        /// <param name="level">The job level (1-100)</param>
        /// <returns>The color vector corresponding to FFLogs parse percentiles</returns>
        public static Vector4 GetFFLogsLevelColor(int level)
        {
            return level switch
            {
                >= 100 => new Vector4(0.90f, 0.80f, 0.50f, 1.0f), // Gold - Perfect (100th percentile)
                >= 99 => new Vector4(0.89f, 0.41f, 0.66f, 1.0f),  // Pink - 99th percentile  
                >= 95 => new Vector4(1.0f, 0.50f, 0.0f, 1.0f),    // Orange - 95-98th percentile
                >= 75 => new Vector4(0.64f, 0.21f, 0.93f, 1.0f),  // Purple - 75-94th percentile
                >= 50 => new Vector4(0.0f, 0.44f, 0.87f, 1.0f),   // Blue - 50-74th percentile  
                >= 25 => new Vector4(0.12f, 1.0f, 0.0f, 1.0f),    // Green - 25-49th percentile
                _ => new Vector4(0.4f, 0.4f, 0.4f, 1.0f)          // Gray - 0-24th percentile
            };
        }

        /// <summary>
        /// Returns a ISharedImmediateTexture for the appropriate status.
        /// </summary>
        /// <param name="statusID">ID of the status.</param>
        public static ISharedImmediateTexture GetTownIcon(uint townID)
        {
            var townList = Plugin.DataManager.GameData.Excel.GetSheet<Town>();
            var town = townList.GetRow(townID);
            return GetIcon((uint)town.Icon);
        }

        public static string lodestoneCharacterUrl = "https://na.finalfantasyxiv.com/lodestone/character/";
        public static string lodestoneCharacterPrivacyUrl = "https://na.finalfantasyxiv.com/lodestone/my/setting/profile/";
        private const string AvatarBaseUrl = "https://img2.finalfantasyxiv.com/f/";
        public static string BlankAvatar = "00000000000000000000000000000000_00000000000000000000000000000000";
        public static string GetAvatarUrl(string avatarLink, bool isLarge)
        {
            if (string.IsNullOrWhiteSpace(avatarLink))
            {
                avatarLink = BlankAvatar; // Blank image
            }

            var sizeSuffix = isLarge ? "fl0.jpg" : "fc0.jpg";
            var url = $"{AvatarBaseUrl}{avatarLink}{sizeSuffix}";
            
            return url;
        }

        /// <summary>
        /// Static mapping of minion names to their acquisition methods
        /// </summary>
        private static readonly Dictionary<string, string> MinionAcquisitionData = new()
        {
            // Common quest reward minions
            {"Goobbue Sproutling", "Quest Reward"},
            {"Wind-up Airship", "Quest Reward"},
            {"Mammet #001", "Quest Reward"},
            {"Baby Opo-opo", "Quest Reward"},
            {"Wayward Hatchling", "Quest Reward"},
            {"Wind-up Cursor", "Quest Reward"},
            {"Black Chocobo Chick", "Pre-order Bonus"},
            {"Cait Sith Doll", "Pre-order Bonus"},
            
            // Dungeon drops
            {"Dust Bunny", "Dungeon Drop"},
            {"Wind-up Goblin", "Dungeon Drop"},
            {"Wind-up Titan", "Dungeon Drop"},
            {"Wind-up Succubus", "Dungeon Drop"},
            {"Fledgling Dodo", "Dungeon Drop"},
            {"Wind-up Qiqirn", "Dungeon Drop"},
            {"Slime Puddle", "Dungeon Drop"},
            {"Poro Roggo", "Dungeon Drop"},
            
            // Achievement rewards
            {"Fat Cat", "Achievement"},
            {"Wind-up Leader", "Achievement"},
            {"Midgardsormr", "Achievement"},
            {"Achievement Certificate", "Achievement"},
            {"Wind-up Warrior of Light", "Achievement"},
            {"Clockwork Barrow", "Achievement"},
            
            // Market Board / Crafted
            {"Wind-up Gentleman", "Market Board"},
            {"Wind-up Lalafell", "Market Board"},
            {"Wind-up Moogle", "Market Board"},
            {"Wind-up Delivery Moogle", "Market Board"},
            {"Wind-up Tonberry", "Market Board"},
            {"Wind-up Odin", "Market Board"},
            {"Wind-up Bahamut", "Market Board"},
            {"Model Vanguard", "Market Board"},
            
            // Raid drops
            {"Onion Prince", "Raid Drop"},
            {"Calca", "Raid Drop"},
            {"Brina", "Raid Drop"},
            {"Wind-up Ragnarok", "Raid Drop"},
            {"Alpha", "Raid Drop"},
            {"Omega-M", "Raid Drop"},
            {"Omega-F", "Raid Drop"},
            
            // PvP rewards
            {"Flame Hatchling", "PvP Reward"},
            {"Storm Hatchling", "PvP Reward"},
            {"Serpent Hatchling", "PvP Reward"},
            {"Behemoth Heir", "PvP Reward"},
            
            // Event/Seasonal
            {"Wind-up Sun", "Seasonal Event"},
            {"Tight-beaked Parrot", "Seasonal Event"},
            {"Wind-up Brickman", "Seasonal Event"},
            {"Wind-up Scathach", "Seasonal Event"},
            {"Wind-up Firion", "Seasonal Event"},
            {"Wind-up Minfillia", "Seasonal Event"},
            
            // Retainer ventures
            {"Baby Brachiosaur", "Retainer Venture"},
            {"Bitty Bigfoot", "Retainer Venture"},
            {"Lesser Panda", "Retainer Venture"},
            {"Penguin Prince", "Retainer Venture"},
            
            // Gold Saucer
            {"Fenrir Pup", "Gold Saucer"},
            {"Sabotender Emperador", "Gold Saucer"},
            {"Lord of Verminion", "Gold Saucer"},
            {"Miniature Minecart", "Gold Saucer"},
            
            // Deep Dungeon
            {"Paissa Brat", "Deep Dungeon"},
            {"Wind-up Paissa", "Deep Dungeon"},
            {"Pomfritz", "Deep Dungeon"},
            
            // Special/Rare
            {"Wind-up Namazu", "Special Quest"},
            {"Copycat Bullfrog", "Special Quest"},
            {"Wind-up Cirina", "Special Quest"},
            {"Wind-up Estinien", "Special Quest"},
            {"Wind-up Alphinaud", "Special Quest"},
            {"Wind-up Alisaie", "Special Quest"},
            
            // Treasure hunts
            {"Treasure Box", "Treasure Hunt"},
            {"Wind-up Founder", "Treasure Hunt"},
            
            // Default fallbacks for common patterns
            {"Wind-up", "Unknown"},
            {"Baby", "Unknown"},
            {"Hatchling", "Unknown"},
            {"Cub", "Unknown"},
            {"Pup", "Unknown"},
            {"Chick", "Unknown"}
        };

        /// <summary>
        /// Gets the acquisition method for a minion based on its name
        /// </summary>
        /// <param name="minionName">The name of the minion</param>
        /// <returns>The acquisition method or "Unknown" if not found</returns>
        public static string GetMinionAcquisitionMethod(string? minionName)
        {
            if (string.IsNullOrEmpty(minionName))
                return "Unknown";

            // Handle hash-based names (fallback from failed Lodestone parsing)
            if (minionName.StartsWith("Minion #") || minionName.Contains(".png") || 
                (minionName.Length > 20 && minionName.All(c => char.IsLetterOrDigit(c))))
            {
                return "Check Lodestone";
            }

            // Direct name match first
            if (MinionAcquisitionData.TryGetValue(minionName, out var method))
                return method;

            // Check for partial matches based on common prefixes/suffixes
            foreach (var kvp in MinionAcquisitionData)
            {
                if (minionName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            // Check for common minion naming patterns
            if (minionName.StartsWith("Wind-up", StringComparison.OrdinalIgnoreCase))
                return "Market Board / Quest";
            if (minionName.EndsWith("Hatchling", StringComparison.OrdinalIgnoreCase) ||
                minionName.EndsWith("Chick", StringComparison.OrdinalIgnoreCase))
                return "PvP / Event";
            if (minionName.StartsWith("Baby", StringComparison.OrdinalIgnoreCase) ||
                minionName.EndsWith("Cub", StringComparison.OrdinalIgnoreCase) ||
                minionName.EndsWith("Pup", StringComparison.OrdinalIgnoreCase))
                return "Retainer Venture";

            return "Unknown";
        }

        /// <summary>
        /// Static mapping of mount names to their acquisition methods
        /// </summary>
        private static readonly Dictionary<string, string> MountAcquisitionData = new()
        {
            // Starter/Company mounts
            {"Company Chocobo", "Quest Reward"},
            {"Chocobo", "Quest Reward"},
            {"Black Chocobo", "Pre-order Bonus"},
            {"Legacy Chocobo", "Pre-order Bonus"},
            
            // Story quest mounts
            {"Magitek Armor", "Quest Reward"},
            {"Manacutter", "Quest Reward"},
            {"Enterprise", "Quest Reward"},
            {"Regalia", "Special Quest"},
            
            // Trial/Primal mounts
            {"Nightmare", "Trial Drop"},
            {"Aithon", "Trial Drop"},
            {"Xanthos", "Trial Drop"},
            {"Gullfaxi", "Trial Drop"},
            {"Enbarr", "Trial Drop"},
            {"Markab", "Trial Drop"},
            {"Boreas", "Trial Drop"},
            {"Firebird", "Achievement"},
            {"Landerwaffe", "Trial Drop"},
            {"Kamuy of the Nine Tails", "Trial Drop"},
            {"Howl", "Trial Drop"},
            {"Reveling Kamuy", "Trial Drop"},
            {"Lunar Kamuy", "Trial Drop"},
            {"Peacock", "Trial Drop"},
            {"Blissful Kamuy", "Trial Drop"},
            
            // Raid mounts
            {"Twintania", "Raid Drop"},
            {"Demi-Bahamut", "Raid Drop"},
            {"Phoenix", "Raid Drop"},
            {"Dark Lanner", "Raid Drop"},
            {"Warring Lanner", "Raid Drop"},
            {"Round Lanner", "Raid Drop"},
            {"Sophic Lanner", "Raid Drop"},
            {"Demonic Lanner", "Raid Drop"},
            {"Zurvan's Whistle", "Raid Drop"},
            {"Alpha", "Raid Drop"},
            {"Omega", "Raid Drop"},
            {"Air Force", "Raid Drop"},
            {"Innocent Gwiber", "Raid Drop"},
            {"Shadow Gwiber", "Raid Drop"},
            {"Fae Gwiber", "Raid Drop"},
            {"Ruby Gwiber", "Raid Drop"},
            {"Emerald Gwiber", "Raid Drop"},
            {"Light Gwiber", "Raid Drop"},
            {"Dark Gwiber", "Raid Drop"},
            
            // Achievement mounts
            {"War Tiger", "Achievement"},
            {"Laurel Goobbue", "Achievement"},
            {"Behemoth", "Achievement"},
            {"Ceremony Chocobo", "Achievement"},
            {"Parade Chocobo", "Achievement"},
            {"Armored Chocobo", "Achievement"},
            {"Flying Cumulus", "Achievement"},
            {"Kirin", "Achievement"},
            {"Magitek Death Claw", "Achievement"},
            {"Amaro", "Achievement"},
            
            // PvP mounts
            {"Flame Warsteed", "PvP Reward"},
            {"Serpent Warsteed", "PvP Reward"},
            {"Storm Warsteed", "PvP Reward"},
            {"Ginga", "PvP Reward"},
            {"Raigo", "PvP Reward"},
            {"Gloria", "PvP Reward"},
            {"Bellona", "PvP Reward"},
            
            // Gold Saucer mounts
            {"Fenrir", "Gold Saucer"},
            {"Archon Throne", "Gold Saucer"},
            {"Regalia Type-G", "Gold Saucer"},
            {"SDS Fenrir", "Gold Saucer"},
            {"Gabriel mk-III", "Gold Saucer"},
            
            // Crafted/Market Board mounts
            {"Cavalry Drake", "Market Board"},
            {"Cavalry Elbst", "Market Board"},
            {"Battle Bear", "Market Board"},
            {"Battle Tiger", "Market Board"},
            {"War Panther", "Market Board"},
            {"Adamantoise", "Market Board"},
            {"Morbol", "Market Board"},
            {"Zu", "Market Board"},
            {"Falcon", "Market Board"},
            {"Coeurl", "Market Board"},
            {"Flying Chair", "Market Board"},
            {"Magitek Sky Armor", "Market Board"},
            {"Unicorn", "Market Board"},
            
            // Seasonal/Event mounts
            {"Fat Moogle", "Seasonal Event"},
            {"Snowman", "Seasonal Event"},
            {"Polar Bear", "Seasonal Event"},
            {"Sleipnir", "Seasonal Event"},
            {"Starlight Bear", "Seasonal Event"},
            
            // Deep Dungeon mounts
            {"Dodo", "Deep Dungeon"},
            {"Pegasus", "Deep Dungeon"},
            {"Night Pegasus", "Deep Dungeon"},
            
            // Eureka/Bozja mounts
            {"Cerberus", "Eureka"},
            {"Ozma", "Eureka"},
            {"Tyrannosaur", "Bozja"},
            {"Lone Hero", "Bozja"},
            {"Gabriel Alpha", "Bozja"},
            
            // Treasure hunt mounts
            {"Capricorn", "Treasure Hunt"},
            {"Korpokkur Kolossus", "Treasure Hunt"},
            {"True Griffin", "Treasure Hunt"},
            
            // Special/Rare
            {"Garlond GL-II", "Special Quest"},
            {"Garlond GL-I", "Special Quest"},
            {"Whisper-go", "Special Quest"},
            {"Magitek Predator", "Special Quest"},
            {"Pod 602", "Raid Drop"},
            
            // Default fallbacks for common patterns
            {"Magitek", "Market Board"},
            {"Gwiber", "Raid Drop"},
            {"Lanner", "Raid Drop"},
            {"Kamuy", "Trial Drop"}
        };

        /// <summary>
        /// Gets the acquisition method for a mount based on its name
        /// </summary>
        /// <param name="mountName">The name of the mount</param>
        /// <returns>The acquisition method or "Unknown" if not found</returns>
        public static string GetMountAcquisitionMethod(string? mountName)
        {
            if (string.IsNullOrEmpty(mountName))
                return "Unknown";

            // Handle hash-based names (fallback from failed Lodestone parsing)
            if (mountName.StartsWith("Mount #") || mountName.Contains(".png") || 
                (mountName.Length > 20 && mountName.All(c => char.IsLetterOrDigit(c))))
            {
                return "Check Lodestone";
            }

            // Direct name match first
            if (MountAcquisitionData.TryGetValue(mountName, out var method))
                return method;

            // Check for partial matches based on common prefixes/suffixes
            foreach (var kvp in MountAcquisitionData)
            {
                if (mountName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            // Check for common mount naming patterns
            if (mountName.Contains("Chocobo", StringComparison.OrdinalIgnoreCase))
                return "Quest Reward";
            if (mountName.Contains("Magitek", StringComparison.OrdinalIgnoreCase))
                return "Market Board";
            if (mountName.EndsWith("Gwiber", StringComparison.OrdinalIgnoreCase) ||
                mountName.EndsWith("Lanner", StringComparison.OrdinalIgnoreCase))
                return "Raid Drop";
            if (mountName.EndsWith("Kamuy", StringComparison.OrdinalIgnoreCase))
                return "Trial Drop";
            if (mountName.Contains("War", StringComparison.OrdinalIgnoreCase) ||
                mountName.Contains("Battle", StringComparison.OrdinalIgnoreCase))
                return "Market Board";

            return "Unknown";
        }

    }
}
