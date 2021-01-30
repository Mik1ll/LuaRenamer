# ScriptRenamer
Renamer Plugin for Shoko  
Requires Antlr4 and Java Runtime to compile  
antlr4 quick-start: https://github.com/antlr/antlr4/blob/master/doc/getting-started.md

## Installation
1. Download the [latest release](https://github.com/Mik1ll/ScriptRenamer/releases)
1. Unzip the binaries in the install location/Shoko Server/plugins or in the application data folder C:\ProgramData\ShokoServer\plugins (Windows), /home/.shoko/Shoko.CLI/plugins (Linux CLI)
1. Add these settings in settings-server.json:
```
"Plugins": {
    "EnabledRenamers": {
      "ScriptRenamer": true
    },
    "RenamerPriorities": {
      "ScriptRenamer": 0
    }
  }
```

## Usage

### Important Note for File Moving
The only destination folders settable by the renamer are import folders with Drop Type of Destination or Both.  
The final destination MUST match the Name of a drop folder (not the Path) in order to move the file.
Destination import folder name must be unique or moving file will fail.

### Shoko Desktop
1. Navigate to Utilities/File Renaming
1. Either create a new script or change the type of the current script to ScriptRenamer in the drop-down menu.
1. Type your script and Save (next to the script type drop-down).
1. Test your script using the preview utility in the same window.

### Script Grammar
Refer to [this grammar](ScriptRenamer/ScriptRenamer.g4) for full syntax in [EBNF form](https://en.wikipedia.org/wiki/Extended_Backus%E2%80%93Naur_form).  

Targets:
1. filename
1. destination
1. subfolder

Statements: 
1. if (*bool expr*) *statement*
1. if (*bool expr*) *statement* else *statement*
1. *target*? add *string*+
1. *target*? set *string*+
1. *target*? replace *string* *string*
1. { *statement* }
1. cancel / cancelrename / cancelmove


Collections:
1. *collection label*
1. *collection label* has *collection enum*
1. *collection label* has *collection enum* and *other collection enum*

Boolean Expressions (In order of precedence):
1. not *bool expr*
1. *collection*
1. *type label* is *type enum*
1. *number* *relational operator* *number*
1. *(bool expr, number, string)* == *(bool expr, number, string)*
1. *(bool expr, number, string)* != *(bool expr, number, string)*
1. *bool expr* and *bool expr*
1. *bool expr* or *bool expr*
1. (*bool expr*)
1. *bool*
    
Dates:
1. *date label*
1. *date label*.*(Year, Month, or Day)*

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
GroupShort
GroupLong
CRCLower
CRCUpper
Source
Resolution
AnimeType
EpisodeType
EpisodePrefix
VideoCodecLong
VideoCodecShort
Duration
GroupName
OldFilename
OriginalFilename
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
first(*collection*) returns the first element in a collection
```
if (EpisodeType is Episode and len(EpisodeCount) >= 2 and EpisodeNumber <= 9)
    add '0'
add EpisodeNumber
```  
Episode number padding example.  
len(*(number, collection, or string)*) returns the number of digits in a number, elements in a collection, and characters in a string

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
