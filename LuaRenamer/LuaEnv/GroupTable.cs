// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class GroupTable : Table
{
    [LuaType("string")]
    [LuaDescription("The name of the group")]
    public string name => Get();

    [LuaType("Anime")]
    [LuaDescription("The main anime in the group")]
    public AnimeTable mainanime => new() { Fn = Get() };

    [LuaType("Anime[]")]
    [LuaDescription("All animes in the group")]
    public ArrayTable<AnimeTable> animes => new() { Fn = Get() };
}
