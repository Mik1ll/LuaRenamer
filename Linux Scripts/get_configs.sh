#!/usr/bin/env bash

usage() {
  cat >&2 <<EOF
Usage: ${BASH_SOURCE[0]// /\\ } [OPTION]...
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

if ! command -v jq >/dev/null 2>&1; then
  printf '%s\n' 'Please install jq to use this script'
fi

if ! curl -s --connect-timeout 2 -H 'Accept: application/json' "http://$host/api/v3/Init/Status" | jq -e '.State == 2 // .State == "Started"' >/dev/null; then
  printf '%s\n' "Unable to connect or server not running/started at target http://$host"
  exit 1
fi

loginjson=$(jq -n --arg user "$user" --arg pass "$pass" '{user:$user, pass:$pass, device:"rename_bash_script"}')
apikey=$(curl -s -H 'Content-Type: application/json' -d "$loginjson" "http://$host/api/Auth" | jq -r '.apikey')

if ! [[ ${apikey//-/} =~ ^[[:xdigit:]]{32}$ ]]; then
  printf '%s\n' 'Login did not return an api key, check --user and --pass'
  exit 1
fi

curl -s -H "apikey: $apikey" "http://$host/api/v3/Renamer/Config" | jq
