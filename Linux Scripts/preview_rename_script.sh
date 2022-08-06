#!/usr/bin/env bash
if ! type jq > /dev/null; then
  echo "Please install jq to use this script"
fi
if [ $# -eq 0 ]; then
	echo "Pass filename of script as first parameter and optionally number of results as second parameter"
	echo "Provides preview of filenames after running the script"
	exit -1
fi
if ! test -f "$1"; then
    echo "$1 does not exist"
    exit -1
fi
script_json="{
  \"RenameScriptID\": 0,
  \"ScriptName\": \"AAA_WORKINGFILE_TEMP_AAA\",
  \"RenamerType\": \"LuaRenamer\",
  \"IsEnabledOnImport\": 0,
  \"Script\": $(jq -Rsa . "$1"),
  \"ExtraData\": null
}"
# Upload preview script to Shoko
curl -s -o /dev/null -d "$script_json" -H 'Content-Type: application/json' 'http://localhost:8111/v1/RenameScript'
# Get a random file to preview the effect of the renamer script

if [ -z "$2" ]; then
  count="10"
else
  count="$2"
fi
curl -s "http://localhost:8111/v1/File/Rename/RandomPreview/$count/1" | \
jq '.[].VideoLocalID' | \
while read -r vlocal_id
do
	curl -s "http://localhost:8111/v1/File/Rename/Preview/$vlocal_id" | jq '.NewFileName'
done