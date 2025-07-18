// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class DateTimeTable : Table
{
    [LuaType("integer")]
    [LuaDescription("Year (four digits)")]
    public string year => Get();

    [LuaType("integer")]
    [LuaDescription("Month (1-12)")]
    public string month => Get();

    [LuaType("integer")]
    [LuaDescription("Day of the month (1-31)")]
    public string day => Get();

    [LuaType("integer")]
    [LuaDescription("Day of the year (1-366)")]
    public string yday => Get();

    [LuaType("integer")]
    [LuaDescription("Day of the week (1-7, 1 is Sunday)")]
    public string wday => Get();

    [LuaType("integer")]
    [LuaDescription("Hour (0-23)")]
    public string hour => Get();

    [LuaType("integer")]
    [LuaDescription("Minute (0-59)")]
    public string min => Get();

    [LuaType("integer")]
    [LuaDescription("Second (0-59)")]
    public string sec => Get();

    [LuaType("boolean")]
    [LuaDescription("Is Daylight Saving Time in effect")]
    public string isdst => Get();
}
