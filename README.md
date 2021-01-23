# ScriptRenamer
Renamer Plugin for Shoko
Requires Antlr4 and Java Runtime to compile
antlr4 quick-start: https://github.com/antlr/antlr4/blob/master/doc/getting-started.md

See .g4 file for syntax (in EBNF)
Sample Script:
```
if (GroupShort)
    add '[' GroupShort '] '
else if (GroupLong) 
    add '[' GroupLong '] '
if (AnimeTitleEnglish) 
    add AnimeTitleEnglish ' '
else 
    add AnimeTitle ' '
if (EpisodeType is Episode and len(EpisodeCount) >= 2 and EpisodeNumber <= 9) 
    add '0'
add EpisodePrefix EpisodeNumber ' '
if (EpisodeTitleEnglish)
    add EpisodeTitleEnglish ' '
else
    add first(EpisodeTitles has Main) ' '
add '('
if (Width and Height)
    add Width 'x' Height ' '
if (VideoCodecShort)
    add VideoCodecShort ' '
if (BitDepth)
    add BitDepth 'bit '
add Source ') '
if (DubLanguages has English)
    if (DubLanguages has Japanese)
        add '[DUAL-AUDIO] '
    else
        add '[DUB] '
if (DubLanguages has Japanese and not SubLanguages has English)
    add '[raw] '
if (Restricted)
    if (Censored)
        add '[CEN] '
    else
        add '[UNC] '
add '[' CRCUpper ']'

// Import folders:
if (Restricted and ImportFolders has 'h-anime')
    destination set 'h-anime'
else if (AnimeType is Movie)
    destination set 'Movies'
else
    destination set 'Anime'
if (AnimeTitles has English)
    if (AnimeTitles has English has Main)
        subfolder set first(AnimeTitles has English has Main)
    else if (AnimeTitles has English has Official)
        subfolder set first(AnimeTitles has English has Official)
    else
        subfolder set first(AnimeTitles has English)
else
    subfolder set first(AnimeTitles has Main)
```
