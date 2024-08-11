#!/usr/bin/env bash
if ! command -v jq >/dev/null 2>&1; then
  echo "Please install jq to use this script"
fi
min_pos_arg=0
pos_arg=0
host='localhost'
port='8111'
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
      echo "Usage: ${BASH_SOURCE[0]// /\\ } [--help|-h] [--host <host>] [--port <port>]"
      exit 0
      ;;
    -*)
      >&2 printf "Error: Invalid named argument/flag\n"
      exit 1
      ;;
    *)
      ((pos_arg=pos_arg+1))
      case "$pos_arg" in
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

if [[ $(curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host:$port/api/v3/Init/Status" | jq '.State==2') != 'true' ]]; then
  echo "Unabled to connect or server not running/started at target host+port"
  exit 1
fi

curl -s "http://$host:$port/v1/RenameScript" | jq
