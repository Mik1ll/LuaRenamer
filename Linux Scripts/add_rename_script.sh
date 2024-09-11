#!/usr/bin/env bash

usage() {
  >&2 cat << EOF
Usage: ${BASH_SOURCE[0]// /\\ } [options] <script path>

-h, --help                Show help.
--host <host>             Shoko server host. [default: localhost]
--port <port>             Shoko server port. [default: 8111]
-i, --import-run          Run this script on import, disables other scripts if 
                          they are set to run on import.
-t <renamer type>, --type <renamer type>
                          The renamer id to set for the script.
                          [default: LuaRenamer]
--user <username>         Shoko username [default: Default]
--pass <password>         Shoko password
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
user='Default'
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
    --user)
      user="$2"
      shift 2
      ;;
    --pass)
      pass="$2"
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

if [[ ! -f "$script_filename" ]]; then
  echo "\"$script_filename\" does not exist."
  exit 1
elif [[ $(basename "$script_filename") != *.lua ]]; then
  echo "Filename must end in .lua."
  exit 1
fi

if [[ $(curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host:$port/api/v3/Init/Status" | jq '.State==2') != 'true' ]]; then
  echo "Unabled to connect or server not running/started at http://$host:$port."
  exit 1
fi

loginjson=$(jq --null-input --arg user "$user" --arg pass "$pass" '. + {user:$user, pass:$pass, device:"rename_bash_script"}')
apikey=$(curl -s -H "Content-Type: application/json" -d "$loginjson" "http://$host:$port/api/Auth" | jq -r '.apikey')

if ! [[ ${apikey//-/} =~ ^[[:xdigit:]]{32}$ ]]; then
  echo "Login did not return an api key, check --user and --pass"
  exit 1
fi

renamer_response=$(curl -s -i -H "apikey: $apikey" -H 'Accept: application/json' "http://$host:$port/api/v3/Renamer/$type")
if [[ -z $(echo "$renamer_response" | head -n 1 - | grep '200') ]]; then
  echo "Renamer type was not found on the server. Check the renamer ID, ensure the renamer is installed, then restart server after install."
  exit 1
fi

renamer_json=$(printf %s "$renamer_response" | tr -d '\r' | awk -v RS='' 'NR==2')
default_settings_json=$(printf %s "$renamer_json" | jq '.DefaultSettings')

if [[ $(printf %s "$renamer_json" | jq '.Settings | any(.Name == "Script" and .SettingType == "Code")') != 'true' ]]; then
  echo "Renamer does not have a setting called 'Script' with setting type 'Code', can't continue."
  exit 1
fi


script_name=$(basename "$script_filename" .lua)
script_name_url_encoded=$(printf %s "$script_name" | jq -Rr @uri)

script_content=$(<"$script_filename")

script_response=$(curl -s -i -H "apikey: $apikey" -H 'Accept: application/json' "http://$host:$port/api/v3/Renamer/Config/$script_name_url_encoded")
if [[ ! -z $(echo "$script_response" | head -n 1 - | grep '200') ]]; then
  echo "Found config with same name, replacing."
  script_json=$(printf %s "$script_response" | tr -d '\r' | awk -v RS='' 'NR==2')
  new_script_json=$(printf %s "$script_json" | jq --arg input "$script_content" '(.Settings[] | select(.Name == "Script") | .Value) |= $input')
  update_response=$(curl -s -i -H "apikey: $apikey" -H 'Content-Type: application/json' -X PUT -d "$new_script_json" "http://$host:$port/api/v3/Renamer/Config/$script_name_url_encoded")
elif [[ ! -z $(echo "$script_response" | head -n 1 - | grep '404') ]]; then
  echo "Adding new config."
  new_script_json=$(printf %s "$default_settings_json" | jq --arg renamerId "$type" --arg name "$script_name" --arg input "$script_content" '{ RenamerID: $renamerId, Name: $name, Settings: (.[] |= if .Name == "Script" then .Value = $input else . end) }')
  update_response=$(curl -s -i -H "apikey: $apikey" -H 'Content-Type: application/json' -X POST -d "$new_script_json" "http://$host:$port/api/v3/Renamer/Config")
fi

RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

if [[ ! -z $(echo "$update_response" | head -n 1 - | grep '200') ]]; then
  printf "${GREEN}%s${NC}\n" "Success!"
  update_json=$(printf %s "$update_response" | tr -d '\r' | awk -v RS='' 'NR==2' | jq)
  echo "$update_json" | jq -C
else
  printf "${RED}%s${NC}\n" "Failed! Response:"
  echo "$update_response"
  exit 1
fi