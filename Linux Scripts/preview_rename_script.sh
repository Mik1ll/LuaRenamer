#!/usr/bin/env bash

usage() {
  >&2 cat << EOF
Usage: ${BASH_SOURCE[0]// /\\ } [-h | --help] [--host <host>] [--port <port>] [-c <count> | --count <count>] <script path>
EOF
}

options=$(getopt -o "hc:" -l "help,host:,port:,count:" -- "$@")
[[ $? -eq 0 ]] || {
  usage
  exit 1
}
eval set -- "$options"

host='localhost'
port='8111'
count='10'
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
    -c | --count)
      count="$2"
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
  echo "Error: must provide at least $min_pos_arg positional argument(s)"
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "Please install jq to use this script"
fi

if ! [[ -f "$script_filename" ]]; then
  echo "\"$script_filename\" does not exist."
  exit 1
elif ! [[ $(basename "$script_filename") == *.lua ]]; then
  echo "Filename must end in .lua."
  exit 1
fi

if [[ $(curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host:$port/api/v3/Init/Status" | jq '.State==2') != 'true' ]]; then
  echo "Unabled to connect or server not running/started at target http://$host:$port."
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
curl -s -o /dev/null -d "$script_json" -H 'Content-Type: application/json' "http://$host:$port/v1/RenameScript"
# Get a random file to preview the effect of the renamer script

curl -s "http://$host:$port/v1/File/Rename/RandomPreview/$count/1" | \
jq '.[].VideoLocalID' | \
while read -r vlocal_id
do
	curl -s "http://$host:$port/v1/File/Rename/Preview/$vlocal_id" | jq '.NewFileName'
done
