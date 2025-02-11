#!/usr/bin/env bash

usage() {
  cat >&2 <<EOF
Usage: ${BASH_SOURCE[0]// /\\ } [OPTION]... CONFIG_NAME
Rename/move all files in Shoko using the CONFIG.

  -h, --help                Show help.
  -s, --host=HOST           Shoko server host. [default: localhost:8111]
  -u, --user=USERNAME       Shoko username [default: Default]
      --pass=PASSWORD       Shoko password.
  -m, --move                Also move the files.
EOF
}

options=$(getopt -o "hs:u:m" -l "help,host:,user:,pass:,move" -- "$@") || { usage; exit 1; }
eval set -- "$options"

host='localhost:8111'
move='false'
user='Default'
while true; do
  case "$1" in
    -h | --help) usage; exit 0 ;;
    -s | --host) host="$2"; shift 2 ;;
    -u | --user) user="$2"; shift 2 ;;
    --pass) pass="$2"; shift 2 ;;
    -m | --move) move='true'; shift ;;
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
  printf '%s\n' 'Please install jq to use this script.'
fi

if ! curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host/api/v3/Init/Status" | jq -e '.State == 2 // .State == "Started"' >/dev/null; then
  printf '%s\n' "Unable to connect or server not running/started at target http://$host"
  exit 1
fi

loginjson=$(jq -n --arg user "$user" --arg pass "$pass" '{user:$user, pass:$pass, device:"rename_bash_script"}')
apikey=$(curl -s -H "Content-Type: application/json" -d "$loginjson" "http://$host/api/Auth" | jq -r '.apikey')

if ! [[ ${apikey//-/} =~ ^[[:xdigit:]]{32}$ ]]; then
  printf '%s\n' "Login did not return an api key, check --user and --pass"
  exit 1
fi

RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

script_name_uri_encoded=$(printf '%s' "$script_name" | jq -Rr @uri)

page=1
while fileIds="$(curl -s -H "apikey: $apikey" -H 'Accept: application/json' "http://$host/api/v3/File?sortOrder=FileID&page=$page&pageSize=1000&exclude=Unrecognized" | jq -e 'if .List == [] then null else .List | map(.ID) end')"; do
  rename_response=$(curl -s -X POST -d "$fileIds" -H "apikey: $apikey" -H 'Content-Type: application/json' -H 'Accept: application/json' "http://$host/api/v3/Renamer/Config/$script_name_uri_encoded/Relocate?rename=true&move=$move")
  IFS=$'\n' jq -c '.[]' <<< "$rename_response" | \
  while read -r result; do
    if jq -e '.IsSuccess' <<< "$result" >/dev/null; then
        result_name=$(jq -r '.AbsolutePath' <<< "$result")
        if [[ $move == 'false' ]]; then
          result_name="${result_name##*[/\\]}"
        fi
        printf "${GREEN}%s${NC}\n" "$result_name"
      else
        printf "${RED}%s${NC}\n" "$(jq -r '.ErrorMessage' <<<"$result"))"
      fi
  done
  
  ((page+=1))
done
