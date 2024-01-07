# LuaRenamer

## Installation

1. Download the [latest release](https://github.com/Mik1ll/LuaRenamer/releases/latest)
2. Unzip the files into destination
    * (Windows) `C:\ProgramData\ShokoServer\plugins`
    * (Docker) wherever the container location `/home/shoko/.shoko/Shoko.CLI/plugins` is mounted
3. Restart Shoko Server
4. (Optional) Install VS Code and [the Lua extension](https://marketplace.visualstudio.com/items?itemName=sumneko.lua) to edit your script. The extension uses [LuaCATS annotations](https://luals.github.io/wiki/annotations/)
5. Follow instructions in the next section to add your script

## Usage

### With Shoko Desktop

1. Open Shoko Desktop
2. Navigate to Utilities/File Renaming
3. Use the Default script or create a new one and set the type of the script to LuaRenamer in the drop-down menu
4. (Optional) Open the lua subfolder of the extracted plugin in VS Code
5. [Create a script](#script-writing)
6. Paste the script in the text box in Shoko Desktop
7. Add the files you wish to rename
8. Test your script before renaming by pressing Preview. (There is no preview for file moving, only renaming)
9. Pressing Rename does not move files by default, the checkbox to move must also be checked
10. Save your script

### Linux/Without Shoko Desktop

1. Copy/download [the linux scripts](./Linux%20Scripts)
2. (Optional) Open the lua subfolder of the extracted plugin in VS Code
3. [Create a script](#script-writing)
4. Preview the results with [the preview script](./Linux%20Scripts/preview_rename_script.sh) `./preview_rename_script.sh <script filename> [# results]`
5. Add your script to Shoko with [the add script](./Linux%20Scripts/add_rename_script.sh) `./add_rename_script.sh <script filename>`
6. If you want to rename and move all existing files use [the rename script](./Linux%20Scripts/rename_and_move_all.sh) `./rename_and_move_all.sh <script name>`

### Renaming on Import

If you wish to rename/move your files on import you must do two things:

1. Set Rename/Move On Import to true in Shoko settings
2. Ensure your script is saved with the run on import setting true
    1. Check that it is the only script with the setting enabled

## Script Writing

VS Code + [the Lua extension](https://marketplace.visualstudio.com/items?itemName=sumneko.lua) is recommended for script editing.  
The script environment utilizes [LuaCATS annotations](https://luals.github.io/wiki/annotations/), allowing the extension to provide type linting.

### The Environment

The lua environment is sandboxed, removing operations from standard libraries such as io, and os. See BaseEnv in [LuaContext](./LuaRenamer/LuaContext.cs).  
The script is run in a fresh environment for every file.  
Only the output variables defined in [env.lua](./LuaRenamer/lua/env.lua) will have any effect outside of the script.

* [env.lua](./LuaRenamer/lua/env.lua)*: The starting environment, output variable values will change renaming/moving behaviour
* [defs.lua](./LuaRenamer/lua/defs.lua)*: Table definitions available from Shoko
* [enums.lua](./LuaRenamer/lua/enums.lua)*: Enumeration definitions
* [lualinq.lua](./LuaRenamer/lua/lualinq.lua): A modified utility library ([original](https://github.com/xanathar/lualinq), [docs](./LuaRenamer/lua/LuaLinq.pdf)) that adds functional query methods similar to [LINQ](https://learn.microsoft.com/en-us/dotnet/csharp/linq/)
* [utils.lua](./LuaRenamer/lua/utils.lua): Additional utility functions can be defined here

&ast; This file is not executed, it serves as documentation/annotations

### Script Settings

In addition to the `filename`, `destination` and `subfolder` output variables, these variables affect the result of your script.

* `use_existing_anime_location`<a id="eAnimeLocation"></a> If true, the subfolder of the most recent file of the same anime is reused if one exists. This takes precedence over the subfolder set in the script (default: false)
* `replace_illegal_chars` If true, replaces all illegal path characters in subfolder and file name with alternatives. See [ReplaceMap in Utils.cs](./LuaRenamer/Utils.cs) (default: false)
* `remove_illegal_chars` If true, removes all illegal path characters in subfolder and file name. If false, illegal characters are replaced with underscores or replaced if `replace_illegal_chars` is true. (default: false)
* `skip_rename` If true, the result of running the script is discarded when renaming. (default: false)
* `skip_move` If true, the result of running the script is discarded when moving. (default: false)

### Notes for File Moving

Import folders are only valid destination candidates if they exist and have either the 'Destination' or 'Both' Drop Type.  
Destination defaults to the nearest (to the file) valid import folder.  
Destination is set via:

* Import folder name (string)
* Server folder path (string)
* Import folder reference (selected from 'importfolders' array)

If destination set via path, it is matched to import folder path with converted directory seperators but no other special handling (relative path or expansion).

Subfolder defaults to the anime title in your preferred language.  
Subfolder is set via:

* Subfolder name (string)
* Path segments (array-table, e.g. `{"parent dir name", "subdir name", "..."}`)

If set via a string subfolder name, directory separators within the string are ignored or replaced depending on preference.  
Also see [use_existing_anime_location in Script Settings](#eAnimeLocation)

### [The Example Script](./LuaRenamer/lua/example.lua)

The example script provides a sensible default renaming template. Some variables may be customized at the top of the file, and can serve as a good base for your own script.

### Common Scenarios

#### I want to split my collection across import folders

The easiest option is to set the destination by import folder name. Keep in mind the import folder must have the Destination or Both drop type. You may also specify the destination by the full path of the import folder on the server or by referencing it directly via `importfolders`.

```lua
if anime.restricted then
  destination = "hentai"
else
  destination = "anime"
end
```

#### I want to split my collection across subfolders

```lua
if anime.type == AnimeType.Movie then
    subfolder = { "Anime Movies", anime.preferredname }
else
    subfolder = { "Anime", anime.preferredname }
end
```

#### I want my anime to be grouped according to Shoko

Adding Shoko group name to subfolder path when there are multiple series in group.  
Warning: adding new series to a group with one entry will not move the old series into a subfolder, so you should probably use this when batch renaming/moving existing series

```lua
if #groups == 1 and #groups[1].animes > 1 then
  subfolder = {groups[1].name, anime.preferredname}
end
```
