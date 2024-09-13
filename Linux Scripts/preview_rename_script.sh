#!/usr/bin/env bash

usage() {
  cat >&2 <<EOF
Usage: ${BASH_SOURCE[0]// /\\ } [OPTION] SCRIPT
Preview the move/rename results of a SCRIPT.

  -h, --help                Show help.
  -s, --host=HOST           Shoko server host. [default: localhost:8111]
  -u, --user=USERNAME       Shoko username [default: Default]
      --pass=PASSWORD       Shoko password.
  -c, --count=COUNT         How many results to request.
  -t, --type=RENAMERID      The renamer id to set for the script, e.g. WebAOM, 
                            LuaRenamer, ScriptRenamer. [default: LuaRenamer]
EOF
}

options=$(getopt -o "hc:t:s:u:" -l "help,host:,count:,type:,user:,pass:" -- "$@") || { usage; exit 1; }
eval set -- "$options"

host='localhost:8111'
count='10'
type='LuaRenamer'
user='Default'
while true; do
  case "$1" in
    -s | --host)
      host="$2"
      shift 2
      ;;
    -c | --count)
      count="$2"
      shift 2
      ;;
    -u | --user)
      user="$2"
      shift 2
      ;;
    --pass)
      pass="$2"
      shift 2
      ;;
    -t | --type)
      type="$2"
      shift 2
      ;;
    -h | --help)
      usage
      exit 0
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
      script_path="$1"
      shift
      ;;
    *)
      echo "Error: Cannot take any more positional arguments" >&2
      exit 1
      ;;
  esac
done

if [[ $pos_arg -lt $min_pos_arg ]]; then
  echo "Error: must provide at least $min_pos_arg positional argument(s)"
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "Please install jq to use this script"
fi

if ! [[ -f "$script_path" ]]; then
  echo "\"$script_path\" does not exist."
  exit 1
fi

if ! curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host/api/v3/Init/Status" | jq -e '.State==2' >/dev/null; then
  echo "Unable to connect or server not running/started at http://$host."
  exit 1
fi

loginjson=$(jq --null-input --arg user "$user" --arg pass "$pass" '. + {user:$user, pass:$pass, device:"rename_bash_script"}')
apikey=$(curl -s -H "Content-Type: application/json" -d "$loginjson" "http://$host/api/Auth" | jq -r '.apikey')

if ! [[ ${apikey//-/} =~ ^[[:xdigit:]]{32}$ ]]; then
  echo "Login did not return an api key, check --user and --pass"
  exit 1
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
curl -s -o /dev/null -d "$script_json" -H 'Content-Type: application/json' "http://$host/v1/RenameScript"
# Get a random file to preview the effect of the renamer script

curl -s "http://$host/v1/File/Rename/RandomPreview/$count/1" | \
jq '.[].VideoLocalID' | \
while read -r vlocal_id
do
	curl -s "http://$host/v1/File/Rename/Preview/$vlocal_id" | jq '.NewFileName'
done
