// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Group)]
public class GroupTable : Table
{
    [LuaType(LuaTypeNames.@string, "The name of the group")]
    public string name => Get();

    [LuaType(LuaTypeNames.Anime, "The main anime in the group")]
    public AnimeTable mainanime => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.Anime}[]", "All animes in the group")]
    public ArrayTable<AnimeTable> animes => new() { Fn = Get() };
}
