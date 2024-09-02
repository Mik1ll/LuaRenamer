#!/usr/bin/env bash

usage() {
  >&2 cat << EOF
Usage: ${BASH_SOURCE[0]// /\\ } [options] <script path>

-h, --help                Show help.
--host=<host>             Shoko server host. [default: localhost]
--port=<port>             Shoko server port. [default: 8111]
-i, --import-run          Run this script on import, disables other scripts if 
                          they are set to run on import.
-t <renamer type>, --type=<renamer type>
                          The renamer id to set for the script.
                          [default: LuaRenamer]
<script path>             The path to the .lua script to add. Will use filename 
                          sans extension as the script name.
EOF
}

options=$(getopt -o "hit:" -l "help,host:,port:,import-run:,type:" -- "$@")
[[ $? -eq 0 ]] || {
  usage
  exit 1
}
eval set -- "$options"

host='localhost'
port='8111'
import_run=0
type='LuaRenamer'
while true; do
  case "$1" in
    --host)
      host="$2"
      shift 2
      ;;
    --port)
      port="$2"
      shift 2
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    -i | --import-run)
      import_run=1
      shift
      ;;
    -t | --type)
      type="$2"
      shift 2
      ;;
    --)
      shift
      break 
      ;;
  esac
done


min_pos_arg=1
pos_arg=0
while [[ $# -gt 0 ]]; do
  ((pos_arg=pos_arg+1))
  case "$pos_arg" in
    1)
      script_filename="$1"
      shift
      ;;
    *)
      >&2 printf "Error: Cannot take any more positional arguments\n"
      exit 1
      ;;
  esac
done


if [[ $pos_arg -lt $min_pos_arg ]]; then
  echo "Error: must provide at least $min_pos_arg positional argument(s)."
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "Please install jq to use this script."
fi

if ! [[ -f "$script_filename" ]]; then
  echo "\"$script_filename\" does not exist."
  exit 1
elif ! [[ $(basename "$script_filename") == *.lua ]]; then
  echo "Filename must end in .lua."
  exit 1
fi

if [[ $(curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host:$port/api/v3/Init/Status" | jq '.State==2') != 'true' ]]; then
  echo "Unabled to connect or server not running/started at http://$host:$port."
  exit 1
fi

if [[ $(curl -s -H 'Accept: application/json' "http://$host:$port/v1/RenameScript/Types" | jq "has(\"$type\")") != 'true' ]]; then
  echo "Renamer type was not found on the server. Check the renamer ID, ensure the renamer is installed, then restart server after install."
  exit 1
fi

script_name=$(basename "$script_filename" .lua)
scriptid=$(curl -s "http://$host:$port/v1/RenameScript" | jq -c ".[] | select(.ScriptName==\"$script_name\").RenameScriptID")
if [[ -z "$scriptid" ]]; then
  scriptid="0"
  echo "Adding new script."
else
  echo "Found script with same name, replacing."
fi

script_content=$(jq -Rsa . "$script_filename")

read -r -d '' script_json << EOM
{
  "ScriptName": "$script_name",
  "Script": $script_content,
  "IsEnabledOnImport": $import_run,
  "RenameScriptID": $scriptid,
  "RenamerType": "$type",
  "ExtraData": null
}
EOM

# Upload script to Shoko
curl -s -o /dev/null -d "$script_json" -H 'Content-Type: application/json' "http://$host:$port/v1/RenameScript"

# TODO: Change value of IsEnabledOnImport for all other scripts if it is set
