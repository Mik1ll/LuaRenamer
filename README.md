# ScriptRenamer
Renamer Plugin for Shoko
Requires Antlr4 and Java Runtime to compile

On Windows CLASSPATH should point to antlr4 complete jar and antlr.bat in PATH with command : ```java org.antlr.v4.Tool %*```

Sample Script:
```
if (GroupShort) {
    filename add '[' GroupShort '] '
} else if (GroupLong) {
    filename add '[' GroupLong '] '
}
if (AnimeTitleEnglish) {
    filename add AnimeTitleEnglish ' '
} else {
    filename add AnimeTitle ' '
}
if (EpisodeType is Episode and len(EpisodeCount) >= 2 and EpisodeNumber <= 9) {
    filename add '0'
}
filename add EpisodeNumber ' '
if (EpisodeTitleEnglish) {
    filename add EpisodeTitleEnglish ' '
} else {
    filename add first(EpisodeTitles has Main) ' '
}
filename add Resolution ' ' VideoCodecShort ' ' Source ' '
if (DubLanguages has English and DubLanguages has Japanese) {
    filename add '[DUAL-AUDIO] '
} else if (DubLanguages has English) {
    filename add '[DUB] '
}
if (DubLanguages has Japanese and not SubLanguages has English) {
    filename add '[raw] '
}
if (Restricted) {
    if (Censored) {
        filename add '[CEN] '
    } else {
        filename add '[UNC] '
    }
}
filename add CRCUpper

// Import folders
if (Restricted and ImportFolders has 'h-anime') {
    destination set 'h-anime'
} else if (AnimeType is Movie) {
    destination set 'Movies'
} else {
    destination set 'Anime'
}
if (AnimeTitles has English) {
    if (AnimeTitles has English has Main) {
        subfolder set first(AnimeTitles has English has Main)
    } else if (AnimeTitles has English has Official) {
        subfolder set first(AnimeTitles has English has Official)
    } else {
        subfolder set first(AnimeTitles has English)
    }
} else {
    subfolder set first(AnimeTitles has Main)
}
```
