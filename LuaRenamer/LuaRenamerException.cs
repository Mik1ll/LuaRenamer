using System;

namespace LuaRenamer;

public class LuaRenamerException : Exception
{
    public LuaRenamerException()
    {
    }

    public LuaRenamerException(string message) : base(message)
    {
    }

    public LuaRenamerException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
