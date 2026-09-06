using AwesomeAssertions;
using LuaRenamer.LuaEnv.Models;
using LuaRenamer.Tests.Fakes;
using Shoko.Abstractions.Metadata.Enums;
using Xunit;

namespace LuaRenamer.Tests.Env;

/// <summary>
/// The file slice of the host mapping, asserted on the produced model. Only the parts with real logic are
/// here — the release-URI parse, the per-type hash lookup, the placeholder release-group filter, the LFE
/// channel arithmetic and enum-to-name on stream languages. Straight copies are covered by the marshaling
/// properties, not restated field by field.
/// </summary>
public class FileProducerTests
{
    [Fact]
    public void ReleaseUriTailBecomesTheAniDbFileId()
    {
        AniDbModel? anidb = ModelProducers.FileToModel(HostFakes.FileWith(HostFakes.Release())).anidb;

        anidb.Should().NotBeNull();
        anidb!.id.Should().Be(Ids.AnidbFile);
        anidb.version.Should().Be(2);
        anidb.description.Should().Be("release notes");
    }

    [Fact]
    public void ANonAniDbReleaseUriProducesNoAniDbSlice() =>
        ModelProducers.FileToModel(HostFakes.FileWith(HostFakes.Release("https://other.example/file/1")))
            .anidb.Should().BeNull();

    [Fact]
    public void HashesAreLookedUpByTypeAndAnAbsentTypeIsEmpty()
    {
        HashesModel hashes = ModelProducers.FileToModel(HostFakes.FileWith()).hashes;

        hashes.ed2k.Should().Be("ed2khash");   // off Video.ED2K, not the hash list
        hashes.crc.Should().Be("CRCVAL");
        hashes.sha1.Should().Be("SHA1VAL");
        hashes.md5.Should().BeNull();
    }

    [Fact]
    public void ThePlaceholderReleaseGroupIsFilteredOut()
    {
        ModelProducers.FileToModel(HostFakes.FileWith(
                HostFakes.Release(group: HostFakes.ReleaseGroup(name: "raw/unknown", shortName: "raw"))))
            .anidb!.releasegroup.Should().BeNull("\"raw/unknown\" is Shoko's stand-in for no group at all");

        ModelProducers.FileToModel(HostFakes.FileWith(HostFakes.Release(group: HostFakes.ReleaseGroup())))
            .anidb!.releasegroup!.shortname.Should().Be("GG");
    }

    [Theory]
    // A layout naming an LFE channel is reported as n.1 — five full channels plus the 0.1 low-frequency one.
    [InlineData(6, "L R C LFE Ls Rs", 5.1)]
    [InlineData(2, "L R", 2.0)]
    public void AudioChannelsAccountForTheLfeChannel(int channels, string layout, double expected) =>
        ModelProducers.FileToModel(HostFakes.FileWith(media: HostFakes.Media(HostFakes.AudioStream(channels, layout))))
            .media!.audio[0].channels.Should().BeApproximately(expected, 1e-9);

    [Fact]
    public void StreamLanguagesAreNamed()
    {
        MediaModel? media = ModelProducers.FileToModel(HostFakes.FileWith(media: HostFakes.Media())).media;

        media!.sublanguages.Should().Equal("English");                  // off the text streams
        media.audio[0].language.Should().Be(nameof(TitleLanguage.Japanese));
    }

    [Fact]
    public void AbsentMediaProducesNoMediaSlice() =>
        ModelProducers.FileToModel(HostFakes.FileWith()).media.Should().BeNull();
}
