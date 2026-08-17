using System;
using System.Collections.Generic;
using System.Linq;
using LuaRenamer.LuaEnv;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Release;

namespace LuaRenamer.Tests;

/// <summary>
/// Producer-side proof for the file slice: Shoko's <see cref="IVideoFile"/> graph →
/// <see cref="ModelProducers.FileToModel"/> → <see cref="ModelTranslator"/> → a Lua-consumable table.
/// Focuses on the bits with custom logic rather than straight copies: the AniDB release-URI id parse,
/// the per-type hash lookup, the "raw/unknown" release-group filter, the LFE audio-channel math, and
/// enum→name on stream languages.
/// </summary>
[TestClass]
public class FileProducerTests
{
    private LuaSandbox _lua = null!;
    private ModelTranslator _translator = null!;

    [TestInitialize]
    public void Init()
    {
        _lua = new LuaSandbox(LuaScripts.LuaLinq, LuaScripts.Utils);
        _translator = new ModelTranslator(_lua);
    }

    [TestCleanup]
    public void Cleanup() => _lua.Dispose();

    private static IHashDigest Hash(string type, string value) => Mock.Of<IHashDigest>(h => h.Type == type && h.Value == value);

    private static IAudioStream Audio() => Mock.Of<IAudioStream>(a =>
        a.CompressionMode == "Lossy" &&
        a.Channels == 6 && a.ChannelLayout == "L R C LFE Ls Rs" && // contains LFE -> 6-1+0.1 = 5.1
        a.SamplingRate == 48000 &&
        a.Codec == Mock.Of<IStreamCodecInfo>(c => c.Simplified == "AAC") &&
        a.Language == TitleLanguage.Japanese &&
        a.Title == null);

    private static IVideoStream Video() => Mock.Of<IVideoStream>(v =>
        v.Width == 1920 && v.Height == 1080 && v.Resolution == "1080p" &&
        v.FrameRate == 23.976m && v.BitRate == 4_000_000 && v.BitDepth == 8 &&
        v.Codec == Mock.Of<IStreamCodecInfo>(c => c.Simplified == "h264"));

    private static IMediaInfo Media() => Mock.Of<IMediaInfo>(m =>
        m.Chapters == new List<IChapterInfo>() &&
        m.Duration == TimeSpan.FromMinutes(24) &&
        m.BitRate == 5_000_000 &&
        m.TextStreams == new[] { Mock.Of<ITextStream>(t => t.Language == TitleLanguage.English) } &&
        m.AudioStreams == new[] { Audio() } &&
        m.VideoStream == Video());

    private static IReleaseInfo Release(string uri, IReleaseGroup? group) => Mock.Of<IReleaseInfo>(r =>
        r.ReleaseURI == uri &&
        r.IsCensored == false &&
        r.Source == Enum.GetValues<ReleaseSource>().First() &&
        r.Version == 2 &&
        r.ReleasedAt == new DateOnly(2021, 3, 4) &&
        r.Comment == "release notes" &&
        r.Group == group &&
        r.MediaInfo == Mock.Of<IReleaseMediaInfo>(mi =>
            mi.SubtitleLanguages == new[] { TitleLanguage.English } &&
            mi.AudioLanguages == new[] { TitleLanguage.Japanese }));

    private static IVideoFile MakeFile(IReleaseInfo? release, IMediaInfo? media)
    {
        IVideo video = Mock.Of<IVideo>(v =>
            v.EarliestKnownName == "earliest.mkv" &&
            v.ED2K == "ED2KHASH" &&
            v.Hashes == new[] { Hash("CRC32", "CRCVAL"), Hash("SHA1", "SHA1VAL") } && // no MD5 entry on purpose
            v.ReleaseInfo == release &&
            v.MediaInfo == media);
        return Mock.Of<IVideoFile>(f =>
            f.FileName == "My Video.mkv" &&
            f.Path == "/data/My Video.mkv" &&
            f.Size == 123_456_789L &&
            f.Video == video &&
            f.ManagedFolder == Mock.Of<IManagedFolder>(mf =>
                mf.ID == 7 && mf.Name == "Anime" && mf.Path == "/data" &&
                mf.DropFolderType == Enum.GetValues<DropFolderType>().First()));
    }

    [TestMethod]
    public void File_Scalars_Hashes_And_ImportFolder()
    {
        _lua["file"] = _translator.Translate(ModelProducers.FileToModel(
            MakeFile(Release("https://anidb.net/file/987654", null), null)));

        Assert.AreEqual("My Video", _lua.DoString("return file.name")[0]);   // extension stripped
        Assert.AreEqual(".mkv", _lua.DoString("return file.extension")[0]);
        Assert.AreEqual("/data/My Video.mkv", _lua.DoString("return file.path")[0]);
        Assert.AreEqual(123_456_789L, _lua.DoString("return file.size")[0]);
        Assert.AreEqual("earliest", _lua.DoString("return file.earliestname")[0]);

        // hashes: ed2k from Video.ED2K; crc/sha1 looked up by Type; absent MD5 -> nil
        Assert.AreEqual("ED2KHASH", _lua.DoString("return file.hashes.ed2k")[0]);
        Assert.AreEqual("CRCVAL", _lua.DoString("return file.hashes.crc")[0]);
        Assert.AreEqual("SHA1VAL", _lua.DoString("return file.hashes.sha1")[0]);
        Assert.AreEqual(true, _lua.DoString("return file.hashes.md5 == nil")[0]);

        Assert.AreEqual(7L, _lua.DoString("return file.importfolder.id")[0]);
        Assert.AreEqual("/data", _lua.DoString("return file.importfolder.location")[0]);

        Assert.AreEqual(true, _lua.DoString("return file.media == nil")[0]); // null media -> absent
    }

    [TestMethod]
    public void AniDb_Release_Uri_Parsed_And_Mapped()
    {
        IReleaseGroup group = Mock.Of<IReleaseGroup>(g => g.ID == "42" && g.Name == "GoodGroup" && g.ShortName == "GG");
        _lua["file"] = _translator.Translate(ModelProducers.FileToModel(
            MakeFile(Release("https://anidb.net/file/987654", group), null)));

        Assert.AreEqual(987654L, _lua.DoString("return file.anidb.id")[0]); // tail after the 23-char prefix
        Assert.AreEqual(false, _lua.DoString("return file.anidb.censored")[0]);
        Assert.AreEqual(2L, _lua.DoString("return file.anidb.version")[0]);
        Assert.AreEqual("release notes", _lua.DoString("return file.anidb.description")[0]);
        Assert.AreEqual(2021L, _lua.DoString("return file.anidb.releasedate.year")[0]);
        Assert.AreEqual(3L, _lua.DoString("return file.anidb.releasedate.month")[0]);
        Assert.AreEqual("GG", _lua.DoString("return file.anidb.releasegroup.shortname")[0]);
        Assert.AreEqual("English", _lua.DoString("return file.anidb.media.sublanguages[1]")[0]); // TitleLanguage -> name
        Assert.AreEqual("Japanese", _lua.DoString("return file.anidb.media.dublanguages[1]")[0]);
    }

    [TestMethod]
    public void AniDb_Is_Absent_For_NonAniDb_Uri()
    {
        _lua["file"] = _translator.Translate(ModelProducers.FileToModel(
            MakeFile(Release("https://other.example/file/1", null), null)));

        Assert.AreEqual(true, _lua.DoString("return file.anidb == nil")[0]);
    }

    [TestMethod]
    public void ReleaseGroup_RawUnknown_Is_Filtered()
    {
        IReleaseGroup raw = Mock.Of<IReleaseGroup>(g => g.ID == "1" && g.Name == "raw/unknown" && g.ShortName == "raw");
        _lua["file"] = _translator.Translate(ModelProducers.FileToModel(
            MakeFile(Release("https://anidb.net/file/5", raw), null)));

        Assert.AreEqual(true, _lua.DoString("return file.anidb.releasegroup == nil")[0]);
    }

    [TestMethod]
    public void Media_Streams_Mapped_With_Lfe_Channel_Math()
    {
        _lua["file"] = _translator.Translate(ModelProducers.FileToModel(
            MakeFile(Release("https://anidb.net/file/5", null), Media())));

        Assert.AreEqual(false, _lua.DoString("return file.media.chaptered")[0]); // empty Chapters
        Assert.AreEqual(1440L, _lua.DoString("return file.media.duration")[0]);  // 24 min -> 1440 s
        Assert.AreEqual("English", _lua.DoString("return file.media.sublanguages[1]")[0]); // text stream language

        Assert.AreEqual("h264", _lua.DoString("return file.media.video.codec")[0]);
        Assert.AreEqual("1080p", _lua.DoString("return file.media.video.res")[0]);
        Assert.AreEqual(23.976, (double)_lua.DoString("return file.media.video.framerate")[0], 1e-9);

        Assert.AreEqual("AAC", _lua.DoString("return file.media.audio[1].codec")[0]);
        Assert.AreEqual("Japanese", _lua.DoString("return file.media.audio[1].language")[0]);
        Assert.AreEqual(5.1, (double)_lua.DoString("return file.media.audio[1].channels")[0], 1e-9); // LFE -> 6-1+0.1
    }
}
