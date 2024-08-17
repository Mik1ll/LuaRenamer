Lua Renamer is a plugin for [Shoko Server](https://github.com/ShokoAnime/ShokoServer). It allows users to rename their collection via an Lua 5.4 interface.  
This renamer is fitting for users with more advanced collection renaming/organization requirements.

Limitations: The Lua environment is sandboxed such that interaction with the operating system/file system/networking is unavailable.

For support/questions join the [Shoko Discord server](https://discord.gg/shokoanime) and message [mikill](discord://-/users/116043375433482241).

## Installation

1. Download the [the release appropriate for your Shoko Server version.](https://github.com/Mik1ll/LuaRenamer/releases)
    * The [latest release](https://github.com/Mik1ll/LuaRenamer/releases/latest) should be compatible with current Stable.
    * Pre-releases will be compatible with Shoko Daily depending on the Abstractions Version, check release notes and [Shoko Server tags](https://github.com/ShokoAnime/ShokoServer/tags) for compatibility.
2. Unzip the folder into the Shoko plugin directory:
    * (Windows) `C:\ProgramData\ShokoServer\plugins`
    * (Docker) wherever the container location `/home/shoko/.shoko/Shoko.CLI/plugins` is mounted.
3. Restart Shoko Server.
4. (Recommended) Install [VS Code](https://code.visualstudio.com/download) and [the Lua extension](https://marketplace.visualstudio.com/items?itemName=sumneko.lua) to edit your script.
5. Follow instructions in the next section to add a script.

## Usage

1. Open the Server WebUI (port 8111 by default) and log in
2. Navigate to Utilities/File Rename.
3. Click the cog wheel icon to open the renamer config panel.
4. Create a new renamer config, enter a name and select LuaRenamer from the select box. If LuaRenamer is not visible, the renamer failed to load, check the server logs.
5. Add the files (button next to the config cog wheel) you wish to rename. The rename/move preview will automatically populate with changes you make.
6. The Move checkbox chooses whether the files are renamed and moved or only renamed.
7. The config can be set to be the Default, which is used when Rename On Import and Move On Import are set in the Import Settings. 
8. Once you are happy with the preview you can save the config and click Rename Files to rename/move+rename the previewed files.

### Renaming on Import

If you wish to rename/move your files on import you must do two things:

1. Set Rename/Move On Import to true in Shoko settings (via WebUI or settings-server.json).
2. Ensure your renamer config is saved as the Default.

## Script Writing

Check out [This short guide](https://learnxinyminutes.com/docs/lua/) if you are new to Lua.  

### With VS Code (Recommended)

1. VS Code + [the Lua extension](https://marketplace.visualstudio.com/items?itemName=sumneko.lua) is recommended for script editing.
    * The script environment provides [LuaCATS annotations](https://luals.github.io/wiki/annotations/) for type linting.
2. Open the plugin's lua subfolder in VS Code via `File -> Open Folder...`
3. You can create a new .lua script or edit the existing `default.lua` (editing default.lua will not change existing scripts on the server, but will be used when new scripts are created)
4. When your script is ready to test, copy the script content into the Renamer config in the WebUI. See [Usage](#Usage)

### The Environment

The lua environment is sandboxed, removing operations from standard libraries such as `io`, and `os`. See BaseEnv in [LuaContext](./LuaRenamer/LuaContext.cs).  
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

* `use_existing_anime_location` If true, the subfolder with the most files of the same anime is reused if one exists. This takes precedence over the subfolder set in the script (default: false)
* `replace_illegal_chars` If true, replaces all illegal path characters in subfolder and file name with alternatives. See [ReplaceMap in Utils.cs](./LuaRenamer/Utils.cs) (default: false)
* `remove_illegal_chars` If true, removes all illegal path characters in subfolder and file name. If false, illegal characters are replaced with underscores or replaced if `replace_illegal_chars` is true. (default: false)
* `skip_rename` If true, the result of running the script is discarded when renaming. (default: false)
* `skip_move` If true, the result of running the script is discarded when moving. (default: false)

## Notes for File Moving

### Destination

<wbr/>Import folders are only valid destination candidates if they exist and have either the `Destination` or `Both` Drop Type.  
> [!NOTE]
> Using `use_existing_anime_location` may bypass this restriction, allowing `None` but not `Source`. This may change in the future.

Destination defaults to the nearest (to the file) valid import folder.  
Destination is set via one of:

* <wbr/>Import folder name (string)
* Server folder path (string)
* <wbr/>Import folder reference (selected from `importfolders` array or `file.importfolder`)

If destination set via path, it is matched to import folder path with converted directory seperators but no other special handling (relative path or expansion).

### Subfolder

Subfolder defaults to the anime title in your preferred language.  
Subfolder is set via one of:

* Subfolder name (string)
* Path segments (array-table, e.g. `{"parent dir name", "subdir name", "..."}`)

If set via a string subfolder name, directory separators within the string are ignored or replaced depending on preference.  
Also see [`use_existing_anime_location` in Script Settings](#Script-Settings)

## Common Questions/Scenarios

### How do I split my collection across import folders?

The easiest option is to set the destination by import folder name. Keep in mind the import folder must have the Destination or Both drop type. You may also specify the destination by the full path of the import folder on the server or by referencing it directly via `importfolders`.

```lua
if anime.restricted then
  destination = "hentai"
else
  destination = "anime"
end
```

### How do I split my collection across subfolders?

```lua
if anime.type == AnimeType.Movie then
    subfolder = { "Anime Movies", anime.preferredname }
else
    subfolder = { "Anime", anime.preferredname }
end
```

### How do I group my anime according to Shoko?

Adding Shoko group name to subfolder path when there are multiple series in group.

```lua
if #groups == 1 and #groups[1].animes > 1 then
  subfolder = {groups[1].name, anime.preferredname}
end
```

### How do I move/rename my anime collection according to seasons?

AniDB, Shoko's metadata provider does not have the concept of seasons. Therefore the metadata available cannot be cleanly mapped. I recommend using Shoko Metadata for Plex or Shokofin for Jellyfin as your client instead of depending on other metadata providing plugins.

### How can I hard/soft link my files instead of moving them?

Neither Shoko nor this plugin has the ability to create file links. I recommend creating any links before the file is processed by Shoko. Usually download clients have the option to run a script on download completion. You can create a script to link files to a Shoko drop source folder. Feel free to contact me if you need help with this.  
Note: If you hard link your files you will need to create an import folder for each file system/volume used.
