// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

public partial class DateTimeTable : LuaTableWriter
{
    public DateTimeTable(LuaTable t) : base(t) { }

    [LuaField("Year (four digits)")]
    public required long year { init => Set(value); }

    [LuaField("Month (1-12)")]
    public required long month { init => Set(value); }

    [LuaField("Day of the month (1-31)")]
    public required long day { init => Set(value); }

    [LuaField("Day of the year (1-366)")]
    public required long yday { init => Set(value); }

    [LuaField("Day of the week (1-7, 1 is Sunday)")]
    public required long wday { init => Set(value); }

    [LuaField("Hour (0-23)")]
    public required long hour { init => Set(value); }

    [LuaField("Minute (0-59)")]
    public required long min { init => Set(value); }

    [LuaField("Second (0-59)")]
    public required long sec { init => Set(value); }

    [LuaField("Is Daylight Saving Time in effect")]
    public required bool isdst { init => Set(value); }

    public static implicit operator LuaRef<DateTimeTable>(DateTimeTable t) => new(t._t);
}
