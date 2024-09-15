#!/usr/bin/env bash

usage() {
  cat >&2 <<EOF
Usage: ${BASH_SOURCE[0]// /\\ } [OPTION] SCRIPT
Add or update a config on Shoko with the content of SCRIPT. The config name will
be set to the SCRIPT's base file name.

  -h, --help                Show help.
  -s, --host=HOST           Shoko server host. [default: localhost:8111]
  -u, --user=USERNAME       Shoko username. [default: Default]
      --pass=PASSWORD       Shoko password.
  -t, --type=RENAMERID      The renamer id to set for the script, e.g. WebAOM, 
                            LuaRenamer, ScriptRenamer. [default: LuaRenamer]
  -d, --default             Make this config the default, it will be used if 
                            move/rename on import is enabled.
EOF
}

options=$(getopt -o 'hs:u:t:d' -l 'help,host:,user:,pass:,type:,default' -- "$@") || { usage; exit 1; }
eval set -- "$options"

host='localhost:8111'
default=false
type='LuaRenamer'
user='Default'
while true; do
  case "$1" in
    -h | --help) usage; exit 0 ;;
    -s | --host) host="$2"; shift 2 ;;
    -u | --user) user="$2"; shift 2 ;;
    --pass) pass="$2"; shift 2 ;;
    -t | --type) type="$2"; shift 2 ;;
    -d | --default) default=true; shift ;;
    --) shift; break ;;
  esac
done

min_pos_arg=1
pos_arg=0
while [[ $# -gt 0 ]]; do
  ((pos_arg=pos_arg+1))
  case "$pos_arg" in
    1) script_path="$1"; shift ;;
    *) printf '%s\n' 'Error: Cannot take any more positional arguments' >&2; exit 1 ;;
  esac
done

if [[ $pos_arg -lt $min_pos_arg ]]; then
  printf '%s\n' "Error: must provide at least $min_pos_arg positional argument(s)."
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  printf '%s\n' 'Please install jq to use this script.'
fi

if ! [[ -f "$script_path" ]]; then
  printf '%s\n' "\"$script_path\" does not exist."
  exit 1
fi

if ! curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host/api/v3/Init/Status" | jq -e '.State==2' >/dev/null; then
  printf '%s\n' "Unable to connect or server not running/started at http://$host."
  exit 1
fi

loginjson=$(jq -n --arg user "$user" --arg pass "$pass" '{user:$user, pass:$pass, device:"rename_bash_script"}')
apikey=$(curl -s -H 'Content-Type: application/json' -d "$loginjson" "http://$host/api/Auth" | jq -r '.apikey')

if ! [[ ${apikey//-/} =~ ^[[:xdigit:]]{32}$ ]]; then
  printf '%s\n' 'Login did not return an api key, check --user and --pass'
  exit 1
fi

renamer_response=$(curl -s -i -H "apikey: $apikey" -H 'Accept: application/json' "http://$host/api/v3/Renamer/$type")
if ! printf '%s' "$renamer_response" | head -n 1 - | grep -q '200'; then
  printf '%s\n' 'Renamer type was not found on the server. Check the renamer ID, ensure the renamer is installed, then restart server after install.'
  exit 1
fi

renamer_json=$(printf '%s' "$renamer_response" | tr -d '\r' | awk -v 'RS=' 'NR==2')
default_settings_json=$(printf '%s' "$renamer_json" | jq '.DefaultSettings')

if ! printf '%s' "$renamer_json" | jq -e '.Settings | any(.Name == "Script" and .SettingType == "Code")' >/dev/null; then
  printf '%s\n' "Renamer does not have a setting called 'Script' with setting type 'Code', can't continue."
  exit 1
fi

script_filename=$(basename -- "$script_path")
script_name="${script_filename%.*}"
script_name_uri_encoded=$(printf '%s' "$script_name" | jq -Rr @uri)

script_content=$(<"$script_path")

script_response=$(curl -s -i -H "apikey: $apikey" -H 'Accept: application/json' "http://$host/api/v3/Renamer/Config/$script_name_uri_encoded")
if printf '%s' "$script_response" | head -n 1 - | grep -q '200'; then
  printf '%s\n' 'Found config with same name, replacing.'
  script_json=$(printf '%s' "$script_response" | tr -d '\r' | awk -v 'RS=' 'NR==2')
  new_script_json=$(printf '%s' "$script_json" | jq --arg input "$script_content" '(.Settings[] | select(.Name == "Script") | .Value) |= $input')
  update_response=$(curl -s -i -H "apikey: $apikey" -H 'Content-Type: application/json' -X PUT -d "$new_script_json" "http://$host/api/v3/Renamer/Config/$script_name_uri_encoded")
elif printf '%s' "$script_response" | head -n 1 - | grep -q '404'; then
  printf '%s\n' 'Adding new config.'
  new_script_json=$(printf '%s' "$default_settings_json" | jq --arg renamerId "$type" --arg name "$script_name" --arg input "$script_content" '{ RenamerID: $renamerId, Name: $name, Settings: (.[] |= if .Name == "Script" then .Value = $input else . end) }')
  update_response=$(curl -s -i -H "apikey: $apikey" -H 'Content-Type: application/json' -X POST -d "$new_script_json" "http://$host/api/v3/Renamer/Config")
else
  printf '%s\n' "$script_response"
  printf "${RED}%s${NC}\n" "Failed! Bad status code for config response."
  exit 1
fi

RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

if printf '%s' "$update_response" | head -n 1 - | grep -q '200'; then
  update_json=$(printf '%s' "$update_response" | tr -d '\r' | awk -v 'RS=' 'NR==2' | jq)
  printf '%s' "$update_json" | jq -C
  if $default; then
    patch_json=$(jq -n --arg name "$script_name" '[{"op":"replace", "path":"/Plugins/Renamer/DefaultRenamer", "value": $name }]')
    settings_response=$(curl -s -i -H "apikey: $apikey" -H 'Content-Type: application/json' -X PATCH -d "$patch_json" "http://$host/api/v3/Settings")
    if printf %s "$settings_response" | head -n 1 - | grep -q '200'; then
      printf "${GREEN}%s${NC}\n" "Success! Updated the config and set as the default."
    else
      printf '%s\n' "$settings_response"
      printf "${RED}%s${NC}\n" "Failed! Updated the config, but got bad status code for default setting response."
    fi
  else
    printf "${GREEN}%s${NC}\n" "Success! Updated the config."
  fi
else
  printf '%s\n' "$update_response"
  printf "${RED}%s${NC}\n" "Failed! Bad status code for update response."
  exit 1
fi
