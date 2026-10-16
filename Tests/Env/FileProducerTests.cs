using AwesomeAssertions;
using LuaRenamer.LuaEnv.Models;
using LuaRenamer.Tests.Fakes;
using NSubstitute;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Release;
using Xunit;

namespace LuaRenamer.Tests.Env;

/// <summary>
/// The file slice of the host mapping, asserted on the produced model. Only the parts with real logic are
/// here — the release-URI parse, the per-type hash lookup, the placeholder release-group filter, the LFE
/// channel arithmetic, enum-to-name on stream languages, and the values the host leaves absent. Straight copies
/// are covered by the marshaling properties, not restated field by field.
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

    [Theory]
    // Shoko's own stand-ins for no group at all, matched as it matches them: either name, any case.
    [InlineData("raw/unknown", "GG")]
    [InlineData("Raw/Unknown", "GG")]
    [InlineData("raw", "GG")]
    [InlineData("unknown", "GG")]
    [InlineData("GoodGroup", "unk")]
    [InlineData("GoodGroup", "raw/unk")]
    public void APlaceholderReleaseGroupIsFilteredOut(string name, string shortName) =>
        ModelProducers.FileToModel(HostFakes.FileWith(
                HostFakes.Release(group: HostFakes.ReleaseGroup(name: name, shortName: shortName))))
            .anidb!.releasegroup.Should().BeNull();

    [Fact]
    public void ARealReleaseGroupIsKept() =>
        ModelProducers.FileToModel(HostFakes.FileWith(HostFakes.Release(group: HostFakes.ReleaseGroup())))
            .anidb!.releasegroup!.shortname.Should().Be("GG");

    [Fact]
    public void AReleaseOfUnknownCensorshipStaysUnknown()
    {
        IReleaseInfo release = HostFakes.Release();
        release.IsCensored.Returns((bool?)null);

        ModelProducers.FileToModel(HostFakes.FileWith(release)).anidb!.censored.Should().BeNull();
    }

    [Fact]
    public void AReleaseWithNoLanguageListsHasEmptyOnes()
    {
        // The host reports no media info at all when the release recorded neither list.
        IReleaseInfo release = HostFakes.Release();
        release.MediaInfo.Returns((IReleaseMediaInfo?)null);

        AniDbMediaModel media = ModelProducers.FileToModel(HostFakes.FileWith(release)).anidb!.media;

        media.sublanguages.Should().BeEmpty();
        media.dublanguages.Should().BeEmpty();
    }

    [Fact]
    public void AVideoWithNoRecordedNameHasNoEarliestName()
    {
        IVideoFile file = HostFakes.FileWith();
        file.Video.EarliestKnownName.Returns((string?)null);

        ModelProducers.FileToModel(file).earliestname.Should().BeNull();
    }

    [Theory]
    // A layout naming an LFE channel is reported as n.1 — the full channels plus the 0.1 low-frequency one.
    [InlineData(6, "L R C LFE Ls Rs", 5.1)]
    [InlineData(8, "L R C LFE Lb Rb Ls Rs", 7.1)]
    [InlineData(2, "L R", 2.0)]
    // MediaInfo leaves the layout out for some codecs and containers.
    [InlineData(2, null, 2.0)]
    public void AudioChannelsAccountForTheLfeChannel(int channels, string? layout, double expected) =>
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
    public void AnAudioOnlyFileHasNoVideoSlice()
    {
        IMediaInfo media = HostFakes.Media();
        media.VideoStream.Returns((IVideoStream?)null);

        MediaModel model = ModelProducers.FileToModel(HostFakes.FileWith(media: media)).media!;

        model.video.Should().BeNull();
        model.audio.Should().ContainSingle();
    }

    [Fact]
    public void AbsentMediaProducesNoMediaSlice() =>
        ModelProducers.FileToModel(HostFakes.FileWith()).media.Should().BeNull();
}
