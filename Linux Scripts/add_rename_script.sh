#!/usr/bin/env bash
if ! command -v jq >/dev/null 2>&1; then
  echo "Please install jq to use this script"
fi
if [[ $# -eq 0 ]]; then 
	echo "Pass filename as parameter to add it to shoko and enable run on import"
	exit 1
fi
if ! [[ -f "$1" ]]; then
    echo "$1 does not exist"
    exit 1
fi
if [[ $(basename "$1") == *" "* ]]; then
  echo "Filename must not have spaces"
  exit 1
elif ! [[ $(basename "$1") == *.lua ]]; then
  echo "Filename must end in .lua"
  exit 1
fi
scriptbasename=$(basename "$1" .lua)
scriptid=$(curl -s "http://localhost:8111/v1/RenameScript" | jq -c ".[] | select(.ScriptName==\"$scriptbasename\").RenameScriptID")
if [[ -z "$scriptid" ]]; then
  scriptid="0"
fi
script_json="{
  \"ScriptName\": \"$scriptbasename\",
  \"Script\": $(jq -Rsa . "$1"),
  \"IsEnabledOnImport\": 1,
  \"RenameScriptID\": $scriptid,
  \"RenamerType\": \"LuaRenamer\",
  \"ExtraData\": null
}"

# Upload script to Shoko
curl -s -o /dev/null -d "$script_json" -H 'Content-Type: application/json' 'http://localhost:8111/v1/RenameScript'
