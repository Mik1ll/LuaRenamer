# ScriptRenamer
Renamer Plugin for Shoko

## Installation
1. Download the [latest release](https://github.com/Mik1ll/ScriptRenamer/releases)
1. Unzip the binaries in the install location/Shoko Server/plugins or in the application data folder C:\ProgramData\ShokoServer\plugins (Windows), /home/.shoko/Shoko.CLI/plugins (Linux CLI)
1. Follow instructions in the next section to add your script

## Usage
#### Important Note for File Moving
The only destination folders settable by the renamer are import folders with Drop Type of Destination or Both.  
The final destination MUST match the name or absolute path of a drop folder in order to move the file.
If using name to set, destination import folder name must be unique or moving file will fail.

### Shoko Desktop
1. Navigate to Utilities/File Renaming
1. Use the Default script and set the type of the script to ScriptRenamer in the drop-down menu. Don't add a new script, as they are currently ignored when importing/scanning.
1. Type your script and Save (next to the script type drop-down).
1. Test your script using the preview utility in the same window.

### Script Grammar
Refer to [this grammar](ScriptRenamer/ScriptRenamer.g4) for full syntax in [EBNF form](https://en.wikipedia.org/wiki/Extended_Backus%E2%80%93Naur_form).  

Targets:
1. filename
1. destination
1. subfolder
```
can use single wildcard in place of subfolder names to match old subfolder names at same depth.
            e.g. old: anime/mystuff/name, new: movies/*/newname, result: movies/mystuff/newname
```

Statements: 
1. if (***bool expr***) ***statement***
1. if (***bool expr***) ***statement*** else ***statement***
1. ***target***? add ***string***+    ```Append strings to the end of the current target```
1. ***target***? set ***string***+    ```Reset the target to the strings```
1. ***target***? replace ***string*** ***string***    ```Replace all instances of first string by the second string in the target```
1. { ***statement*** }    ```Standard code block, enclosing multiple statements, required after if/else statements if using multiple statements```
1. cancel ***string****    ```Cancel renaming and moving with an exception```
1. skipRename | skipMove    ```Skip renaming or moving, deferring to the next renamer/mover in the priority list```


Collections:
1. ***collection label***
1. ***collection label*** has ***collection enum***    ```String for AudioCodec and Import Folders (name or absolute path)```
1. ***collection label*** has ***collection enum*** and ***other collection enum***    ```Only supported by AnimeTitle and EpisodeTitle, for Types+Language enums```
1. first(***collection***)    ```Get first element of a collection```

Boolean Expressions (In order of precedence):
1. not ***bool expr***    ```Invert the value of the expression```
1. ***collection***    ```True if collection is non-empty```
1. ***type label*** is ***type enum***    ```Used by AnimeType and EpisodeType, checks the type```
1. ***number*** ***relational operator*** ***number***    ```Only supports integers at this time```
1. ***(bool expr, number, or string)*** (== | !=) ***(bool expr, number, or string)***    ```Checks equality/inequality```
1. ***bool expr*** and ***bool expr***    ```Boolean and expression```
1. ***bool expr*** or ***bool expr***    ```Boolean or expression```
1. (***bool expr***)    ```Expression parentheses for enforcing order of operations```
1. ***bool*** 

Bools:
1. true | false
1. ***number*** ```True if non-zero```
1. ***string*** ```True if non-empty```

Numbers:
1. \[+-]?\[0-9]+
1. ***number label***
1. len(***collection*** | ***string***)    ```Length of a collection or a string```

Strings:
1. '***char****' | "***char****"
1. ***string label***
1. ***collection*** ```Comma delimited list, null if empty```
1. ***number***
1. ***date***

Dates:
1. ***date label***
1. ***date label***.***(Year, Month, or Day)***

Comments:
1. //***char*******newline*** ```Line comment```
1. /\*(***char***\*)\*/ ```Block Comment```

### Labels
#### Strings
```
AnimeTitlePreferred or AnimeTitle
AnimeTitleRomaji
AnimeTitleEnglish
AnimeTitleJapanese
EpisodeTitleRomaji
EpisodeTitleEnglish
EpisodeTitleJapanese
GroupShort    // Release Group short name
GroupLong    // Release Groupu long name
CRCLower
CRCUpper
Source
Resolution    // Standardized Resolution, use Height 'x' Width for exact dimensions
AnimeType
EpisodeType
EpisodePrefix
VideoCodecLong    // Entire CodecID returned by MediaInfo (or AniDB if no local media info), usually you want the short codec
VideoCodecShort    // Simplified video codec
Duration
GroupName    // Shoko's Group name
OldFilename     // Filename before the renamer script was run
OriginalFilename    // Filename stored by AniDB when a file is added to the database
Dates:
    AnimeReleaseDate
    EpisodeReleaseDate
    FileReleaseDate
```

#### Numbers
```
AnimeID
EpisodeID
EpisodeNumber
Version
Width
Height
EpisodeCount
BitDepth
AudioChannels
SeriesInGroup
```

#### Booleans
```
Restricted
Censored
Chaptered
ManuallyLinked
```

#### Collections
```
AudioCodecs
DubLanguages
SubLanguages
AnimeTitles
EpisodeTitles
ImportFolders
```

#### Enumerations
##### TitleType
```
Main
None
Official
Short
Synonym
```
##### EpisodeType
```
Episode
Credits
Special
Trailer
Parody
Other
```
##### AnimeType
```
Movie
OVA
TVSeries
TVSpecial
Web
Other
```
##### Language (see grammar for full list)
```
Unknown
English
Romaji
Japanese
(cont ...)
```

#### Snippets
```
if (not Sublanguages) filename add '[raw]'
else {
    add 'subs:' Sublanguages
}
```  
Collections can evaluate to true if it has any elements, false if it is empty.  
If/else statements can substitute a block {} with a single statement.   
Can optionally add 'filename' target in front of actions, it is the default target.  
'add' and 'set' actions take one or more strings as arguments. 
Collections can also evaluate as a comma-seperated string.  
```
if (AnimeTitles has English and Main)
    subfolder set first(AnimeTitles has English and Main)
```  
Title collections can have two specifiers: language and type.  
first(***collection***) returns the first element in a collection
```
if (EpisodeType is Episode and len(EpisodeCount) >= 2 and EpisodeNumber <= 9)
    add '0'
add EpisodeNumber
```  
Episode number padding example.  
len(***(number, collection, or string)***) returns the number of elements in a collection, or characters in a string. EpisodeCount is converted to a string automatically.

```
// this is a line comment
/* this is a multi- 
line comment */
```

### Sample Script
```
if (GroupShort)
    add '[' GroupShort '] '
else if (GroupLong)
    add '[' GroupLong '] '
if (AnimeTitleEnglish)
    add AnimeTitleEnglish ' '
else
    add AnimeTitle ' '
add EpisodePrefix
if (EpisodeType is Episode and len(EpisodeCount) >= 2 and EpisodeNumber <= 9)
    add '0'
add EpisodeNumber
if (Version > 1)
    add 'v' Version
add ' '
if (EpisodeTitleEnglish)
    add EpisodeTitleEnglish ' '
else
    add first(EpisodeTitles has Main) ' '
add Resolution ' ' VideoCodecShort ' '
if (BitDepth)
    add BitDepth 'bit '
add Source ' '
if (DubLanguages has English)
    if (DubLanguages has Japanese)
        add '[DUAL-AUDIO] '
    else
        add '[DUB] '
else if (DubLanguages has Japanese and not SubLanguages has English)
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
    if (AnimeTitles has English and Main)
        subfolder set first(AnimeTitles has English and Main)
    else if (AnimeTitles has English and Official)
        subfolder set first(AnimeTitles has English and Official)
    else
        subfolder set first(AnimeTitles has English)
else
    subfolder set first(AnimeTitles has Main)
```

# Compilation
Requires Antlr4 and Java Runtime to compile  
antlr4 quick-start: https://github.com/antlr/antlr4/blob/master/doc/getting-started.md
