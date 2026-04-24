using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Common.Math;
using Memoria.Handlers;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Memoria.Properties;
using Lumina.Excel.Sheets;

namespace Memoria
{
    public static class Tools
    {
        public static int UnixTime
        {
            get
            {
                return (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        public static string ToTimeSinceString(int unixTime)
        {
            var value = DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime;
            TimeSpan ts = DateTime.Now.Subtract(value);

            if (ts.Days > 0)
                return string.Format(Loc.ToolsTimeSinceAgo, ts.Days, (ts.Days > 1) ? Loc.ToolsDays : Loc.ToolsDay);
            else if (ts.Hours > 0)
                return string.Format(Loc.ToolsTimeSinceMinutesAgo, ts.Hours, (ts.Hours > 1) ? Loc.ToolsHours : Loc.ToolsHour, ts.Minutes);
            else if (ts.Minutes > 0)
                return string.Format(Loc.ToolsTimeSinceSecondsAgo, ts.Minutes, (ts.Minutes > 1) ? Loc.ToolsMinutes : Loc.ToolsMinute, ts.Seconds);
            else if (ts.Seconds > 0)
                return string.Format(Loc.ToolsTimeSinceAgo, ts.Seconds, (ts.Seconds > 1) ? Loc.ToolsSeconds : Loc.ToolsSecond);
            else if (ts.Seconds == 0)
                return string.Format(Loc.ToolsJustNow);
            return ts.ToString();
        }

        public static string TimeFromNow(int unixTime)
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime;
            TimeSpan span = dt - DateTime.Now;

            if (span.Days > 365)
            {
                int years = (span.Days / 365);
                return String.Format(Loc.ToolsAboutFromNow, years, years == 1 ? Loc.ToolsYear : Loc.ToolsYears);
            }
            if (span.Days > 30)
            {
                int months = (span.Days / 30);
                return String.Format(Loc.ToolsAboutFromNow, months, months == 1 ? Loc.ToolsMonth : Loc.ToolsMonths);
            }
            if (span.Days > 0)
                return String.Format(Loc.ToolsAboutFromNow, span.Days, span.Days == 1 ? Loc.ToolsDay : Loc.ToolsDays);
            if (span.Hours > 0)
                return String.Format(Loc.ToolsAboutFromNow, span.Hours, span.Hours == 1 ? Loc.ToolsHour : Loc.ToolsHours);
            if (span.Minutes > 0)
                return String.Format(Loc.ToolsAboutFromNow, span.Minutes, span.Minutes == 1 ? Loc.ToolsMinute : Loc.ToolsMinutes);
            if (span.Seconds > 0)
                return String.Format(Loc.ToolsAboutSecondsFromNow, span.Seconds);
            if (span.Seconds == 0)
                return Loc.ToolsJustNow;
            return string.Empty;
        }

        public static string UnixTimeConverter(int unixTime)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime.ToString();
        }
        public static string UnixTimeConverter(int? unixTime)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime!.Value).ToLocalTime().DateTime.ToString();
        }

        /// <summary>
        /// Convert a DateTime to Unix seconds, treating the DateTime as UTC regardless of Kind.
        /// Write sites always use DateTime.UtcNow; this centralizes the read-side assumption.
        /// </summary>
        public static int ToUnixSecondsUtc(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return (int)((DateTimeOffset)utc).ToUnixTimeSeconds();
        }

        public static TerritoryType? GetTerritory(ushort territoryId) => new ExcelResolver<TerritoryType>(territoryId).GameData;

        public static string GetTerritoryName(ushort territoryId) =>
            new ExcelResolver<TerritoryType>(territoryId).GameData is { PlaceName.Value.Name: var name, PlaceNameRegion.Value.Name: var region }
            ? $"{name}, {region}" : "[Unknown]";
    }
}
