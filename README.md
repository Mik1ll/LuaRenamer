## LuaRenamer

Lua file renaming and moving plugin for Shoko. Uses Lua 5.4.

## Installation

1. Download the [latest release](https://github.com/Mik1ll/LuaRenamer/releases/latest)
2. Unzip the files into destination
    * (Windows) `C:\ProgramData\ShokoServer\plugins`
    * (Docker) wherever the container location `/home/shoko/.shoko/Shoko.CLI/plugins` is mounted
3. Restart Shoko Server
4. (Optional) Install VS Code and [the Lua extension](https://marketplace.visualstudio.com/items?itemName=sumneko.lua) to edit your script. The
   extension uses [LuaCATS annotations](https://luals.github.io/wiki/annotations/)
5. Follow instructions in the next section to add your script

## Usage

### With Shoko Desktop

1. Open Shoko Desktop
2. Navigate to Utilities/File Renaming
3. Use the Default script or create a new one and set the type of the script to LuaRenamer in the drop-down menu
4. (Optional) Open the lua subfolder of the extracted plugin in VS Code
5. Look at the [example script](./LuaRenamer/lua/example.lua) and the bottom of [defs.lua](./LuaRenamer/lua/defs.lua) to get an idea of available variables
    1. (Optional) Check out [The lualinq docs](./LuaRenamer/lua/LuaLinq.pdf) for info on uses of 'from()'
6. Create a script or copy and edit the [example script](./LuaRenamer/lua/example.lua)
7. Paste the script in the text box in Shoko Desktop
8. Test your script by adding files and clicking preview in the utility under the text box until you are satisfied
9. Check run on import, and save the script (next to the script type drop-down)
10. You may manually rename and move the files (if checked) of your collection in the utility

### Linux/Without Shoko Desktop

1. Copy/download [the linux scripts](./Linux%20Scripts)
2. (Optional) Open the lua subfolder of the extracted plugin in VS Code
3. Look at the [example script](./LuaRenamer/lua/example.lua) and the bottom of [defs.lua](./LuaRenamer/lua/defs.lua) to get an idea of available variables
    1. (Optional) Check out [The lualinq docs](./LuaRenamer/lua/LuaLinq.pdf) for info on uses of 'from()'
4. Create a script or copy and edit the [example script](./LuaRenamer/lua/example.lua)
5. Preview the results with [the preview script](./Linux%20Scripts/preview_rename_script.sh) `./preview_rename_script.sh <script filename> [# results]`
6. Add your script to Shoko with [the add script](./Linux%20Scripts/add_rename_script.sh) `./add_rename_script.sh <script filename>`
7. If you want to rename and move all existing files use [the rename script](./Linux%20Scripts/rename_and_move_all.sh) `./rename_and_move_all.sh <script name>`

## Notes for File Moving

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
If 'use_existing_anime_location' is set to true, the subfolder of the most recent file of the same anime is reused if one exists. 
This takes precedence over the subfolder set in the script.

## Script Environment

The lua environment is sandboxed, removing operations from standard libraries such as io, and os. See BaseEnv in [LuaContext](./LuaRenamer/LuaContext.cs).  
Additionally, a modified version of [lualinq from xanathar](https://github.com/xanathar/lualinq), licensed under the BSD 3 clause, has
been [included](./LuaRenamer/lua/lualinq.lua) for convenience. [Original Documentation](./LuaRenamer/lua/LuaLinq.pdf)

See [defs.lua](./LuaRenamer/lua/defs.lua) for all exposed data definitions/structure available from shoko.

## [Example Script](./LuaRenamer/lua/example.lua)

## Useful Snippets

Choosing a destination (picks folders by name, or folder path of existing import folder)

```lua
if anime.restricted then
  destination = "hentai"
else
  destination = "anime"
end
```

Choosing a destination by table (complex), edit second line to add conditions

```lua
destination = from(importfolders):where(function (importfld) ---@param importfld ImportFolder
  return importfld.type == ImportFolderType.Destination or importfld.type == ImportFolderType.Both
end):first()
```

Adding Shoko group name to subfolder path when there are multiple series in group.  
Warning: adding new series to a group with one entry will not move the old series into a subfolder, so you should probably use it when batch renaming/moving
existing series

```lua
if #groups == 1 and #groups[1].animes > 1 then
  subfolder = {groups[1].name, anime.preferredname}
end
```
