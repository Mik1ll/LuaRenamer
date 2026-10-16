using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using NSubstitute;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;
using Xunit;

namespace LuaRenamer.Tests.Env;

public class GroupProducerTests
{
    [Fact]
    public void ThePreferredTitleNamesTheGroup()
    {
        IShokoGroup group = HostFakes.Group(Ids.ShokoGroup, "preferred", HostFakes.Series());
        group.DefaultTitle.Value.Returns("default");

        ModelProducers.GroupToModel(group).name.Should().Be("preferred");
    }

    [Fact]
    public void WithNoTitleInAPreferredLanguageTheDefaultTitleNamesTheGroup()
    {
        // Scripts build subfolders from group.name, so a nil here empties the whole subfolder table.
        IShokoGroup group = HostFakes.Group(Ids.ShokoGroup, "default", HostFakes.Series());
        group.PreferredTitle.Returns((ITitle?)null);

        ModelProducers.GroupToModel(group).name.Should().Be("default");
    }
}
