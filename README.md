# LuaRenamer
Lua file renaming and moving plugin for Shoko. Uses Lua 5.4.

## Installation
1. Download the [latest release](https://github.com/Mik1ll/LuaRenamer/releases/latest)
2. Unzip the files into C:\ProgramData\ShokoServer\plugins (Windows) or /home/.shoko/Shoko.CLI/plugins (Linux/Docker)
3. (Optional) You may COPY (not move) the lua sub-directory to a convenient location for script editing
4. It is strongly recommened to install VS Code and [the Lua extension](https://marketplace.visualstudio.com/items?itemName=sumneko.lua) to edit your script. The
   extension uses [EmmyLua for annotations](https://github.com/sumneko/lua-language-server/wiki/EmmyLua-Annotations)
5. Follow instructions in the next section to add your script

## Usage
1. Open the lua folder under the directory you extracted, in VS Code
2. Look at the example script and the bottom of defs.lua to get an idea of available variables
3. Create a new file or modify the example script to fit your needs. NOTE: 'example.lua' is not read by the renamer, you must save it as a script within shoko, see next steps
4. Open Shoko Desktop
5. Navigate to Utilities/File Renaming
6. Use the Default script or create a new one and set the type of the script to LuaRenamer in the drop-down menu
7. Copy your script and paste it in the text box
8. Test your script by adding files and clicking preview in the utility under the text box until you are satisfied
9. Check run on import, and save the script (next to the script type drop-down)
10. You may manually rename and move the files (if checked) of your collection in the utility

## Important Notes for File Moving
Destination defaults to the nearest (longest matching prefix) destination folder it can find.  
If destination is set, it must be set to an existing import folder using the name or path (string), or an import folder table
If destination set to a path, it is compared to import folder path with converted directory seperators but no other special handling (relative path or expansion)  
The only destination folders settable by the renamer are import folders with Drop Type of Destination or Both.  
Subfolder defaults to your preferred language anime title.  
If subfolder is set, it must be set to an array-table of path segments e.g. subfolder = {"parent dir name", "subdir name", "..."}  
If 'use_existing_anime_location' is set to true, the last added file's location from the same anime will be used if it exists.  

## Script Environment
The lua environment is sandboxed, removing operations from standard libraries such as io, and os. See BaseEnv in [NLuaSingleton](./LuaRenamer/NLuaSingleton.cs).  
Additionally, a modified version of [lualinq from xanathar](https://github.com/xanathar/lualinq), licensed under the BSD 3 clause, has
been [included](./LuaRenamer/lua/lualinq.lua) for convenience. [Original Documentation](./LuaRenamer/lua/LuaLinq.pdf)

See [defs.lua](./LuaRenamer/lua/defs.lua) for all exposed data definitions/structure available from shoko.

## [Example Script](./LuaRenamer/lua/example.lua)

## Snippets
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
Warning: adding new series to a group with one entry will not move the old series into a subfolder, so you should probably use it when batch renaming/moving existing series
```lua
if #groups == 1 and #groups[1].seriesids > 1 then
  subfolder = {groups[1].name, anime.preferredname}
end
```
