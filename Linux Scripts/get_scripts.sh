#!/usr/bin/env bash

usage() {
  >&2 cat << EOF
Usage: ${BASH_SOURCE[0]// /\\ } [-h | --help] [--host <host>] [--port <port>]
EOF
}

options=$(getopt -o "h" -l "help,host:,port:" -- "$@")
[[ $? -eq 0 ]] || {
  usage
  exit 1
}
eval set -- "$options"

host='localhost'
port='8111'
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
    --)
      shift
      break 
      ;;
  esac
done

if ! command -v jq >/dev/null 2>&1; then
  echo "Please install jq to use this script"
fi

if [[ $(curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host:$port/api/v3/Init/Status" | jq '.State==2') != 'true' ]]; then
  echo "Unabled to connect or server not running/started at target http://$host:$port"
  exit 1
fi

curl -s "http://$host:$port/v1/RenameScript" | jq
