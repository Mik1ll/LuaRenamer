#!/usr/bin/env bash
if ! command -v jq >/dev/null 2>&1; then
  echo "Please install jq to use this script"
fi
min_pos_arg=1
pos_arg=0
host='localhost'
port='8111'
move='false'
while [[ $# -gt 0 ]]; do
  case "$1" in
    --host*)
      if [[ "$1" != *=* ]]; then shift; fi # Value is next arg if no `=`
      host="${1#*=}"
      ;;
    --port*)
      if [[ "$1" != *=* ]]; then shift; fi
      port="${1#*=}"
      ;;
    --help|-h)
      echo "Usage: ${BASH_SOURCE[0]// /\\ } [--help|-h] [--host <host>] [--port <port>] [--move] <script name>"
      exit 0
      ;;
    --move)
      move='true'
      ;;
    -*)
      >&2 printf "Error: Invalid named argument/flag\n"
      exit 1
      ;;
    *)
      ((pos_arg=pos_arg+1))
      case "$pos_arg" in
        1)
          script_name="$1"
          ;;
        *)
          >&2 printf "Error: Cannot take any more positional arguments\n"
          exit 1
          ;;
      esac
      ;;
  esac
  shift
done

if [[ $pos_arg -lt $min_pos_arg ]]; then
  echo "Error: must provide at least $min_pos_arg positional argument(s)"
  exit 1
fi

if [[ $(basename "$script_name") == *.lua ]]; then
  echo "Use the script name on the server, not the file name, re-add the script if it is updated"
  exit 1
fi

if [[ $(curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host:$port/api/v3/Init/Status" | jq '.State==2') != 'true' ]]; then
  echo "Server not running/started at target host+port"
  exit 1
fi

if [[ -z $(curl -s "http://$host:$port/v1/RenameScript" | jq ".[] | select(.ScriptName==\"$script_name\")") ]]; then
  echo "Script \"$script_name\" does not exist on the server, try adding it first"
  exit 1
fi
RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

script_name_uri_encoded=$(jq -rn --arg 'x' "$script_name" '$x|@uri')
curl -s "http://$host:$port/v1/File/Rename/RandomPreview/2147483647/1" | \
jq '.[].VideoLocalID' | \
while read -r vlocal_id
do
	result=$(curl -s "http://$host:$port/v1/File/Rename/$vlocal_id/$script_name_uri_encoded/$move")
	result_name=$(jq -r '.NewFileName' <<< "$result")
	if [[ $(jq '.Success' <<< "$result") == 'true' ]]; then
	  printf "${GREEN}%s${NC}\n" "$result_name"
	else
	  printf "${RED}%s${NC}\n" "$result_name"
	fi
done
