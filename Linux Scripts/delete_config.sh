#!/usr/bin/env bash

usage() {
  cat >&2 <<EOF
Usage: ${BASH_SOURCE[0]// /\\ } [OPTION]... CONFIG_NAME
Get every config stored in Shoko.

  -h, --help                Show help.
  -s, --host=HOST           Shoko server host. [default: localhost:8111]
  -u, --user=USERNAME       Shoko username [default: Default]
      --pass=PASSWORD       Shoko password.
EOF
}

options=$(getopt -o 'hs:u:' -l 'help,host:,user:,pass:' -- "$@") || { usage; exit 1; }
eval set -- "$options"

host='localhost:8111'
user='Default'
while true; do
  case "$1" in
    -h | --help) usage; exit 0 ;;
    -s | --host) host="$2"; shift 2 ;;
    -u | --user) user="$2"; shift 2 ;;
    --pass) pass="$2"; shift 2 ;;
    --) shift; break ;;
  esac
done

min_pos_arg=1
pos_arg=0
while [[ $# -gt 0 ]]; do
  ((pos_arg=pos_arg+1))
  case "$pos_arg" in
    1) script_name="$1"; shift ;;
    *) printf '%s\n' 'Error: Cannot take any more positional arguments' >&2; exit 1 ;;
  esac
done

if [[ $pos_arg -lt $min_pos_arg ]]; then
  printf '%s\n' "Error: must provide at least $min_pos_arg positional argument(s)"
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  printf '%s\n' 'Please install jq to use this script'
fi

if ! status=$(curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host/api/v3/Init/Status"); then
  printf '%s\n' "Unable to connect to server at http://$host"
  exit 1
fi

if ! jq -e '.State == 2 // .State == "Started"' >/dev/null <<< "$status"; then
  printf '%s\n' "Server not running/started at http://$host"
  exit 1
fi

loginjson=$(jq -n --arg user "$user" --arg pass "$pass" '{user:$user, pass:$pass, device:"rename_bash_script"}')
apikey=$(curl -s -H 'Content-Type: application/json' -d "$loginjson" "http://$host/api/Auth" | jq -r '.apikey')

if ! [[ ${apikey//-/} =~ ^[[:xdigit:]]{32}$ ]]; then
  printf '%s\n' 'Login did not return an api key, check --user and --pass'
  exit 1
fi

RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

script_name_uri_encoded=$(printf '%s' "$script_name" | jq -Rr @uri)


delete_response=$(curl -i -s -X DELETE -H "apikey: $apikey" "http://$host/api/v3/Renamer/Config/$script_name_uri_encoded")
if printf '%s' "$delete_response" | head -n 1 - | grep -q '200'; then
  printf "${GREEN}%s${NC}\n" "Success! Deleted the config from the server."
else
  printf '%s\n' "$delete_response"
  printf "${RED}%s${NC}\n" "Failed! Config was not deleted."
fi
