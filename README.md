# ScriptRenamer
Renamer Plugin for Shoko

## Installation
1. Download the [latest release](https://github.com/Mik1ll/ScriptRenamer/releases/latest)
1. Unzip the binaries in the install location/Shoko Server/plugins or in the application data folder C:\ProgramData\ShokoServer\plugins (Windows), /home/.shoko/Shoko.CLI/plugins (Linux CLI)
1. Follow instructions in the next section to add your script

## Usage
### Shoko Desktop
1. Navigate to Utilities/File Renaming
1. Use the Default script and set the type of the script to ScriptRenamer in the drop-down menu. Don't add a new script, as they are currently ignored when importing/scanning.
1. Type your script and Save (next to the script type drop-down).
1. Test your script using the preview utility in the same window.

### Important Notes for File Moving
If 'findLastLocation' is used, the last added file's location from the same anime will be used if it exists.
The only destination folders settable by the renamer are import folders with Drop Type of Destination or Both.  
The final destination MUST match the name or absolute path of a drop folder in order to move the file.  
If using name to set, destination import folder name must be unique or moving file will fail.  
Both Destination and Subfolder must be set or moving will fail.

## Sample Script
```
if (GroupShort)
    add '[' GroupShort '] '
if (AnimeTitles has English and Main)
    add first(AnimeTitles has English and Main) ' '
else if (AnimeTitles has English and Official)
    add first(AnimeTitles has English and Official) ' '
else
    add AnimeTitle ' '
if (not (AnimeType is Movie and EpisodeCount == 1 and EpisodeType is Episode)) {
    add EpisodeNumbers pad MaxEpisodeCount
    if (Version > 1)
        add 'v' Version
    add ' '
    if (not MultiLinked)
        if (EpisodeTitleEnglish)
            add EpisodeTitleEnglish ' '
        else
            add first(EpisodeTitles has Main) ' '
}
add '(' Resolution ' ' VideoCodecShort ' '
if (BitDepth)
    add BitDepth 'bit '
if (Source)
    add Source
else
    add 'Unknown'
add ') '
if (DubLanguages has English)
    if (DubLanguages has Japanese)
        add '[DUAL-AUDIO] '
    else
        add '[DUB] '
else if (DubLanguages has Japanese and not SubLanguages has English)
    add '[RAW] '
if (Restricted)
    if (Censored)
        add '[CEN] '
    else
        add '[UNC] '
add '[' CRCUpper ']'

// Import folders:
if (Restricted and ImportFolders has 'Hentai')
    destination set 'Hentai'
else if (AnimeType is Movie)
    destination set 'Movies'
else
    destination set 'Anime'

if (AnimeTitles has English and Main)
    subfolder set first(AnimeTitles has English and Main)
else if (AnimeTitles has English and Official)
    subfolder set first(AnimeTitles has English and Official)
else
    subfolder set AnimeTitle
```

### Labels
#### Strings
```
AnimeTitlePreferred or AnimeTitle
AnimeTitleRomaji        // Note: these may fall back to synonym titles, use first(AnimeTitles has <Language> and <TitleType>) if you want a specific type
AnimeTitleEnglish       //
AnimeTitleJapanese      //
EpisodeTitleRomaji      // Same as above, use EpisodeTitles collection for specific type
EpisodeTitleEnglish     //
EpisodeTitleJapanese    //
GroupShort    // Release Group short name
GroupLong    // Release Group long name
CRCLower
CRCUpper
Source
Resolution    // Standardized Resolution, use Height 'x' Width for exact dimensions
AnimeType
EpisodeType
EpisodePrefix
VideoCodecLong    // Entire CodecID returned by MediaInfo (or AniDB if no local media info), usually you want the short codec
VideoCodecShort    // Simplified video codec
VideoCodecAniDB    // Codec string from AniDB
Duration
GroupName    // Shoko's Group name
OldFilename     // Filename before the renamer script was run
OriginalFilename    // Filename stored by AniDB when a file is added to the database
OldImportFolder    // Import folder before move **Not available while renaming**
EpisodeNumbers    // All episode numbers, as a space-seperated string. e.g. "1-3 5-6 C2 S1-2 S4 P5" Can also use padding like numbers.
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
EpisodeCount     // Number of episodes of this episodes type
BitDepth
AudioChannels
SeriesInGroup     // Number of series associated with a Shoko group
LastEpisodeNumber  // Same as EpisodeNumber unless file is associated with multiple episodes. Last episode in first contiguous series of episode numbers
MaxEpisodeCount    // Max of all episode type counts
```

#### Booleans
```
Restricted
Censored
Chaptered
ManuallyLinked
InDropSource    // True if import folder moving from is a drop source **Not available while renaming**
MultiLinked    // If file is linked with multiple episodes
```

#### Collections
```
AudioCodecs
DubLanguages
SubLanguages
AnimeTitles
EpisodeTitles
ImportFolders    // Available drop folders (marked as destination) **Not available while renaming**
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
add EpisodeNumber pad MaxEpisodeCount
```  
Episode number padding. Can use EpisodeCount or any other number, pads to match number of digits.


```
add EpisodeNumber
if (LastEpisodeNumber != EpisodeNumber)
    add '-' LastEpisodeNumber
```
Adds support for files with a range of episodes


```
// this is a line comment
/* this is a multi- 
line comment */
```

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
1. findLastLocation    ```Enables using last added file's location from the same anime```

Collections:
1. ***collection label***
1. ***collection label*** has ***collection enum***    ```String for AudioCodec and Import Folders (name or absolute path)```
1. ***collection label*** has ***collection enum*** and ***other collection enum***    ```Only supported by AnimeTitle and EpisodeTitle, for Types+Language enums```
1. first(***collection***)    ```Get first element of a collection```

Boolean Expressions (In order of precedence):
1. not ***bool expr***    ```Invert the value of the expression```
1. ***collection***    ```True if collection is non-empty```
1. ***type label*** is ***type enum***    ```Used by AnimeType and EpisodeType, checks the type```
1. ***string atom*** contains ***string atom***    ```True if string contains another string as a substring```
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
1. ***number*** (pad ***number***)?    ```Able to pad number up to same number of digits as second number, commonly used with EpisodeCount or MaxEpisodeCount. Special case: works with EpisodeNumbers string```
1. ***date***
1. ***string*** + ***string***
1. replace(***string***, ***old string***, ***new string***)    ```Returns string with old string replaced with new string```
1. substr(***string***, ***index number***)    ```Returns string starting at given index```
1. substr(***string***, ***index number***, ***length number***)    ```Returns string starting at given index with given length```
1. trunc(***string***, ***length number***)    ```Returns string with characters after length sliced off```
1. trim(***string***)    ```Trims whitespace on ends of string```

Dates:
1. ***date label***
1. ***date label***.***(Year, Month, or Day)***

Comments:
1. //***char*******newline*** ```Line comment```
1. /\*(***char***\*)\*/ ```Block Comment```

# Compilation
Requires Antlr4 and Java Runtime to compile  
antlr4 quick-start: https://github.com/antlr/antlr4/blob/master/doc/getting-started.md
