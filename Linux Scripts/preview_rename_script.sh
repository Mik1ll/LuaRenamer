#!/usr/bin/env bash

usage() {
  cat >&2 <<EOF
Usage: ${BASH_SOURCE[0]// /\\ } [OPTION] SCRIPT
Preview the rename/move results of a SCRIPT.

  -h, --help                Show help.
  -s, --host=HOST           Shoko server host. [default: localhost:8111]
  -u, --user=USERNAME       Shoko username [default: Default]
      --pass=PASSWORD       Shoko password.
  -c, --count=COUNT         How many results to request. [default: 10]
  -t, --type=RENAMERID      The renamer id to set for the script, e.g. WebAOM, 
                            LuaRenamer, ScriptRenamer. [default: LuaRenamer]
  -m, --move                Also preview the move result.
EOF
}

options=$(getopt -o "hs:u:c:t:m" -l "help,host:,user:,pass:,count:,type:,move" -- "$@") || { usage; exit 1; }
eval set -- "$options"

host='localhost:8111'
count='10'
type='LuaRenamer'
user='Default'
move='false'
while true; do
  case "$1" in
    -h | --help) usage; exit 0 ;;
    -s | --host) host="$2"; shift 2 ;;
    -u | --user) user="$2"; shift 2 ;;
    --pass) pass="$2"; shift 2 ;;
    -c | --count) count="$2"; shift 2 ;;
    -t | --type) type="$2"; shift 2 ;;
    -m | --move) move='true'; shift ;;
    --) shift; break ;;
  esac
done

min_pos_arg=1
pos_arg=0
while [[ $# -gt 0 ]]; do
  ((pos_arg=pos_arg+1))
  case "$pos_arg" in
    1) script_path="$1"; shift ;;
    *) echo "Error: Cannot take any more positional arguments" >&2; exit 1 ;;
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

loginjson=$(jq -n --arg user "$user" --arg pass "$pass" '{user:$user, pass:$pass, device:"rename_bash_script"}')
apikey=$(curl -s -H "Content-Type: application/json" -d "$loginjson" "http://$host/api/Auth" | jq -r '.apikey')

if ! [[ ${apikey//-/} =~ ^[[:xdigit:]]{32}$ ]]; then
  echo "Login did not return an api key, check --user and --pass"
  exit 1
fi

renamer_response=$(curl -s -i -H "apikey: $apikey" -H 'Accept: application/json' "http://$host/api/v3/Renamer/$type")
if ! printf %s "$renamer_response" | head -n 1 - | grep -q '200'; then
  echo 'Renamer type was not found on the server. Check the renamer ID, ensure the renamer is installed, then restart server after install.'
  exit 1
fi

renamer_json=$(printf %s "$renamer_response" | tr -d '\r' | awk -v 'RS=' 'NR==2')
default_settings_json=$(printf %s "$renamer_json" | jq '.DefaultSettings')

if ! printf %s "$renamer_json" | jq -e '.Settings | any(.Name == "Script" and .SettingType == "Code")' >/dev/null; then
  echo "Renamer does not have a setting called 'Script' with setting type 'Code', can't continue."
  exit 1
fi

RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

# Get a random files to preview the effect of the renamer script
random_ids_json=$(curl -s "http://$host/v1/File/Rename/RandomPreview/$count/1" | jq -c 'map(.VideoLocalID)')

script_content=$(<"$script_path")
preview_json=$(printf %s "$default_settings_json" | jq --arg renamerId "$type" --arg input "$script_content" --argjson ids "$random_ids_json" '{FileIDs: $ids, Config: { RenamerID: $renamerId, Name: "PreviewConfig", Settings: (.[] |= if .Name == "Script" then .Value = $input else . end) }}')

preview_response=$(curl -s -X POST -d "$preview_json" -H "apikey: $apikey" -H 'Content-Type: application/json' -H 'Accept: application/json' "http://$host/api/v3/Renamer/Preview?rename=true&move=$move")
echo "$preview_response" | jq -c '.[]' | \
while read -r result; do
  result_name=$(jq -r '.AbsolutePath' <<< "$result")
  if [[ $move == 'false' ]]; then
    result_name="${result_name##*[/\\]}"
  fi
  if jq -e '.IsSuccess' <<< "$result" >/dev/null; then
    printf "${GREEN}%s${NC}\n" "$result_name"
  else
    printf "${RED}%s${NC}\n" "$result_name"
  fi
done
